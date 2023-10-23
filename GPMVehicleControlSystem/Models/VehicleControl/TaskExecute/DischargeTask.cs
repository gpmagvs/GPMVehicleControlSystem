using AGVSystemCommonNet6.AGVDispatch.Messages;
using AGVSystemCommonNet6.Alarm.VMS_ALARM;
using AGVSystemCommonNet6.Log;
using GPMVehicleControlSystem.Models.Buzzer;
using GPMVehicleControlSystem.Models.VehicleControl.Vehicles;
using static GPMVehicleControlSystem.Models.VehicleControl.VehicleComponent.clsForkLifter;
using static GPMVehicleControlSystem.Models.VehicleControl.VehicleComponent.clsLaser;
using static GPMVehicleControlSystem.VehicleControl.DIOModule.clsDIModule;
using static GPMVehicleControlSystem.VehicleControl.DIOModule.clsDOModule;

namespace GPMVehicleControlSystem.Models.VehicleControl.TaskExecute
{
    public class DischargeTask : ChargeTask
    {
        public override ACTION_TYPE action { get; set; } = ACTION_TYPE.Discharge;

        public DischargeTask(Vehicle Agv, clsTaskDownloadData taskDownloadData) : base(Agv, taskDownloadData)
        {
        }
        public override async Task<bool> LaserSettingBeforeTaskExecute()
        {
            try
            {
                await Agv.Laser.AllLaserDisable();
                await Agv.Laser.ModeSwitch(LASER_MODE.Loading);
                await Agv.Laser.FrontBackLasersEnable(false, true);
                return true;
            }
            catch (Exception ex)
            {
                LOG.ERROR(ex);
                return false;
            }
        }
        public override void DirectionLighterSwitchBeforeTaskExecute()
        {
            Task.Factory.StartNew(async () =>
            {
                await Task.Delay(1000);
                Agv.DirectionLighter.Backward();
            });
        }

        protected override async Task<(bool success, AlarmCodes alarmCode)> HandleAGVCActionSucceess()
        {
            if (ForkLifter != null)
            {
                (bool confirm, AlarmCodes alarm_code) ForkGoHomeActionResult = (false, AlarmCodes.None);
                await RegisterSideLaserTriggerEvent();
                while (Agv.ForkLifter.CurrentForkLocation != FORK_LOCATIONS.HOME)
                {
                    ForkGoHomeActionResult = await ForkLifter.ForkGoHome();
                }
                await UnRegisterSideLaserTriggerEvent();

                LOG.WARN($"Fork Go Home When AGVC Action Finish , {ForkGoHomeActionResult.confirm}:{ForkGoHomeActionResult.alarm_code}");
                if (!ForkGoHomeActionResult.confirm)
                {
                    return (true, ForkGoHomeActionResult.alarm_code);
                }
            }
            return await base.HandleAGVCActionSucceess();
        }
        public override async Task<(bool confirm, AlarmCodes alarm_code)> BeforeTaskExecuteActions()
        {
            Agv.WagoDO.SetState(DO_ITEM.Recharge_Circuit, false);
            return (true, AlarmCodes.None);
        }


    }
}
