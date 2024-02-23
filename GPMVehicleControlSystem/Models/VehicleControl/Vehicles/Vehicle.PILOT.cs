using AGVSystemCommonNet6;
using AGVSystemCommonNet6.AGVDispatch.Messages;
using AGVSystemCommonNet6.AGVDispatch.Model;
using AGVSystemCommonNet6.Log;
using AGVSystemCommonNet6.Vehicle_Control.Models;
using AGVSystemCommonNet6.Vehicle_Control.VCS_ALARM;
using AGVSystemCommonNet6.Vehicle_Control.VCSDatabase;
using GPMVehicleControlSystem.Models.VehicleControl.TaskExecute;
using GPMVehicleControlSystem.Models.VehicleControl.VehicleComponent;
using System.Diagnostics;
using static AGVSystemCommonNet6.clsEnums;

namespace GPMVehicleControlSystem.Models.VehicleControl.Vehicles
{
    public partial class Vehicle
    {
        private string TaskName = "";
        public TASK_RUN_STATUS CurrentTaskRunStatus = TASK_RUN_STATUS.NO_MISSION;
        internal bool AutoOnlineRaising = false;
        internal clsParkingAccuracy lastParkingAccuracy;
        public enum EQ_HS_METHOD
        {
            PIO,
            MODBUS,
            /// <summary>
            /// 模擬
            /// </summary>
            EMULATION
        }

        private TaskBase _ExecutingTask;
        public TaskBase ExecutingTaskModel
        {
            get => _ExecutingTask;
            set
            {
                if (_ExecutingTask != value)
                {
                    _ExecutingTask = value;
                }
            }
        }

        Dictionary<string, List<int>> TaskTrackingTags = new Dictionary<string, List<int>>();

        internal clsTaskDownloadData _RunTaskData = new clsTaskDownloadData()
        {
            IsLocalTask = true,
            IsActionFinishReported = true
        };


        /// <summary>
        /// 
        /// </summary>
        /// <param name="taskDownloadData"></param>
        /// <returns></returns>
        internal TASK_DOWNLOAD_RETURN_CODES AGVSTaskDownloadConfirm(clsTaskDownloadData taskDownloadData)
        {

            TASK_DOWNLOAD_RETURN_CODES returnCode = TASK_DOWNLOAD_RETURN_CODES.OK;

            ACTION_TYPE action_type = taskDownloadData.Action_Type;
            AGVSystemCommonNet6.MAP.MapPoint? destinePoint = NavingMap.Points.Values.FirstOrDefault(pt => pt.TagNumber == taskDownloadData.Destination);
            if (Sub_Status == SUB_STATUS.DOWN) //TODO More Status Confirm when recieve AGVS Task
                returnCode = TASK_DOWNLOAD_RETURN_CODES.AGV_STATUS_DOWN;

            if (destinePoint?.StationType != STATION_TYPE.Normal && action_type == ACTION_TYPE.None)
                returnCode = TASK_DOWNLOAD_RETURN_CODES.AGV_CANNOT_GO_TO_WORKSTATION_WITH_NORMAL_MOVE_ACTION;

            LOG.INFO($"Check Status When AGVS Taskdownload, Return Code:{returnCode}({(int)returnCode})");
            return returnCode;
        }

