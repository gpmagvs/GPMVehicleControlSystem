using AGVSystemCommonNet6.Alarm.VMS_ALARM;
using GPMVehicleControlSystem.Models.Buzzer;
using GPMVehicleControlSystem.Models.VehicleControl.AGVControl;
using GPMVehicleControlSystem.Models.VehicleControl.VehicleComponent;
using static GPMVehicleControlSystem.VehicleControl.DIOModule.clsDIModule;
using static GPMVehicleControlSystem.VehicleControl.DIOModule.clsDOModule;

namespace GPMVehicleControlSystem.Models.VehicleControl
{
    /// <summary>
    /// 巡檢AGV
    /// </summary>
    public partial class InspectionAGV : Vehicle
    {
        public bool IsBattery1Exist => WagoDI.GetState(DI_ITEM.Battery_1_Exist_1) && WagoDI.GetState(DI_ITEM.Battery_1_Exist_2) && WagoDI.GetState(DI_ITEM.Battery_1_Exist_3) && WagoDI.GetState(DI_ITEM.Battery_1_Exist_4);
        public bool IsBattery2Exist => WagoDI.GetState(DI_ITEM.Battery_2_Exist_1) && WagoDI.GetState(DI_ITEM.Battery_2_Exist_2) && WagoDI.GetState(DI_ITEM.Battery_2_Exist_3) && WagoDI.GetState(DI_ITEM.Battery_2_Exist_4);
        public bool IsBattery1Locked => WagoDI.GetState(DI_ITEM.Battery_1_Lock_Sensor);
        public bool IsBattery2Locked => WagoDI.GetState(DI_ITEM.Battery_2_Lock_Sensor);

        public InspectionAGV()
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

        internal override async Task<(bool confirm, string message)> Initialize()
        {
            //初始化儀器
            (bool confirm, string message) measurementInitResult = await InspectorAGVC?.MeasurementInit();
            if (!measurementInitResult.confirm)
                return (false, measurementInitResult.message);
            bool Battery1LockOK = IsBattery1Exist && IsBattery1Locked;
            bool Battery2LockOK = IsBattery2Exist && IsBattery2Locked;
            if (!Battery1LockOK | !Battery2LockOK)
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
        protected internal override void InitAGVControl(string RosBridge_IP, int RosBridge_Port)
        {
            AGVC = new InspectorAGVCarController(RosBridge_IP, RosBridge_Port);
            AGVC.Connect();
            AGVC.ManualController.vehicle = this;
            BuzzerPlayer.rossocket = AGVC.rosSocket;
            BuzzerPlayer.Alarm();
        }
        public bool Battery1Lock()
        {
            return ChangeBatteryLockState(1, true);
        }

        public bool Battery2Lock()
        {
            return ChangeBatteryLockState(2, true);
        }

        public bool Battery1UnLock()
        {
            return ChangeBatteryLockState(1, false);
        }

        public bool Battery2UnLock()
        {
            return ChangeBatteryLockState(2, false);
        }
        private bool ChangeBatteryLockState(int battery_no, bool lockBattery)
        {
            var noLockAlarmCode = battery_no == 1 ? AlarmCodes.Battery1_Not_Lock : AlarmCodes.Battery2_Not_Lock;
            try
            {
                var lockDO = battery_no == 1 ? DO_ITEM.Battery_1_Lock : DO_ITEM.Battery_2_Lock;
                var unlockDO = battery_no == 1 ? DO_ITEM.Battery_1_Unlock : DO_ITEM.Battery_2_Unlock;
                WagoDO.SetState(lockDO, false);
                WagoDO.SetState(unlockDO, false);
                if (lockBattery)
                    WagoDO.SetState(lockDO, true);
                else
                    WagoDO.SetState(unlockDO, true);
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
