using AGVSystemCommonNet6;
using AGVSystemCommonNet6.AGVDispatch.Messages;
using AGVSystemCommonNet6.AGVDispatch.Model;
using AGVSystemCommonNet6.GPMRosMessageNet.Messages;
using AGVSystemCommonNet6.Log;
using AGVSystemCommonNet6.Vehicle_Control.VCS_ALARM;
using GPMVehicleControlSystem.Models.Buzzer;
using GPMVehicleControlSystem.Models.VehicleControl.AGVControl;
using GPMVehicleControlSystem.Models.VehicleControl.VehicleComponent;
using GPMVehicleControlSystem.Models.WorkStation;
using GPMVehicleControlSystem.VehicleControl.DIOModule;
using RosSharp.RosBridgeClient.Actionlib;
using System.Diagnostics;
using System.Linq;
using System.Net.Sockets;
using System.Reflection.Metadata;
using static AGVSystemCommonNet6.clsEnums;
using static GPMVehicleControlSystem.Models.VehicleControl.AGVControl.CarController;
using static GPMVehicleControlSystem.Models.VehicleControl.AGVControl.InspectorAGVCarController;
using static GPMVehicleControlSystem.VehicleControl.DIOModule.clsDIModule;
using static GPMVehicleControlSystem.VehicleControl.DIOModule.clsDOModule;

namespace GPMVehicleControlSystem.Models.VehicleControl.Vehicles
{
    /// <summary>
    /// 巡檢AGV
    /// </summary>
    public partial class TsmcMiniAGV : Vehicle
    {

        public enum BAT_LOCK_ACTION
        {
            LOCK, UNLOCK,
            Stop
        }

        public override CARGO_STATUS CargoStatus { get; } = CARGO_STATUS.NO_CARGO_CARRARYING_CAPABILITY;
        public virtual bool IsBattery1Exist => WagoDI.GetState(DI_ITEM.Battery_1_Exist_1) && WagoDI.GetState(DI_ITEM.Battery_1_Exist_2) && !WagoDI.GetState(DI_ITEM.Battery_1_Exist_3) && !WagoDI.GetState(DI_ITEM.Battery_1_Exist_4);
        public virtual bool IsBattery2Exist => WagoDI.GetState(DI_ITEM.Battery_2_Exist_1) && WagoDI.GetState(DI_ITEM.Battery_2_Exist_2) && !WagoDI.GetState(DI_ITEM.Battery_2_Exist_3) && !WagoDI.GetState(DI_ITEM.Battery_2_Exist_4);
        public bool IsBattery1Locked => WagoDI.GetState(DI_ITEM.Battery_1_Lock_Sensor);
        public bool IsBattery2Locked => WagoDI.GetState(DI_ITEM.Battery_2_Lock_Sensor);
        public bool IsBattery1UnLocked => WagoDI.GetState(DI_ITEM.Battery_1_Unlock_Sensor);
        public bool IsBattery2UnLocked => WagoDI.GetState(DI_ITEM.Battery_2_Unlock_Sensor);

        public event EventHandler<clsMeasureResult> OnMeasureComplete;

        public TsmcMiniAGV()
        {
            WheelDrivers = new clsDriver[] {
             new clsDriver{ location = clsDriver.DRIVER_LOCATION.RIGHT_FORWARD},
             new clsDriver{ location = clsDriver.DRIVER_LOCATION.LEFT_FORWARD},
             new clsDriver{ location = clsDriver.DRIVER_LOCATION.RIGHT_BACKWARD},
             new clsDriver{ location = clsDriver.DRIVER_LOCATION.LEFT_BACKWARD},
             };
        }

        protected InspectorAGVCarController? MiniAgvAGVC = new InspectorAGVCarController();

        public override clsCSTReader CSTReader { get; set; } = null;
        public override clsDirectionLighter DirectionLighter { get; set; } = new clsInspectorAGVDirectionLighter();
        public override Dictionary<ushort, clsBattery> Batteries { get; set; } = new Dictionary<ushort, clsBattery>() {
            {1,new clsBattery{
            } },
            {2,new clsBattery{ } },
        };

