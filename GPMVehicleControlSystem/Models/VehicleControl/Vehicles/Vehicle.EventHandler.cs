using AGVSystemCommonNet6;
using AGVSystemCommonNet6.AGVDispatch.Messages;
using AGVSystemCommonNet6.GPMRosMessageNet.Messages;
using AGVSystemCommonNet6.GPMRosMessageNet.Messages.SickMsg;
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
using GPMVehicleControlSystem.Service;
using Microsoft.Extensions.Caching.Memory;

namespace GPMVehicleControlSystem.Models.VehicleControl.Vehicles
{
    public partial class Vehicle
    {
        private bool IsLaserRecoveryHandled = false;
        internal bool WaitingForChargeStatusChangeFlag = false;
        private Debouncer _vehicleDirectionChangedDebouncer = new Debouncer();
        private Debouncer _videoRecordDebuncer = new Debouncer();
        GuardVideoService guardVideoService = new GuardVideoService();
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
            AGVC.OnSpeedControlChanged += AGVC_OnSpeedControlChanged;
            ChargeTask.OnChargeCircuitOpening += HandleChargeTaskTryOpenChargeCircuit;
            Navigation.OnDirectionChanged += Navigation_OnDirectionChanged;
            Navigation.OnLastVisitedTagUpdate += HandleLastVisitedTagChanged;
            Navigation.OnRoboPoseUpdateTimeout += Navigation_OnRoboPoseUpdateTimeout;
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
            Laser.OnSickApplicationError += Laser_OnSickApplicationError;
            Laser.SubscribeDiagnosticsTopic();

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
                        if (StaSysControl.isAGVCRestarting)
                            return false;
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

            StaSysControl.OnAGVCRestartFinish += async (sender, e) =>
            {
                SendNotifyierToFrontend("車控系統重啟完成!");
            };

        }

        private void AGVC_OnSpeedControlChanged(object? sender, ROBOT_CONTROL_CMD e)
        {
            memoryCache.Set("CurrentRobotSpeedCommand", e.ToString());
            frontendHubContext.Clients.All.SendAsync("CurrentRobotSpeedCommand", e.ToString());
            Task.Factory.StartNew(async () =>
            {
                LogDebugMessage($"當前速度控制->{e}");
            });
        }

        private void Laser_OnSickApplicationError(object? sender, EventArgs e)
        {
            Task.Factory.StartNew(() =>
            {
                logger.LogError($"Sick Laser Application Error, maybe N3 Fatal now");
                SoftwareEMO(AlarmCodes.Sick_Lidar_Application_Error);
            });
        }

        private void Navigation_OnRoboPoseUpdateTimeout(object? sender, EventArgs e)
        {
            Task.Factory.StartNew(() =>
            {
                SoftwareEMO(AlarmCodes.Motion_control_Disconnected);
            });
        }

        private void HandleCSTReaderStateChanged(object? sender, int state)
        {
            LogDebugMessage($"CST Reader State Changed to [{state}]");
        }

        private ManualResetEvent WaitOperatorCheckCargoStatusDone = new ManualResetEvent(false);
        private bool LoadTask_OnManualCheckCargoStatusTrigger(Params.clsManualCheckCargoStatusParams.CheckPointModel checkPointData)
        {
            WaitOperatorCheckCargoStatusDone.Reset();
            frontendHubContext.Clients.All.SendAsync("ManualCheckCargoStatus", checkPointData);
            BuzzerPlayer.SoundPlaying = SOUNDS.WaitingCargoStatusCheck;
            bool checkDone = WaitOperatorCheckCargoStatusDone.WaitOne(TimeSpan.FromSeconds(checkPointData.Timeout));
            logger.LogInformation($"Operator Check Cargo Status Timeout:{!checkDone}");
            BuzzerPlayer.SoundPlaying = SOUNDS.Stop;
            return checkDone;
        }
        internal void ManualCheckCargoStatusDone(string userName = "")
        {
            logger.LogInformation($"Operator {userName} Check Cargo Status Done.");
            WaitOperatorCheckCargoStatusDone.Set();
        }

        internal void CargoStatusManualCheckDoneWhenUnloadFailure(string userName = "")
        {
            logger.LogInformation($"Operator {userName} Check Cargo Status Done when unload but cargo placement state error");
            CargoStateStorer.SetWaitOperatorConfirmCargoStatus();
        }

