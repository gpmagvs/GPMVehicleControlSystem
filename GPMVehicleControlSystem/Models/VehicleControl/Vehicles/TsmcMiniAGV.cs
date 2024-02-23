using AGVSystemCommonNet6;
using AGVSystemCommonNet6.AGVDispatch.Messages;
using AGVSystemCommonNet6.AGVDispatch.Model;
using AGVSystemCommonNet6.Log;
using AGVSystemCommonNet6.Vehicle_Control.VCS_ALARM;
using GPMVehicleControlSystem.Models.Buzzer;
using GPMVehicleControlSystem.Models.VehicleControl.AGVControl;
using GPMVehicleControlSystem.Models.VehicleControl.VehicleComponent;
using GPMVehicleControlSystem.Models.WorkStation;
using GPMVehicleControlSystem.VehicleControl.DIOModule;
using RosSharp.RosBridgeClient.Actionlib;
using System.Diagnostics;
using System.Net.Sockets;
using static AGVSystemCommonNet6.clsEnums;
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
        public override CARGO_STATUS CargoStatus { get; } = CARGO_STATUS.NO_CARGO_CARRARYING_CAPABILITY;
        public bool IsBattery1Exist => WagoDI.GetState(DI_ITEM.Battery_1_Exist_1) && WagoDI.GetState(DI_ITEM.Battery_1_Exist_2) && !WagoDI.GetState(DI_ITEM.Battery_1_Exist_3) && !WagoDI.GetState(DI_ITEM.Battery_1_Exist_4);
        public bool IsBattery2Exist => WagoDI.GetState(DI_ITEM.Battery_2_Exist_1) && WagoDI.GetState(DI_ITEM.Battery_2_Exist_2) && !WagoDI.GetState(DI_ITEM.Battery_2_Exist_3) && !WagoDI.GetState(DI_ITEM.Battery_2_Exist_4);
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

        private InspectorAGVCarController? OHAAGVC;

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
        protected override void DIOStatusChangedEventRegist()
        {
            base.DIOStatusChangedEventRegist();

            WagoDI.SubsSignalStateChange(DI_ITEM.Horizon_Motor_Error_1, HandleDriversStatusErrorAsync);
            WagoDI.SubsSignalStateChange(DI_ITEM.Horizon_Motor_Error_2, HandleDriversStatusErrorAsync);
            WagoDI.SubsSignalStateChange(DI_ITEM.Horizon_Motor_Error_3, HandleDriversStatusErrorAsync);
            WagoDI.SubsSignalStateChange(DI_ITEM.Horizon_Motor_Error_4, HandleDriversStatusErrorAsync);

            WagoDI.OnEMOButtonPressed += EMOButtonPressedHandler;//巡檢AGVEMO按鈕有獨立的INPUT
        }
        protected async override Task DOSignalDefaultSetting()
        {
            WagoDO.AllOFF();
            await WagoDO.SetState(DO_ITEM.AGV_DiractionLight_R, true);
            await WagoDO.SetState(DO_ITEM.Right_LsrBypass, true);
            await WagoDO.SetState(DO_ITEM.Left_LsrBypass, true);
            await WagoDO.SetState(DO_ITEM.Left_Protection_Sensor_IN_1, true);
            await WagoDO.SetState(DO_ITEM.Left_Protection_Sensor_IN_2, true);
            await WagoDO.SetState(DO_ITEM.Left_Protection_Sensor_IN_3, true);
            await WagoDO.SetState(DO_ITEM.Left_Protection_Sensor_IN_4, true);
            await WagoDO.SetState(DO_ITEM.Instrument_Servo_On, true);
            await Laser.ModeSwitch(0);
        }
        protected virtual void EMOButtonPressedHandler(object? sender, EventArgs e)
        {
            SoftwareEMO(AlarmCodes.EMO_Button);
        }
        bool Laser3rdTriggerHandlerFlag = false;
        protected override async void HandleLaserArea3SinalChange(object? sender, bool di_state)
        {
            if (Operation_Mode == OPERATOR_MODE.MANUAL)
                return;
            if (AGVC.ActionStatus != ActionStatus.ACTIVE)
                return;


            clsIOSignal diState = (clsIOSignal)sender;
            base.HandleLaserArea3SinalChange(sender, di_state);
            bool isLaserTrigger = !di_state;
            bool isFrontLaser = diState.Input == DI_ITEM.FrontProtection_Area_Sensor_3;
            if (isLaserTrigger && !Laser3rdTriggerHandlerFlag)
            {
                Laser3rdTriggerHandlerFlag = true;
                bool result_success;
                Stopwatch sw = Stopwatch.StartNew();
                LOG.WARN($"[TSMC Inspection AGV] Laser 第三段觸發,等待障礙物清除後 Reset Motors");
                while (!WagoDI.GetState(DI_ITEM.FrontProtection_Area_Sensor_3) || !WagoDI.GetState(DI_ITEM.BackProtection_Area_Sensor_3))
                {
                    await Task.Delay(1);
                    if (Sub_Status == SUB_STATUS.DOWN)
                        return;
                }
                await Task.Delay(3000);
                LOG.WARN($"[TSMC Inspection AGV] Reset Motors[After {sw.ElapsedMilliseconds} ms]");
                result_success = await WagoDO.ResetSaftyRelay();
                if (result_success)
                {
                    LOG.WARN($"[TSMC Inspection AGV] Safty relay reset done.");
                    result_success = await ResetMotor();
                    if (result_success)
                    {
                        await AGVC.CarSpeedControl(CarController.ROBOT_CONTROL_CMD.SPEED_Reconvery, isFrontLaser ? CarController.SPEED_CONTROL_REQ_MOMENT.FRONT_LASER_3_RECOVERY : CarController.SPEED_CONTROL_REQ_MOMENT.BACK_LASER_3_RECOVERY);
                        LOG.WARN($"[TSMC Inspection AGV] 馬達已Reset");
                        AlarmManager.ClearAlarm();
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
        protected override void AlarmManager_OnUnRecoverableAlarmOccur(object? sender, AlarmCodes alarm_code)
        {
            if (Laser3rdTriggerHandlerFlag)
                return;
            base.AlarmManager_OnUnRecoverableAlarmOccur(sender, alarm_code);
        }
        protected internal async override void SoftwareEMO(AlarmCodes alarmCode)
        {
            Task.Factory.StartNew(() => BuzzerPlayer.Alarm());
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
                if (Battery1LockNG | Battery2LockNG)
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

        internal override async void ResetHandshakeSignals()
        {
            await WagoDO.SetState(DO_ITEM.AGV_VALID, false);
            await WagoDO.SetState(DO_ITEM.AGV_CS_0, false);
            await WagoDO.SetState(DO_ITEM.AGV_L_REQ, false);
            await WagoDO.SetState(DO_ITEM.AGV_U_REQ, false);
            await WagoDO.SetState(DO_ITEM.AGV_READY, false);
        }
        public override async Task<bool> ResetMotor(bool bypass_when_motor_busy_on = true, string callName = "")
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
            OHAAGVC = AGVC as InspectorAGVCarController;
            OHAAGVC.OnInstrumentMeasureDone += HandleAGVCInstrumentMeasureDone;
        }

        private void HandleAGVCInstrumentMeasureDone(clsMeasureDone result)
        {
            clsMeasureResult measure_result = ParseMeasureData(result.result_cmd);
            measure_result.StartTime = result.start_time;
            measure_result.TaskName = ExecutingTaskModel.RunningTaskData.Task_Name;
            MeasureCompleteInvoke(measure_result);
        }
        internal void MeasureCompleteInvoke(clsMeasureResult measure_result)
        {
            OnMeasureComplete?.Invoke(this, measure_result);
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


        public async Task<bool> Battery1Lock()
        {
            return await ChangeBatteryLockState(1, true);
        }

        public async Task<bool> Battery2Lock()
        {
            return await ChangeBatteryLockState(2, true);
        }

        public async Task<bool> Battery1UnLock()
        {
            return await ChangeBatteryLockState(1, false);
        }

        public async Task<bool> Battery2UnLock()
        {
            return await ChangeBatteryLockState(2, false);
        }
        private async Task<bool> ChangeBatteryLockState(int battery_no, bool lockBattery)
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
                var isBatLocking = battery_no == 1 ? IsBattery1Locked : IsBattery2Locked;
                var lockDO = battery_no == 1 ? DO_ITEM.Battery_1_Lock : DO_ITEM.Battery_2_Lock;
                var unlockDO = battery_no == 1 ? DO_ITEM.Battery_1_Unlock : DO_ITEM.Battery_2_Unlock;
                await OffAllBatLockUnlockDO();
                await Task.Delay(200);
                if (lockBattery)
                    await WagoDO.SetState(lockDO, true);
                else
                    await WagoDO.SetState(unlockDO, true);

                CancellationTokenSource cst = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                while (!(battery_no == 1 ? (lockBattery ? IsBattery1Locked : IsBattery1UnLocked) : (lockBattery ? IsBattery2Locked : IsBattery2UnLocked)))
                {
                    if (cst.IsCancellationRequested)
                    {
                        await OffAllBatLockUnlockDO();
                        return false;
                    }
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

        internal async Task<(bool confirm, string message)> StartMeasure(int tagID)
        {
            (bool confirm, string message) response = await OHAAGVC.StartInstrumentMeasure(tagID);
            return response;
        }

        internal async Task<(bool confirm, string message)> MeasurementInit()
        {
            (bool confirm, string message) init_result = await OHAAGVC.MeasurementInit();
            LOG.INFO($"儀器初始化 {init_result.confirm},{init_result.message}");
            return init_result;
        }

        /// <summary>
        /// 進行定位
        /// </summary>
        /// <returns></returns>
        internal async Task<(bool confirm, string message)> Localization(ushort tagID)
        {
            double current_loc_x = Navigation.Data.robotPose.pose.position.x;
            double current_loc_y = Navigation.Data.robotPose.pose.position.y;
            double theta = Navigation.Angle;
            (bool confrim, string message) result = await OHAAGVC.SetCurrentTagID(tagID, "", current_loc_x, current_loc_y, theta);
            if (!result.confrim)
            {
                AlarmManager.AddWarning(AlarmCodes.Localization_Fail);
            }
            return result;
        }
        protected override async void HandleDriversStatusErrorAsync(object? sender, bool status)
        {
            if (!status)
                return;
            await Task.Delay(1000);
            if (!WagoDI.GetState(DI_ITEM.EMO) | IsResetAlarmWorking)
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
    }
}