        /// <summary>
        /// 巡檢AGV沒有充電迴路
        /// </summary>
        public override bool IsChargeCircuitOpened => false;

        protected internal override async Task InitAGVControl(string RosBridge_IP, int RosBridge_Port)
        {
            await base.InitAGVControl(RosBridge_IP, RosBridge_Port);
        }
        protected override async void Navigation_OnDirectionChanged(object? sender, clsNavigation.AGV_DIRECTION direction)
        {

            bool frontLaserActive = direction != clsNavigation.AGV_DIRECTION.BACKWARD &&
                direction != clsNavigation.AGV_DIRECTION.BACKWARD_OBSTACLE &&
                direction != clsNavigation.AGV_DIRECTION.AVOID_OBSTACLE;

            bool backLaserActive = direction == clsNavigation.AGV_DIRECTION.BACKWARD;

            await Laser.FrontBackLasersEnable(frontLaserActive, backLaserActive);

            base.Navigation_OnDirectionChanged(sender, direction);

        }

        internal override void CreateLaserInstance()
        {
            Laser = new clsAMCLaser(WagoDO, WagoDI)
            {
                Spin_Laser_Mode = Parameters.Spin_Laser_Mode
            };
        }

        protected override void DIOStatusChangedEventRegist()
        {
            base.DIOStatusChangedEventRegist();

            WagoDI.SubsSignalStateChange(DI_ITEM.Horizon_Motor_Error_1, HandleDriversStatusErrorAsync);
            WagoDI.SubsSignalStateChange(DI_ITEM.Horizon_Motor_Error_2, HandleDriversStatusErrorAsync);
            WagoDI.SubsSignalStateChange(DI_ITEM.Horizon_Motor_Error_3, HandleDriversStatusErrorAsync);
            WagoDI.SubsSignalStateChange(DI_ITEM.Horizon_Motor_Error_4, HandleDriversStatusErrorAsync);

            WagoDI.SubsSignalStateChange(DI_ITEM.Safty_PLC_Output, HandleSaftyPLCOutputStatusChanged);

            //WagoDI.SubsSignalStateChange(DI_ITEM.FrontProtection_Area_Sensor_3, HandleLaserTriggerSaftyRelay);
            //WagoDI.SubsSignalStateChange(DI_ITEM.BackProtection_Area_Sensor_3, HandleLaserTriggerSaftyRelay);
            //WagoDI.SubsSignalStateChange(DI_ITEM.RightProtection_Area_Sensor_3, HandleLaserTriggerSaftyRelay);
            //WagoDI.SubsSignalStateChange(DI_ITEM.LeftProtection_Area_Sensor_3, HandleLaserTriggerSaftyRelay);
            //WagoDI.SubsSignalStateChange(DI_ITEM.RightProtection_Area_Sensor_1, HandleSideLaserArea1SinalChange);
            //WagoDI.SubsSignalStateChange(DI_ITEM.LeftProtection_Area_Sensor_1, HandleSideLaserArea1SinalChange);

            WagoDI.SubsSignalStateChange(DI_ITEM.Front_Right_Ultrasound_Sensor, HandleUltrasoundSensorTrigger);
            WagoDI.SubsSignalStateChange(DI_ITEM.Back_Left_Ultrasound_Sensor, HandleUltrasoundSensorTrigger);

            WagoDI.OnEMOButtonPressed += EMOButtonPressedHandler;//巡檢AGVEMO按鈕有獨立的INPUT
        }

        private async void HandleSaftyPLCOutputStatusChanged(object? sender, bool io_state)
        {
            //B接點 > OFF表示異常
            bool _SaftyPLCOuputError = !io_state;

            if (!_SaftyPLCOuputError)
                return;
            if (AGVC.ActionStatus != ActionStatus.ACTIVE && AGVC.ActionStatus != ActionStatus.PENDING)
                return;
            LOG.TRACE($"Safty PLC Ouput Error [HandleSaftyPLCOutputStatusChanged]");
            while (!_SaftyRelayResetAllow())
            {
                await Task.Delay(100);
            }
            await WagoDO.ResetSaftyRelay();

            if (!WagoDI.GetState(DI_ITEM.Safty_PLC_Output))
            {
                LOG.WARN($"Reset Safty Relay Done But Safty_PLC_Output still OFF. Recall HandleSaftyPLCOutputStatusChanged");
                HandleSaftyPLCOutputStatusChanged(sender, false);
                return;
            }
            LOG.TRACE($"No Obstacle. Reset Safty Relay.  [HandleSaftyPLCOutputStatusChanged]");

            bool _SaftyRelayResetAllow()
            {
                return AGVC.CurrentSpeedControlCmd != ROBOT_CONTROL_CMD.STOP;
            }
        }

