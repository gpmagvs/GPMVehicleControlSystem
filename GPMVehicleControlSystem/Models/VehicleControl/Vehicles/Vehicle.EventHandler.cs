using AGVSystemCommonNet6;
using AGVSystemCommonNet6.AGVDispatch.Messages;
using AGVSystemCommonNet6.GPMRosMessageNet.Messages;
using AGVSystemCommonNet6.GPMRosMessageNet.Messages.SickMsg;
using AGVSystemCommonNet6.Log;
using AGVSystemCommonNet6.MAP;
using AGVSystemCommonNet6.Vehicle_Control.VCSDatabase;
using AGVSystemCommonNet6.Vehicle_Control.VCS_ALARM;
using GPMVehicleControlSystem.Models.Buzzer;
using GPMVehicleControlSystem.Models.VehicleControl.AGVControl;
using GPMVehicleControlSystem.Models.VehicleControl.VehicleComponent;
using GPMVehicleControlSystem.VehicleControl.DIOModule;
using RosSharp.RosBridgeClient.Actionlib;
using System.Diagnostics;
using static AGVSystemCommonNet6.AGVDispatch.Messages.clsTaskDownloadData;
using static AGVSystemCommonNet6.clsEnums;
using static GPMVehicleControlSystem.Models.VehicleControl.AGVControl.CarController;
using static GPMVehicleControlSystem.VehicleControl.DIOModule.clsDIModule;
using static GPMVehicleControlSystem.VehicleControl.DIOModule.clsDOModule;
using MathNet.Numerics;
using AGVSystemCommonNet6.Alarm;
using GPMVehicleControlSystem.Tools;
using Microsoft.EntityFrameworkCore.Internal;
using static GPMVehicleControlSystem.Models.VehicleControl.VehicleComponent.clsLaser;
using static GPMVehicleControlSystem.Models.VehicleControl.VehicleComponent.clsNavigation;
using Microsoft.AspNetCore.SignalR;
using GPMVehicleControlSystem.Models.VehicleControl.Vehicles.CargoStates;
using GPMVehicleControlSystem.Models.TaskExecute;
using AGVSystemCommonNet6.GPMRosMessageNet.Actions;
using YamlDotNet.Core.Tokens;

namespace GPMVehicleControlSystem.Models.VehicleControl.Vehicles
{
    public partial class Vehicle
    {
        private bool IsLaserRecoveryHandled = false;
        internal bool WaitingForChargeStatusChangeFlag = false;
        private Debouncer _vehicleDirectionChangedDebouncer = new Debouncer();

        private bool IsAutoControlRechargeCircuitSuitabtion
        {
            get
            {
                return Remote_Mode == REMOTE_MODE.OFFLINE && !WagoDI.GetState(DI_ITEM.Horizon_Motor_Switch);
            }
        }
        private bool IsNoObstacleAroundAGV => WagoDI.GetState(DI_ITEM.FrontProtection_Area_Sensor_2) &&
            WagoDI.GetState(DI_ITEM.BackProtection_Area_Sensor_2) &&
            WagoDI.GetState(DI_ITEM.LeftProtection_Area_Sensor_3) &&
            WagoDI.GetState(DI_ITEM.RightProtection_Area_Sensor_3);

        /// <summary>
        /// 是否偵測雷測觸發
        /// </summary>
        protected bool IsSaftyProtectActived => Operation_Mode == OPERATOR_MODE.AUTO && AGVC.ActionStatus == ActionStatus.ACTIVE;

        public bool ModuleInformationUpdatedInitState { get; private set; } = false;

        protected virtual void CommonEventsRegist()
        {
            //DBhelper.OnDataBaseChanged += CopyDataBaseToLogFolder;
            BuzzerPlayer.OnBuzzerPlay += () => { return Parameters.BuzzerOn; };
            AlarmManager.OnUnRecoverableAlarmOccur += AlarmManager_OnUnRecoverableAlarmOccur;
            AGVC.OnSpeedRecoveryRequesting += HandleSpeedReconveryRequesetRaised;
            AGVC.OnCstTriggerButTypeUnknown += HandleCstTriggerButTrayKnownEvent;
            AGVC.OnActionSendToAGVCRaising += HandleSendActionGoalToAGVCRaised;
            AGVC.OnAGVCActionActive += HandleAGVCActionActive;
            AGVC.OnAGVCActionSuccess += HandleAGVCActionSuccess;
            ChargeTask.OnChargeCircuitOpening += HandleChargeTaskTryOpenChargeCircuit;
            Navigation.OnDirectionChanged += Navigation_OnDirectionChanged;
            Navigation.OnLastVisitedTagUpdate += HandleLastVisitedTagChanged;
            BarcodeReader.OnAGVReachingTag += BarcodeReader_OnAGVReachingTag;
            BarcodeReader.OnAGVLeavingTag += BarcodeReader_OnAGVLeavingTag;
            IMU.OnImuStatesError += HandleIMUStatesError;
            IMU.OnOptionsFetching += () => { return Parameters.ImpactDetection; };
            Laser.OnSideLaserBypassSetting += () =>
            {
                bool _leftBypass = Parameters.SensorBypass.LeftSideLaserBypass;
                bool _rightBypass = Parameters.SensorBypass.RightSideLaserBypass;
                return (_leftBypass, _rightBypass);
            };
            clsOrderInfo.OnGetPortExistStatus += () => { return CargoStateStorer.HasAnyCargoOnAGV(Parameters.LDULD_Task_No_Entry); };
            OnParamEdited += (param) => { this.Parameters = param; };
            BuzzerPlayer.BeforeBuzzerMovePlay += () =>
            {
                if (_RunTaskData.Task_Name.ToUpper().Contains("CHARGE"))
                    return SOUNDS.GoToChargeStation;
                else
                    return SOUNDS.Move;
            };

            DirectionLighter.OnAGVDirectionChangeToForward += () =>
            {
                return Parameters.FrontLighterFlashWhenNormalMove;
            };
            foreach (var driver in WheelDrivers)
            {
                driver.OnAlarmHappened += async (alarm_code) =>
                {
                    if (alarm_code != AlarmCodes.None)
                    {
                        Task<bool> state = await Task.Factory.StartNew(async () =>
                        {
                            await Task.Delay(10);
                            bool isEMOING = WagoDI.GetState(DI_ITEM.EMO) == false;
                            if (isEMOING)
                                return false;
                            bool isResetAlarmProcessing = IsResetAlarmWorking;
                            return !isResetAlarmProcessing;
                        });
                        bool isAlarmNeedAdd = await state;
                        return isAlarmNeedAdd;
                    }
                    else
                    {
                        return true;
                    }
                };
            }

            if (Parameters.AgvType != AGV_TYPE.INSPECTION_AGV)
            {
                clsBattery.OnBatteryUnderVoltage += HandleBatteryUnderVoltage;
                //clsBattery.OnBatteryOverTemperature += HandleBatteryOverTemperature;
            }
            CarComponent.OnCommunicationError += CarComponent_OnCommunicationError;
            //clsSick.OnLocalizationStationError += ClsSick_OnLocalizationStationError;
            clsSick.OnMapMatchStatusToLow += ClsSick_OnMapMatchStatusToLow;

            LoadTask.OnManualCheckCargoStatusTrigger += LoadTask_OnManualCheckCargoStatusTrigger;

            if (CSTReader != null)
                CSTReader.onCSTReaderStateChanged += HandleCSTReaderStateChanged;

        }