        /// <summary>
        /// 執行派車系統任務
        /// 19:10:07 端點未掃描到QR Code
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="taskDownloadData"></param>
        internal async void ExecuteAGVSTask(object? sender, clsTaskDownloadData taskDownloadData)
        {
            AGV_Reset_Flag = AGVSResetCmdFlag = false;
            Thread _executingTaskThread = new Thread(async (_taskDownloadData) =>
            {
                clsTaskDownloadData _downloadedData = (clsTaskDownloadData)_taskDownloadData;
                AGV_Reset_Flag = AGVSResetCmdFlag = false;
                if (IsActionFinishTaskFeedbackExecuting)
                {
                    LOG.WARN($"Recieve AGVs Task But [ACTION_FINISH] Feedback TaskStatus Process is Running...");
                }
                CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                while (IsActionFinishTaskFeedbackExecuting)
                {
                    if (cts.IsCancellationRequested)
                    {
                        taskfeedbackCanceTokenSoruce?.Cancel();
                        IsActionFinishTaskFeedbackExecuting = false;
                        break;
                    }
                    await Task.Delay(1);
                }
                LOG.WARN($"Recieve AGVs Task and Prepare to Excute!- NO [ACTION_FINISH] Feedback TaskStatus Process is Running!");
                clsEQHandshakeModbusTcp.HandshakingModbusTcpProcessCancel?.Cancel();
                AGVC.OnAGVCActionChanged = null;
                if (ExecutingTaskModel != null)
                {
                    ExecutingTaskModel.Dispose();
                }

                _RunTaskData = _downloadedData.Clone();
                _RunTaskData.IsEQHandshake = false;
                _RunTaskData.IsActionFinishReported = false;
                _RunTaskData.VibrationRecords = new List<clsVibrationRecord>();

                AlarmManager.ClearAlarm();
                Sub_Status = SUB_STATUS.RUN;
                await Laser.AllLaserActive();
                WriteTaskNameToFile(_downloadedData.Task_Name);
                LOG.INFO($"Task Download: Task Name = {_downloadedData.Task_Name} , Task Simple = {_downloadedData.Task_Simplex}", false);
                LOG.WARN($"{_downloadedData.Task_Simplex},Trajectory: {string.Join("->", _downloadedData.ExecutingTrajecory.Select(pt => pt.Point_ID))}");
                ACTION_TYPE action = _downloadedData.Action_Type;
                IsWaitForkNextSegmentTask = false;

                LOG.TRACE($"IsLocal Task ? => {_RunTaskData.IsLocalTask}");
                await Task.Run(async () =>
                {
                    if (action == ACTION_TYPE.None)
                    {
                        ExecutingTaskModel = new NormalMoveTask(this, _downloadedData);
                    }
                    else
                    {
                        if (_downloadedData.CST.Length == 0 && Remote_Mode == REMOTE_MODE.OFFLINE)
                            _downloadedData.CST = new clsCST[1] { new clsCST { CST_ID = $"TAEMU{DateTime.Now.ToString("mmssfff")}" } };
                        if (action == ACTION_TYPE.Charge)
                            ExecutingTaskModel = new ChargeTask(this, _downloadedData);
                        else if (action == ACTION_TYPE.Discharge)
                            ExecutingTaskModel = new DischargeTask(this, _downloadedData);
                        else if (action == ACTION_TYPE.Load)
                        {
                            ExecutingTaskModel = new LoadTask(this, _downloadedData);
                            (ExecutingTaskModel as LoadTask).lduld_record.TaskName = _RunTaskData.Task_Name;

                            if (_RunTaskData.CST.Length > 0)
                            {
                                (ExecutingTaskModel as LoadTask).lduld_record.CargoID_FromAGVS = _RunTaskData.CST.First().CST_ID;
                            }

                        }
                        else if (action == ACTION_TYPE.Unload)
                        {
                            ExecutingTaskModel = new UnloadTask(this, _downloadedData);
                            (ExecutingTaskModel as UnloadTask).lduld_record.TaskName = _RunTaskData.Task_Name;

                            if (_RunTaskData.CST.Length > 0)
                            {
                                (ExecutingTaskModel as UnloadTask).lduld_record.CargoID_FromAGVS = _RunTaskData.CST.First().CST_ID;
                            }
                        }
                        else if (action == ACTION_TYPE.Park)
                            ExecutingTaskModel = new ParkTask(this, _downloadedData);
                        else if (action == ACTION_TYPE.Unpark)
                            ExecutingTaskModel = new UnParkTask(this, _downloadedData);
                        else if (action == ACTION_TYPE.Measure)
                            ExecutingTaskModel = new MeasureTask(this, taskDownloadData);
                        else if (action == ACTION_TYPE.ExchangeBattery)
                            ExecutingTaskModel = new ExchangeBatteryTask(this, _downloadedData);
                        else
                        {
                            throw new NotImplementedException();
                        }
                    }
                    previousTagPoint = ExecutingTaskModel?.RunningTaskData.ExecutingTrajecory[0];
                    ExecutingTaskModel.ForkLifter = ForkLifter;
                    IsLaserRecoveryHandled = false;
                    _RunTaskData.IsEQHandshake = ExecutingTaskModel.eqHandshakeMode == WorkStation.WORKSTATION_HS_METHOD.HS;

                    if (Parameters.OrderInfoFetchSource == ORDER_INFO_FETCH_SOURCE.FROM_TASK_DOWNLOAD_CONTENT)
                    {
                        if (_downloadedData.Action_Type == ACTION_TYPE.None)
                            orderInfoViewModel = _downloadedData.OrderInfo;
                        else
                        {
                            orderInfoViewModel = new clsTaskDownloadData.clsOrderInfo
                            {
                                ActionName = _downloadedData.Action_Type,
                                DestineName = DestinationMapPoint == null ? _downloadedData.Destination.ToString() : DestinationMapPoint.Name
                            };
                        }
                    }

                    var result = await ExecutingTaskModel.Execute();
                    if (result != AlarmCodes.None)
                    {
                        Sub_Status = SUB_STATUS.DOWN;
                        LOG.Critical($"{action} 任務失敗:Alarm:{result}");
                        AlarmManager.AddAlarm(result, false);
                        AGVC.OnAGVCActionChanged = null;
                    }
                });

            });
            _executingTaskThread.IsBackground = true;
            _executingTaskThread.Start(taskDownloadData);
        }