        private async void HandleUltrasoundSensorTrigger(object? sender, bool state)
        {
            if (!IsSaftyProtectActived)
                return;
            clsIOSignal signal = (clsIOSignal)sender;
            var input = signal.Input;
            if (state)
            {
                LOG.TRACE($"{input} Trigger! AGV STOP");
                await AGVC.CarSpeedControl(ROBOT_CONTROL_CMD.STOP, SPEED_CONTROL_REQ_MOMENT.UltrasoundSensor, false);
            }
            else
            {
                LOG.TRACE($"{input} Recovery! AGV Speed Recovery");
                await AGVC.CarSpeedControl(ROBOT_CONTROL_CMD.SPEED_Reconvery, SPEED_CONTROL_REQ_MOMENT.UltrasoundSensorRecovery, true);

            }

        }

        private SemaphoreSlim _LaserAreaHhandleslim = new SemaphoreSlim(1, 1);

        bool Laser3rdTriggerHandlerFlag = false;
        private async void HandleLaserTriggerSaftyRelay(object? sender, bool state)
        {
            await _LaserAreaHhandleslim.WaitAsync();
            try
            {

                if (!IsSaftyProtectActived)
                    return;

                clsIOSignal inputIO = (clsIOSignal)sender;

                bool IsLaserBypass(clsIOSignal input)
                {
                    if (input.Input == DI_ITEM.RightProtection_Area_Sensor_3)
                        return WagoDO.GetState(DO_ITEM.Right_LsrBypass);

                    else if (input.Input == DI_ITEM.LeftProtection_Area_Sensor_3)
                        return WagoDO.GetState(DO_ITEM.Left_LsrBypass);

                    else if (input.Input == DI_ITEM.FrontProtection_Area_Sensor_3)
                        return WagoDO.GetState(DO_ITEM.Front_LsrBypass);
                    else if (input.Input == DI_ITEM.BackProtection_Area_Sensor_3)
                        return WagoDO.GetState(DO_ITEM.Back_LsrBypass);
                    return true;
                }
                if (IsLaserBypass(inputIO))
                    return;

                bool LaserTrigger = !state;
                if (LaserTrigger)
                {
                    Laser3rdTriggerHandlerFlag = true;
                    while (!IsAllLaserNoTrigger())
                    {
                        LOG.TRACE($"等待障礙物移除");

                        if (GetSub_Status() == SUB_STATUS.DOWN)
                            return;

                        if (IsLaserBypass(inputIO) || !IsSaftyProtectActived)
                            break;

                        await Task.Delay(1000);
                    }
                    await Task.Delay(1000);

                    if (GetSub_Status() == SUB_STATUS.DOWN)
                        return;

                    var safty_relay_reset_result = await WagoDO.ResetSaftyRelay();
                    if (safty_relay_reset_result)
                    {
                        LOG.WARN($"[TSMC Inspection AGV] Safty relay reset done.");
                        safty_relay_reset_result = await ResetMotor(false);
                        if (safty_relay_reset_result)
                        {
                            LOG.WARN($"[TSMC Inspection AGV] 馬達已Reset");
                            AlarmManager.ClearAlarm();
                            //BuzzerPlayer.Stop();
                            //await Task.Delay(100);

                            //if (AGVC.ActionStatus == ActionStatus.ACTIVE)
                            //{
                            //    if (_RunTaskData.Action_Type == ACTION_TYPE.None)
                            //        BuzzerPlayer.Move();
                            //    else
                            //        BuzzerPlayer.Action();

                            //    await Task.Delay(1000);
                            //}
                            //await AGVC.CarSpeedControl(CarController.ROBOT_CONTROL_CMD.SPEED_Reconvery, SPEED_CONTROL_REQ_MOMENT.LASER_RECOVERY);

                        }
                        else
                        {
                            LOG.WARN($"[TSMC Inspection AGV] 馬達Reset失敗");
                        }
                    }
                    else
                        LOG.WARN($"[TSMC Inspection AGV] Safty relay reset 失敗");
                    Laser3rdTriggerHandlerFlag = false;

                }
            }
            catch (Exception ex)
            {
                LOG.Critical(ex);
            }
            finally
            {
                _LaserAreaHhandleslim.Release();
            }

        }

