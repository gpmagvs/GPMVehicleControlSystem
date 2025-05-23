using AGVSystemCommonNet6.AGVDispatch.Messages;
using AGVSystemCommonNet6.Vehicle_Control.VCS_ALARM;
using GPMVehicleControlSystem.Models.Buzzer;
using GPMVehicleControlSystem.Models.VehicleControl.Vehicles;
using static AGVSystemCommonNet6.clsEnums;
using static GPMVehicleControlSystem.Models.VehicleControl.VehicleComponent.clsLaser;
using static GPMVehicleControlSystem.VehicleControl.DIOModule.clsDOModule;

namespace GPMVehicleControlSystem.Models.TaskExecute
{
    public class ParkTask : ChargeTask
    {
        public override ACTION_TYPE action { get; set; } = ACTION_TYPE.Park;
        public ParkTask(Vehicle Agv, clsTaskDownloadData taskDownloadData) : base(Agv, taskDownloadData)
        {
            height = 0;
        }
        public override async Task<(bool confirm, AlarmCodes alarm_code)> BeforeTaskExecuteActions()
        {
            Agv.WagoDO.SetState(DO_ITEM.Recharge_Circuit, false);
            return (true, AlarmCodes.None);
        }

        protected override void BuzzerPlayMusic(ACTION_TYPE action)
        {
            BuzzerPlayer.SoundPlaying = SOUNDS.Action;
        }
        public override void DirectionLighterSwitchBeforeTaskExecute()
        {
            Agv.DirectionLighter.Forward();
        }

        internal override async Task<(bool success, AlarmCodes alarmCode)> HandleAGVCActionSucceess()
        {
            BuzzerPlayer.SoundPlaying = SOUNDS.Stop;
            return (true, AlarmCodes.None);
        }
    }
}
