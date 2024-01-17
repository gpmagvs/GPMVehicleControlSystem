using AGVSystemCommonNet6;
using AGVSystemCommonNet6.AGVDispatch.Messages;
using AGVSystemCommonNet6.GPMRosMessageNet.Messages;
using AGVSystemCommonNet6.GPMRosMessageNet.Messages.SickMsg;
using AGVSystemCommonNet6.Log;
using AGVSystemCommonNet6.MAP;
using AGVSystemCommonNet6.Vehicle_Control.VCSDatabase;
using AGVSystemCommonNet6.Vehicle_Control.VCS_ALARM;
using GPMVehicleControlSystem.Models.Buzzer;
using GPMVehicleControlSystem.Models.Emulators;
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
using GPMVehicleControlSystem.Models.VehicleControl.TaskExecute;
using MathNet.Numerics;

namespace GPMVehicleControlSystem.Models.VehicleControl.Vehicles
{
    public partial class Vehicle
    {
        private bool IsLaserRecoveryHandled = false;
        internal bool WaitingForChargeStatusChangeFlag = false;
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
        private bool IsLaserMonitorActived => Operation_Mode == OPERATOR_MODE.AUTO && AGVC.ActionStatus == ActionStatus.ACTIVE;
        protected virtual void CommonEventsRegist()
        {
            //DBhelper.OnDataBaseChanged += CopyDataBaseToLogFolder;
            BuzzerPlayer.OnBuzzerPlay += () => { return Parameters.BuzzerOn; };
            AlarmManager.OnUnRecoverableAlarmOccur += AlarmManager_OnUnRecoverableAlarmOccur;
            AGVC.OnSpeedRecoveryRequesting += HandleSpeedReconveryRequesetRaised;
            AGVC.OnActionSendToAGVCRaising += HandleSendActionGoalToAGVCRaised;
            ChargeTask.OnChargeCircuitOpening += HandleChargeTaskTryOpenChargeCircuit;
            Navigation.OnDirectionChanged += Navigation_OnDirectionChanged;
            Navigation.OnLastVisitedTagUpdate += HandleLastVisitedTagChanged;
            BarcodeReader.OnAGVReachingTag += BarcodeReader_OnAGVReachingTag;
            BarcodeReader.OnAGVLeavingTag += BarcodeReader_OnAGVLeavingTag;
            IMU.OnImuStatesError += HandleIMUStatesError;
            IMU.OnOptionsFetching += () => { return Parameters.ImpactDetection; };
            clsOrderInfo.OnGetPortExistStatus += () => { return HasAnyCargoOnAGV(); };
            OnParamEdited += (param) => { this.Parameters = param; };
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
            WagoDI.OnResetButtonPressed += async (s, e) => await ResetAlarmsAsync(true);
            WagoDI.SubsSignalStateChange(DI_ITEM.RightProtection_Area_Sensor_3, HandleSideLaserSignal);
            WagoDI.SubsSignalStateChange(DI_ITEM.LeftProtection_Area_Sensor_3, HandleSideLaserSignal);
            WagoDI.SubsSignalStateChange(DI_ITEM.FrontProtection_Area_Sensor_1, HandleLaserArea1SinalChange);
            WagoDI.SubsSignalStateChange(DI_ITEM.BackProtection_Area_Sensor_1, HandleLaserArea1SinalChange);
            WagoDI.SubsSignalStateChange(DI_ITEM.FrontProtection_Area_Sensor_2, HandleLaserArea2SinalChange);
            WagoDI.SubsSignalStateChange(DI_ITEM.BackProtection_Area_Sensor_2, HandleLaserArea2SinalChange);
            WagoDI.SubsSignalStateChange(DI_ITEM.FrontProtection_Area_Sensor_3, HandleLaserArea3SinalChange);
            WagoDI.SubsSignalStateChange(DI_ITEM.BackProtection_Area_Sensor_3, HandleLaserArea3SinalChange);
        }
        private bool HandleChargeTaskTryOpenChargeCircuit()
        {
            if (!Parameters.BatteryModule.ChargeWhenLevelLowerThanThreshold)
                return true;
            var threshold = Parameters.BatteryModule.ChargeLevelThreshold;
            bool anyBatteryLevelLowerThreshold = Batteries.Any(bat => bat.Value.Data.batteryLevel < threshold);
            var batlevels = string.Join(",", Batteries.Values.Select(bat => bat.Data.batteryLevel));
            LOG.INFO($"[Charge Circuit Only Open when level lower than threshold({threshold} %)] Charge Circuit Open Check : {(anyBatteryLevelLowerThreshold ? "Allowed" : "Forbid")}|Battery Levels ={batlevels}");
            return anyBatteryLevelLowerThreshold;
        }