        protected async override Task DOSignalDefaultSetting()
        {
            //WagoDO.AllOFF();
            await WagoDO.SetState(DO_ITEM.AGV_DiractionLight_R, true);
            await WagoDO.SetState(DO_ITEM.Right_LsrBypass, true);
            await WagoDO.SetState(DO_ITEM.Left_LsrBypass, true);
            await WagoDO.SetState(DO_ITEM.Left_Protection_Sensor_IN_1, true);
            await WagoDO.SetState(DO_ITEM.Left_Protection_Sensor_IN_2, true);
            await WagoDO.SetState(DO_ITEM.Left_Protection_Sensor_IN_3, true);
            await WagoDO.SetState(DO_ITEM.Left_Protection_Sensor_IN_4, true);
            await WagoDO.SetState(DO_ITEM.Instrument_Servo_On, true);
            await Laser.ModeSwitch(16);
        }
        protected virtual void EMOButtonPressedHandler(object? sender, EventArgs e)
        {
            SoftwareEMO(AlarmCodes.EMO_Button);
        }

        protected override void AlarmManager_OnUnRecoverableAlarmOccur(object? sender, AlarmCodes alarm_code)
        {
            if (AGVC.ActionStatus == ActionStatus.ACTIVE && Laser3rdTriggerHandlerFlag && alarm_code != AlarmCodes.Bumper)
                return;
            base.AlarmManager_OnUnRecoverableAlarmOccur(sender, alarm_code);
        }
        protected internal async override void SoftwareEMO(AlarmCodes alarmCode)
        {
            BuzzerPlayer.Alarm();
            if (Laser3rdTriggerHandlerFlag)
            {
                LOG.WARN($"EMS Trigger by Laser 3rd and Reset process is running, No Abort Task and AGV is not Down Status");
                return;
            }
            base.SoftwareEMO(alarmCode);
        }
        protected internal override void SoftwareEMO()
        {
            base.SoftwareEMO();
        }
        internal override async void ResetHandshakeSignals()
        {
            base.ResetHandshakeSignals();
            await WagoDO.SetState(DO_ITEM.AGV_L_REQ, false);
            await WagoDO.SetState(DO_ITEM.AGV_U_REQ, false);
            await WagoDO.SetState(DO_ITEM.AGV_CS_0, false);
            await WagoDO.SetState(DO_ITEM.AGV_CS_1, false);
            await WagoDO.SetState(DO_ITEM.AGV_Check_REQ, false);

        }
        protected override bool CheckMotorIOError()
        {
            return WagoDI.GetState(DI_ITEM.Horizon_Motor_Error_1) || WagoDI.GetState(DI_ITEM.Horizon_Motor_Error_2) || WagoDI.GetState(DI_ITEM.Horizon_Motor_Error_3) || WagoDI.GetState(DI_ITEM.Horizon_Motor_Error_4);
        }
        protected override bool CheckEMOButtonNoRelease()
        {
            return WagoDI.GetState(DI_ITEM.EMO_Button);
        }
        protected override async Task<(bool confirm, string message)> InitializeActions(CancellationTokenSource cancellation)
        {
            //初始化儀器
            //(bool confirm, string message) measurementInitResult = await InspectorAGVC?.MeasurementInit();
            //if (!measurementInitResult.confirm)
            //    return (false, measurementInitResult.message);
            if (Parameters.InspectionAGV.CheckBatteryLockStateWhenInit)
            {
                bool Battery1LockNG = IsBattery1Exist && !IsBattery1Locked;
                bool Battery2LockNG = IsBattery2Exist && !IsBattery2Locked;
                if (Battery1LockNG || Battery2LockNG)
                {
                    string err_msg = "";
                    if (IsBattery1Exist)
                    {
                        err_msg += "電池1 ";
                        AlarmManager.AddWarning(AlarmCodes.Battery1_Not_Lock);
                    }
                    if (IsBattery2Exist)
                    {
                        err_msg += " 電池2";
                        AlarmManager.AddWarning(AlarmCodes.Battery2_Not_Lock);
                    }
                    err_msg += " 尚未Lock";
                    return (false, $"[{AlarmCodes.Battery_Not_Lock}] {err_msg}");
                }
            }
            return (true, "");
        }