        private void HandleCSTReaderStateChanged(object? sender, int state)
        {
            DebugMessageBrocast($"CST Reader State Changed to [{state}]");
        }

        private ManualResetEvent WaitOperatorCheckCargoStatusDone = new ManualResetEvent(false);
        private bool LoadTask_OnManualCheckCargoStatusTrigger(Params.clsManualCheckCargoStatusParams.CheckPointModel checkPointData)
        {
            WaitOperatorCheckCargoStatusDone.Reset();
            frontendHubContext.Clients.All.SendAsync("ManualCheckCargoStatus", checkPointData);
            BuzzerPlayer.WaitingCargoStatusCheck();
            bool checkDone = WaitOperatorCheckCargoStatusDone.WaitOne(TimeSpan.FromSeconds(checkPointData.Timeout));
            logger.LogInformation($"Operator Check Cargo Status Timeout:{!checkDone}");
            BuzzerPlayer.Stop("LoadTask_OnManualCheckCargoStatusTrigger");
            return checkDone;
        }
        internal void ManualCheckCargoStatusDone(string userName = "")
        {
            logger.LogInformation($"Operator {userName} Check Cargo Status Done.");
            WaitOperatorCheckCargoStatusDone.Set();
        }

        private void ClsSick_OnMapMatchStatusToLow(object? sender, EventArgs e)
        {
            AlarmManager.AddWarning(AlarmCodes.Map_Recognition_Rate_Too_Low);
        }

        private void ClsSick_OnLocalizationStationError(object? sender, EventArgs e)
        {
            AlarmManager.AddWarning(AlarmCodes.Localization_Fail);
        }

        private void CarComponent_OnCommunicationError(object? sender, CarComponent e)
        {
            CarComponent component = (CarComponent)e;
            if (component.component_name == CarComponent.COMPOENT_NAME.BATTERY)
            {
                AlarmManager.AddWarning(AlarmCodes.Battery_Status_Error_);
            }
        }

        /// <summary>
        /// 註冊DIO狀態變化事件
        /// </summary>
        protected virtual void DIOStatusChangedEventRegist()
        {

            WagoDI.OnDisonnected += WagoDI_OnDisonnected;
            WagoDI.OnReConnected += WagoDI_OnReConnected;
            WagoDI.OnEMO += EMOTriggerHandler;
            WagoDI.OnBumpSensorPressed += WagoDI_OnBumpSensorPressed;
            WagoDI.OnResetButtonPressed += HandleResetButtonPush;
            WagoDI.SubsSignalStateChange(DI_ITEM.Panel_Reset_PB, Panel_Reset_Button_Input_State_Handler);
            //WagoDI.OnResetButtonPressed += async (s, e) => await ResetAlarmsAsync(true);

            Dictionary<DI_ITEM, Action<object, bool>> InputsEventsMap = new Dictionary<DI_ITEM, Action<object, bool>>()
            {
                { DI_ITEM.Limit_Switch_Sensor, HandleLimitSwitchSensorSignalChange},
                { DI_ITEM.Fork_Frontend_Abstacle_Sensor, HandleForkFrontendObsSensorSignalChange},
            };
            foreach (KeyValuePair<DI_ITEM, Action<object, bool>> item in InputsEventsMap)
            {
                var _input = item.Key;
                Action<object, bool> _handler = item.Value;
                WagoDI.SubsSignalStateChange(_input, new EventHandler<bool>(_handler));
            }

            WagoDO.SubsSignalStateChange(DO_ITEM.Recharge_Circuit, (sender, state) => { frontendHubContext?.Clients.All.SendAsync("ReChargeCircuitChanged", state); });
            CargoStateStore.trayExistSensorItems.ForEach(sensor =>
            {
                WagoDI.SubsSignalStateChange(sensor, (sender, state) => { CargoStateStorer.HandleCargoExistSensorStateChanged(sender, EventArgs.Empty); });
            });
            CargoStateStore.rackExistSensorItems.ForEach(sensor =>
            {
                WagoDI.SubsSignalStateChange(sensor, (sender, state) => { CargoStateStorer.HandleCargoExistSensorStateChanged(sender, EventArgs.Empty); });
            });
        }

        private async void HandleAGVCActionSuccess(object? sender, EventArgs e)
        {
            if (AGVC.CycleStopActionExecuting)
            {
                DebugMessageBrocast($"[Action Status Changed To SUCCEEDED by Cycle Stop Action Done Check. ] Action Status is SUCCESSED, reset actionClient.goal = new TaskCommandGoal()");
                AGVC.actionClient.goal = new TaskCommandGoal();
            }
            if (_RunTaskData.Destination == Navigation.LastVisitedTag)
            {
                DebugMessageBrocast($"Action Status now is SUCCESSED and AGV Position is Destine of Executed Task({Navigation.LastVisitedTag})");
                AGVC.actionClient.goal = new AGVSystemCommonNet6.GPMRosMessageNet.Actions.TaskCommandGoal();
            }
            _ = Task.Run(async () =>
            {
                await EndLaserObsMonitorAsync();
            });
        }