        private async void TryGetTransferInformationFromCIM(string task_Name)
        {
            if (!Parameters.CIMConn)
                return;

            await Task.Factory.StartNew(async () =>
            {
                try
                {
                    TransferData transferData = new TransferData();
                    CancellationTokenSource cancTs = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                    while (transferData.ActionType == null)
                    {
                        await Task.Delay(1000);
                        if (cancTs.IsCancellationRequested)
                        {
                            LOG.ERROR($"Cannot Download Transfer Infomation From CIM");
                            return;
                        }
                        transferData = await QueryTaskInfoFromCIM(task_Name);
                    }

                    if (transferData != null)
                    {
                        var SourcePointIndex = transferData.FromStationId;
                        var DestinePointIndex = transferData.ToStationId;
                        string Action = transferData.ActionType;
                        string SourceEQName = string.Empty;
                        string DestinEQName = string.Empty;

                        if (NavingMap.Points.TryGetValue(DestinePointIndex, out var SourcePoint))
                        {
                            SourceEQName = SourcePoint.Graph.Display;
                        }
                        if (NavingMap.Points.TryGetValue(DestinePointIndex, out var DestinePoint))
                        {
                            DestinEQName = DestinePoint.Graph.Display;
                        }
                        orderInfoViewModel.SourceName = SourceEQName;
                        orderInfoViewModel.DestineName = DestinEQName;
                        LOG.INFO($"Download TransferTask= {orderInfoViewModel.ToJson()}", color: ConsoleColor.Green);

                    }
                }
                catch (Exception ex)
                {
                    LOG.ERROR(ex);
                }
            });
        }

        private void WriteTaskNameToFile(string task_Name)
        {
            try
            {
                TaskName = task_Name;
                File.WriteAllText("task_name.txt", task_Name);
            }
            catch (Exception ex)
            {
                LOG.ERROR(ex.Message, ex);
            }
        }

        private void ReadTaskNameFromFile()
        {
            if (File.Exists("task_name.txt"))
                TaskName = File.ReadAllText("task_name.txt");
        }


        private clsMapPoint? previousTagPoint;
        private void HandleLastVisitedTagChanged(object? sender, int newVisitedNodeTag)
        {
            Task.Factory.StartNew(async () =>
            {

                UpdateLastVisitedTagOfParam(newVisitedNodeTag);

                if (Operation_Mode == OPERATOR_MODE.MANUAL)
                    return;
                if (ExecutingTaskModel == null)
                    return;

                previousTagPoint = ExecutingTaskModel.RunningTaskData.ExecutingTrajecory.FirstOrDefault(pt => pt.Point_ID == newVisitedNodeTag);

                if (ExecutingTaskModel.action == ACTION_TYPE.None)
                {
                    var laser_mode = ExecutingTaskModel.RunningTaskData.ExecutingTrajecory.FirstOrDefault(pt => pt.Point_ID == newVisitedNodeTag).Laser;
                    await Laser.ModeSwitch(laser_mode, true);
                }

                if (ExecutingTaskModel.RunningTaskData.TagsOfTrajectory.Last() != Navigation.LastVisitedTag)
                {
                    FeedbackTaskStatus(TASK_RUN_STATUS.NAVIGATING);
                }
            });
        }