        public override async Task<bool> ResetMotor(bool triggerByResetButtonPush, bool bypass_when_motor_busy_on = true)
        {
            try
            {
                await WagoDO.ResetSaftyRelay();
                if (!CheckMotorIOError())
                    return true;
                bool io_write_success = await WagoDO.SetState(DO_ITEM.Horizon_Motor_Stop, true);
                if (!io_write_success)
                    return false;
                await Task.Delay(200);
                io_write_success = await WagoDO.SetState(DO_ITEM.Horizon_Motor_Reset, true);
                if (!io_write_success)
                    return false;
                await Task.Delay(200);
                io_write_success = await WagoDO.SetState(DO_ITEM.Horizon_Motor_Reset, false);
                if (!io_write_success)
                    return false;
                await Task.Delay(200);
                io_write_success = await WagoDO.SetState(DO_ITEM.Horizon_Motor_Stop, false);
                if (!io_write_success)
                    return false;
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message + ex.StackTrace);
                Environment.Exit(0);
                AlarmManager.AddAlarm(AlarmCodes.Code_Error_In_System, false);
                return false;
            }
        }

        protected override void CreateAGVCInstance(string RosBridge_IP, int RosBridge_Port)
        {
            AGVC = new InspectorAGVCarController(RosBridge_IP, RosBridge_Port);
            MiniAgvAGVC = AGVC as InspectorAGVCarController;
            MiniAgvAGVC.OnInstrumentMeasureDone += HandleAGVCInstrumentMeasureDone;
        }


        internal void MeasureCompleteInvoke(clsMeasureResult measure_result)
        {
            OnMeasureComplete?.Invoke(this, measure_result);
        }

        public int ToIntVal(string valStr)
        {
            if (valStr == "NA")
                return -1;
            if (int.TryParse(valStr, out int val))
                return val;
            else
            {
                LOG.WARN($"int convert fail. convert ${valStr} to int fail");
                return -1;
            }
        }

        /// <summary>
        /// 鎖定1號電池
        /// </summary>
        /// <returns></returns>
        public virtual async Task<bool> Battery1Lock()
        {
            LOG.TRACE("Mini AGV- Try Lock Battery No.1");
            return await ChangeBatteryLockState(1, BAT_LOCK_ACTION.LOCK);
        }

        /// <summary>
        /// 鎖定2號電池
        /// </summary>
        /// <returns></returns>
        public virtual async Task<bool> Battery2Lock()
        {
            LOG.TRACE("Mini AGV- Try Lock Battery No.2");
            return await ChangeBatteryLockState(2, BAT_LOCK_ACTION.LOCK);
        }