        private void ClsSick_OnMapMatchStatusToLow(object? sender, EventArgs e)
        {
            AlarmManager.AddWarning(AlarmCodes.Map_Recognition_Rate_Too_Low);
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

            WagoDI.SubsSignalStateChange(DI_ITEM.RightProtection_Area_Sensor_4, (sender, signalState) =>
            {
                if (!signalState && !Parameters.SensorBypass.RightSideLaserBypass)
                    SoftwareEMO(AlarmCodes.Side_Laser_Abnormal);
            });
            WagoDI.SubsSignalStateChange(DI_ITEM.LeftProtection_Area_Sensor_4, (sender, signalState) =>
            {
                if (!signalState && !Parameters.SensorBypass.LeftSideLaserBypass)
                    SoftwareEMO(AlarmCodes.Side_Laser_Abnormal);
            });
            WagoDI.SubsSignalStateChange(DI_ITEM.Horizon_Motor_Switch, HandleHorizon_Motor_SwitchStateChanged);

            WagoDI.SubsSignalStateChange(DI_ITEM.Wheels_Driver_OVER_TEMPERATUR, HandleWheelsDriverOTSignalStateChanged);

            WagoDI.SubsSignalStateChange(DI_ITEM.ZAxis_Driver_OVER_TEMPERATUR, HandleZAxisDriverOTSignalStateChanged);

        }

        private void HandleZAxisDriverOTSignalStateChanged(object? sender, bool state)
        {
            if (!state) //TODO confirm signal is A or B type
                AlarmManager.AddAlarm(AlarmCodes.Vertical_Motor_Driver_Over_Temperature, false);
            else
                AlarmManager.ClearAlarm(AlarmCodes.Vertical_Motor_Driver_Over_Temperature);
        }

        private void HandleWheelsDriverOTSignalStateChanged(object? sender, bool state)
        {
            if (!state) //TODO confirm signal is A or B type
                AlarmManager.AddAlarm(AlarmCodes.Wheel_Motor_Driver_Over_Temperature, false);
            else
                AlarmManager.ClearAlarm(AlarmCodes.Wheel_Motor_Driver_Over_Temperature);
        }

        protected virtual void HandleHorizon_Motor_SwitchStateChanged(object? sender, bool state)
        {
            Task.Factory.StartNew(async () =>
            {
                bool isMoving = AGVC.IsRunning;
                bool switchON = !state;
                if (switchON)
                {
                    bool isResetButtonPressing = WagoDI.GetState(DI_ITEM.Panel_Reset_PB);
                    if (isResetButtonPressing)
                    {
                        if (isMoving)
                            await AGVC.CarSpeedControl(ROBOT_CONTROL_CMD.DECELERATE);
                        await WagoDO.SetState(DO_ITEM.Horizon_Motor_Free, true);
                        DirectionLighter.Backward();
                    }
                }
                else
                {
                    if (isMoving)
                        await AGVC.CarSpeedControl(ROBOT_CONTROL_CMD.DECELERATE);
                    await WagoDO.SetState(DO_ITEM.Horizon_Motor_Free, false);
                    await DirectionLighter.CloseAll();

                    if (isMoving)
                    {
                        Debouncer debouncer = new Debouncer();
                        debouncer.Debounce(async () =>
                        {
                            await AGVC.CarSpeedControl(ROBOT_CONTROL_CMD.SPEED_Reconvery);
                        }, 1000);
                    }
                }
            });
        }

        private async void HandleAGVCActionSuccess(object? sender, EventArgs e)
        {
            if (AGVC.CycleStopActionExecuting)
            {
                LogDebugMessage($"[Action Status Changed To SUCCEEDED by Cycle Stop Action Done Check. ] Action Status is SUCCESSED, reset actionClient.goal = new TaskCommandGoal()");
                AGVC.actionClient.goal = new TaskCommandGoal();
            }
            if (_RunTaskData.Destination == Navigation.LastVisitedTag)
            {
                LogDebugMessage($"Action Status now is SUCCESSED and AGV Position is Destine of Executed Task({Navigation.LastVisitedTag})");
                AGVC.actionClient.goal = new AGVSystemCommonNet6.GPMRosMessageNet.Actions.TaskCommandGoal();
            }
            _ = Task.Run(async () =>
            {
                LogDebugMessage("EndLaserObsMonitorAsync->HandleAGVCActionSuccess");
                await EndLaserObsMonitorAsync();
            });
        }

