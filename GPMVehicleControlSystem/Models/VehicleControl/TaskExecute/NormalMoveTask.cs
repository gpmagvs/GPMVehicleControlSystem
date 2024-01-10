using AGVSystemCommonNet6;
using AGVSystemCommonNet6.AGVDispatch.Messages;
using AGVSystemCommonNet6.Log;
using AGVSystemCommonNet6.MAP;
using AGVSystemCommonNet6.Vehicle_Control.VCS_ALARM;
using GPMVehicleControlSystem.Models.Buzzer;
using GPMVehicleControlSystem.Models.NaviMap;
using GPMVehicleControlSystem.Models.VehicleControl.Vehicles;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using SQLitePCL;
using System.Diagnostics;
using static AGVSystemCommonNet6.clsEnums;

namespace GPMVehicleControlSystem.Models.VehicleControl.TaskExecute
{
    public class NormalMoveTask : TaskBase
    {

        public override ACTION_TYPE action { get; set; } = ACTION_TYPE.None;
        private bool ForkActionStartWhenReachSecondartPTFlag = false;
        private static int NextSecondartPointTag = 0;
        private static int NextWorkStationPointTag = 0;
        public NormalMoveTask(Vehicle Agv, clsTaskDownloadData taskDownloadData) : base(Agv, taskDownloadData)
        {
            var destine = taskDownloadData.Destination;
            var end_of_traj = taskDownloadData.Trajectory.Last().Point_ID;
            isSegmentTask = destine != end_of_traj;
            if (isSegmentTask)
            {
                LOG.TRACE($"分段任務接收:軌跡終點:{end_of_traj},目的地:{destine}");
            }

            Agv.BarcodeReader.OnAGVReachingTag -= BarcodeReader_OnAGVReachingTag;
            ForkActionStartWhenReachSecondartPTFlag = DetermineIsNeedDoForkAction(taskDownloadData, out NextSecondartPointTag, out NextWorkStationPointTag);
            LOG.INFO($"抵達終點後 Fork 動作:{ForkActionStartWhenReachSecondartPTFlag}(二次定位點{NextSecondartPointTag},取放貨站點 {NextWorkStationPointTag})");
            if (ForkActionStartWhenReachSecondartPTFlag)
            {
                Agv.BarcodeReader.OnAGVReachingTag += BarcodeReader_OnAGVReachingTag;
            }
        }

        private bool DetermineIsNeedDoForkAction(clsTaskDownloadData taskDownloadData, out int DoActionTag, out int nextWorkStationPointTag)
        {
            DoActionTag = nextWorkStationPointTag = -1;

            if (Agv.Parameters.AgvType != AGV_TYPE.FORK)
                return false;

            var _next_action = taskDownloadData.OrderInfo.ActionName;
            bool _need = _next_action == ACTION_TYPE.Load || _next_action == ACTION_TYPE.Unload || _next_action == ACTION_TYPE.Charge || _next_action == ACTION_TYPE.LoadAndPark;
            if (_need)
            {
                var workstation_tag = !taskDownloadData.OrderInfo.IsTransferTask ? taskDownloadData.OrderInfo.DestineTag :
                                        _next_action == ACTION_TYPE.Unload ? taskDownloadData.OrderInfo.SourceTag : taskDownloadData.OrderInfo.DestineTag;

                if (!Agv.NavingMap.GetStationTags().Contains(workstation_tag))
                {
                    LOG.WARN($"圖資中不存在 Tag {workstation_tag}");
                    return false;
                }

                var workstation_point = Agv.NavingMap.Points.Values.FirstOrDefault(pt => pt.TagNumber == workstation_tag);
                if (workstation_point == null)
                {
                    LOG.WARN($"圖資中不存在站點 {workstation_tag}");
                    return false;
                }
                if (workstation_point.StationType == STATION_TYPE.Normal)
                {
                    LOG.WARN($"工作站點的類型錯誤 {workstation_point.StationType}");
                    return false;
                }

                nextWorkStationPointTag = workstation_point.TagNumber;
                var _secondary_pt_index = workstation_point.Target.First().Key;
                if (Agv.NavingMap.Points.TryGetValue(_secondary_pt_index, out MapPoint secondaryPoint))
                {
                    DoActionTag = secondaryPoint.TagNumber;
                    return true;
                }
                else
                {
                    LOG.WARN($"找不到工作點位的二次定位點 TAG ({_next_action})");
                    return false;
                }

            }
            else
            {
                LOG.WARN($"訂單任務下一個動作不需要升降牙叉({_next_action})");

                return false;
            }
        }