        /// <summary>
        /// 解鎖1號電池
        /// </summary>
        /// <returns></returns>
        public virtual async Task<bool> Battery1UnLock()
        {
            if (!IsLockActionAllow(1, out string rejectReason))
            {
                return false;
            }
            LOG.TRACE("Mini AGV- Try Unlock Battery No.1");
            return await ChangeBatteryLockState(1, BAT_LOCK_ACTION.UNLOCK);
        }
        /// <summary>
        /// 解鎖2號電池
        /// </summary>
        /// <returns></returns>
        public virtual async Task<bool> Battery2UnLock()
        {
            if (!IsLockActionAllow(2, out string rejectReason))
            {
                return false;
            }
            LOG.TRACE("Mini AGV- Try Unlock Battery No.2");
            return await ChangeBatteryLockState(2, BAT_LOCK_ACTION.UNLOCK);
        }
        protected virtual bool IsUnlockActionAllow(int toLockBatNumber, out string rejectReason)
        {
            rejectReason = "";
            return true;
        }
        protected virtual bool IsLockActionAllow(int toLockBatNumber, out string rejectReason)
        {
            rejectReason = "";
            return true;
        }
        /// <summary>
        /// 等待電池完成鎖定
        /// </summary>
        /// <param name="battery_no">電池編號</param>
        /// <returns></returns>
        protected bool WaitBatteryLocked(int battery_no)
        {
            CancellationTokenSource cst = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            bool IsBatLocked(int battery_no)
            {
                return (battery_no == 1 ? IsBattery1Locked : IsBattery2Locked);
            }
            LOG.TRACE($"Start wait battery-{battery_no} Locked done...");
            while (!IsBatLocked(battery_no))
            {
                Thread.Sleep(1);
                if (cst.IsCancellationRequested)
                {
                    LOG.WARN($"Battery-{battery_no} [Lock] LOCK Sensor 檢知 Timeout");
                    break;
                }
            }
            return IsBatLocked(battery_no);
        }
        /// <summary>
        /// 等待電池完成解鎖
        /// </summary>
        /// <param name="battery_no">電池編號</param>
        /// <returns></returns>
        protected bool WaitBatteryUnLocked(int battery_no)
        {
            CancellationTokenSource cst = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            bool IsBatUnLocked(int battery_no)
            {
                return (battery_no == 1 ? IsBattery1UnLocked : IsBattery2UnLocked);
            }
            LOG.TRACE($"Start wait battery-{battery_no} Unlocked done...");
            while (!IsBatUnLocked(battery_no))
            {
                Thread.Sleep(1);
                if (cst.IsCancellationRequested)
                {
                    LOG.WARN($"Battery-{battery_no} [Unlock] UNLOCK Sensor 檢知  Timeout");
                    break;
                }
            }
            return IsBatUnLocked(battery_no);
        }

        internal async Task<(bool confirm, string message)> StartMeasure(int tagID)
        {
            (bool confirm, string message) response = await MiniAgvAGVC.StartInstrumentMeasure(tagID);
            return response;
        }

        internal async Task<(bool confirm, string message)> MeasurementInit()
        {
            (bool confirm, string message) init_result = await MiniAgvAGVC.MeasurementInit();
            LOG.INFO($"儀器初始化 {init_result.confirm},{init_result.message}");
            return init_result;
        }