        private async void HandleAGVCActionActive(object? sender, EventArgs e)
        {
            _ = Task.Run(async () =>
            {
                SetSub_Status(SUB_STATUS.RUN);
                StartLaserObstacleMonitor();
                if (ExecutingTaskEntity.action == ACTION_TYPE.None)
                    BuzzerPlayer.SoundPlaying = SOUNDS.Move;
            });
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
                    await Task.Delay(3000, _ResetButtonOnPressingCts.Token);
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
            ResetAlarmsAsync(true);
            //BuzzerPlayer.Stop("HandleResetButtonPush");
            //await TryResetMotors();
            //AlarmManager.ClearAlarm();
            //BuzzerPlayer.Stop("HandleResetButtonPush");

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
            {
                LogDebugMessage($"要求車控速度恢復但尚有雷射觸發中", true);
                return (false, "要求車控速度恢復但尚有雷射觸發中");
            }
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
                                BuzzerPlayer.SoundPlaying = SOUNDS.Move;
                            else
                                BuzzerPlayer.SoundPlaying = SOUNDS.Action;
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
            BuzzerPlayer.SoundPlaying = SOUNDS.Alarm;
            StatusLighter.DOWN();
        }
        public REMOTE_MODE RemoteModeWhenHorizonMotorAlarm { get; protected set; } = REMOTE_MODE.OFFLINE;
        protected async virtual void HandleDriversStatusErrorAsync(object? sender, bool status)
        {

            if (!status || StaSysControl.isAGVCRestarting)
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

        protected DateTime previousSoftEmoTime = DateTime.MinValue;
        protected virtual async void AlarmManager_OnUnRecoverableAlarmOccur(object? sender, AlarmCodes alarm_code)
        {
            _ = Task.Run(async () =>
            {
                await AGVC.EmergencyStop(true);
            });
            SoftwareEMO(alarm_code);
        }

        internal async Task StartRecordViedo()
        {
            _ = Task.Factory.StartNew(async () =>
            {
                _videoRecordDebuncer.Debounce(async () =>
                {
                    logger.LogInformation("嚴重異常觸發錄影守衛");

                    bool startSucess = await guardVideoService.StartRecord();
                    logger.LogInformation($"錄影守衛啟動:{(startSucess ? "成功" : "失敗!!!!!")}");

                }, 1000);
            });
        }

        protected virtual async void Navigation_OnDirectionChanged(object? sender, clsNavigation.AGV_DIRECTION direction)
        {
            Laser.agvDirection = direction;
            int _debunceDelay = direction == AGV_DIRECTION.BYPASS ? 10 : 100;
            if (direction == AGV_DIRECTION.FORWARD)
                AGVC.CarSpeedControl(ROBOT_CONTROL_CMD.SPEED_Reconvery);
            _vehicleDirectionChangedDebouncer.Debounce(() =>
            {
                if (AGVC.ActionStatus == ActionStatus.ACTIVE && direction != clsNavigation.AGV_DIRECTION.REACH_GOAL)
                    Laser.LaserChangeByAGVDirection(sender, direction);
                DirectionLighter.LightSwitchByAGVDirection(sender, direction);
            }, _debunceDelay);
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
                frontendHubContext.Clients.All.SendAsync("ModuleInformation", _ModuleInformation);
                Stopwatch stopwatch = Stopwatch.StartNew();
                Odometry = _ModuleInformation.Mileage;
                Navigation.StateData = _ModuleInformation.nav_state;
                IMU.StateData = _ModuleInformation.IMU;
                GuideSensor.StateData = _ModuleInformation.GuideSensor;
                BarcodeReader.StateData = _ModuleInformation.reader;
                VerticalDriverState.StateData = _ModuleInformation.Action_Driver;

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
                StaSysControl.isAGVCRestarting = false;
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


        private void HandleSickLocalizationStateChanged(object? sender, LocalizationControllerResultMessage0502 sick_loc_data)
        {
            SickData.StateData = sick_loc_data;
        }
    }
}
