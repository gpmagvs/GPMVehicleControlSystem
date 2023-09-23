using AGVSystemCommonNet6.AGVDispatch.Messages;
using AGVSystemCommonNet6.AGVDispatch.Model;
using AGVSystemCommonNet6.Alarm.VMS_ALARM;
using AGVSystemCommonNet6.Log;
using GPMVehicleControlSystem.Models.Buzzer;
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
            Agv.DirectionLighter.Forward();
        }
        private CancellationTokenSource cancelFlashCts = new CancellationTokenSource();
        /// <summary>
        /// stream-like direction lighter flash
        /// </summary>
        private async void FlashDirectorLighter()
        {
            Agv.DirectionLighter.CloseAll();
            cancelFlashCts = new CancellationTokenSource();

            var flash_dos = new DO_ITEM[] {
                DO_ITEM.AGV_DiractionLight_Left,
                DO_ITEM.AGV_DiractionLight_Right,
                DO_ITEM.AGV_DiractionLight_Right_2,
                DO_ITEM.AGV_DiractionLight_Left_2
            };
            _ = Task.Factory.StartNew(async () =>
            {
                while (!cancelFlashCts.IsCancellationRequested)
                {
                    foreach (var item in flash_dos)
                    {
                        await Agv.WagoDO.SetState(item, true);
                        await Task.Delay(50);
                        await Agv.WagoDO.SetState(item, false);
                        await Task.Delay(50);
                    }
                }
            });
        }

        public override async Task<(bool confirm, AlarmCodes alarm_code)> BeforeTaskExecuteActions()
        {
            (bool confirm, string message) init_result = await TsmcMiniAGV.MeasurementInit();
            LOG.INFO($"儀器初始化 {init_result.confirm},{init_result.message}");
            return await base.BeforeTaskExecuteActions();
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
            LOG.INFO($"AGV Reach InPoint, Go to first Pt: {taskData.Destination}");
            return await Agv.AGVC.ExecuteTaskDownloaded(taskData);
        }
        /// <summary>
        /// AGV到點後，進行量測
        /// </summary>
        /// <returns></returns>
        protected override async Task<(bool success, AlarmCodes alarmCode)> HandleAGVCActionSucceess()
        {

            TsmcMiniAGV.OnMeasureComplete += TsmcMiniAGV_OnMeasureComplete;
            await Task.Delay(1000);
            FlashDirectorLighter();
            BuzzerPlayer.Measure();
            LOG.WARN($"AGV Reach {Agv.Navigation.LastVisitedTag}, Start Measure First Point");
            (bool confirm, string message) response = await TsmcMiniAGV.StartMeasure(Agv.Navigation.LastVisitedTag);
            if (!response.confirm)
            {
                TsmcMiniAGV_OnMeasureComplete(this, new clsMeasureResult(Agv.Navigation.LastVisitedTag)
                {
                    result = "error",
                });
                return (true, AlarmCodes.None);
            }
            else
                return (true, AlarmCodes.None);
        }

        private async void TsmcMiniAGV_OnMeasureComplete(object? sender, clsMeasureResult measure_result)
        {

            Agv.ReportMeasureResult(measure_result);
            //[0,  1,2,3,4]
            int totalMeasurePointNum = RunningTaskData.ExecutingTrajecory.Length - 1;//扣掉進入點
            int completed_point_index_ = RunningTaskData.ExecutingTrajecory.ToList().FindIndex(pt => pt.Point_ID == measure_result.TagID);
            LOG.INFO($"{completed_point_index_} / {totalMeasurePointNum}");
            cancelFlashCts.Cancel();
            if (completed_point_index_ == totalMeasurePointNum) //全部的測量點都量測完畢拉
            {
                TsmcMiniAGV.OnMeasureComplete -= TsmcMiniAGV_OnMeasureComplete;
                Agv.DirectionLighter.Backward();
                BuzzerPlayer.Move();
                LOG.INFO($"Bay Point 量測結束，開始離開Bay");
                RunningTaskData = RunningTaskData.CreateGoHomeTaskDownloadData();
                Agv.ExecutingTask.RunningTaskData = RunningTaskData;
                AGVCActionStatusChaged += HandleAGVCBackToEntryPointDone;
                Agv.FeedbackTaskStatus(TASK_RUN_STATUS.NAVIGATING);
                await Agv.AGVC.ExecuteTaskDownloaded(RunningTaskData);
            }
            else //移動到下一個點
            {
                clsTaskDownloadData taskData = RunningTaskData.Splice(completed_point_index_, 2, true);
                AGVCActionStatusChaged += HandleAGVCReachMeasurePoint;
                BuzzerPlayer.Move();
                Agv.FeedbackTaskStatus(TASK_RUN_STATUS.NAVIGATING);
                await Agv.AGVC.ExecuteTaskDownloaded(taskData);
            }
        }

        private async void HandleAGVCBackToEntryPointDone(ActionStatus status)
        {
            LOG.WARN($"[ {RunningTaskData.Task_Simplex} -{action}-Back To Entry  Point of Bay] AGVC Action Status Changed: {status}.");

            if (Agv.Sub_Status == SUB_STATUS.DOWN)
            {
                AGVCActionStatusChaged -= HandleAGVCBackToEntryPointDone;
                return;
            }
            if (status == ActionStatus.SUCCEEDED)
            {
                AGVCActionStatusChaged = null;
                Agv.Sub_Status = SUB_STATUS.IDLE;
                await Task.Delay(500);
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
                await Task.Delay(1000);
                FlashDirectorLighter();
                BuzzerPlayer.Measure();
                LOG.WARN($"AGV Reach {Agv.Navigation.LastVisitedTag}, Start Measure.");
                (bool confirm, string message) result = await TsmcMiniAGV.StartMeasure(Agv.Navigation.LastVisitedTag);
                if (!result.confirm)
                {
                    LOG.ERROR($"AGV Measure service callback fail.");

                    TsmcMiniAGV_OnMeasureComplete(this, new clsMeasureResult(Agv.Navigation.LastVisitedTag)
                    {
                        result = "error",
                    });
                }
            }
        }
    }
}
