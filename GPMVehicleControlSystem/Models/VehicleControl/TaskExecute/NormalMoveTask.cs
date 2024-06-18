using AGVSystemCommonNet6;
using AGVSystemCommonNet6.AGVDispatch.Messages;
using AGVSystemCommonNet6.MAP;
using AGVSystemCommonNet6.Vehicle_Control.VCS_ALARM;
using GPMVehicleControlSystem.Models.Buzzer;
using GPMVehicleControlSystem.Models.NaviMap;
using GPMVehicleControlSystem.Models.VehicleControl.AGVControl;
using GPMVehicleControlSystem.Models.VehicleControl.Vehicles;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using RosSharp.RosBridgeClient.Actionlib;
using SQLitePCL;
using System.Diagnostics;
using static AGVSystemCommonNet6.clsEnums;
using static AGVSystemCommonNet6.MAP.MapPoint;

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
                logger.Trace($"分段任務接收:軌跡終點:{end_of_traj},目的地:{destine}");
            }

        }
        protected override Task<CarController.SendActionCheckResult> TransferTaskToAGVC()
        {
            if (Agv.Parameters.AgvType == AGV_TYPE.FORK)
            {
                ForkActionStartWhenReachSecondartPTFlag = DetermineIsNeedDoForkAction(RunningTaskData, out NextSecondartPointTag, out NextWorkStationPointTag);
                logger.Info($"抵達終點後 Fork 動作:{ForkActionStartWhenReachSecondartPTFlag}(二次定位點{NextSecondartPointTag},取放貨站點 {NextWorkStationPointTag})");
                if (ForkActionStartWhenReachSecondartPTFlag)
                {
                    StartTrackingSecondaryPointReach();
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

        internal void StartTrackingSecondaryPointReach()
        {
            Task.Run(async () =>
            {
                while (Agv.GetSub_Status() == SUB_STATUS.RUN)
                {
                    await Task.Delay(1);
                    var _currentTag = Agv.BarcodeReader.CurrentTag;
                    if (_currentTag == null)
                        continue;
                    if (NextSecondartPointTag == _currentTag)
                    {
                        if (Agv.AGVC.CycleStopActionExecuting)
                        {
                            break;
                        }
                        var isunLoad = Agv._RunTaskData.OrderInfo.NextAction == ACTION_TYPE.Unload;
                        var ischarge = Agv._RunTaskData.OrderInfo.NextAction == ACTION_TYPE.Charge;

                        logger.Warn($"抵達二次定位點 TAG{_currentTag} 牙叉準備上升({Agv._RunTaskData.OrderInfo.ActionName})");
                        try
                        {
                            double _position_aim = 0;
                            if (Agv.WorkStations.Stations.TryGetValue(NextWorkStationPointTag, out WorkStation.clsWorkStationData? _stationData))
                            {
                                var orderInfo = Agv._RunTaskData.OrderInfo;
                                bool isCarryOrder = orderInfo.ActionName == ACTION_TYPE.Carry;
                                var height = 0;
                                if (isCarryOrder)
                                {
                                    bool isNextGoalEqualSource = orderInfo.NextAction == ACTION_TYPE.Unload;
                                    if (isNextGoalEqualSource)
                                        height = orderInfo.SourceSlot;
                                    else
                                        height = orderInfo.DestineSlot;
                                }
                                else
                                {
                                    height = orderInfo.NextAction == ACTION_TYPE.Charge ? 0 : orderInfo.DestineSlot;
                                }

                                if (_stationData.LayerDatas.TryGetValue(height, out WorkStation.clsStationLayerData? _settings))
                                {
                                    _position_aim = isunLoad || ischarge ? _settings.Down_Pose : _settings.Up_Pose;
                                    var _Height_PreAction = Agv.Parameters.ForkAGV.SaftyPositionHeight < _position_aim ? Agv.Parameters.ForkAGV.SaftyPositionHeight : _position_aim;
                                    logger.Warn($"抵達二次定位點 TAG{_currentTag}, 牙叉開始動作上升至第{height}層. ({_Height_PreAction}cm)");

                                    Task.Run(async () =>
                                    {
                                        Agv.ForkLifter.IsHeightPreSettingActionRunning = true;
                                        var result = await Agv.ForkLifter.ForkPose(_Height_PreAction, 1);
                                        Agv.ForkLifter.IsHeightPreSettingActionRunning = false;
                                        if (!result.confirm)
                                        {
                                            Abort(AlarmCodes.Fork_Action_Aborted);
                                        }
                                    });
                                    break;
                                }
                                else
                                {
                                    AlarmManager.AddWarning(AlarmCodes.Fork_WorkStation_Teach_Data_Not_Found_layer);
                                    Abort(AlarmCodes.Fork_Pose_Change_Fail_When_Reach_Secondary);
                                    break;
                                }
                            }
                            else
                            {
                                AlarmManager.AddWarning(AlarmCodes.Fork_WorkStation_Teach_Data_Not_Found_Tag);
                                Abort(AlarmCodes.Fork_Pose_Change_Fail_When_Reach_Secondary);
                                break;
                            }
                        }
                        catch (Exception ex)
                        {
                            logger.Error(ex, ex.Message);
                            Abort(AlarmCodes.Fork_Pose_Change_Fail_When_Reach_Secondary);
                            break;
                        }
                    }

                }
                logger.Trace($"牙叉提前上升至安全位置程序結束..");
            });
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
                    logger.Warn($"貨物傾倒偵測已在執行中!!");
                }
            }
            //Task.Run(() => WatchVirtualPtAndStopWorker());
            Agv.TryControlAutoDoor(Agv.Navigation.LastVisitedTag);
            return base.BeforeTaskExecuteActions();
        }

        internal override async Task<(bool success, AlarmCodes alarmCode)> HandleAGVCActionSucceess()
        {
            await Agv.Laser.AllLaserDisable();
            return await base.HandleAGVCActionSucceess();
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

                logger.Info($"Wait AGV Move(Active), Will Start Cargo Bias Detection.");
                CancellationTokenSource cts = new CancellationTokenSource();
                cts.CancelAfter(10000);
                while (Agv.AGVC.ActionStatus != RosSharp.RosBridgeClient.Actionlib.ActionStatus.ACTIVE)
                {
                    await Task.Delay(1);
                    if (cts.IsCancellationRequested)
                    {
                        Agv.IsCargoBiasDetecting = false;
                        logger.Error($"Wait AGV Move(Active) Timeout, cargo Bias Detection not start.");
                        return;
                    }
                }
                logger.Info($"Start Cargo Bias Detection.");
                while (Agv.CargoStatus == Vehicle.CARGO_STATUS.HAS_CARGO_NORMAL && Agv.ExecutingTaskEntity.action == ACTION_TYPE.None)
                {
                    await Task.Delay(1);
                    if (Agv.AGVC.ActionStatus != RosSharp.RosBridgeClient.Actionlib.ActionStatus.ACTIVE)
                    {
                        break;
                    }
                    if (Agv.CargoStatus == Vehicle.CARGO_STATUS.HAS_CARGO_BUT_BIAS || Agv.CargoStatus == Vehicle.CARGO_STATUS.NO_CARGO)
                    {
                        logger.Warn($"貨物傾倒偵測觸發-Check1");
                        await Task.Delay(500); //避免訊號瞬閃導致誤偵測
                        if (Agv.CargoStatus != Vehicle.CARGO_STATUS.HAS_CARGO_NORMAL)
                        {
                            logger.Error($"貨物傾倒偵測觸發-Check2_Actual Trigger. AGV Will Cycle Stop");
                            Agv.IsCargoBiasTrigger = true;
                            Agv.AGVC.ResetTask(RESET_MODE.CYCLE_STOP);
                            break;
                        }
                    }
                }
                Agv.IsCargoBiasDetecting = false;
            });
        }

        public override async Task<bool> LaserSettingBeforeTaskExecute()
        {
            await Agv.Laser.AllLaserActive();
            var _agvcActionStatus = Agv.AGVC.ActionStatus;
            if (_agvcActionStatus == ActionStatus.ACTIVE || _agvcActionStatus == ActionStatus.PENDING)
                return true;
            return await Agv.Laser.ModeSwitch(this.RunningTaskData.ExecutingTrajecory.First().Laser, true);
        }

    }
}
