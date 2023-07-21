using AGVSystemCommonNet6.Alarm.VMS_ALARM;
using GPMVehicleControlSystem.Models.Buzzer;
using GPMVehicleControlSystem.Models.VehicleControl.AGVControl;
using GPMVehicleControlSystem.Models.VehicleControl.VehicleComponent;
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

        internal override async Task<(bool confirm, string message)> Initialize()
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

            return await base.Initialize();
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
                if (!WagoDI.GetState(clsDIModule.DI_ITEM.Horizon_Motor_Busy_1) | !WagoDI.GetState(clsDIModule.DI_ITEM.Horizon_Motor_Busy_2) |
                    !WagoDI.GetState(clsDIModule.DI_ITEM.Horizon_Motor_Busy_3) | !WagoDI.GetState(clsDIModule.DI_ITEM.Horizon_Motor_Busy_4))
                {
                    Console.WriteLine("Reset Motor Process Start");
                    WagoDO.SetState(DO_ITEM.Horizon_Motor_Stop, true);
                    //安全迴路RELAY
                    WagoDO.SetState(DO_ITEM.Safety_Relays_Reset, true);
                    await Task.Delay(200);
                    WagoDO.SetState(DO_ITEM.Safety_Relays_Reset, false);
                    await Task.Delay(200);
                    WagoDO.SetState(DO_ITEM.Horizon_Motor_Reset, true);
                    await Task.Delay(200);
                    WagoDO.SetState(DO_ITEM.Horizon_Motor_Reset, false);
                    await Task.Delay(200);
                    WagoDO.SetState(DO_ITEM.Horizon_Motor_Stop, false);
                    Console.WriteLine("Reset Motor Process End");
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
        protected internal override void InitAGVControl(string RosBridge_IP, int RosBridge_Port)
        {
            AGVC = new InspectorAGVCarController(RosBridge_IP, RosBridge_Port);
            AGVC.Connect();
            AGVC.ManualController.vehicle = this;
            BuzzerPlayer.rossocket = AGVC.rosSocket;
            BuzzerPlayer.Alarm();
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
                void OffAllBatLockUnlockDO()
                {

                    WagoDO.SetState(DO_ITEM.Battery_1_Lock, false);
                    WagoDO.SetState(DO_ITEM.Battery_2_Lock, false);
                    WagoDO.SetState(DO_ITEM.Battery_1_Unlock, false);
                    WagoDO.SetState(DO_ITEM.Battery_2_Unlock, false);
                }
                var isBatLocking = battery_no == 1 ? IsBattery1Locked : IsBattery2Locked;
                var lockDO = battery_no == 1 ? DO_ITEM.Battery_1_Lock : DO_ITEM.Battery_2_Lock;
                var unlockDO = battery_no == 1 ? DO_ITEM.Battery_1_Unlock : DO_ITEM.Battery_2_Unlock;
                OffAllBatLockUnlockDO();
                WagoDO.SetState(unlockDO, false);
                if (lockBattery)
                    WagoDO.SetState(lockDO, true);
                else
                    WagoDO.SetState(unlockDO, true);

                CancellationTokenSource cst = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                while (!(battery_no == 1 ? (lockBattery ? IsBattery1Locked : IsBattery1UnLocked) : (lockBattery ? IsBattery2Locked : IsBattery2UnLocked)))
                {
                    if (cst.IsCancellationRequested)
                    {
                        OffAllBatLockUnlockDO();
                        return false;
                    }
                }
                OffAllBatLockUnlockDO();
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
