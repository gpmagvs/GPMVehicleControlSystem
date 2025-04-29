using AGVSystemCommonNet6.AGVDispatch.Messages;
using AGVSystemCommonNet6.AGVDispatch;
using static AGVSystemCommonNet6.clsEnums;
using AGVSystemCommonNet6.AGVDispatch.Model;
using Newtonsoft.Json;
using GPMVehicleControlSystem.Models.NaviMap;
using AGVSystemCommonNet6.GPMRosMessageNet.Actions;
using RosSharp.RosBridgeClient.Actionlib;
using AGVSystemCommonNet6.MAP;
using System.Diagnostics;
using AGVSystemCommonNet6.Vehicle_Control.VCS_ALARM;
using System.Threading.Tasks;
using GPMVehicleControlSystem.Models.VehicleControl.VehicleComponent;
using static GPMVehicleControlSystem.Models.VehicleControl.Vehicles.Vehicle;
using static AGVSystemCommonNet6.MAP.MapPoint;
using AGVSystemCommonNet6;
using AGVSystemCommonNet6.Notify;
using GPMVehicleControlSystem.Models.VehicleControl.Vehicles.CargoStates;
using GPMVehicleControlSystem.Tools;
using GPMVehicleControlSystem.Tools.DiskUsage;
using Microsoft.Extensions.Caching.Memory;

namespace GPMVehicleControlSystem.Models.VehicleControl.Vehicles
{
    public partial class Vehicle
    {

        private Queue<FeedbackData> ActionFinishReportFailQueue = new Queue<FeedbackData>();
        private SemaphoreSlim TaskDispatchFlowControlSemaphoreSlim = new SemaphoreSlim(1, 1);
        /// <summary>
        /// 記憶OnlineMode Query發生T1 Timeout 當下的上線狀態。
        /// </summary>
        private REMOTE_MODE _onlineModeWhenOnlineQueryActionT1Timeout = REMOTE_MODE.OFFLINE;

        private Debouncer _TaskDownloadHandleDebouncer = new Debouncer();
        private async Task AGVSInit()
        {
            string vms_ip = Parameters.Connections[Params.clsConnectionParam.CONNECTION_ITEM.AGVS].IP;
            int vms_port = Parameters.Connections[Params.clsConnectionParam.CONNECTION_ITEM.AGVS].Port;
            //AGVS
            AGVS = new clsAGVSConnection(vms_ip, vms_port, Parameters.VMSParam.LocalIP, logger: agvsLogger, msgHsDuration: Parameters.VMSParam.MsgHsDuration);
            AGVS.Setup(Parameters.SID, Parameters.VehicleName);
            AGVS.UseWebAPI = Parameters.VMSParam.Protocol == VMS_PROTOCOL.GPM_VMS;
            AGVS.OnWebAPIProtocolGetRunningStatus += HandleWebAPIProtocolGetRunningStatus;
            AGVS.OnTcpIPProtocolGetRunningStatus += HandleTcpIPProtocolGetRunningStatus;
            AGVS.OnRemoteModeChanged += HandleRemoteModeChangeReq;
            AGVS.OnTaskDownload += AGVSTaskDownloadConfirm;
            AGVS.OnTaskResetReq = HandleAGVSTaskCancelRequest;
            AGVS.OnTaskDownloadFeekbackDone += AGVS_OnTaskDownloadFeekbackDone;
            AGVS.OnConnectionRestored += AGVS_OnConnectionRestored;
            AGVS.OnDisconnected += AGVS_OnDisconnected;
            AGVS.OnTaskFeedBack_T1Timeout += Handle_AGVS_TaskFeedBackT1Timeout;
            AGVS.OnOnlineModeQuery_T1Timeout += Handle_AGVS_OnlineModeQuery_T1Timeout;
            AGVS.OnOnlineModeQuery_Recovery += Handle_AGVS_OnOnlineModeQuery_Recovery;
            AGVS.OnRunningStatusReport_T1Timeout += Handle_AGVS_RunningStatusReport_T1Timeout;
            AGVS.OnStartWaitMainStatusIDLEReported += HandleOnStartWaitMainStatusIDLEReported;
            AGVS.OnEndWaitMainStatusIDLEReported += HandleOnEndWaitMainStatusIDLEReported;
            AGVS.OnPingFail += AGVSPingFailHandler;
            AGVS.OnPingSuccess += AGVSPingSuccessHandler;

            ReloadLocalMap();
            await Task.Delay(1000);
            await DownloadMapFromServer();

            //if (Parameters.SyncEQInfoFromAGVS)
            //{
            //    var eqinfomations = await GetWorkStationEQInformation(this.NavingMap.Points.Values.Where(st => st.StationType != STATION_TYPE.Normal).Select(st => st.TagNumber).ToList());
            //    if (eqinfomations != null)
            //    {
            //        WorkStations.SyncInfo(eqinfomations);
            //        SaveTeachDAtaSettings();
            //    }
            //}

            AGVS.Start();
            AGVS.TrySendOnlineModeChangeRequest(BarcodeReader.CurrentTag, REMOTE_MODE.OFFLINE);
        }