        private void HandleIMUStatesError(object? sender, clsIMU.IMUStateErrorEventData imu_event_data)
        {
            RosSharp.RosBridgeClient.MessageTypes.Geometry.Vector3 acc_data = imu_event_data.AccRaw;
            var locInfo = $"當前座標=({Navigation.Data.robotPose.pose.position.x},{Navigation.Data.robotPose.pose.position.y})";
            var thetaInfo = $"當前角度={Navigation.Angle}";
            if (imu_event_data.Imu_AlarmCode == AlarmCodes.IMU_Pitch_State_Error)
                AlarmManager.AddAlarm(AlarmCodes.IMU_Pitch_State_Error, false);
            else
                AlarmManager.AddWarning(imu_event_data.Imu_AlarmCode);
            LOG.WARN($"AGV Status Error:[{imu_event_data.Imu_AlarmCode}]\nLocation: ({locInfo},{thetaInfo}).\nState={imu_event_data.ToJson()}");
        }

        private bool HandleSpeedReconveryRequesetRaised()
        {
            return IsAllLaserNoTrigger().Result;
        }

        /// <summary>
        /// 處理Action Goal發送給車控前的檢查
        /// </summary>
        /// <returns></returns>
        private SendActionCheckResult HandleSendActionGoalToAGVCRaised()
        {
            var _confirmResult = new SendActionCheckResult(SendActionCheckResult.SEND_ACTION_GOAL_CONFIRM_RESULT.Accept);
            if (AGVSResetCmdFlag)
            {
                _confirmResult = new SendActionCheckResult(SendActionCheckResult.SEND_ACTION_GOAL_CONFIRM_RESULT.AGVS_CANCEL_TASK_REQ_RAISED);
            }
            LOG.TRACE($"Before Action Goal Send to AGVC Check Result = {_confirmResult.ToJson()}");
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
                    LOG.TRACE($"DB File Copy spend:{stopwatch.ElapsedMilliseconds} ms", Debugger.IsAttached);
                }
                catch (Exception ex)
                {
                    LOG.Critical(ex);
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
                if (currentPt == null) return;

                if (currentPt.IsCharge)
                {
                    if (IsChargeCircuitOpened)
                        return;
                    Task.Factory.StartNew(async () =>
                    {
                        await WagoDO.SetState(DO_ITEM.Recharge_Circuit, true);
                    });
                }
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
                LOG.WARN("AGV Executing Task and Wago Module Reconnected,and No Obstacle,Send Complex Control Speed Reconvery");
                AGVC.CarSpeedControl(ROBOT_CONTROL_CMD.SPEED_Reconvery, SPEED_CONTROL_REQ_MOMENT.IO_MODULE_RECOVERY);
            }
        }

        private void WagoDI_OnDisonnected(object? sender, EventArgs e)
        {
            if (AGVC?.ActionStatus != ActionStatus.ACTIVE)
                return;
            LOG.WARN("AGV Executing Task but Wago Module Disconnect,Send Complex Control STOP => AGV STOP");
            AGVC.CarSpeedControl(ROBOT_CONTROL_CMD.STOP, SPEED_CONTROL_REQ_MOMENT.IO_MODULE_DISCONNECTED);
        }

