using AGVSystemCommonNet6.AGVDispatch.Messages;
using AGVSystemCommonNet6.Log;
using AGVSystemCommonNet6.Vehicle_Control.VCS_ALARM;
using GPMVehicleControlSystem.Models.Buzzer;
using GPMVehicleControlSystem.Models.VehicleControl.Vehicles;
using static AGVSystemCommonNet6.clsEnums;
using static GPMVehicleControlSystem.Models.VehicleControl.VehicleComponent.clsLaser;
using static GPMVehicleControlSystem.VehicleControl.DIOModule.clsDOModule;

namespace GPMVehicleControlSystem.Models.VehicleControl.TaskExecute
{
    public class ChargeTask : TaskBase
    {
        public delegate bool BeforeOpenChargeCircuitdelegate();
        public static BeforeOpenChargeCircuitdelegate OnChargeCircuitOpening;
        public override ACTION_TYPE action { get; set; } = ACTION_TYPE.Charge;

        public ChargeTask(Vehicle Agv, clsTaskDownloadData taskDownloadData) : base(Agv, taskDownloadData)
        {
        }

        public override async Task<bool> LaserSettingBeforeTaskExecute()
        {
            try
            {
                await Agv.Laser.FrontBackLasersEnable(false);
                await Agv.Laser.SideLasersEnable(false);
                await Agv.Laser.ModeSwitch(LASER_MODE.Bypass);
                return true;
            }
            catch (Exception ex)
            {
                LOG.ERROR(ex);
                return false;
            }
        }

        public override Task<(bool confirm, AlarmCodes alarm_code)> BeforeTaskExecuteActions()
        {
            bool open_charge_circuit = true;
            if (OnChargeCircuitOpening != null)
                open_charge_circuit = OnChargeCircuitOpening();

            if (open_charge_circuit)
                Agv.WagoDO.SetState(DO_ITEM.Recharge_Circuit, true);
            return base.BeforeTaskExecuteActions();
        }

        public override void DirectionLighterSwitchBeforeTaskExecute()
        {
            Agv.DirectionLighter.Forward();
        }

        protected override Task<(bool success, AlarmCodes alarmCode)> HandleAGVCActionSucceess()
        {
            var result = base.HandleAGVCActionSucceess();
            if (Agv.Parameters.BatteryModule.ChargeWhenLevelLowerThanThreshold && !Agv.WagoDO.GetState(DO_ITEM.Recharge_Circuit))
            {
                Task.Run(async () =>
                {
                    await Task.Delay(2000);
                    Agv.Sub_Status = SUB_STATUS.Charging;
                });
            }
            if (Agv.IsCharging)
                Agv.Sub_Status = SUB_STATUS.Charging;
            return result;
        }
    }
}