        private async void HandleAGVCActionActive(object? sender, EventArgs e)
        {
            _ = Task.Run(async () =>
            {
                SetSub_Status(SUB_STATUS.RUN);
                StartLaserObstacleMonitor();
                await Task.Delay(300);
                if (ExecutingTaskEntity.action == ACTION_TYPE.None)
                    BuzzerPlayer.Move();
            });
        }
        private void HandleBatteryOverTemperature(object? sender, clsBattery e)
        {
            AlarmManager.AddAlarm(AlarmCodes.Over_Temperature, false);
        }
        private void HandleBatteryUnderVoltage(object? sender, clsBattery e)
        {
            bool _isCharging = Batteries.Any(bat => bat.Value.Data.chargeCurrent > 0);
            if (Parameters.Advance.ShutDownPCWhenLowBatteryLevel && _isCharging)
            {
                ShutdownPCTask();
            }
        }

        internal void ShutdownPCTask()
        {
            PCShutDownHelper.CancelPCShutdownFlag = false;

            Task.Run(async () =>
            {
                try
                {
                    bool IsCurrentStatusCanShutdown()
                    {
                        if (GetSub_Status() == SUB_STATUS.RUN ||
                            AGVC._ActionStatus == ActionStatus.ACTIVE ||
                            AGVC._ActionStatus == ActionStatus.PENDING ||
                            WheelDrivers.Any(driver => driver.Data.speed != 0))
                            return false;
                        return true;
                    }

                    while (!IsCurrentStatusCanShutdown())
                    {
                        await Task.Delay(1000);
                        if (PCShutDownHelper.CancelPCShutdownFlag)
                        {
                            logger.LogInformation($"User cancel PC Shutdwon when wait status change to shutdownable.");
                            return;
                        }
                    }
                    await AGVC.EmergencyStop(true);
                    bool shutdownReady = await PCShutDownHelper.ShutdownAsync();
                    if (shutdownReady)
                    {
                        await WagoDO.SetState(DO_ITEM.Recharge_Circuit, true);
                        logger.LogWarning($"Low battery level PC whill shutdown ");
                        AlarmManager.AddAlarm(AlarmCodes.Battery_Low_Level_Auto_PC_Shutdown, false);
                    }

                }
                catch (Exception ex)
                {
                    logger.LogError(ex, ex.Message);
                }
                finally
                {
                    PCShutDownHelper.CancelPCShutdownFlag = false;
                }
            });

        }

        private CST_TYPE HandleCstTriggerButTrayKnownEvent()
        {
            if (Parameters.HasRackCstReader)
            {
                return CST_TYPE.Rack;
            }
            else
            {
                return CST_TYPE.Tray;
            }
        }

        private CancellationTokenSource _ResetButtonOnPressingCts = new CancellationTokenSource();

        private async void Panel_Reset_Button_Input_State_Handler(object? sender, bool pushed)
        {
            if (pushed)
            {
                _ResetButtonOnPressingCts = new CancellationTokenSource();
                _ButtonOnReleaseAsync();
            }
            else
            {
                this._ResetButtonOnPressingCts.Cancel();
            }

            async Task _ButtonOnReleaseAsync()
            {
                try
                {
                    await Task.Delay(2000, _ResetButtonOnPressingCts.Token);
                    await Laser.ModeSwitch(LASER_MODE.Bypass16, isSettingByResetButtonLongPressed: true);
                    logger.LogTrace("Panel Reset Button Pressed 2s,Laser set Bypass. ^_^ ");

                }
                catch (Exception ex)
                {
                    //button released
                }
            }
        }

        private async void HandleResetButtonPush(object? sender, EventArgs e)
        {
            BuzzerPlayer.Stop("HandleResetButtonPush");
            await TryResetMotors();
            AlarmManager.ClearAlarm();
            BuzzerPlayer.Stop("HandleResetButtonPush");

        }

        protected virtual async Task TryResetMotors()
        {
            await WagoDO.SetState(DO_ITEM.Horizon_Motor_Stop, false);
            await WagoDO.SetState(DO_ITEM.Horizon_Motor_Free, false);

            if (IsAnyHorizonMotorAlarm())
            {
                await WagoDO.SetState(DO_ITEM.Horizon_Motor_Reset, true);
                await Task.Delay(100);
                await WagoDO.SetState(DO_ITEM.Horizon_Motor_Reset, false);

                while (IsAnyHorizonMotorAlarm())
                {
                    await Task.Delay(1);
                }
                await Task.Delay(50);
            }
        }

        protected virtual bool IsAnyHorizonMotorAlarm()
        {
            return WagoDI.GetState(DI_ITEM.Horizon_Motor_Alarm_1) || WagoDI.GetState(DI_ITEM.Horizon_Motor_Alarm_2);
        }

        private void HandleLimitSwitchSensorSignalChange(object? sender, bool input_status)
        {
            clsIOSignal signalObj = (clsIOSignal)sender;
            var sensorName = signalObj.Name;
            var isTriggered = !input_status; //TODO 確認 A接點或B接點
            bool isNonNormalMoving = _RunTaskData.Action_Type != ACTION_TYPE.None && AGVC.ActionStatus == ActionStatus.ACTIVE;
            bool isBackToSecondaryPtIng = _ExecutingTask != null && _ExecutingTask.IsBackToSecondaryPt;
            if (!isTriggered || !isNonNormalMoving || isBackToSecondaryPtIng)
                return;

            logger.LogWarning($"AGV進站過程中限動開關-{sensorName} 觸發!");

            bool isRecoverable = Parameters.SensorBypass.AGVBodyLimitSensorBypass;
            if (isRecoverable)
                AlarmManager.AddWarning(AlarmCodes.Limit_Switch_Sensor);
            else
                AlarmManager.AddAlarm(AlarmCodes.Limit_Switch_Sensor, false);
        }
        private void HandleForkFrontendObsSensorSignalChange(object? sender, bool input_status)
        {
            clsIOSignal signalObj = (clsIOSignal)sender;
            string sensorName = signalObj.Name;

            bool isTriggered = Parameters.ForkAGV.ObsSensorPointType == Params.IO_CONEECTION_POINT_TYPE.A && input_status || Parameters.ForkAGV.ObsSensorPointType == Params.IO_CONEECTION_POINT_TYPE.B && !input_status;
            bool isNonNormalMoving = _RunTaskData.Action_Type != ACTION_TYPE.None && _RunTaskData.Action_Type != ACTION_TYPE.Charge && AGVC.ActionStatus == ActionStatus.ACTIVE;
            bool isBackToSecondaryPtIng = _ExecutingTask != null && _ExecutingTask.IsBackToSecondaryPt;
            if (!isTriggered || !isNonNormalMoving || isBackToSecondaryPtIng)
                return;

            logger.LogWarning($"AGV進站過程中牙叉前端障礙物檢知Sensor觸發!({sensorName})");
            bool isRecoverable = Parameters.SensorBypass.ForkFrontendObsSensorBypass;
            if (isRecoverable)
                AlarmManager.AddWarning(AlarmCodes.Fork_Frontend_has_Obstacle);
            else
                AlarmManager.AddAlarm(AlarmCodes.Fork_Frontend_has_Obstacle, false);
        }

