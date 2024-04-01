using AGVSystemCommonNet6.AGVDispatch.Messages;
using AGVSystemCommonNet6.AGVDispatch;
using AGVSystemCommonNet6.Log;
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

namespace GPMVehicleControlSystem.Models.VehicleControl.Vehicles
{
    public partial class Vehicle
    {

        private Queue<FeedbackData> ActionFinishReportFailQueue = new Queue<FeedbackData>();
        private SemaphoreSlim TaskDispatchFlowControlSemaphoreSlim = new SemaphoreSlim(1, 1);
        private async void AGVSInit()
        {
            string vms_ip = Parameters.Connections[Params.clsConnectionParam.CONNECTION_ITEM.AGVS].IP;
            int vms_port = Parameters.Connections[Params.clsConnectionParam.CONNECTION_ITEM.AGVS].Port;
            //AGVS
            AGVS = new clsAGVSConnection(vms_ip, vms_port, Parameters.VMSParam.LocalIP);
            AGVS.Setup(Parameters.SID, Parameters.VehicleName);
            AGVS.SetLogFolder(Path.Combine(Parameters.LogFolder, "AGVS_Message_Log"));
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
            AGVS.OnRunningStatusReport_T1Timeout += Handle_AGVS_RunningStatusReport_T1Timeout;
            AGVS.OnPingFail += AGVSPingFailHandler;
            AGVS.OnPingSuccess += AGVSPingSuccessHandler;
            AGVS.Start();
            AGVS.TrySendOnlineModeChangeRequest(BarcodeReader.CurrentTag, REMOTE_MODE.OFFLINE);

            if (Parameters.SyncEQInfoFromAGVS)
            {
                var eqinfomations = await GetWorkStationEQInformation();
                if (eqinfomations != null)
                {
                    WorkStations.SyncInfo(eqinfomations);
                    SaveTeachDAtaSettings();
                }
            }

        }

        private async void AGVSPingSuccessHandler()
        {
            await Task.Delay(1).ConfigureAwait(false);
            LOG.TRACE($"AGVS Network restored. ");
            AlarmManager.ClearAlarm(AlarmCodes.AGVS_PING_FAIL);
        }

        private async void AGVSPingFailHandler()
        {
            await Task.Delay(1).ConfigureAwait(false);
            LOG.TRACE($"AGVS Network Ping Fail.... ");
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
            bool isMoveOrderButDestineIsWorkStation = destineStation?.StationType != STATION_TYPE.Normal && action_type == ACTION_TYPE.None;
            if (GetSub_Status() == SUB_STATUS.DOWN) //TODO More Status Confirm when recieve AGVS Task
                returnCode = TASK_DOWNLOAD_RETURN_CODES.AGV_STATUS_DOWN;

            if (isMoveOrderButDestineIsWorkStation)
                returnCode = TASK_DOWNLOAD_RETURN_CODES.AGV_CANNOT_GO_TO_WORKSTATION_WITH_NORMAL_MOVE_ACTION;

            LOG.INFO($"Check Status When AGVS Taskdownload, Return Code:{returnCode}({(int)returnCode})");
            return returnCode;
        }