        internal static void BarcodeReader_OnAGVReachingTag(object? sender, EventArgs e)
        {
            ForkAGV? forkAGV = (StaStored.CurrentVechicle as ForkAGV);
            var _currentTag = forkAGV.BarcodeReader.CurrentTag;
            if (_currentTag == null)
                return;
            if (NextSecondartPointTag == _currentTag)
            {
                var isunLoad = forkAGV._RunTaskData.OrderInfo.ActionName == ACTION_TYPE.Unload;
                var ischarge = forkAGV._RunTaskData.OrderInfo.ActionName == ACTION_TYPE.Charge;

                LOG.WARN($"抵達二次定位點 TAG{_currentTag} 牙叉準備上升({forkAGV._RunTaskData.OrderInfo.ActionName})");
                try
                {
                    forkAGV.BarcodeReader.OnAGVReachingTag -= BarcodeReader_OnAGVReachingTag;

                    

                    double _position_aim = 0;
                    var forkHeightSetting = forkAGV.WorkStations.Stations[NextWorkStationPointTag].LayerDatas[forkAGV._RunTaskData.Height];
                    _position_aim = isunLoad || ischarge ? forkHeightSetting.Down_Pose : forkHeightSetting.Up_Pose;

                    var _Height_PreAction = forkAGV.Parameters.ForkAGV.SaftyPositionHeight < _position_aim ? forkAGV.Parameters.ForkAGV.SaftyPositionHeight : _position_aim;
                    LOG.WARN($"抵達二次定位點 TAG{_currentTag}, 牙叉開始動作上升至({_Height_PreAction}cm)");

                    Task.Run(async () =>
                    {
                        var result = await forkAGV.ForkLifter.ForkPose(_Height_PreAction, 1);
                        if (!result.confirm)
                        {
                            forkAGV.SoftwareEMO(AlarmCodes.Fork_Action_Aborted);
                        }
                    });
                }
                catch (Exception ex)
                {
                    LOG.Critical(ex.Message, ex);
                    forkAGV.SoftwareEMO(AlarmCodes.Fork_Pose_Change_Fail_When_Reach_Secondary);
                }

            }

        }

        public override void DirectionLighterSwitchBeforeTaskExecute()
        {
        }
        public override Task<(bool confirm, AlarmCodes alarm_code)> BeforeTaskExecuteActions()
        {
            if (Agv.Parameters.CargoBiasDetectionWhenNormalMoving && Agv.CargoStatus == Vehicle.CARGO_STATUS.HAS_CARGO_NORMAL)
            {
                if (!Agv.IsCargoBiasDetecting)
                {
                    Agv.IsCargoBiasDetecting = true;
                    StartMonitorCargoBias();
                }
                else
                {
                    LOG.WARN($"貨物傾倒偵測已在執行中!!");
                }
            }
            //Task.Run(() => WatchVirtualPtAndStopWorker());
            return base.BeforeTaskExecuteActions();
        }


        /// <summary>
        /// 偵測貨物傾倒
        /// </summary>
        /// <returns></returns>
        private async Task StartMonitorCargoBias()
        {
            await Task.Delay(1).ContinueWith(async (Task) =>
            {
                Agv.IsCargoBiasTrigger = false;
                if (Agv.Parameters.LDULD_Task_No_Entry)
                {
                    return;
                }

                LOG.INFO($"Wait AGV Move(Active), Will Start Cargo Bias Detection.");
                CancellationTokenSource cts = new CancellationTokenSource();
                cts.CancelAfter(10000);
                while (Agv.AGVC.ActionStatus != RosSharp.RosBridgeClient.Actionlib.ActionStatus.ACTIVE)
                {
                    await Task.Delay(1);
                    if (cts.IsCancellationRequested)
                    {
                        Agv.IsCargoBiasDetecting = false;
                        LOG.ERROR($"Wait AGV Move(Active) Timeout, cargo Bias Detection not start.");
                        return;
                    }
                }
                LOG.INFO($"Start Cargo Bias Detection.");
                while (Agv.CargoStatus == Vehicle.CARGO_STATUS.HAS_CARGO_NORMAL && Agv.ExecutingTaskModel.action == ACTION_TYPE.None)
                {
                    await Task.Delay(1);
                    if (Agv.AGVC.ActionStatus != RosSharp.RosBridgeClient.Actionlib.ActionStatus.ACTIVE)
                    {
                        break;
                    }
                    if (Agv.CargoStatus == Vehicle.CARGO_STATUS.HAS_CARGO_BUT_BIAS | Agv.CargoStatus == Vehicle.CARGO_STATUS.NO_CARGO)
                    {
                        LOG.WARN($"貨物傾倒偵測觸發-Check1");
                        await Task.Delay(500); //避免訊號瞬閃導致誤偵測
                        if (Agv.CargoStatus != Vehicle.CARGO_STATUS.HAS_CARGO_NORMAL)
                        {
                            LOG.ERROR($"貨物傾倒偵測觸發-Check2_Actual Trigger. AGV Will Cycle Stop");
                            Agv.IsCargoBiasTrigger = true;
                            Agv.AGVC.ResetTask(RESET_MODE.CYCLE_STOP);
                            break;
                        }
                    }
                }
                Agv.IsCargoBiasDetecting = false;
            });
        }

        private async void WatchVirtualPtAndStopWorker()
        {
            if (lastPt == null)
                return;

            if (!lastPt.IsVirtualPoint)
                return;
            LOG.WARN("終點站為虛擬點 Start Monitor .");
            double distance = 0;
            Stopwatch sw = Stopwatch.StartNew();

            while ((distance = lastPt.CalculateDistance(Agv.Navigation.Data.robotPose.pose.position.x, Agv.Navigation.Data.robotPose.pose.position.y)) > 0.05)
            {
                if (sw.ElapsedMilliseconds > 3000)
                {
                    sw.Restart();
                    LOG.WARN($"與虛擬點終點站距離:{distance} m");
                }
                await Task.Delay(1);
            }
            LOG.WARN("抵達虛擬點終點站 Stop Monitor .");
            AlarmManager.AddAlarm(AlarmCodes.Destine_Point_Is_Virtual_Point);
            Agv.HandleAGVSTaskCancelRequest(RESET_MODE.ABORT);

        }

        public override async Task<bool> LaserSettingBeforeTaskExecute()
        {
            return await Agv.Laser.ModeSwitch(this.RunningTaskData.ExecutingTrajecory.First().Laser, true);
        }

    }
}