        private bool HandleChargeTaskTryOpenChargeCircuit()
        {
            if (!Parameters.BatteryModule.ChargeWhenLevelLowerThanThreshold)
                return true;
            var threshold = Parameters.BatteryModule.ChargeLevelThreshold;
            bool anyBatteryLevelLowerThreshold = Batteries.Any(bat => bat.Value.Data.batteryLevel < threshold);
            var batlevels = string.Join(",", Batteries.Values.Select(bat => bat.Data.batteryLevel));
            logger.LogInformation($"[Charge Circuit Only Open when level lower than threshold({threshold} %)] Charge Circuit Open Check : {(anyBatteryLevelLowerThreshold ? "Allowed" : "Forbid")}|Battery Levels ={batlevels}");
            return anyBatteryLevelLowerThreshold;
        }

        private void HandleIMUStatesError(object? sender, clsIMU.IMUStateErrorEventData imu_event_data)
        {
            RosSharp.RosBridgeClient.MessageTypes.Geometry.Vector3 acc_data = imu_event_data.AccRaw;
            ALARM_LEVEL _agv_pitch_error_alarm_level = Parameters.ImpactDetection.PitchErrorAlarmLevel;

            var locInfo = $"當前座標=({Navigation.Data.robotPose.pose.position.x},{Navigation.Data.robotPose.pose.position.y})";
            var thetaInfo = $"當前角度={Navigation.Angle}";
            if (imu_event_data.Imu_AlarmCode == AlarmCodes.IMU_Pitch_State_Error)
            {
                if (_agv_pitch_error_alarm_level == ALARM_LEVEL.ALARM)
                    AlarmManager.AddAlarm(AlarmCodes.IMU_Pitch_State_Error, false);
                else
                    AlarmManager.AddWarning(AlarmCodes.IMU_Pitch_State_Error);
            }
            else
                AlarmManager.AddWarning(imu_event_data.Imu_AlarmCode);
            logger.LogWarning($"AGV Status Error:[{imu_event_data.Imu_AlarmCode}]\nLocation: ({locInfo},{thetaInfo}).\nState={imu_event_data.ToJson()}");
        }

        private (bool confirmed, string message) HandleSpeedReconveryRequesetRaised()
        {
            if (!IsAllLaserNoTrigger())
                return (false, "要求車控速度恢復但尚有雷射觸發中");
            return (true, "");
        }

        /// <summary>
        /// 處理Action Goal發送給車控前的檢查
        /// </summary>
        /// <returns></returns>
        private SendActionCheckResult HandleSendActionGoalToAGVCRaised()
        {
            SendActionCheckResult _confirmResult = new(SendActionCheckResult.SEND_ACTION_GOAL_CONFIRM_RESULT.Accept);
            if (TaskCycleStopStatus == TASK_CANCEL_STATUS.RECEIVED_CYCLE_STOP_REQUEST)
            {
                logger.LogWarning($"Before Action Goal Send to AGVC Check Fail => Cycle Stop Request is Raising Now!");
                _confirmResult = new SendActionCheckResult(SendActionCheckResult.SEND_ACTION_GOAL_CONFIRM_RESULT.AGVS_CANCEL_TASK_REQ_RAISED);
            }
            logger.LogTrace($"Before Action Goal Send to AGVC Check Result = {_confirmResult.ToJson()}");
            return _confirmResult;
        }

        private void CopyDataBaseToLogFolder(string database_file_name)
        {
            Task.Factory.StartNew(() =>
            {
                try
                {

                    string subFolder = Path.Combine(LOG.LogFolder, DateTime.Now.ToString("yyyy-MM-dd"));
                    string fileName = Path.Combine(subFolder, Path.GetFileName(database_file_name));
                    Stopwatch stopwatch = Stopwatch.StartNew();
                    File.Copy(database_file_name, fileName, true);
                    stopwatch.Stop();
                    logger.LogTrace($"DB File Copy spend:{stopwatch.ElapsedMilliseconds} ms", Debugger.IsAttached);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, ex.Message);
                }
            });

        }

        private void BarcodeReader_OnAGVLeavingTag(object? sender, uint previousTag)
        {
            if (IsAutoControlRechargeCircuitSuitabtion && Parameters.BatteryModule.Recharge_Circuit_Auto_Control_In_ManualMode)
            {
                var previousPoint = NavingMap.Points.Values.FirstOrDefault(pt => pt.TagNumber == previousTag);
                if (previousPoint != null)
                {
                    if (previousPoint.IsCharge)
                    {
                        if (!IsChargeCircuitOpened)
                            return;

                        Task.Factory.StartNew(async () =>
                        {
                            await WagoDO.SetState(DO_ITEM.Recharge_Circuit, false);
                        });
                    }
                }
            }
        }

        private void BarcodeReader_OnAGVReachingTag(object? sender, EventArgs e)
        {
            if (IsAutoControlRechargeCircuitSuitabtion && Parameters.BatteryModule.Recharge_Circuit_Auto_Control_In_ManualMode)
            {
                var currentPt = NavingMap.Points.Values.FirstOrDefault(pt => pt.TagNumber == BarcodeReader.CurrentTag);
                if (currentPt == null)
                    return;

                if (currentPt.IsCharge)
                {
                    AutomaticChargeStationActions();
                }
            }
        }
        internal async void AutomaticChargeStationActions()
        {
            if (Parameters.Auto_Cleaer_CST_ID_Data_When_Has_Data_But_NO_Cargo && IsNoCargoButIDExist)
            {
                logger.LogWarning($"AGV位於充電站且偵測到AGV有帳無料=>自動清帳");
                CSTReader.ValidCSTID = "";
            }
            if (!IsChargeCircuitOpened)
            {
                await WagoDO.SetState(DO_ITEM.Recharge_Circuit, true);
            }
        }
        private void WagoDI_OnReConnected(object? sender, EventArgs e)
        {
            if (AGVC == null)
                return;
            if (AGVC.ActionStatus != ActionStatus.ACTIVE)
                return;

            if (IsNoObstacleAroundAGV)
            {
                logger.LogWarning("AGV Executing Task and Wago Module Reconnected,and No Obstacle,Send Complex Control Speed Reconvery");
                AGVC.CarSpeedControl(ROBOT_CONTROL_CMD.SPEED_Reconvery, SPEED_CONTROL_REQ_MOMENT.IO_MODULE_RECOVERY);
            }
        }

