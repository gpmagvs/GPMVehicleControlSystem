using AGVSystemCommonNet6.AGVDispatch.Messages;
using AGVSystemCommonNet6.Alarm.VMS_ALARM;
using AGVSystemCommonNet6.Log;
using GPMVehicleControlSystem.Models.Buzzer;
using GPMVehicleControlSystem.Models.VehicleControl.Vehicles;
using RosSharp.RosBridgeClient.Actionlib;
using static AGVSystemCommonNet6.clsEnums;
using static GPMVehicleControlSystem.VehicleControl.DIOModule.clsDOModule;

namespace GPMVehicleControlSystem.Models.VehicleControl.TaskExecute
{
    public class ExchangeBatteryTask : TaskBase
    {
        public ExchangeBatteryTask(Vehicle Agv, clsTaskDownloadData taskDownloadData) : base(Agv, taskDownloadData)
        {
        }

        public override ACTION_TYPE action { get; set; } = ACTION_TYPE.ExchangeBattery;

        public override void DirectionLighterSwitchBeforeTaskExecute()
        {
            Agv.DirectionLighter.Flash(new DO_ITEM[4] {
                 DO_ITEM.AGV_DiractionLight_Right,
                 DO_ITEM.AGV_DiractionLight_Right_2,
                 DO_ITEM.AGV_DiractionLight_Left,
                 DO_ITEM.AGV_DiractionLight_Left_2,
            }, 800);
        }

        public override async Task<bool> LaserSettingBeforeTaskExecute()
        {
            return await Agv.Laser.ModeSwitch(VehicleComponent.clsLaser.LASER_MODE.Bypass);
        }
        protected override async Task<(bool success, AlarmCodes alarmCode)> HandleAGVCActionSucceess()
        {
            BuzzerPlayer.ExchangeBattery();
            await Agv.WagoDO.SetState(DO_ITEM.AGV_VALID, true);
            await Task.Delay(5000);
            //TODO 電池交換站交握

            BuzzerPlayer.Action();
            var gotoEntryPointTask = RunningTaskData.CreateGoHomeTaskDownloadData();
            AGVCActionStatusChaged += OnAGVCBackToEntryPoint;
            Agv.AGVC.ExecuteTaskDownloaded(gotoEntryPointTask);
            return (true, AlarmCodes.None);
        }

        private void OnAGVCBackToEntryPoint(ActionStatus status)
        {
            if (Agv.Sub_Status == SUB_STATUS.DOWN)
            {
                AGVCActionStatusChaged -= OnAGVCBackToEntryPoint;
                return;
            }
            LOG.WARN($"[ {RunningTaskData.Task_Simplex} -{action}-Back To Entry Point of Bat-Exanger] AGVC Action Status Changed: {status}.");

            if (status == ActionStatus.SUCCEEDED)
            {
                AGVCActionStatusChaged = null;
                LOG.INFO($"電池交換完成");
                base.HandleAGVCActionSucceess();
            }
        }

    }
}