        private void UpdateLastVisitedTagOfParam(int newVisitedNodeTag)
        {
            Parameters.LastVisitedTag = newVisitedNodeTag;
            configFileChangedWatcher.EnableRaisingEvents = false;
            SaveParameters(Parameters);
            configFileChangedWatcher.EnableRaisingEvents = true;
        }

        internal async Task ReportMeasureResult(clsMeasureResult measure_result)
        {
            try
            {
                await Task.Delay(100);
                await AGVS.ReportMeasureData(measure_result);
            }
            catch (Exception ex)
            {
                LOG.ERROR(ex.Message, ex);
                AlarmManager.AddWarning(AlarmCodes.Measure_Result_Data_Report_Fail);
            }
        }
        private bool IsActionFinishTaskFeedbackExecuting = false;
        private CancellationTokenSource taskfeedbackCanceTokenSoruce = new CancellationTokenSource();
        private clsTaskDownloadData.clsOrderInfo _orderInfoViewModel = new clsTaskDownloadData.clsOrderInfo();
        public clsTaskDownloadData.clsOrderInfo orderInfoViewModel
        {
            get => _orderInfoViewModel;
            internal set
            {
                if (value.ActionName == ACTION_TYPE.Carry && _RunTaskData.Action_Type != ACTION_TYPE.None)//搬運任務但是當下的任務不是在移動
                {
                    _orderInfoViewModel = new clsTaskDownloadData.clsOrderInfo
                    {
                        DestineName = value.DestineName,
                        SourceName = value.SourceName,
                        ActionName = _RunTaskData.Action_Type,
                        IsTransferTask = true
                    };
                }
                else
                {

                    _orderInfoViewModel = value;
                    _orderInfoViewModel.IsTransferTask = false;
                }
            }
        }