        private void WagoDI_OnDisonnected(object? sender, EventArgs e)
        {
            if (AGVC?.ActionStatus != ActionStatus.ACTIVE)
                return;
            logger.LogWarning("AGV Executing Task but Wago Module Disconnect,Send Complex Control STOP => AGV STOP");
            AGVC.CarSpeedControl(ROBOT_CONTROL_CMD.STOP, SPEED_CONTROL_REQ_MOMENT.IO_MODULE_DISCONNECTED);
        }

        /// <summary>
        /// 處理側邊雷射
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="di_state"></param>
        private async void HandleSideLaserSignal(object? sender, bool di_state)
        {
            if (!IsSaftyProtectActived)
                return;

            clsIOSignal diState = (clsIOSignal)sender;
            bool IsRightLaser = diState.Input == DI_ITEM.RightProtection_Area_Sensor_3;
            bool IsLeftLaser = diState.Input == DI_ITEM.LeftProtection_Area_Sensor_3;
            bool IsRightLsrBypass = WagoDO.GetState(DO_ITEM.Right_LsrBypass);
            bool IsLeftLsrBypass = WagoDO.GetState(DO_ITEM.Left_LsrBypass);
            AlarmCodes alarm_code = IsRightLaser ? AlarmCodes.RightProtection_Area3 : AlarmCodes.LeftProtection_Area3;
            if (IsRightLaser && IsRightLsrBypass)
                return;
            if (IsLeftLaser && IsLeftLsrBypass)
                return;

            if (!di_state)
            {
                await Task.Delay(300);
                if (WagoDI.GetState(diState.Input))
                {
                    var x = Navigation.Data.robotPose.pose.position.x;
                    var y = Navigation.Data.robotPose.pose.position.y;
                    var theta = Navigation.Angle;
                    logger.LogWarning($"{(IsRightLaser ? "Right" : "Left")} Side Laser Flick! ({x},{y},{theta})");
                    if (IsNoObstacleAroundAGV)
                        await AGVC.CarSpeedControl(ROBOT_CONTROL_CMD.DECELERATE, IsRightLaser ? SPEED_CONTROL_REQ_MOMENT.RIGHT_LASER_RECOVERY : SPEED_CONTROL_REQ_MOMENT.LEFT_LASER_RECOVERY);
                    return;
                }

                await AGVC.CarSpeedControl(ROBOT_CONTROL_CMD.STOP, IsRightLaser ? SPEED_CONTROL_REQ_MOMENT.RIGHT_LASER_TRIGGER : SPEED_CONTROL_REQ_MOMENT.LEFT_LASER_TRIGGER);

                LogStatausWhenLaserTrigger(alarm_code);
                AlarmManager.RecordAlarm(alarm_code);
                AGVStatusChangeToAlarmWhenLaserTrigger();

            }
            else
            {
                if (IsLaserRecoveryHandled)
                    return;
                AlarmManager.ClearAlarm(alarm_code);
                if (IsNoObstacleAroundAGV)
                {
                    logger.LogInformation($"[{alarm_code}] 側邊雷射雷射恢復.ROBOT_CONTROL_CMD.SPEED_Reconvery");
                    AGVStatusChangeToRunWhenLaserRecovery(ROBOT_CONTROL_CMD.SPEED_Reconvery, IsRightLaser ? SPEED_CONTROL_REQ_MOMENT.RIGHT_LASER_RECOVERY : SPEED_CONTROL_REQ_MOMENT.LEFT_LASER_RECOVERY);
                }
            }
        }

        /// <summary>
        /// 處理雷射第一段觸發
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        protected async void HandleLaserArea1SinalChange(object? sender, bool e)
        {
            if (!IsSaftyProtectActived)
                return;

            clsIOSignal diState = (clsIOSignal)sender;
            bool isFrontLaser = diState.Input == DI_ITEM.FrontProtection_Area_Sensor_1;
            bool isLsrBypass = isFrontLaser ? WagoDO.GetState(DO_ITEM.Front_LsrBypass) : WagoDO.GetState(DO_ITEM.Back_LsrBypass);

            if (isLsrBypass)
                return;
            var alarm_code = isFrontLaser ? AlarmCodes.FrontProtection_Area2 : AlarmCodes.BackProtection_Area2;
            if (!diState.State)
            {
                logger.LogInformation($"{(isFrontLaser ? "前方" : "後方")} 第一段雷射Trigger.ROBOT_CONTROL_CMD.DECELERATE");
                LogStatausWhenLaserTrigger(alarm_code);
                AlarmManager.AddWarning(alarm_code);
                AGVC.CarSpeedControl(CarController.ROBOT_CONTROL_CMD.DECELERATE, isFrontLaser ? SPEED_CONTROL_REQ_MOMENT.FRONT_LASER_1_TRIGGER : SPEED_CONTROL_REQ_MOMENT.BACK_LASER_1_TRIGGER);
            }
            else
            {
                if (IsLaserRecoveryHandled)
                    return;
                logger.LogInformation($"{(isFrontLaser ? "前方" : "後方")} 第一段雷射恢復.ROBOT_CONTROL_CMD.SPEED_Reconvery");
                _ = Task.Run(async () =>
                {
                    IsLaserRecoveryHandled = false;
                    AlarmManager.ClearAlarm(alarm_code);

                    while (!IsAllLaserNoTrigger())
                    {
                        await Task.Delay(1000);
                    }
                    AGVStatusChangeToRunWhenLaserRecovery(ROBOT_CONTROL_CMD.SPEED_Reconvery, isFrontLaser ? SPEED_CONTROL_REQ_MOMENT.FRONT_LASER_1_RECOVERY : SPEED_CONTROL_REQ_MOMENT.BACK_LASER_1_RECOVERY);
                });
            }
        }

