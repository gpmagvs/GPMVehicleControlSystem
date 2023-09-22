using AGVSystemCommonNet6.AGVDispatch.Messages;
using AGVSystemCommonNet6.Alarm.VMS_ALARM;
using AGVSystemCommonNet6.Log;
using GPMVehicleControlSystem.Models.VehicleControl.Vehicles;
using RosSharp.RosBridgeClient.Actionlib;
using static AGVSystemCommonNet6.clsEnums;
using static GPMVehicleControlSystem.VehicleControl.DIOModule.clsDOModule;

namespace GPMVehicleControlSystem.Models.VehicleControl.TaskExecute
{
    /// <summary>
    /// 量測任務
    /// </summary>
    public class MeasureTask : TaskBase
    {
        private readonly TsmcMiniAGV TsmcMiniAGV;
        public MeasureTask(Vehicle Agv, clsTaskDownloadData taskDownloadData) : base(Agv, taskDownloadData)
        {
            TsmcMiniAGV = Agv as TsmcMiniAGV;
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

        /// <summary>
        /// 量測任務時，當AGV抵達Bay進入點 僅先下發進入點->第一個量測點任務
        /// </summary>
        /// <returns></returns>
        protected override async Task<(bool agvc_executing, string message)> TransferTaskToAGVC()
        {
            clsTaskDownloadData taskData = RunningTaskData.Splice(0, 2, true);
            return await Agv.AGVC.ExecuteTaskDownloaded(taskData);
        }
        /// <summary>
        /// AGV到點後，進行量測
        /// </summary>
        /// <returns></returns>
        protected override async Task<(bool success, AlarmCodes alarmCode)> HandleAGVCActionSucceess()
        {
            TsmcMiniAGV.OnMeasureComplete += TsmcMiniAGV_OnMeasureComplete;
            await TsmcMiniAGV.StartMeasure();
            return (true, AlarmCodes.None);
        }

        private async void TsmcMiniAGV_OnMeasureComplete(object? sender, TsmcMiniAGV.clsMeasureResult measure_result)
        {
            //[0,  1,2,3,4]
            int totalMeasurePointNum = RunningTaskData.ExecutingTrajecory.Length - 1;//扣掉進入點
            int completed_point_index_ = RunningTaskData.ExecutingTrajecory.ToList().FindIndex(pt => pt.Point_ID == measure_result.TagID);
            if (completed_point_index_ == totalMeasurePointNum) //全部的測量點都量測完畢拉
            {
                RunningTaskData = RunningTaskData.CreateGoHomeTaskDownloadData();
                Agv.ExecutingTask.RunningTaskData = RunningTaskData;
                AGVCActionStatusChaged += HandleAGVCReachEntryPoint;
                Agv.FeedbackTaskStatus(TASK_RUN_STATUS.NAVIGATING);
                await Agv.AGVC.ExecuteTaskDownloaded(RunningTaskData);
            }
            else //移動到下一個點
            {
                clsTaskDownloadData taskData = RunningTaskData.Splice(completed_point_index_, 2, true);
                AGVCActionStatusChaged += HandleAGVCReachMeasurePoint;
                Agv.FeedbackTaskStatus(TASK_RUN_STATUS.NAVIGATING);
                await Agv.AGVC.ExecuteTaskDownloaded(taskData);
            }
        }

        private async void HandleAGVCReachEntryPoint(ActionStatus status)
        {
            LOG.WARN($"[ {RunningTaskData.Task_Simplex} -{action}-Back To Entry  Point of Bay] AGVC Action Status Changed: {status}.");

            if (Agv.Sub_Status == SUB_STATUS.DOWN)
            {
                AGVCActionStatusChaged -= HandleAGVCReachEntryPoint;
                return;
            }
            if (status == ActionStatus.SUCCEEDED)
            {
                AGVCActionStatusChaged = null;
                await Agv.FeedbackTaskStatus(TASK_RUN_STATUS.ACTION_FINISH);
            }
        }

        private async void HandleAGVCReachMeasurePoint(ActionStatus status)
        {
            LOG.WARN($"[ {RunningTaskData.Task_Simplex} -{action}-Go To Measure Point] AGVC Action Status Changed: {status}.");
            if (Agv.Sub_Status == SUB_STATUS.DOWN)
            {
                AGVCActionStatusChaged -= HandleAGVCReachMeasurePoint;
                return;
            }
            if (status == ActionStatus.SUCCEEDED)
            {
                AGVCActionStatusChaged = null;
                await Agv.FeedbackTaskStatus(TASK_RUN_STATUS.ACTION_START);
                await TsmcMiniAGV.StartMeasure();
            }
        }
    }
}