        internal async void AGVS_OnTaskDownloadFeekbackDone(object? sender, clsTaskDownloadData taskDownloadData)
        {
            await Task.Delay(1);
            _ = Task.Run(async () =>
            {
                LOG.INFO($"Task Download: Task Name = {taskDownloadData.Task_Name} , Task Simple = {taskDownloadData.Task_Simplex}", false);
                LOG.WARN($"{taskDownloadData.Task_Simplex},Trajectory: {string.Join("->", taskDownloadData.ExecutingTrajecory.Select(pt => pt.Point_ID))}");
                AGV_Reset_Flag = AGVSResetCmdFlag = false;

                await CheckActionFinishFeedbackFinish();
                clsEQHandshakeModbusTcp.HandshakingModbusTcpProcessCancel?.Cancel();
                _TryClearExecutingTask();
                WriteTaskNameToFile(taskDownloadData.Task_Name);
                try
                {
                    await Task.Delay(200);
                    ExecuteAGVSTask(taskDownloadData);
                }
                catch (NullReferenceException ex)
                {
                    LOG.Critical(ex.Message, ex);
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

            });

        }

        private async Task CheckActionFinishFeedbackFinish()
        {
            if (!IsActionFinishTaskFeedbackExecuting)
                return;
            LOG.WARN($"Recieve AGVs Task But [ACTION_FINISH] Feedback TaskStatus Process is Running...");
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
        }

        private void Handle_AGVS_TaskFeedBackT1Timeout(object? sender, FeedbackData feedbackData)
        {
            LOG.WARN($"Task Feedback to AGVS(TaskName={feedbackData.TaskName},state={feedbackData.TaskStatus})=> Canceled because T1 Timeout");
            if (feedbackData.TaskStatus == TASK_RUN_STATUS.ACTION_FINISH)
            {
                LOG.WARN($"Retry Task Feedback to AGVS(TaskName={feedbackData.TaskName},state={feedbackData.TaskStatus})");
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
                LOG.WARN($"Try load map from local : {Parameters.MapParam.LocalMapFileFullName}");
                NavingMap = MapStore.GetMapFromFile(Parameters.MapParam.LocalMapFileFullName);
                if (NavingMap.Note != "empty")
                {
                    LOG.WARN($"Local Map data load success: {NavingMap.Name}({NavingMap.Note})");
                }
            }
            else
            {
                LOG.ERROR($"Local map file dosen't exist({Parameters.MapParam.LocalMapFileFullName})");
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
                        LOG.INFO($"Map Downloaded. Map Name : {NavingMap.Name}, Version: {NavingMap.Note}");
                        return (true, NavingMap);

                    }
                    else
                    {
                        LOG.ERROR($"Cannot download map from server.({MapStore.GetMapUrl})");
                        return (false, null);
                    }
                }
                catch (Exception ex)
                {

                    LOG.Critical($"Map Download Fail....{ex.Message}", ex);
                    return (false, null);
                }
            });
        }



        public async Task<List<clsAGVSConnection.clsEQOptions>> GetWorkStationEQInformation()
        {
            try
            {
                List<clsAGVSConnection.clsEQOptions> eqOptions = await AGVS.GetEQsInfos(WorkStations.Stations.Keys.ToArray());
                LOG.INFO($"WorkStation EQ Infos : \r\n{JsonConvert.SerializeObject(eqOptions, Formatting.Indented)}");
                return eqOptions;
            }
            catch (Exception ex)
            {
                LOG.ERROR($"WorkStation EQ Infos fetch from AGVs Fail {ex.Message}");
                return null;
            }
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

        /// <summary>
        /// 生成支援WebAPI的RunningStatus Model
        /// </summary>
        /// <returns></returns>
        public virtual clsRunningStatus HandleWebAPIProtocolGetRunningStatus()
        {
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
                var status = new clsRunningStatus
                {
                    Cargo_Status = CargoStatus == CARGO_STATUS.HAS_CARGO_NORMAL ? 1 : 0,
                    CargoType = GetCargoType(),
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
                };
                return status;
            }
            catch (Exception ex)
            {
                //LOG.ERROR("GenRunningStateReportData ", ex);
                return new clsRunningStatus();
            }
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
                double[] batteryLevels = Batteries.ToList().FindAll(bky => bky.Value != null).Select(battery => (double)battery.Value.Data.batteryLevel).ToArray();
                var status = new RunningStatus
                {
                    Cargo_Status = CargoStatus == CARGO_STATUS.HAS_CARGO_NORMAL ? 1 : 0,
                    CargoType = GetCargoType(),
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
                //LOG.ERROR("GenRunningStateReportData ", ex);
                return new RunningStatus();
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

            LOG.INFO($"[任務取消] AGVS TASK Cancel Request ({mode}) Reach. Current Action Status={AGVC.ActionStatus}, AGV SubStatus = {GetSub_Status()}", color: ConsoleColor.Red);

            if (AGVSResetCmdFlag)
            {
                LOG.INFO($"[任務取消] AGVSResetCmdFlag 'ON'. Current Action Status={AGVC.ActionStatus}, AGV SubStatus = {GetSub_Status()}", color: ConsoleColor.Yellow);
                return true;
            }

            while (TaskDispatchStatus == TASK_DISPATCH_STATUS.Pending)
            {
                await Task.Delay(1);
            }

            AGVSResetCmdFlag = true;
            IsWaitForkNextSegmentTask = false;

            try
            {

                if (!AGVC.IsRunning && mode == RESET_MODE.CYCLE_STOP)
                {
                    AGVC.OnAGVCActionChanged = null;
                    AGV_Reset_Flag = false;
                    LOG.WARN($"[任務取消] AGVS TASK Cancel Request ({mode}),But AGV is stopped.(IDLE)");
                    await AGVC.SendGoal(new TaskCommandGoal());//下空任務清空
                    FeedbackTaskStatus(TASK_RUN_STATUS.ACTION_FINISH, IsTaskCancel: true);
                    AGVC._ActionStatus = ActionStatus.NO_GOAL;
                    AGV_Reset_Flag = true;
                    return true;
                }
                else
                {
                    bool result = await AGVC.ResetTask(mode);
                    if (mode == RESET_MODE.ABORT)
                    {
                        _ = Task.Factory.StartNew(async () =>
                        {
                            if (!normal_state)
                            {
                                AlarmManager.AddAlarm(AlarmCodes.AGVs_Abort_Task, false);
                            }
                            ExecutingTaskEntity.Abort();
                        });
                    }
                    return result;
                }
            }
            catch (Exception ex)
            {
                LOG.Critical(ex);
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