        protected async void HandleSideLaserArea1SinalChange(object? sender, bool e)
        {
            if (!IsSaftyProtectActived)
                return;

            clsIOSignal diState = (clsIOSignal)sender;
            bool isRightLaser = diState.Input == DI_ITEM.RightProtection_Area_Sensor_1;
            bool isLsrBypass = isRightLaser ? WagoDO.GetState(DO_ITEM.Right_LsrBypass) : WagoDO.GetState(DO_ITEM.Left_LsrBypass);

            if (isLsrBypass)
                return;
            var alarm_code = isRightLaser ? AlarmCodes.RightProtection_Area2 : AlarmCodes.LeftProtection_Area2;
            if (!diState.State)
            {
                logger.LogInformation($"{(isRightLaser ? "右方" : "左方")} 第一段雷射Trigger.ROBOT_CONTROL_CMD.DECELERATE");
                LogStatausWhenLaserTrigger(alarm_code);
                AlarmManager.AddWarning(alarm_code);
                AGVC.CarSpeedControl(CarController.ROBOT_CONTROL_CMD.DECELERATE, isRightLaser ? SPEED_CONTROL_REQ_MOMENT.RIGHT_LASER_TRIGGER : SPEED_CONTROL_REQ_MOMENT.LEFT_LASER_TRIGGER);
            }
            else
            {
                if (IsLaserRecoveryHandled)
                    return;
                _ = Task.Run(async () =>
                {
                    AlarmManager.ClearAlarm(alarm_code);
                    while (!IsAllLaserNoTrigger())
                    {
                        await Task.Delay(1000);
                    }
                    logger.LogInformation($"{(isRightLaser ? "右方" : "左方")} 第一段雷射恢復.ROBOT_CONTROL_CMD.SPEED_Reconvery");
                    AGVStatusChangeToRunWhenLaserRecovery(ROBOT_CONTROL_CMD.SPEED_Reconvery, isRightLaser ? SPEED_CONTROL_REQ_MOMENT.RIGHT_LASER_RECOVERY : SPEED_CONTROL_REQ_MOMENT.LEFT_LASER_RECOVERY);
                });
            }
        }



        /// <summary>
        /// 處理雷射第二段觸發
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        protected void HandleLaserArea2SinalChange(object? sender, bool e)
        {

            if (!IsSaftyProtectActived)
                return;

            clsIOSignal diState = (clsIOSignal)sender;
            bool isFrontLaser = diState.Input == DI_ITEM.FrontProtection_Area_Sensor_2;
            bool isLsrBypass = isFrontLaser ? WagoDO.GetState(DO_ITEM.Front_LsrBypass) : WagoDO.GetState(DO_ITEM.Back_LsrBypass);
            if (isLsrBypass)
                return;
            var alarm_code = diState.Input == DI_ITEM.FrontProtection_Area_Sensor_2 ? AlarmCodes.FrontProtection_Area3 : AlarmCodes.BackProtection_Area3;

            if (!diState.State)
            {
                logger.LogInformation($"{(isFrontLaser ? "前方" : "後方")} 第二段雷射Trigger.ROBOT_CONTROL_CMD.STOP");
                AGVC.CarSpeedControl(ROBOT_CONTROL_CMD.STOP, isFrontLaser ? SPEED_CONTROL_REQ_MOMENT.FRONT_LASER_2_TRIGGER : SPEED_CONTROL_REQ_MOMENT.BACK_LASER_2_TRIGGER);
                LogStatausWhenLaserTrigger(alarm_code);
                AlarmManager.RecordAlarm(alarm_code);
                AGVStatusChangeToAlarmWhenLaserTrigger();
            }
            else
            {
                if (IsLaserRecoveryHandled)
                    return;
                AlarmManager.ClearAlarm(alarm_code);
                if (IsNoObstacleAroundAGV)
                {
                    logger.LogInformation($"{(isFrontLaser ? "前方" : "後方")} 第二段雷射恢復.ROBOT_CONTROL_CMD.DECELERATE");
                    AGVStatusChangeToRunWhenLaserRecovery(ROBOT_CONTROL_CMD.DECELERATE, isFrontLaser ? SPEED_CONTROL_REQ_MOMENT.FRONT_LASER_2_RECOVERY : SPEED_CONTROL_REQ_MOMENT.BACK_LASER_2_RECOVERY);
                }
            }
        }

        /// <summary>
        ///  處理雷射第三段觸發
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="di_state"></param>
        protected virtual void HandleLaserArea3SinalChange(object? sender, bool di_state)
        {
            if (!IsSaftyProtectActived)
                return;

            clsIOSignal diState = (clsIOSignal)sender;

            bool isFrontLaser = diState.Input == DI_ITEM.FrontProtection_Area_Sensor_3;
            bool isLsrBypass = isFrontLaser ? WagoDO.GetState(DO_ITEM.Front_LsrBypass) : WagoDO.GetState(DO_ITEM.Back_LsrBypass);
            if (isLsrBypass)
                return;

            AlarmCodes alarm_code = isFrontLaser ? AlarmCodes.FrontProtection_Area3 : AlarmCodes.BackProtection_Area3;
            if (!di_state)
            {
                logger.LogInformation($"{(isFrontLaser ? "前方" : "後方")} 第三段雷射Trigger.ROBOT_CONTROL_CMD.STOP");
                AGVC.CarSpeedControl(ROBOT_CONTROL_CMD.STOP, isFrontLaser ? SPEED_CONTROL_REQ_MOMENT.FRONT_LASER_3_TRIGGER : SPEED_CONTROL_REQ_MOMENT.BACK_LASER_3_TRIGGER);
                LogStatausWhenLaserTrigger(alarm_code);
                AlarmManager.RecordAlarm(alarm_code);
                AGVStatusChangeToAlarmWhenLaserTrigger();
            }
            else
            {
                if (IsLaserRecoveryHandled)
                    return;
                AlarmManager.ClearAlarm(alarm_code);
                if (IsNoObstacleAroundAGV)
                {
                    logger.LogInformation($"{(isFrontLaser ? "前方" : "後方")} 第三段雷射恢復.ROBOT_CONTROL_CMD.DECELERATE");
                    AGVStatusChangeToRunWhenLaserRecovery(ROBOT_CONTROL_CMD.DECELERATE, isFrontLaser ? SPEED_CONTROL_REQ_MOMENT.FRONT_LASER_3_RECOVERY : SPEED_CONTROL_REQ_MOMENT.BACK_LASER_3_RECOVERY);
                }
            }
        }