        protected override async void HandleDriversStatusErrorAsync(object? sender, bool status)
        {
            if (!status)
                return;
            await Task.Delay(1000);
            if (!WagoDI.GetState(DI_ITEM.EMO) || IsResetAlarmWorking)
                return;

            clsIOSignal signal = (clsIOSignal)sender;
            var input = signal?.Input;
            var alarmCode = AlarmCodes.Wheel_Motor_IO_Error_Left_Front;
            if (input == DI_ITEM.Horizon_Motor_Error_1)
                alarmCode = AlarmCodes.Wheel_Motor_IO_Error_Left_Front;
            if (input == DI_ITEM.Horizon_Motor_Error_2)
                alarmCode = AlarmCodes.Wheel_Motor_IO_Error_Left_Rear;
            if (input == DI_ITEM.Horizon_Motor_Error_3)
                alarmCode = AlarmCodes.Wheel_Motor_IO_Error_Right_Front;
            if (input == DI_ITEM.Horizon_Motor_Error_4)
                alarmCode = AlarmCodes.Wheel_Motor_IO_Error_Right_Rear;
            AlarmManager.AddAlarm(alarmCode, false);
        }
        protected override async void HandshakeIOOff()
        {
            //await WagoDO.SetState(DO_ITEM.AGV_TR_REQ, false);
        }
        protected override async Task TryResetMotors()
        {
            await WagoDO.SetState(DO_ITEM.Safety_Relays_Reset, true);
            await Task.Delay(100);
            await WagoDO.SetState(DO_ITEM.Safety_Relays_Reset, false);
        }
        protected override bool IsAnyMotorAlarm()
        {
            return WagoDI.GetState(DI_ITEM.Horizon_Motor_Alarm_1) || WagoDI.GetState(DI_ITEM.Horizon_Motor_Alarm_2) ||
               WagoDI.GetState(DI_ITEM.Horizon_Motor_Alarm_3) || WagoDI.GetState(DI_ITEM.Horizon_Motor_Alarm_4);
        }
        #region Private Methods
        private void HandleAGVCInstrumentMeasureDone(clsMeasureDone result)
        {
            clsMeasureResult measure_result = ParseMeasureData(result.result_cmd);
            measure_result.StartTime = result.start_time;
            measure_result.TaskName = ExecutingTaskEntity.RunningTaskData.Task_Name;
            MeasureCompleteInvoke(measure_result);
        }
        /// <summary>
        /// 解析量測數據
        /// </summary>
        /// <param name="response_command"></param>
        private clsMeasureResult ParseMeasureData(string response_command)
        {
            string[] command_splited = response_command.Split(',');
            clsMeasureResult mesResult = new clsMeasureResult(Navigation.LastVisitedTag)
            {
                result = command_splited[0],//done/error,
                location = command_splited[1],
                illuminance = ToIntVal(command_splited[2]),//照度(lux,
                decibel = ToIntVal(command_splited[3]),//分貝(dB,
                temperature = ToDoubleVal(command_splited[4], 100, 2),
                humudity = ToDoubleVal(command_splited[5], 100, 2),
                IPA = ToIntVal(command_splited[6]),
                TVOC = ToDoubleVal(command_splited[7], 10, 1),
                Acetone = ToIntVal(command_splited[8]),
                time = command_splited[9],
                partical_03um = ToIntVal(command_splited[10]),
                partical_05um = ToIntVal(command_splited[11]),
                partical_10um = ToIntVal(command_splited[12]),
                partical_30um = ToIntVal(command_splited[13]),
                partical_50um = ToIntVal(command_splited[14]),
                partical_100um = ToIntVal(command_splited[15]),
                PID = ToIntVal(command_splited[16]),

            };
            LOG.INFO($"解析儀器量測數值完成:{mesResult.ToJson()}");
            return mesResult;
        }

        private double ToDoubleVal(string valStr, double ratio, int digitals)
        {
            if (valStr == "NA")
                return -1.0;
            int intVal = -1;
            if ((intVal = ToIntVal(valStr)) != -1)
            {
                return Math.Round(intVal / ratio, digitals);
            }
            else
                return -1.0;
        }
        private async Task<bool> ChangeBatteryLockState(int battery_no, BAT_LOCK_ACTION action)
        {
            var noLockAlarmCode = battery_no == 1 ? AlarmCodes.Battery1_Not_Lock : AlarmCodes.Battery2_Not_Lock;
            try
            {
                async Task OffAllBatLockUnlockDO()
                {
                    await WagoDO.SetState(DO_ITEM.Battery_1_Lock, false);
                    await WagoDO.SetState(DO_ITEM.Battery_2_Lock, false);
                    await WagoDO.SetState(DO_ITEM.Battery_1_Unlock, false);
                    await WagoDO.SetState(DO_ITEM.Battery_2_Unlock, false);
                }
                var lockDO = battery_no == 1 ? DO_ITEM.Battery_1_Lock : DO_ITEM.Battery_2_Lock;
                var unlockDO = battery_no == 1 ? DO_ITEM.Battery_1_Unlock : DO_ITEM.Battery_2_Unlock;
                await OffAllBatLockUnlockDO();
                await Task.Delay(200);
                if (action == BAT_LOCK_ACTION.LOCK)
                {
                    await WagoDO.SetState(lockDO, true);
                    WaitBatteryLocked(battery_no);
                }
                else
                {
                    await WagoDO.SetState(unlockDO, true);
                    WaitBatteryUnLocked(battery_no);
                }
                await OffAllBatLockUnlockDO();
                return true;
            }
            catch (Exception)
            {
                AlarmManager.AddWarning(noLockAlarmCode);
                return false;
            }
        }

        #endregion
    }
}
