using AGVSystemCommonNet6;
using AGVSystemCommonNet6.AGVDispatch.Messages;
using AGVSystemCommonNet6.MAP;
using AGVSystemCommonNet6.Vehicle_Control.VCS_ALARM;
using GPMVehicleControlSystem.Models.Buzzer;
using GPMVehicleControlSystem.Models.NaviMap;
using GPMVehicleControlSystem.Models.VehicleControl.AGVControl;
using GPMVehicleControlSystem.Models.VehicleControl.Vehicles;
using GPMVehicleControlSystem.Models.VehicleControl.Vehicles.CargoStates;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using RosSharp.RosBridgeClient.Actionlib;
using SQLitePCL;
using System.Diagnostics;
using static AGVSystemCommonNet6.clsEnums;
using static AGVSystemCommonNet6.MAP.MapPoint;

namespace GPMVehicleControlSystem.Models.TaskExecute
{
    public class NormalMoveTask : TaskBase
    {

        public override ACTION_TYPE action { get; set; } = ACTION_TYPE.None;
        private bool ForkActionStartWhenReachSecondartPTFlag = false;
        internal static int NextSecondartPointTag = 0;
        internal static int NextWorkStationPointTag = 0;
        public NormalMoveTask(Vehicle Agv, clsTaskDownloadData taskDownloadData) : base(Agv, taskDownloadData)
        {
            var destine = taskDownloadData.Destination;
            var end_of_traj = taskDownloadData.Trajectory.Last().Point_ID;
            isSegmentTask = destine != end_of_traj;
            if (isSegmentTask)
            {
                logger.Trace($"分段任務接收:軌跡終點:{end_of_traj},目的地:{destine}");
            }

        }

        private static string ExecutingTaskNameRecord = "";
        protected override Task<CarController.SendActionCheckResult> TransferTaskToAGVC()
        {
            ExecutingTaskNameRecord = RunningTaskData.Task_Name;
            if (Agv.Parameters.AgvType == AGV_TYPE.FORK)
            {
                Agv.Navigation.OnLastVisitedTagUpdate -= Agv.WatchReachNextWorkStationSecondaryPtHandler;

                Agv.ForkLifter.EarlyMoveUpState.Reset();
                ForkActionStartWhenReachSecondartPTFlag = DetermineIsNeedDoForkAction(RunningTaskData, out NextSecondartPointTag, out NextWorkStationPointTag);
                logger.Info($"抵達終點後 Fork 動作:{ForkActionStartWhenReachSecondartPTFlag}(二次定位點{NextSecondartPointTag},取放貨站點 {NextWorkStationPointTag})");
                bool _isCurrentTagIsNextSecondaryPoint = Agv.Navigation.LastVisitedTag == NextSecondartPointTag;

                if (ForkActionStartWhenReachSecondartPTFlag && !_isCurrentTagIsNextSecondaryPoint)
                {
                    Agv.Navigation.OnLastVisitedTagUpdate += Agv.WatchReachNextWorkStationSecondaryPtHandler;
                    //StartTrackingSecondaryPointReach(ExecutingTaskNameRecord);
                }
                else if (ForkActionStartWhenReachSecondartPTFlag && _isCurrentTagIsNextSecondaryPoint)
                {
                    logger.Info($"當前位置已在工作站進入點,不需監視是否已到達工作站進入點");
                }
            }
            return base.TransferTaskToAGVC();
        }