        private void LogStatausWhenLaserTrigger(AlarmCodes alarm_code)
        {
            string LsrInputState = $"Right={WagoDI.GetState(DI_ITEM.RightProtection_Area_Sensor_3)}";
            LsrInputState += $"\r\nLeft={WagoDI.GetState(DI_ITEM.LeftProtection_Area_Sensor_3)}";
            LsrInputState += $"\r\nFront_1={WagoDI.GetState(DI_ITEM.FrontProtection_Area_Sensor_1)}";
            LsrInputState += $"\r\nFront_2={WagoDI.GetState(DI_ITEM.FrontProtection_Area_Sensor_2)}";
            LsrInputState += $"\r\nFront_3={WagoDI.GetState(DI_ITEM.FrontProtection_Area_Sensor_3)}";
            LsrInputState += $"\r\nFront_4={WagoDI.GetState(DI_ITEM.FrontProtection_Area_Sensor_4)}";
            LsrInputState += $"\r\nBack_1={WagoDI.GetState(DI_ITEM.BackProtection_Area_Sensor_1)}";
            LsrInputState += $"\r\nBack_2={WagoDI.GetState(DI_ITEM.BackProtection_Area_Sensor_2)}";
            LsrInputState += $"\r\nBack_3={WagoDI.GetState(DI_ITEM.BackProtection_Area_Sensor_3)}";
            LsrInputState += $"\r\nBack_4={WagoDI.GetState(DI_ITEM.BackProtection_Area_Sensor_4)}";

            logger.LogTrace($"{alarm_code} 雷射觸發_當前雷射組數={Laser.Mode}," +
                $"當前位置={lastVisitedMapPoint.Name}," +
                $"當前座標=({Navigation.Data.robotPose.pose.position.x},{Navigation.Data.robotPose.pose.position.y})" +
                $"當前角度={Navigation.Angle}" +
                $"\r\nLsr Bypass Settings=>\r\n Right={WagoDO.GetState(DO_ITEM.Right_LsrBypass)}\r\n Left={WagoDO.GetState(DO_ITEM.Left_LsrBypass)}" +
                $"\r\n Front={WagoDO.GetState(DO_ITEM.Front_LsrBypass)}" +
                $"\r\n Back={WagoDO.GetState(DO_ITEM.Back_LsrBypass)}" +
                $"\r\n 雷射輸入=> \r\n{LsrInputState}");

        }
        public async void AGVStatusChangeToRunWhenLaserRecovery(ROBOT_CONTROL_CMD speed_control, SPEED_CONTROL_REQ_MOMENT sPEED_CONTROL_REQ_MOMENT)
        {
            _ = Task.Run(async () =>
            {
                IsLaserRecoveryHandled = true;
                try
                {
                    CancellationTokenSource waitNoObstacleCTS = new CancellationTokenSource();

                    while (!IsAllLaserNoTrigger())
                    {
                        await Task.Delay(1000);
                    }

                    if (AGVC.ActionStatus == ActionStatus.ACTIVE)
                    {
                        _Sub_Status = SUB_STATUS.RUN;
                        StatusLighter.RUN();
                        try
                        {
                            if (_RunTaskData.Action_Type == ACTION_TYPE.None)
                                BuzzerPlayer.Move();
                            else
                                BuzzerPlayer.Action();
                        }
                        catch (Exception ex)
                        {
                            logger.LogError(ex, ex.Message);
                        }

                    }
                    if (speed_control == ROBOT_CONTROL_CMD.SPEED_Reconvery)
                    {
                        logger.LogTrace($"速度恢復-減速後加速");

                        AGVC.OnSTOPCmdRequesting += HandleSTOPCmdRequesting;
                        await AGVC.CarSpeedControl(ROBOT_CONTROL_CMD.DECELERATE, sPEED_CONTROL_REQ_MOMENT);

                        while (!IsAllLaserNoTrigger())
                        {
                            await Task.Delay(100);
                            if (waitNoObstacleCTS.IsCancellationRequested)
                            {
                                AGVC.OnSTOPCmdRequesting -= HandleSTOPCmdRequesting;
                                logger.LogTrace($"取消等待:無障礙物後速度恢復，因STOP命令已下達");
                                return;
                            }
                        }
                        AGVC.OnSTOPCmdRequesting -= HandleSTOPCmdRequesting;
                        await Task.Delay(1000);
                        if (!IsAllLaserNoTrigger())
                            return;
                    }
                    await AGVC.CarSpeedControl(speed_control, sPEED_CONTROL_REQ_MOMENT);

                    void HandleSTOPCmdRequesting(object sender, EventArgs arg)
                    {
                        waitNoObstacleCTS.Cancel();
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, ex.Message);
                }
                finally
                {
                    IsLaserRecoveryHandled = false;
                }

            });
        }
        protected void AGVStatusChangeToAlarmWhenLaserTrigger()
        {
            _Sub_Status = SUB_STATUS.ALARM;
            BuzzerPlayer.Stop("AGVStatusChangeToAlarmWhenLaserTrigger");
            BuzzerPlayer.Alarm();
            StatusLighter.DOWN();
        }
        public REMOTE_MODE RemoteModeWhenHorizonMotorAlarm { get; protected set; } = REMOTE_MODE.OFFLINE;
        protected async virtual void HandleDriversStatusErrorAsync(object? sender, bool status)
        {


            if (!status)
                return;
            if (IsMotorAutoRecoverable())
            {
                RemoteModeWhenHorizonMotorAlarm = Remote_Mode.Clone();
                return;
            }

            await Task.Delay(1000);
            if (!WagoDI.GetState(DI_ITEM.EMO) || IsResetAlarmWorking)
                return;

            clsIOSignal signal = (clsIOSignal)sender;
            var input = signal?.Input;
            var alarmCode = AlarmCodes.Wheel_Motor_IO_Error_Left;
            if (input == DI_ITEM.Horizon_Motor_Alarm_1)
                alarmCode = AlarmCodes.Wheel_Motor_IO_Error_Left;
            else if (input == DI_ITEM.Horizon_Motor_Alarm_2)
                alarmCode = AlarmCodes.Wheel_Motor_IO_Error_Right;

            if (GetSub_Status() != SUB_STATUS.IDLE && GetSub_Status() != SUB_STATUS.Charging)
            {
                AlarmManager.AddAlarm(alarmCode, false);
                return;
            }

            AlarmManager.AddWarning(alarmCode);
            //#region 嘗試Reset馬達
            //_ = Task.Factory.StartNew(async () =>
            //{
            //    logger.LogWarning($"Horizon Motor IO Alarm, Try auto reset process start");
            //    await Task.Delay(500);
            //    while (signal.State) //異常持續存在
            //    {
            //        await Task.Delay(1000);
            //        IsMotorReseting = false;
            //        await ResetMotorWithWait(TimeSpan.FromSeconds(5), signal, alarmCode);
            //    }
            //});

            //#endregion
        }
        protected private async Task<bool> ResetMotorWithWait(TimeSpan timeout, clsIOSignal? signal, AlarmCodes alarmCode)
        {
            CancellationTokenSource cancellation = new CancellationTokenSource(timeout);
            while (IsMotorReseting)
            {
                await Task.Delay(100);
                if (!signal.State)
                {
                    AlarmManager.ClearAlarm(alarmCode);
                    return true;
                }

                if (cancellation.IsCancellationRequested)
                {
                    logger.LogWarning($"Reset Motor Fail:Waiting other motor reset process end TIMEOUT");
                    return false;
                }

            }
            if (!signal.State)
            {
                AlarmManager.ClearAlarm(alarmCode);
                logger.LogInformation($"{signal.Name} state {signal.State}, Alarm is reset");
                return true;
            }

            AlarmManager.ClearAlarm(alarmCode);
            return await ResetMotor(false);
        }
        protected DateTime previousSoftEmoTime = DateTime.MinValue;
        protected virtual async void AlarmManager_OnUnRecoverableAlarmOccur(object? sender, AlarmCodes alarm_code)
        {
            _ = Task.Run(async () =>
            {
                await AGVC.EmergencyStop(true);
            });
            SoftwareEMO(alarm_code);
        }