        private void HandleOnEndWaitMainStatusIDLEReported(object? sender, EventArgs e)
        {
            DebugMessageBrocast($"Action Finish上報前等待主狀態IDLE上報已完成...");
        }

        private void HandleOnStartWaitMainStatusIDLEReported(object? sender, EventArgs e)
        {
            DebugMessageBrocast($"Action Finish上報前等待主狀態-IDLE上報完成...");
        }

        private async void AGVSPingSuccessHandler()
        {
            logger.LogTrace($"AGVS Network restored. ");
            AlarmManager.ClearAlarm(AlarmCodes.AGVS_PING_FAIL);
        }

        private async void AGVSPingFailHandler()
        {
            logger.LogTrace($"AGVS Network Ping Fail.... ");
            AlarmManager.AddWarning(AlarmCodes.AGVS_PING_FAIL);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="taskDownloadData"></param>
        /// <returns></returns>
        internal TASK_DOWNLOAD_RETURN_CODES AGVSTaskDownloadConfirm(clsTaskDownloadData taskDownloadData)
        {

            TASK_DOWNLOAD_RETURN_CODES returnCode = TASK_DOWNLOAD_RETURN_CODES.OK;
            AGV_Reset_Flag = AGVSResetCmdFlag = false;
            ACTION_TYPE action_type = taskDownloadData.Action_Type;
            MapPoint? destineStation = NavingMap.Points.Values.FirstOrDefault(pt => pt.TagNumber == taskDownloadData.Destination);
            if (destineStation == null) //表示派車有新增點位 但本地圖資尚未更新
                return TASK_DOWNLOAD_RETURN_CODES.OK;

            bool isMoveOrderButDestineIsWorkStation = destineStation?.StationType != STATION_TYPE.Normal && action_type == ACTION_TYPE.None;
            if (GetSub_Status() == SUB_STATUS.DOWN) //TODO More Status Confirm when recieve AGVS Task
                returnCode = TASK_DOWNLOAD_RETURN_CODES.AGV_STATUS_DOWN;

            if (Parameters.AgvType == AGV_TYPE.INSPECTION_AGV)
                return TASK_DOWNLOAD_RETURN_CODES.OK;


            if (Parameters.AgvType != AGV_TYPE.INSPECTION_AGV && isMoveOrderButDestineIsWorkStation)
                returnCode = TASK_DOWNLOAD_RETURN_CODES.AGV_CANNOT_GO_TO_WORKSTATION_WITH_NORMAL_MOVE_ACTION;

            //於非主幹道站點收到走行任務
            if (lastVisitedMapPoint.StationType != STATION_TYPE.Normal && action_type == ACTION_TYPE.None)
                returnCode = TASK_DOWNLOAD_RETURN_CODES.AGV_CANNOT_EXECUTE_NORMAL_MOVE_ACTION_IN_NON_NORMAL_POINT;

            if (Main_Status == MAIN_STATUS.RUN && _RunTaskData?.Action_Type != ACTION_TYPE.None)
                returnCode = TASK_DOWNLOAD_RETURN_CODES.AGV_CANNOT_EXECUTE_TASK_WHEN_WORKING_AT_WORKSTATION;
            //收到Discharge任務，但Homing_Trajectory 最後一點不是一般點位
            if (action_type == ACTION_TYPE.Discharge)
            {
                var homing_traj = taskDownloadData.Homing_Trajectory;
                if (!homing_traj.Any() || homing_traj.Length == 1)
                    returnCode = TASK_DOWNLOAD_RETURN_CODES.Homing_Trajectory_Error;
                MapPoint? destineStationOfHoming = NavingMap.Points.Values.FirstOrDefault(pt => pt.TagNumber == homing_traj.Last().Point_ID);
                if (destineStationOfHoming?.StationType != STATION_TYPE.Normal)
                    returnCode = TASK_DOWNLOAD_RETURN_CODES.Homing_Trajectory_Error;
            }

            logger.LogInformation($"Check Status When AGVS Taskdownload, Return Code:{returnCode}({(int)returnCode})");
            if (returnCode != TASK_DOWNLOAD_RETURN_CODES.OK)
            {
                logger.LogWarning($"Reject AGVS Task : {returnCode}({(int)returnCode})");
                AlarmManager.AddWarning(AlarmCodes.Reject_AGVS_Task);
            }

            return returnCode;
        }

        /// <summary>
        /// 確認任務下載回覆給派車後 執行流程(任務下載至車控)
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="taskDownloadData"></param>
        internal async void AGVS_OnTaskDownloadFeekbackDone(object? sender, clsTaskDownloadData taskDownloadData)
        {
            //bool _isAgvRunning = GetSub_Status() == SUB_STATUS.RUN || AGVC.ActionStatus == ActionStatus.ACTIVE || AGVC.ActionStatus == ActionStatus.PENDING;
            await Task.Delay(1);
            //_TaskDownloadHandleDebouncer.OnActionCanceled += OnActionDisposed;
            //_TaskDownloadHandleDebouncer.Debounce(async () =>
            //{
            //    _TaskDownloadHandleDebouncer.OnActionCanceled -= OnActionDisposed;
            //    await TaskDownloadAction(taskDownloadData);
            //}, 700, $"TaskDownload-{taskDownloadData.Task_Simplex}");

            _ = Task.Run(() =>
             {
                 TaskDownloadAction(taskDownloadData);
             });

            async Task TaskDownloadAction(clsTaskDownloadData taskDownloadData)
            {
                TaskDispatchStatus = TASK_DISPATCH_STATUS.Pending;
                try
                {
                    if (AGV_Reset_Flag)
                        return;
                    logger.LogInformation($"Task Download: Task Name = {taskDownloadData.Task_Name} , Task Simple = {taskDownloadData.Task_Simplex}", false);
                    logger.LogWarning($"{taskDownloadData.Task_Simplex},Trajectory: {string.Join("->", taskDownloadData.ExecutingTrajecory.Select(pt => pt.Point_ID))}");

                    await CheckActionFinishFeedbackFinish();
                    clsEQHandshakeModbusTcp.HandshakingModbusTcpProcessCancel?.Cancel();
                    _TryClearExecutingTask();
                    WriteTaskNameToFile(taskDownloadData.Task_Name);
                    try
                    {
                        await Task.Delay(20);
                        ExecuteAGVSTask(taskDownloadData);
                    }
                    catch (NullReferenceException ex)
                    {
                        logger.LogError(ex.Message, ex);
                    }

                    void _TryClearExecutingTask()
                    {
                        AGVC.OnAGVCActionChanged = null;

                        if (ExecutingTaskEntity != null)
                        {
                            ExecutingTaskEntity.TaskCancelByReplan.Cancel();
                            ExecutingTaskEntity.Dispose();
                        }
                    }

                }
                catch (Exception)
                {
                }
                finally
                {
                    AGV_Reset_Flag = false;
                }
            }

            void OnActionDisposed(object? sender, string newActionName)
            {
                //使用前一筆任務Action類型決定要上報?
                TASK_RUN_STATUS _status = _RunTaskData.Action_Type == ACTION_TYPE.None ? TASK_RUN_STATUS.NAVIGATING : TASK_RUN_STATUS.ACTION_FINISH;
                _RunTaskData = taskDownloadData.Clone();
                FeedbackTaskStatus(_status);
                _TaskDownloadHandleDebouncer.OnActionCanceled -= OnActionDisposed;
                logger.LogWarning($"命令-{_RunTaskData.Task_Simplex} 已終止:因 {newActionName} 命令下達,FeedbackTaskStatus 上報 {_status}");
            }
        }

        /// <summary>
        /// 處理任務取消請求
        /// </summary>
        /// <param name="mode">取消模式</param>
        /// <param name="normal_state"></param>
        /// <returns></returns>
        internal async Task<bool> HandleAGVSTaskCancelRequest(RESET_MODE mode, bool normal_state = false)
        {
            //_TaskDownloadHandleDebouncer.Debounce(() => { }, 1, "CycleStop");
            TaskCycleStopStatus = TASK_CANCEL_STATUS.RECEIVED_CYCLE_STOP_REQUEST;
            logger.LogInformation($"[任務取消] AGVS TASK Cancel Request ({mode}) Reach. Current Action Status={AGVC.ActionStatus}, AGV SubStatus = {GetSub_Status()}");

            if (mode == RESET_MODE.ABORT)
            {
                AGVC.EmergencyStop(true);
                AlarmManager.AddAlarm(AlarmCodes.AGVs_Abort_Task, false);
                TaskCycleStopStatus = TASK_CANCEL_STATUS.FINISH_CYCLE_STOP_REQUEST;
                if (!normal_state)
                    AlarmManager.AddAlarm(AlarmCodes.AGVs_Abort_Task, false);
                return true;
            }

            AGVSResetCmdFlag = true;
            IsWaitForkNextSegmentTask = false;

            bool isNoTaskRunning = TaskDispatchStatus == TASK_DISPATCH_STATUS.IDLE && (Main_Status == MAIN_STATUS.IDLE || Main_Status == MAIN_STATUS.DOWN);
            try
            {
                if (isNoTaskRunning)
                {
                    AGV_Reset_Flag = false;
                    logger.LogWarning($"[任務取消] AGVS TASK Cancel Request ({mode}),But AGV is stopped.(IDLE)");
                    await AGVC.SendGoal(new TaskCommandGoal());//下空任務清空
                    FeedbackTaskStatus(TASK_RUN_STATUS.ACTION_FINISH, IsTaskCancel: true);
                    AGVC._ActionStatus = ActionStatus.NO_GOAL;
                    AGV_Reset_Flag = true;
                    AGVC.OnAGVCActionChanged = null;
                    TaskCycleStopStatus = TASK_CANCEL_STATUS.FINISH_CYCLE_STOP_REQUEST;
                    return true;
                }
                else
                {
                    if (TaskDispatchStatus == TASK_DISPATCH_STATUS.Pending)
                        while (TaskDispatchStatus == TASK_DISPATCH_STATUS.Pending)
                        {
                            await Task.Delay(1);
                        }
                    TaskCycleStopStatus = TASK_CANCEL_STATUS.EXECUTING_CYCLE_STOP_REQUEST;
                    bool result = await AGVC.ResetTask(mode);

                    if (TaskDispatchStatus == TASK_DISPATCH_STATUS.IDLE)
                        FeedbackTaskStatus(TASK_RUN_STATUS.ACTION_FINISH, IsTaskCancel: true);

                    return result;
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, ex.Message);
                if (mode == RESET_MODE.CYCLE_STOP)
                    AlarmManager.AddAlarm(AlarmCodes.Exception_When_AGVC_AGVS_Task_Reset_CycleStop, false);
                else
                    AlarmManager.AddAlarm(AlarmCodes.Exception_When_AGVC_AGVS_Task_Reset_Abort, false);
                return false;
            }
            finally
            {
            }

        }

        internal async Task<(bool confirm, string message)> TryTemporaryStopWhenReachTag(int stopTag)
        {
            if (AGVC.ActionStatus != ActionStatus.ACTIVE && AGVC.ActionStatus != ActionStatus.PENDING)
                return (false, "No Task");

            List<clsMapPoint> currentTraj = _RunTaskData.ExecutingTrajecory.ToList();
            clsMapPoint? stopPoint = currentTraj.FirstOrDefault(pt => pt.Point_ID == stopTag);
            if (stopPoint == null)
                return (false, $"Request Stop Tag:{stopTag} no in trajectory");

            int indexOfStopPoint = currentTraj.IndexOf(stopPoint);
            if (indexOfStopPoint == currentTraj.Count - 1) //要求停車的位置是任務軌跡終點
                return (true, $"Stop Tag {stopTag} equal trajectory goal.");
            //int indexOfSpeedDecreaseStartPoint = indexOfStopPoint - 1;

            _ = Task.Run(async () =>
            {
                double goal_x = stopPoint.X;
                double goal_y = stopPoint.Y;
                double distance = 0;
                while ((distance = _calculateDistance(Navigation.Data.robotPose.pose.position.x, Navigation.Data.robotPose.pose.position.y, goal_x, goal_y)) > 3.0)
                {
                    await Task.Delay(1000);
                    logger.LogTrace($"[TryTemporaryStopWhenReachTag] Wait AGV Reach in to Decrease Speed Region..{distance} m");
                }
                logger.LogTrace($"[TryTemporaryStopWhenReachTag] AGV Reach in to Decrease Speed Region, Send DECELERATE request to AGVC.");
                await AGVC.CarSpeedControl(AGVControl.CarController.ROBOT_CONTROL_CMD.DECELERATE, AGVControl.CarController.SPEED_CONTROL_REQ_MOMENT.AGVS_REQUEST, false);

                logger.LogTrace($"[TryTemporaryStopWhenReachTag] Wait AGV Reach Tag {stopPoint.Point_ID}");
                while (Navigation.Data.lastVisitedNode.data != stopPoint.Point_ID)
                {
                    await Task.Delay(100);
                }

                logger.LogTrace($"[TryTemporaryStopWhenReachTag] AGV Reach Stop Tag {stopTag}, Send STOP request to AGVC.");
                await AGVC.CarSpeedControl(AGVControl.CarController.ROBOT_CONTROL_CMD.STOP, AGVControl.CarController.SPEED_CONTROL_REQ_MOMENT.AGVS_REQUEST, false);

                double _calculateDistance(double x1, double y1, double x2, double y2)
                {
                    return Math.Sqrt(Math.Pow(x2 - x1, 2) + Math.Pow(y2 - y1, 2));
                }

            });
            return (true, "");
        }
        private async Task CheckActionFinishFeedbackFinish()
        {
            if (!IsActionFinishTaskFeedbackExecuting)
                return;
            logger.LogWarning($"Recieve AGVs Task But [ACTION_FINISH] Feedback TaskStatus Process is Running...");
            CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            while (IsActionFinishTaskFeedbackExecuting)
            {
                await Task.Delay(1);
                if (cts.IsCancellationRequested)
                {
                    break;
                }
            }
        }

        private void Handle_AGVS_RunningStatusReport_T1Timeout(object? sender, EventArgs e)
        {
            AlarmManager.AddWarning(AlarmCodes.RunningStatusReport_T1_Timeout);
        }

        private void Handle_AGVS_OnlineModeQuery_T1Timeout(object? sender, EventArgs e)
        {
            AlarmManager.AddWarning(AlarmCodes.OnlineModeQuery_T1_Timeout);
            _onlineModeWhenOnlineQueryActionT1Timeout = Remote_Mode.Clone();
            if (GetSub_Status() != SUB_STATUS.RUN)
                Remote_Mode = REMOTE_MODE.OFFLINE;
        }

        private void Handle_AGVS_OnOnlineModeQuery_Recovery(object? sender, EventArgs e)
        {
            logger.LogInformation("Online Mode Query Request Restored!");
            AlarmManager.ClearAlarm(AlarmCodes.OnlineModeQuery_T1_Timeout);

            if (_onlineModeWhenOnlineQueryActionT1Timeout == REMOTE_MODE.ONLINE && _Remote_Mode == REMOTE_MODE.OFFLINE)
            {
                Task.Run(async () =>
                {
                    try
                    {
                        (bool success, RETURN_CODE return_code) result = (false, RETURN_CODE.NG);
                        int tryCnt = 1;
                        while (!result.success)
                        {
                            logger.LogInformation($"Try Online To AGVS because remote mode when T1 is Online...(第{tryCnt}次嘗試)");

                            if (GetSub_Status() == SUB_STATUS.DOWN)
                            {
                                logger.LogWarning($"Try Online To AGVS CANCELED, Because AGV Status is DOWN now.");
                                return;
                            }

                            if (AGVS.IsOnlineModeQueryTimeout)
                            {
                                logger.LogWarning($"Try Online To AGVS CANCELED, Because Online Mode Query T1 Timeout occuring aggin..");
                                return;
                            }
                            if (tryCnt >= 11)
                            {
                                logger.LogWarning($"Try Online To AGVS CANCELED, Because retry count > 10.");
                                return;
                            }
                            result = await Online_Mode_Switch(REMOTE_MODE.ONLINE, bypassStatusCheck: true);
                            string returyResultStr = $"{(result.success ? "成功" : "失敗")} , Return Code = {result.return_code}";
                            logger.LogInformation($"Try Online To AGVS because remote mode when T1 is Online...(第{tryCnt}次嘗試)=>{returyResultStr}");
                            await Task.Delay(1000);
                            tryCnt++;
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, ex.Message);
                    }
                    finally
                    {
                        logger.LogInformation($"Retry ONLINE process in Handle_AGVS_OnOnlineModeQuery_Recovery callback is done.");
                    }

                });
            }
        }

        private void Handle_AGVS_TaskFeedBackT1Timeout(object? sender, FeedbackData feedbackData)
        {
            logger.LogWarning($"Task Feedback to AGVS(TaskName={feedbackData.TaskName},state={feedbackData.TaskStatus})=> Canceled because T1 Timeout");
            if (feedbackData.TaskStatus == TASK_RUN_STATUS.ACTION_FINISH)
            {
                logger.LogWarning($"Retry Task Feedback to AGVS(TaskName={feedbackData.TaskName},state={feedbackData.TaskStatus})");
                Task.Factory.StartNew(async () =>
                {
                    taskfeedbackCanceTokenSoruce = new CancellationTokenSource();
                    _RunTaskData.IsActionFinishReported = await AGVS.TryTaskFeedBackAsync(feedbackData.TaskName,
                                                         feedbackData.TaskSimplex,
                                                         feedbackData.TaskSequence,
                                                         feedbackData.PointIndex,
                                                         TASK_RUN_STATUS.ACTION_FINISH,
                                                         Navigation.LastVisitedTag,
                                                         Navigation.CurrentCoordination,
                                                         taskfeedbackCanceTokenSoruce.Token,
                                                         feedbackData.IsFeedbackBecauseTaskCancel);
                });
            }
            AlarmManager.AddWarning(AlarmCodes.Task_Feedback_T1_Timeout);
        }

        internal void ReloadLocalMap()
        {
            if (File.Exists(Parameters.MapParam.LocalMapFileFullName))
            {
                logger.LogWarning($"Try load map from local : {Parameters.MapParam.LocalMapFileFullName}");
                NavingMap = MapStore.GetMapFromFile(Parameters.MapParam.LocalMapFileFullName);
                if (NavingMap.Note != "empty")
                {
                    logger.LogWarning($"Local Map data load success: {NavingMap.Name}({NavingMap.Note})");
                }
            }
            else
            {
                logger.LogError($"Local map file dosen't exist({Parameters.MapParam.LocalMapFileFullName})");
            }
        }
        internal async Task<(bool confirm, Map map)> DownloadMapFromServer()
        {
            return await Task.Run(async () =>
            {
                try
                {
                    var _NavingMap = await MapStore.GetMapFromServer();

                    if (_NavingMap != null)
                    {
                        _NavingMap.Segments = MapManager.CreateSegments(_NavingMap);
                        MapStore.SaveCurrentMap(_NavingMap, out string map_file_saved_path);
                        NavingMap = _NavingMap;
                        Parameters.MapParam.LocalMapFileName = $"temp/{NavingMap.Name}.json";
                        SaveParameters(Parameters);
                        logger.LogInformation($"Map Downloaded. Map Name : {NavingMap.Name}, Version: {NavingMap.Note}");
                        return (true, NavingMap);

                    }
                    else
                    {
                        logger.LogError($"Cannot download map from server.({MapStore.GetMapUrl})");
                        return (false, null);
                    }
                }
                catch (Exception ex)
                {

                    logger.LogError($"Map Download Fail....{ex.Message}", ex);
                    return (false, null);
                }
            });
        }

        private void AGVS_OnDisconnected(object? sender, EventArgs e)
        {
            AlarmManager.AddWarning(AlarmCodes.AGVs_Disconnected);
            RemoteModeSettingWhenAGVsDisconnect = _Remote_Mode == REMOTE_MODE.ONLINE ? REMOTE_MODE.ONLINE : REMOTE_MODE.OFFLINE;
            AutoOnlineRaising = _Remote_Mode == REMOTE_MODE.ONLINE;
            Remote_Mode = REMOTE_MODE.OFFLINE;
        }
        private void AGVS_OnConnectionRestored(object? sender, EventArgs e)
        {
            AlarmManager.ClearAlarm(AlarmCodes.AGVs_Disconnected);
            Task.Factory.StartNew(async () =>
            {
                await Task.Delay(1000);
                if (RemoteModeSettingWhenAGVsDisconnect == REMOTE_MODE.ONLINE && !IsActionFinishTaskFeedbackExecuting && (GetSub_Status() == SUB_STATUS.IDLE || GetSub_Status() == SUB_STATUS.Charging))
                {
                    HandleRemoteModeChangeReq(REMOTE_MODE.ONLINE);
                }
            });
        }
        internal async Task HandleAGVSTrafficControllingNotify()
        {
            if (GetSub_Status() != SUB_STATUS.IDLE)
                return;
            DirectionLighter.TrafficControllingLightsFlash(300);
        }

        /// <summary>
        /// 生成支援WebAPI的RunningStatus Model
        /// </summary>
        /// <returns></returns>
        public virtual clsRunningStatus HandleWebAPIProtocolGetRunningStatus()
        {
            //_memoryCache.Set<DiskUsageState>("DiskStatus", homeDiskUsage);
            DiskUsageState homeDiskUsage = memoryCache.Get<DiskUsageState>("DiskStatus");
            homeDiskUsage = homeDiskUsage ?? new DiskUsageState();

            clsCoordination Corrdination = new clsCoordination();
            MAIN_STATUS _Main_Status = Main_Status;
            int lastVisitedNode = 0;
            if (Navigation.Data != null)
            {
                Corrdination.X = Math.Round(Navigation.Data.robotPose.pose.position.x, 3);
                Corrdination.Y = Math.Round(Navigation.Data.robotPose.pose.position.y, 3);
                Corrdination.Theta = Math.Round(Navigation.Angle, 3);
                lastVisitedNode = Navigation.Data.lastVisitedNode.data;
            }
            AGVSystemCommonNet6.AGVDispatch.Model.clsAlarmCode[] alarm_codes = GetAlarmCodesUserReportToAGVS_WebAPI();
            try
            {
                double[] batteryLevels = Batteries.ToList().FindAll(bt => bt.Value != null).Select(battery => (double)battery.Value.Data.batteryLevel).ToArray();
                double[] batteryTemperatures = Batteries.ToList().FindAll(bt => bt.Value != null).Select(battery => (double)battery.Value.Data.maxCellTemperature).ToArray();

                int cargoExist = CargoStateStorer.IsCargoMountedNormal(Parameters.LDULD_Task_No_Entry) ? 1 : 0;
                int cargoType = cargoExist == 1 ? (int)CargoStateStorer.GetCargoType() : 0;

                var status = new clsRunningStatus
                {
                    Cargo_Status = cargoExist,
                    CargoType = cargoType,
                    AGV_Status = _Main_Status,
                    Electric_Volume = batteryLevels,
                    Electric_Temperatures = batteryTemperatures,
                    Last_Visited_Node = lastVisitedNode,
                    Coordination = Corrdination,
                    Odometry = Odometry,
                    AGV_Reset_Flag = AGV_Reset_Flag,
                    Alarm_Code = alarm_codes,
                    Escape_Flag = ExecutingTaskEntity == null ? false : ExecutingTaskEntity.RunningTaskData.Escape_Flag,
                    IsCharging = GetIsCharging(),
                    AppVersion = StaStored.APPVersion,
                    AvailableDiskSpace = homeDiskUsage.TotalAvailableSpace
                };
                return status;
            }
            catch (Exception ex)
            {
                //logger.LogError("GenRunningStateReportData ", ex);
                return new clsRunningStatus();
            }
        }

        private bool SimulationCargoExist()
        {
            if (!Parameters.CargoExistSensorParams.ExistSensorSimulation)
                return false;
            return !string.IsNullOrEmpty(CSTReader.ValidCSTID);
        }
        private AGVSystemCommonNet6.AGVDispatch.Model.clsAlarmCode[] GetAlarmCodesUserReportToAGVS_WebAPI()
        {
            return AlarmManager.CurrentAlarms.ToList().FindAll(alarm => alarm.Value.EAlarmCode != AlarmCodes.None).Select(alarm => new AGVSystemCommonNet6.AGVDispatch.Model.clsAlarmCode
            {
                Alarm_ID = alarm.Value.Code,
                Alarm_Level = alarm.Value.ELevel == AGVSystemCommonNet6.Vehicle_Control.VCS_ALARM.clsAlarmCode.LEVEL.Alarm ? 1 : 0,
                Alarm_Description = alarm.Value.CN,
                Alarm_Description_EN = alarm.Value.Description,
                Alarm_Category = alarm.Value.IsRecoverable ? 0 : 1,
            }).DistinctBy(alarm => alarm.Alarm_ID).ToArray();
        }

        /// <summary>
        /// 生成支援TCPIP通訊的RunningStatus Model
        /// </summary>
        /// <returns></returns>
        protected virtual RunningStatus HandleTcpIPProtocolGetRunningStatus()
        {
            clsCoordination Corrdination = new clsCoordination();
            MAIN_STATUS _Main_Status = Main_Status;
            Corrdination.X = Math.Round(Navigation.Data.robotPose.pose.position.x, 3);
            Corrdination.Y = Math.Round(Navigation.Data.robotPose.pose.position.y, 3);
            Corrdination.Theta = Math.Round(Navigation.Angle, 3);
            //gen alarm codes 
            AGVSystemCommonNet6.AGVDispatch.Messages.clsAlarmCode[] alarm_codes = GetAlarmCodesUserReportToAGVS();
            try
            {
                int cargoExist = CargoStateStorer.IsCargoMountedNormal(Parameters.LDULD_Task_No_Entry) ? 1 : 0;
                int cargoType = cargoExist == 1 ? (int)CargoStateStorer.GetCargoType() : 0;

                double[] batteryLevels = Batteries.ToList().FindAll(bky => bky.Value != null).Select(battery => (double)battery.Value.Data.batteryLevel).ToArray();
                var status = new RunningStatus
                {
                    Cargo_Status = cargoExist,
                    CargoType = cargoType,
                    AGV_Status = _Main_Status,
                    Electric_Volume = batteryLevels,
                    Last_Visited_Node = lastVisitedMapPoint.IsVirtualPoint ? lastVisitedMapPoint.TagNumber : Navigation.Data.lastVisitedNode.data,
                    Coordination = Corrdination,
                    Odometry = Odometry,
                    AGV_Reset_Flag = AGV_Reset_Flag,
                    Alarm_Code = alarm_codes,
                    Escape_Flag = ExecutingTaskEntity == null ? false : ExecutingTaskEntity.RunningTaskData.Escape_Flag,
                    IsCharging = GetIsCharging()
                };
                return status;
            }
            catch (Exception ex)
            {
                //logger.LogError("GenRunningStateReportData ", ex);
                return new RunningStatus();
            }
        }


        private static AGVSystemCommonNet6.AGVDispatch.Messages.clsAlarmCode[] GetAlarmCodesUserReportToAGVS()
        {
            return AlarmManager.CurrentAlarms.ToList().FindAll(alarm => alarm.Value.EAlarmCode != AlarmCodes.None).Select(alarm => new AGVSystemCommonNet6.AGVDispatch.Messages.clsAlarmCode
            {
                Alarm_ID = alarm.Value.Code,
                Alarm_Level = alarm.Value.IsRecoverable ? 0 : 1,
                Alarm_Description = alarm.Value.CN,
                Alarm_Description_EN = alarm.Value.Description,
                Alarm_Category = alarm.Value.IsRecoverable ? 0 : 1,
            }).DistinctBy(alarm => alarm.Alarm_ID).ToArray();
        }
    }
}