        /// <summary>
        /// 上報任務狀態
        /// </summary>
        /// <param name="status"></param>
        /// <param name="delay">延遲毫秒數</param>
        /// <returns></returns>
        internal async Task FeedbackTaskStatus(TASK_RUN_STATUS status, int delay = 1000, AlarmCodes alarm_tracking = AlarmCodes.None, bool IsTaskCancel = false)
        {
            bool _isActionFinishFeedback = status == TASK_RUN_STATUS.ACTION_FINISH;

            if (_isActionFinishFeedback && Sub_Status == SUB_STATUS.IDLE)
                orderInfoViewModel.ActionName = ACTION_TYPE.NoAction;
            int currentPosIndexInTrajectory = GetCurrentTagIndexOfTrajectory();

            try
            {
                bool needReOnline = false;
                if ((!AGVS.IsConnected() || AGVS.IsGetOnlineModeTrying) && (Debugger.IsAttached ? true : !_RunTaskData.IsLocalTask))
                {
                    if (!_isActionFinishFeedback)
                    {
                        LOG.ERROR($"AGVs {(AGVS.IsGetOnlineModeTrying ? "Trying Get OnlineMode Now" : "disconnected")}, Task Status-{status} Feedback Bypass");
                        return;
                    }
                    else
                    {
                        LOG.ERROR($"Task Status-{status} need waiting AGVs connection restored..");
                        while (!AGVS.IsConnected())
                        {
                            await Task.Delay(10);
                        }
                        LOG.INFO($"Connection of AGVs is restored now !! . Task Status-{status} will reported out ");
                        needReOnline = true;
                    }
                }

                IsActionFinishTaskFeedbackExecuting = _isActionFinishFeedback;
                if (_isActionFinishFeedback && !IsTaskCancel)
                {
                    try
                    {
                        IsWaitForkNextSegmentTask = !AGVSResetCmdFlag && _RunTaskData.IsSegmentTask;
                    }
                    catch (Exception ex)
                    {
                        IsWaitForkNextSegmentTask = false;
                        LOG.WARN($"[FeedbackTaskStatus] Exception Occur when determine 'IsWaitForkNextSegmentTask' value. Forcing set as FALSE{ex.Message}");
                    }

                    if (_RunTaskData.IsActionFinishReported && !AGVSResetCmdFlag)
                    {
                        IsActionFinishTaskFeedbackExecuting = false;
                        return;
                    }
                    else
                        _RunTaskData.IsActionFinishReported = true;
                }
                taskfeedbackCanceTokenSoruce = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                await Task.Delay(alarm_tracking == AlarmCodes.None && _isActionFinishFeedback ? delay : 10);
                CurrentTaskRunStatus = status;
                if ((Debugger.IsAttached ? true : !_RunTaskData.IsLocalTask))
                {
                    double X = Math.Round(Navigation.Data.robotPose.pose.position.x, 3);
                    double Y = Math.Round(Navigation.Data.robotPose.pose.position.y, 3);
                    double Theta = Math.Round(Navigation.Angle, 3);
                    clsCoordination coordination = new clsCoordination(X, Y, Theta);
                    if (alarm_tracking != AlarmCodes.None)
                    {
                        await WaitAlarmCodeReported(alarm_tracking);
                    }
                    await AGVS.TryTaskFeedBackAsync(_RunTaskData, currentPosIndexInTrajectory, status, Navigation.LastVisitedTag, coordination, IsTaskCancel, taskfeedbackCanceTokenSoruce);
                }
                if (_isActionFinishFeedback)
                {
                    CurrentTaskRunStatus = TASK_RUN_STATUS.WAIT;
                    if (ExecutingTaskModel != null)
                    {
                        ExecutingTaskModel?.Abort();
                        ExecutingTaskModel?.Dispose();
                        ExecutingTaskModel = null;
                    }
                    if (needReOnline || (!_RunTaskData.IsLocalTask && RemoteModeSettingWhenAGVsDisconnect == REMOTE_MODE.ONLINE))
                    {
                        //到這AGVs連線已恢復
                        _ = Task.Factory.StartNew(async () =>
                        {
                            await Task.Delay(1000);
                            while (Sub_Status != SUB_STATUS.IDLE)
                                await Task.Delay(1000);
                            LOG.WARN($"[{Sub_Status}] Raise ONLINE Request . Because Action_Finish_Feedback is proccessed before.");
                            bool OnlineSuccess = HandleRemoteModeChangeReq(REMOTE_MODE.ONLINE, false);
                            AutoOnlineRaising = false;

                        });
                        AutoOnlineRaising = true;
                    }
                }
                IsActionFinishTaskFeedbackExecuting = false;
            }
            catch (Exception ex)
            {
                LOG.Critical(ex);
            }
            IsActionFinishTaskFeedbackExecuting = false;
        }

        private async Task WaitAlarmCodeReported(AlarmCodes alarm_tracking)
        {
            LOG.WARN($"Before TaskFeedback, AlarmCodes({alarm_tracking}) reported tracking ");
            bool alarm_reported()
            {
                if (AGVS.UseWebAPI)
                    return AGVS.previousRunningStatusReport_via_WEBAPI.Alarm_Code.Any(al => al.Alarm_ID == (int)alarm_tracking);
                else
                    return AGVS.previousRunningStatusReport_via_TCPIP.Alarm_Code.Any(al => al.Alarm_ID == (int)alarm_tracking);
            }
            CancellationTokenSource cancel_wait = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            while (!alarm_reported())
            {
                await Task.Delay(1);
                if (cancel_wait.IsCancellationRequested)
                {
                    LOG.TRACE($"AlarmCodes({alarm_tracking}) not_reported ,, timeout(10 sec) ");
                    return;
                }
            }
            LOG.WARN($"AlarmCodes({alarm_tracking}) reported ! ");

        }

        internal int GetCurrentTagIndexOfTrajectory()
        {
            try
            {
                clsMapPoint? currentPt = _RunTaskData.ExecutingTrajecory.FirstOrDefault(pt => pt.Point_ID == Navigation.LastVisitedTag);
                if (currentPt == null)
                {
                    LOG.ERROR("計算目前點位在移動路徑中的INDEX過程發生錯誤 !");
                    return -1;
                }
                else
                {
                    return _RunTaskData.ExecutingTrajecory.ToList().IndexOf(currentPt);
                }
            }
            catch (Exception ex)
            {
                LOG.ERROR("GetCurrentTagIndexOfTrajectory exception occur !", ex);
                throw new NullReferenceException();
            }

        }

    }
}