        /// <summary>
        /// 處理側邊雷射
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="di_state"></param>
        private async void HandleSideLaserSignal(object? sender, bool di_state)
        {
            if (!IsLaserMonitorActived)
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
                    LOG.ERROR($"{(IsRightLaser ? "Right" : "Left")} Side Laser Flick! ({x},{y},{theta})");
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
                IsLaserRecoveryHandled = false;
                AlarmManager.ClearAlarm(alarm_code);
                if (IsNoObstacleAroundAGV)
                {
                    LOG.INFO($"[{alarm_code}] 側邊雷射雷射恢復.ROBOT_CONTROL_CMD.SPEED_Reconvery");
                    AGVStatusChangeToRunWhenLaserRecovery(ROBOT_CONTROL_CMD.SPEED_Reconvery, IsRightLaser ? SPEED_CONTROL_REQ_MOMENT.RIGHT_LASER_RECOVERY : SPEED_CONTROL_REQ_MOMENT.LEFT_LASER_RECOVERY);
                }
            }
        }

        /// <summary>
        /// 處理雷射第一段觸發
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void HandleLaserArea1SinalChange(object? sender, bool e)
        {
            if (!IsLaserMonitorActived)
                return;

            clsIOSignal diState = (clsIOSignal)sender;
            bool isFrontLaser = diState.Input == DI_ITEM.FrontProtection_Area_Sensor_1;
            bool isLsrBypass = isFrontLaser ? WagoDO.GetState(DO_ITEM.Front_LsrBypass) : WagoDO.GetState(DO_ITEM.Back_LsrBypass);

            if (isLsrBypass)
                return;
            var alarm_code = isFrontLaser ? AlarmCodes.FrontProtection_Area2 : AlarmCodes.BackProtection_Area2;
            if (!diState.State)
            {
                LOG.INFO($"{(isFrontLaser ? "前方" : "後方")} 第一段雷射Trigger.ROBOT_CONTROL_CMD.DECELERATE");
                LogStatausWhenLaserTrigger(alarm_code);
                AlarmManager.AddWarning(alarm_code);
                AGVC.CarSpeedControl(CarController.ROBOT_CONTROL_CMD.DECELERATE, isFrontLaser ? SPEED_CONTROL_REQ_MOMENT.FRONT_LASER_1_TRIGGER : SPEED_CONTROL_REQ_MOMENT.BACK_LASER_1_TRIGGER);
            }
            else
            {
                IsLaserRecoveryHandled = false;
                AlarmManager.ClearAlarm(alarm_code);
                if (await IsAllLaserNoTrigger())
                {
                    LOG.INFO($"{(isFrontLaser ? "前方" : "後方")} 第一段雷射恢復.ROBOT_CONTROL_CMD.SPEED_Reconvery");
                    Task.Factory.StartNew(() =>
                    {
                        AGVStatusChangeToRunWhenLaserRecovery(ROBOT_CONTROL_CMD.SPEED_Reconvery, isFrontLaser ? SPEED_CONTROL_REQ_MOMENT.FRONT_LASER_1_RECOVERY : SPEED_CONTROL_REQ_MOMENT.BACK_LASER_1_RECOVERY);
                    });
                }
            }
        }

        /// <summary>
        /// 處理雷射第二段觸發
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void HandleLaserArea2SinalChange(object? sender, bool e)
        {

            if (!IsLaserMonitorActived)
                return;

            clsIOSignal diState = (clsIOSignal)sender;
            bool isFrontLaser = diState.Input == DI_ITEM.FrontProtection_Area_Sensor_2;
            bool isLsrBypass = isFrontLaser ? WagoDO.GetState(DO_ITEM.Front_LsrBypass) : WagoDO.GetState(DO_ITEM.Back_LsrBypass);
            if (isLsrBypass)
                return;
            var alarm_code = diState.Input == DI_ITEM.FrontProtection_Area_Sensor_2 ? AlarmCodes.FrontProtection_Area3 : AlarmCodes.BackProtection_Area3;

            if (!diState.State)
            {
                LOG.INFO($"{(isFrontLaser ? "前方" : "後方")} 第二段雷射Trigger.ROBOT_CONTROL_CMD.STOP");
                AGVC.CarSpeedControl(ROBOT_CONTROL_CMD.STOP, isFrontLaser ? SPEED_CONTROL_REQ_MOMENT.FRONT_LASER_2_TRIGGER : SPEED_CONTROL_REQ_MOMENT.BACK_LASER_2_TRIGGER);
                LogStatausWhenLaserTrigger(alarm_code);
                AlarmManager.RecordAlarm(alarm_code);
                AGVStatusChangeToAlarmWhenLaserTrigger();
            }
            else
            {
                IsLaserRecoveryHandled = false;
                AlarmManager.ClearAlarm(alarm_code);
                if (IsNoObstacleAroundAGV)
                {
                    LOG.INFO($"{(isFrontLaser ? "前方" : "後方")} 第二段雷射恢復.ROBOT_CONTROL_CMD.DECELERATE");
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
            if (!IsLaserMonitorActived)
                return;

            clsIOSignal diState = (clsIOSignal)sender;

            bool isFrontLaser = diState.Input == DI_ITEM.FrontProtection_Area_Sensor_3;
            bool isLsrBypass = isFrontLaser ? WagoDO.GetState(DO_ITEM.Front_LsrBypass) : WagoDO.GetState(DO_ITEM.Back_LsrBypass);
            if (isLsrBypass)
                return;

            AlarmCodes alarm_code = isFrontLaser ? AlarmCodes.FrontProtection_Area3 : AlarmCodes.BackProtection_Area3;
            if (!di_state)
            {
                LOG.INFO($"{(isFrontLaser ? "前方" : "後方")} 第三段雷射Trigger.ROBOT_CONTROL_CMD.STOP");
                AGVC.CarSpeedControl(ROBOT_CONTROL_CMD.STOP, isFrontLaser ? SPEED_CONTROL_REQ_MOMENT.FRONT_LASER_3_TRIGGER : SPEED_CONTROL_REQ_MOMENT.BACK_LASER_3_TRIGGER);
                LogStatausWhenLaserTrigger(alarm_code);
                AlarmManager.RecordAlarm(alarm_code);
                AGVStatusChangeToAlarmWhenLaserTrigger();
            }
            else
            {
                IsLaserRecoveryHandled = false;
                AlarmManager.ClearAlarm(alarm_code);
                if (IsNoObstacleAroundAGV)
                {
                    LOG.INFO($"{(isFrontLaser ? "前方" : "後方")} 第三段雷射恢復.ROBOT_CONTROL_CMD.DECELERATE");
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

            LOG.TRACE($"{alarm_code} 雷射觸發_當前雷射組數={Laser.Mode}," +
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
            await Task.Delay(1000);
            CancellationTokenSource waitNoObstacleCTS = new CancellationTokenSource();

            if (IsNoObstacleAroundAGV)
            {
                if (Debugger.IsAttached)
                {
                    StaEmuManager.wagoEmu.SetState(DI_ITEM.FrontProtection_Area_Sensor_1, false);
                }
                if (AGVC.ActionStatus == ActionStatus.ACTIVE && !IsLaserRecoveryHandled)
                {
                    IsLaserRecoveryHandled = true;
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
                        LOG.ERROR(ex);
                    }

                }
                if (speed_control == ROBOT_CONTROL_CMD.SPEED_Reconvery)
                {
                    LOG.TRACE($"速度恢復-減速後加速");

                    AGVC.OnSTOPCmdRequesting += HandleSTOPCmdRequesting;
                    await AGVC.CarSpeedControl(ROBOT_CONTROL_CMD.DECELERATE, sPEED_CONTROL_REQ_MOMENT);
                    while (!await IsAllLaserNoTrigger())
                    {
                        await Task.Delay(100);
                        if (waitNoObstacleCTS.IsCancellationRequested)
                        {
                            AGVC.OnSTOPCmdRequesting -= HandleSTOPCmdRequesting;
                            LOG.TRACE($"取消等待:無障礙物後速度恢復，因STOP命令已下達");
                            return;
                        }
                    }
                    AGVC.OnSTOPCmdRequesting -= HandleSTOPCmdRequesting;
                    await Task.Delay(1000);
                    if (!await IsAllLaserNoTrigger())
                        return;
                }
                await AGVC.CarSpeedControl(speed_control, sPEED_CONTROL_REQ_MOMENT);

            }
            void HandleSTOPCmdRequesting(object sender, EventArgs arg)
            {
                waitNoObstacleCTS.Cancel();
            }
        }
        protected void AGVStatusChangeToAlarmWhenLaserTrigger()
        {
            _Sub_Status = SUB_STATUS.ALARM;
            BuzzerPlayer.Alarm();
            StatusLighter.DOWN();
        }

        protected async virtual void HandleDriversStatusErrorAsync(object? sender, bool status)
        {
            if (!status)
                return;

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

            if (Sub_Status != SUB_STATUS.IDLE & Sub_Status != SUB_STATUS.Charging)
            {
                AlarmManager.AddAlarm(alarmCode, false);
                return;
            }

            AlarmManager.AddWarning(alarmCode);
            #region 嘗試Reset馬達
            _ = Task.Factory.StartNew(async () =>
            {
                LOG.WARN($"Horizon Motor IO Alarm, Try auto reset process start");
                await Task.Delay(500);
                while (signal.State) //異常持續存在
                {
                    await Task.Delay(1000);
                    IsMotorReseting = false;
                    await ResetMotorWithWait(TimeSpan.FromSeconds(5), signal, alarmCode);
                }
            });

            #endregion
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
                    LOG.WARN($"Reset Motor Fail:Waiting other motor reset process end TIMEOUT");
                    return false;
                }

            }
            if (!signal.State)
            {
                AlarmManager.ClearAlarm(alarmCode);
                LOG.INFO($"{signal.Name} state {signal.State}, Alarm is reset");
                return true;
            }

            AlarmManager.ClearAlarm(alarmCode);
            return await ResetMotor(false);
        }
        protected DateTime previousSoftEmoTime = DateTime.MinValue;
        protected virtual async void AlarmManager_OnUnRecoverableAlarmOccur(object? sender, AlarmCodes alarm_code)
        {
            AGVC.EmergencyStop();
            SoftwareEMO(alarm_code);
        }


        private void Navigation_OnDirectionChanged(object? sender, clsNavigation.AGV_DIRECTION direction)
        {
            DirectionLighter.LightSwitchByAGVDirection(sender, direction);
            if (AGVC.ActionStatus == ActionStatus.ACTIVE)
            {
                //雷射
                if (direction != clsNavigation.AGV_DIRECTION.STOP)
                    Laser.LaserChangeByAGVDirection(sender, direction);
            }
        }

        protected virtual void EMOTriggerHandler(object? sender, EventArgs e)
        {

            if (Parameters.SimulationMode)
            {
                StaEmuManager.agvRosEmu.SetDriversAlarm(errorCode: 10);

                if (Parameters.AgvType == AGV_TYPE.INSPECTION_AGV)
                {
                    StaEmuManager.wagoEmu.SetState(DI_ITEM.Horizon_Motor_Alarm_1, true);
                    StaEmuManager.wagoEmu.SetState(DI_ITEM.Horizon_Motor_Alarm_2, true);
                    StaEmuManager.wagoEmu.SetState(DI_ITEM.Horizon_Motor_Alarm_3, true);
                    StaEmuManager.wagoEmu.SetState(DI_ITEM.Horizon_Motor_Alarm_4, true);

                    if (Parameters.Version == 1)
                    {
                        StaEmuManager.wagoEmu.SetState(DI_ITEM.Horizon_Motor_Busy_1, false);
                        StaEmuManager.wagoEmu.SetState(DI_ITEM.Horizon_Motor_Busy_2, false);
                        StaEmuManager.wagoEmu.SetState(DI_ITEM.Horizon_Motor_Busy_3, false);
                        StaEmuManager.wagoEmu.SetState(DI_ITEM.Horizon_Motor_Busy_4, false);
                    }
                }
                else
                {

                    StaEmuManager.wagoEmu.SetState(DI_ITEM.Horizon_Motor_Busy_1, false);
                    StaEmuManager.wagoEmu.SetState(DI_ITEM.Horizon_Motor_Busy_2, false);
                    StaEmuManager.wagoEmu.SetState(DI_ITEM.Horizon_Motor_Alarm_1, true);
                    StaEmuManager.wagoEmu.SetState(DI_ITEM.Horizon_Motor_Alarm_2, true);
                }
            }
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

                ushort battery_id = _ModuleInformation.Battery.batteryID;
                if (Batteries.TryGetValue(battery_id, out var battery))
                {
                    battery.StateData = _ModuleInformation.Battery;
                }
                else
                {
                    Batteries.Add(battery_id, new clsBattery()
                    {
                        StateData = _ModuleInformation.Battery,
                    });
                }
                Batteries = Batteries.ToList().FindAll(b => b.Value != null).ToDictionary(b => b.Key, b => b.Value);

                if (!WaitingForChargeStatusChangeFlag)
                    JudgeIsBatteryCharging();

                stopwatch.Stop();
                //if (stopwatch.ElapsedMilliseconds >= AGVC.Throttle_rate_of_Topic_ModuleInfo)
                //    LOG.WARN($"[Thread = {Thread.CurrentThread.ManagedThreadId}] Handle /module_information data time spend= {stopwatch.ElapsedMilliseconds} ms");

            }
            catch (Exception ex)
            {
                LOG.Critical(ex);
            }
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
