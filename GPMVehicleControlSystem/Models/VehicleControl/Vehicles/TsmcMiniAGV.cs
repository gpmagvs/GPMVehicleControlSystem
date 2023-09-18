using AGVSystemCommonNet6.Alarm.VMS_ALARM;
using GPMVehicleControlSystem.Models.Buzzer;
using GPMVehicleControlSystem.Models.VehicleControl.AGVControl;
using GPMVehicleControlSystem.Models.VehicleControl.VehicleComponent;
using GPMVehicleControlSystem.Models.WorkStation;
using GPMVehicleControlSystem.VehicleControl.DIOModule;
using System.Net.Sockets;
using static GPMVehicleControlSystem.VehicleControl.DIOModule.clsDIModule;
using static GPMVehicleControlSystem.VehicleControl.DIOModule.clsDOModule;

namespace GPMVehicleControlSystem.Models.VehicleControl.Vehicles
{
    /// <summary>
    /// 巡檢AGV
    /// </summary>
    public partial class TsmcMiniAGV : Vehicle
    {
        public override CARGO_STATUS CargoStatus { get; protected set; } = CARGO_STATUS.NO_CARGO_CARRARYING_CAPABILITY;
        public bool IsBattery1Exist => WagoDI.GetState(DI_ITEM.Battery_1_Exist_1) && WagoDI.GetState(DI_ITEM.Battery_1_Exist_2) && !WagoDI.GetState(DI_ITEM.Battery_1_Exist_3) && !WagoDI.GetState(DI_ITEM.Battery_1_Exist_4);
        public bool IsBattery2Exist => WagoDI.GetState(DI_ITEM.Battery_2_Exist_1) && WagoDI.GetState(DI_ITEM.Battery_2_Exist_2) && !WagoDI.GetState(DI_ITEM.Battery_2_Exist_3) && !WagoDI.GetState(DI_ITEM.Battery_2_Exist_4);
        public bool IsBattery1Locked => WagoDI.GetState(DI_ITEM.Battery_1_Lock_Sensor);
        public bool IsBattery2Locked => WagoDI.GetState(DI_ITEM.Battery_2_Lock_Sensor);
        public bool IsBattery1UnLocked => WagoDI.GetState(DI_ITEM.Battery_1_Unlock_Sensor);
        public bool IsBattery2UnLocked => WagoDI.GetState(DI_ITEM.Battery_2_Unlock_Sensor);

        public TsmcMiniAGV()
        {
            WheelDrivers = new clsDriver[] {
             new clsDriver{ location = clsDriver.DRIVER_LOCATION.RIGHT_FORWARD},
             new clsDriver{ location = clsDriver.DRIVER_LOCATION.LEFT_FORWARD},
             new clsDriver{ location = clsDriver.DRIVER_LOCATION.RIGHT_BACKWARD},
             new clsDriver{ location = clsDriver.DRIVER_LOCATION.LEFT_BACKWARD},
             };
        }

        private InspectorAGVCarController? InspectorAGVC => AGVC as InspectorAGVCarController;

        public override clsCSTReader CSTReader { get; set; } = null;
        public override clsDirectionLighter DirectionLighter { get; set; } = new clsInspectorAGVDirectionLighter();
        public override Dictionary<ushort, clsBattery> Batteries { get; set; } = new Dictionary<ushort, clsBattery>() {
            {1,new clsBattery{
            } },
            {2,new clsBattery{ } },
        };

