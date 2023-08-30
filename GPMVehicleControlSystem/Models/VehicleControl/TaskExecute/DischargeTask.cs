using AGVSystemCommonNet6.AGVDispatch.Messages;
using AGVSystemCommonNet6.Alarm.VMS_ALARM;
using AGVSystemCommonNet6.Log;
using GPMVehicleControlSystem.Models.Buzzer;
using GPMVehicleControlSystem.Models.VehicleControl.Vehicles;
using static GPMVehicleControlSystem.Models.VehicleControl.VehicleComponent.clsLaser;
using static GPMVehicleControlSystem.VehicleControl.DIOModule.clsDOModule;

namespace GPMVehicleControlSystem.Models.VehicleControl.TaskExecute
{
    public class DischargeTask : ChargeTask
    {
        public override ACTION_TYPE action { get; set; } = ACTION_TYPE.Discharge;

        public DischargeTask(Vehicle Agv, clsTaskDownloadData taskDownloadData) : base(Agv, taskDownloadData)
        {
        }
        public override async void LaserSettingBeforeTaskExecute()
        {
            await Agv.Laser.FrontBackLasersEnable(false);
            await Agv.Laser.SideLasersEnable(false);
            await Agv.Laser.ModeSwitch(LASER_MODE.Loading);
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
                var goHomeResult = await ForkLifter.ForkGoHome(wait_done: true);
                LOG.WARN($"Fork Go Home When AGVC Action Finish , {goHomeResult.confirm}:{goHomeResult.message}");
                if (!goHomeResult.confirm)
                {
                    return (true, AlarmCodes.Fork_Arm_Pose_Error);
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