        protected override async Task WaitTaskDoneAsync()
        {
            logger.Trace($"等待 AGV完成 [移動] 任務");
            await Task.Delay(10);
            try
            {
                while (Agv.AGVC.IsRunning)
                {
                    await Task.Delay(1);
                    if (TaskCancelByReplan.IsCancellationRequested)
                    {
                        throw new TaskCanceledException();
                    }
                }
                logger.Trace($"AGV完成 [移動] 任務, Alarm Code: {task_abort_alarmcode}.]");

            }
            catch (TaskCanceledException ex)
            {
                task_abort_alarmcode = AlarmCodes.Replan;
                _wait_agvc_action_done_pause.Set();
                logger.Trace($"[移動]任務-Replan.{ex.Message}, Alarm Code:{task_abort_alarmcode}.]");
            }
            catch (Exception ex)
            {
                task_abort_alarmcode = AlarmCodes.Replan;
                _wait_agvc_action_done_pause.Set();
                logger.Trace($"[移動]任務-Replan.{ex.Message}=>{task_abort_alarmcode}.]");
            }
        }
        private bool DetermineIsNeedDoForkAction(clsTaskDownloadData taskDownloadData, out int DoActionTag, out int nextWorkStationPointTag)
        {
            DoActionTag = nextWorkStationPointTag = -1;

            var _next_action = taskDownloadData.OrderInfo.NextAction;
            bool _need = _next_action == ACTION_TYPE.Load || _next_action == ACTION_TYPE.Unload || _next_action == ACTION_TYPE.Charge || _next_action == ACTION_TYPE.LoadAndPark;
            if (_need)
            {
                var workstation_tag = !taskDownloadData.OrderInfo.IsTransferTask ? taskDownloadData.OrderInfo.DestineTag :
                                        _next_action == ACTION_TYPE.Unload ? taskDownloadData.OrderInfo.SourceTag : taskDownloadData.OrderInfo.DestineTag;

                if (!Agv.NavingMap.GetStationTags().Contains(workstation_tag))
                {
                    logger.Warn($"圖資中不存在 Tag {workstation_tag}");
                    return false;
                }

                var workstation_point = Agv.NavingMap.Points.Values.FirstOrDefault(pt => pt.TagNumber == workstation_tag);
                if (workstation_point == null)
                {
                    logger.Warn($"圖資中不存在站點 {workstation_tag}");
                    return false;
                }
                if (workstation_point.StationType == STATION_TYPE.Normal)
                {
                    logger.Warn($"工作站點的類型錯誤 {workstation_point.StationType}");
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
                    logger.Warn($"找不到工作點位的二次定位點 TAG ({_next_action})");
                    return false;
                }

            }
            else
            {
                logger.Warn($"訂單任務下一個動作不需要升降牙叉({_next_action})");

                return false;
            }
        }

        public override void DirectionLighterSwitchBeforeTaskExecute()
        {
        }
        public override async Task<(bool confirm, AlarmCodes alarm_code)> BeforeTaskExecuteActions()
        {
            await LaserSettingBeforeTaskExecute();
            if (Agv.Parameters.CargoBiasDetectionWhenNormalMoving && Agv.CargoStateStorer.GetCargoStatus(Agv.Parameters.LDULD_Task_No_Entry) == CARGO_STATUS.HAS_CARGO_NORMAL)
            {
                if (!Agv.IsCargoBiasDetecting)
                {
                    Agv.IsCargoBiasDetecting = true;
                }
                else
                {
                    logger.Warn($"貨物傾倒偵測已在執行中!!");
                }
            }
            //Task.Run(() => WatchVirtualPtAndStopWorker());
            Agv.TryControlAutoDoor(Agv.Navigation.LastVisitedTag);
            return await base.BeforeTaskExecuteActions();
        }

        internal override async Task<(bool success, AlarmCodes alarmCode)> HandleAGVCActionSucceess()
        {
            await Agv.Laser.AllLaserDisable();
            return await base.HandleAGVCActionSucceess();
        }
        public override async Task<bool> LaserSettingBeforeTaskExecute()
        {
            await Agv.Laser.AllLaserActive();
            var _agvcActionStatus = Agv.AGVC.ActionStatus;
            if (_agvcActionStatus == ActionStatus.ACTIVE || _agvcActionStatus == ActionStatus.PENDING)
                return true;
            return await Agv.Laser.ModeSwitch(RunningTaskData.ExecutingTrajecory.First().Laser, true);
        }

    }
}