        protected override async Task<(bool confirm, string message)> InitializeActions(CancellationTokenSource cancellation)
        {
            //初始化儀器
            //(bool confirm, string message) measurementInitResult = await InspectorAGVC?.MeasurementInit();
            //if (!measurementInitResult.confirm)
            //    return (false, measurementInitResult.message);
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
            return (true, "");
        }
        protected override void DOSignalDefaultSetting()
        {
            base.DOSignalDefaultSetting();
            WagoDO.SetState(DO_ITEM.Left_Protection_Sensor_IN_1, true);
            WagoDO.SetState(DO_ITEM.Left_Protection_Sensor_IN_2, true);
            WagoDO.SetState(DO_ITEM.Left_Protection_Sensor_IN_3, true);
            WagoDO.SetState(DO_ITEM.Left_Protection_Sensor_IN_4, true);
            WagoDO.SetState(DO_ITEM.Instrument_Servo_On, true);

        }
        public override async Task<bool> ResetMotor()
        {
            try
            {
                await WagoDO.ResetSaftyRelay();
                if (!WagoDI.GetState(DI_ITEM.Horizon_Motor_Busy_1) | !WagoDI.GetState(DI_ITEM.Horizon_Motor_Busy_2) |
                    !WagoDI.GetState(DI_ITEM.Horizon_Motor_Busy_3) | !WagoDI.GetState(DI_ITEM.Horizon_Motor_Busy_4))
                {
                    await WagoDO.SetState(DO_ITEM.Horizon_Motor_Stop, true);
                    await Task.Delay(200);
                    await WagoDO.SetState(DO_ITEM.Horizon_Motor_Reset, true);
                    await Task.Delay(200);
                    await WagoDO.SetState(DO_ITEM.Horizon_Motor_Reset, false);
                    await Task.Delay(200);
                    await WagoDO.SetState(DO_ITEM.Horizon_Motor_Stop, false);
                }
                return true;
            }
            catch (SocketException ex)
            {
                AlarmManager.AddAlarm(AlarmCodes.Wago_IO_Write_Fail, false);
                return false;
            }
            catch (Exception ex)
            {
                AlarmManager.AddAlarm(AlarmCodes.Code_Error_In_System, false);
                return false;
            }
        }

        protected override void CreateAGVCInstance(string RosBridge_IP, int RosBridge_Port)
        {
            AGVC = new InspectorAGVCarController(RosBridge_IP, RosBridge_Port);
            (AGVC as InspectorAGVCarController).OnInstrumentMeasureDone += HandleAGVCInstrumentMeasureDone;
        }

        private void HandleAGVCInstrumentMeasureDone(string req_command)
        {
            ParseMeasureData(req_command);
        }

        /// <summary>
        /// 解析量測數據
        /// </summary>
        /// <param name="response_command"></param>
        private clsMeasureResult ParseMeasureData(string response_command)
        {
            string[] command_splited = response_command.Split(',');
            clsMeasureResult mesResult = new clsMeasureResult
            {
                result = command_splited[0],//done/erro,
                location = command_splited[1],
                illuminance = ToIntVal(command_splited[2]),//照度(lux,
                decibel = ToIntVal(command_splited[3]),//分貝(dB,
                temperature = ToDoubleVal(command_splited[4], 100, 2),
                humudity = ToDoubleVal(command_splited[5], 100, 2),
                IPA = ToIntVal(command_splited[6]),
                TVOC = ToDoubleVal(command_splited[7], 10, 1),
                time = command_splited[8],
                partical_03um = ToIntVal(command_splited[9]),
                partical_05um = ToIntVal(command_splited[10]),
                partical_10um = ToIntVal(command_splited[11]),
                partical_30um = ToIntVal(command_splited[12]),
                partical_50um = ToIntVal(command_splited[13]),
                partical_100um = ToIntVal(command_splited[14])
            };
            return mesResult;
        }

        private double ToDoubleVal(string valStr, double ratio, int digitals)
        {
            if (valStr == "NA")
                return 0.0;
            return Math.Round(Convert.ToUInt16(valStr) / ratio, digitals);
        }
        public int ToIntVal(string valStr)
        {
            if (valStr == "NA")
                return 0;
            return Convert.ToInt16(valStr);
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
                await WagoDO.SetState(unlockDO, false);
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
    }
}