        protected virtual async void Navigation_OnDirectionChanged(object? sender, clsNavigation.AGV_DIRECTION direction)
        {
            Laser.agvDirection = direction;
            int _debunceDelay = direction == AGV_DIRECTION.BYPASS ? 10 : 100;

            _vehicleDirectionChangedDebouncer.Debounce(() =>
            {
                if (AGVC.ActionStatus == ActionStatus.ACTIVE && direction != clsNavigation.AGV_DIRECTION.REACH_GOAL)
                    Laser.LaserChangeByAGVDirection(sender, direction);
                DirectionLighter.LightSwitchByAGVDirection(sender, direction);
            }, _debunceDelay);
        }

        private async Task _CheckLaserStateAfter(int _oriLaser)
        {
            _ = Task.Factory.StartNew(async () =>
            {
                await Task.Delay(1000);
                bool _isAGVStillForward = Navigation.Direction == clsNavigation.AGV_DIRECTION.FORWARD;
                if (!_isAGVStillForward)
                    return;

                bool _isLaserSettingError = Laser.AgvsLsrSetting != _oriLaser;
                if (_isLaserSettingError)
                {
                    logger.LogWarning($"偵測到AGV向前但雷射組數為{Laser.AgvsLsrSetting}(應為{_oriLaser})");
                    await Laser.ModeSwitch(_oriLaser, true);
                }
            });
        }

        protected virtual void EMOTriggerHandler(object? sender, EventArgs e)
        {
            SoftwareEMO(AlarmCodes.EMS);
        }


        protected virtual async Task DOSettingWhenEmoTrigger()
        {
            await WagoDO.SetState(DO_ITEM.Horizon_Motor_Stop, true);
        }

        protected virtual void WagoDI_OnBumpSensorPressed(object? sender, EventArgs e)
        {
            AlarmManager.AddAlarm(AlarmCodes.Bumper, false);
        }

        /// <summary>
        /// 處理車控發佈的 Moduleinformation
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="_ModuleInformation"></param>
        protected virtual void ModuleInformationHandler(object? sender, ModuleInformation _ModuleInformation)
        {
            try
            {

                Stopwatch stopwatch = Stopwatch.StartNew();
                Odometry = _ModuleInformation.Mileage;
                Navigation.StateData = _ModuleInformation.nav_state;
                IMU.StateData = _ModuleInformation.IMU;
                GuideSensor.StateData = _ModuleInformation.GuideSensor;
                BarcodeReader.StateData = _ModuleInformation.reader;
                VerticalDriverState.StateData = _ModuleInformation.Action_Driver;
                if (ForkLifter != null)
                {
                    ForkLifter.fork_ros_controller.CurrentPosition = VerticalDriverState.CurrentPosition;
                }
                for (int i = 0; i < _ModuleInformation.Wheel_Driver.driversState.Length; i++)
                    WheelDrivers[i].StateData = _ModuleInformation.Wheel_Driver.driversState[i];


                var _lastVisitedMapPoint = NavingMap == null ? new AGVSystemCommonNet6.MAP.MapPoint
                {
                    Name = Navigation.LastVisitedTag.ToString(),
                    TagNumber = Navigation.LastVisitedTag
                } : NavingMap.Points.Values.FirstOrDefault(pt => pt.TagNumber == this.Navigation.LastVisitedTag);
                lastVisitedMapPoint = _lastVisitedMapPoint == null ? new AGVSystemCommonNet6.MAP.MapPoint() { Name = "Unknown" } : _lastVisitedMapPoint;
                BatteryStatusUpdate(_ModuleInformation.Battery);

                if (!WaitingForChargeStatusChangeFlag)
                    JudgeIsBatteryCharging();

                stopwatch.Stop();
                ModuleInformationUpdatedInitState = true;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, ex.Message);
            }
            finally
            {
                _ModuleInformation = null;
            }

        }

        protected virtual void BatteryStatusUpdate(BatteryState _BatteryState)
        {
            ushort battery_id = _BatteryState.batteryID;
            if (Batteries.TryGetValue(battery_id, out clsBattery? battery))
            {
                battery.StateData = _BatteryState;
            }
            else
            {
                Batteries.Add(battery_id, new clsBattery()
                {
                    StateData = _BatteryState,
                });
            }
            Batteries = Batteries.ToList().FindAll(b => b.Value != null).ToDictionary(b => b.Key, b => b.Value);
        }


        private MapPoint GetLastVisitedMapPoint()
        {
            if (NavingMap == null)
                return new MapPoint
                {
                    Name = Navigation.LastVisitedTag.ToString(),
                    TagNumber = Navigation.LastVisitedTag
                };
            return NavingMap.Points.Values.FirstOrDefault(pt => pt.TagNumber == Navigation.LastVisitedTag);//虛擬點
        }

        private void HandleSickLocalizationStateChanged(object? sender, LocalizationControllerResultMessage0502 sick_loc_data)
        {
            SickData.StateData = sick_loc_data;
        }
    }
}
