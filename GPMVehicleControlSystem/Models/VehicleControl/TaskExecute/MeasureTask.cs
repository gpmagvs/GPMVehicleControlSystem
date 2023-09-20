using AGVSystemCommonNet6.AGVDispatch.Messages;
using AGVSystemCommonNet6.Alarm.VMS_ALARM;
using GPMVehicleControlSystem.Models.VehicleControl.Vehicles;
using static GPMVehicleControlSystem.VehicleControl.DIOModule.clsDOModule;

namespace GPMVehicleControlSystem.Models.VehicleControl.TaskExecute
{
    /// <summary>
    /// 量測任務
    /// </summary>
    public class MeasureTask : TaskBase
    {
        public MeasureTask(Vehicle Agv, clsTaskDownloadData taskDownloadData) : base(Agv, taskDownloadData)
        {
        }

        public override ACTION_TYPE action { get; set; } = ACTION_TYPE.Measure;

        public override void DirectionLighterSwitchBeforeTaskExecute()
        {
            var flash_dos = new DO_ITEM[] {
                DO_ITEM.AGV_DiractionLight_Right,
                DO_ITEM.AGV_DiractionLight_Right_2,
                DO_ITEM.AGV_DiractionLight_Left,
                DO_ITEM.AGV_DiractionLight_Left_2
            };
            Agv.DirectionLighter.Flash(flash_dos, 800);
        }
        public override Task<(bool confirm, AlarmCodes alarm_code)> BeforeTaskExecuteActions()
        {
            return base.BeforeTaskExecuteActions();
        }
        public override async Task<bool> LaserSettingBeforeTaskExecute()
        {
            await Agv.Laser.ModeSwitch(VehicleComponent.clsLaser.LASER_MODE.Normal);
            return true;
        }
    }
}
