using AGVSystemCommonNet6;
using AGVSystemCommonNet6.AGVDispatch.Messages;
using AGVSystemCommonNet6.AGVDispatch.Model;
using AGVSystemCommonNet6.Alarm;
using AGVSystemCommonNet6.Log;
using AGVSystemCommonNet6.Vehicle_Control.Models;
using AGVSystemCommonNet6.Vehicle_Control.VCS_ALARM;
using AGVSystemCommonNet6.Vehicle_Control.VCSDatabase;
using GPMVehicleControlSystem.Models.Buzzer;
using GPMVehicleControlSystem.Models.VehicleControl.TaskExecute;
using GPMVehicleControlSystem.Models.VehicleControl.VehicleComponent;
using GPMVehicleControlSystem.VehicleControl.DIOModule;
using MathNet.Numerics;
using RosSharp.RosBridgeClient.Actionlib;
using System.Diagnostics;
using System.Reflection;
using System.Security.Claims;
using static AGVSystemCommonNet6.clsEnums;
using static GPMVehicleControlSystem.Models.VehicleControl.AGVControl.CarController;
using static GPMVehicleControlSystem.Models.VehicleControl.VehicleComponent.clsLaser;
using static GPMVehicleControlSystem.Models.VehicleControl.Vehicles.Vehicle;
using static GPMVehicleControlSystem.VehicleControl.DIOModule.clsDOModule;

namespace GPMVehicleControlSystem.Models.VehicleControl.Vehicles
{
    public partial class Vehicle
    {
        private string TaskName = "";
        public TASK_RUN_STATUS CurrentTaskRunStatus = TASK_RUN_STATUS.NO_MISSION;
        internal bool AutoOnlineRaising = false;
        internal clsParkingAccuracy lastParkingAccuracy;
        private bool _IsCargoBiasDetecting = false;
        internal bool IsCargoBiasDetecting
        {
            get => _IsCargoBiasDetecting;
            set
            {
                if (_IsCargoBiasDetecting != value)
                {
                    _IsCargoBiasDetecting = value;
                    if (!_IsCargoBiasDetecting)
                        logger.LogWarning($"貨物傾倒偵測結束-AGV Move Finish");
                    else
                        logger.LogWarning($"貨物傾倒偵測開始");
                }
            }
        }
        internal bool IsCargoBiasTrigger = false;

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
        public TaskBase ExecutingTaskEntity
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

        public enum TASK_DISPATCH_STATUS
        {
            IDLE,
            Pending,
            Running,
        }

        public TASK_DISPATCH_STATUS TaskDispatchStatus = TASK_DISPATCH_STATUS.IDLE;

        /// <summary>
        /// 執行派車系統任務
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="taskDownloadData"></param>
        internal async Task ExecuteAGVSTask(clsTaskDownloadData taskDownloadData)
        {
            ACTION_TYPE action = taskDownloadData.Action_Type;
            try
            {
                await TaskDispatchFlowControlSemaphoreSlim.WaitAsync();
                TaskDispatchStatus = TASK_DISPATCH_STATUS.Pending;
                logger.LogTrace($"Start Execute Task-{taskDownloadData.Task_Simplex}");
                // logger.LogWarning($"Recieve AGVs Task and Prepare to Excute!- NO [ACTION_FINISH] Feedback TaskStatus Process is Running!");
                _RunTaskData = taskDownloadData.Clone();
                _RunTaskData.IsEQHandshake = false;
                _RunTaskData.IsActionFinishReported = false;
                _RunTaskData.VibrationRecords = new List<clsVibrationRecord>();

                AlarmManager.ClearAlarm();
                await Task.Delay(10);
                if (AGV_Reset_Flag)
                    return;
                await Laser.AllLaserActive();
                IsWaitForkNextSegmentTask = false;
                ExecutingTaskEntity = CreateTaskBasedOnDownloadedData(taskDownloadData);
                if (ExecutingTaskEntity == null)
                {
                    throw new NullReferenceException("ExecutingTaskEntity is null(CreateTaskBasedOnDownloadedData return.)");
                }
                ExecutingTaskEntity.ForkLifter = ForkLifter;
                orderInfoViewModel = taskDownloadData.OrderInfo;
                Console.WriteLine(orderInfoViewModel.ToJson());
            }
            catch (Exception ex)
            {
                logger.LogError(ex, ex.Message);
                SoftwareEMO(AlarmCodes.Code_Error_In_System);
                return;
            }
            finally
            {
                TaskDispatchFlowControlSemaphoreSlim.Release();
            }
            await Task.Run(async () =>
            {
                if (AGV_Reset_Flag)
                    return;
                //LDULDRecord
                IsLaserRecoveryHandled = false;
                bool _isNeedHandshaking = ExecutingTaskEntity.IsNeedHandshake;
                _RunTaskData.IsEQHandshake = _isNeedHandshaking;
                AGV_Reset_Flag = AGVSResetCmdFlag = false;
                TaskDispatchStatus = TASK_DISPATCH_STATUS.Running;
                List<AlarmCodes> alarmCodes = (await ExecutingTaskEntity.Execute()).FindAll(al => al != AlarmCodes.None);
                logger.LogTrace($"Execute Task Done-{ExecutingTaskEntity?.RunningTaskData.Task_Simplex}");
                if (alarmCodes.Any(al => al == AlarmCodes.Replan))
                {
                    logger.LogWarning("Replan.");
                    return;
                }
                if (alarmCodes.Any(al => al == AlarmCodes.Send_Goal_to_AGV_But_AGVS_Cancel_Req_Raised))
                {
                    logger.LogWarning("AGVS Cancel Request Raised and AGV is executing cycle stop action");
                    return;
                }

                IEnumerable<AlarmCodes> _current_alarm_codes = new List<AlarmCodes>();
                bool IsAlarmHappedWhenTaskExecuting = alarmCodes.Count != 0;
                bool IsAGVNowIsDown = GetSub_Status() == SUB_STATUS.DOWN;
                bool IsHandShakeFailByEQPIOStatusErrorBeforeAGVBusy = alarmCodes.Contains(AlarmCodes.Precheck_IO_EQ_PIO_State_Not_Reset) ||
                                                                      alarmCodes.Contains(AlarmCodes.Precheck_IO_Fail_EQ_L_REQ) ||
                                                                      alarmCodes.Contains(AlarmCodes.Precheck_IO_Fail_EQ_U_REQ) ||
                                                                      alarmCodes.Contains(AlarmCodes.Precheck_IO_Fail_EQ_READY) ||
                                                                      alarmCodes.Contains(AlarmCodes.Handshake_Fail_EQ_READY_UP) ||
                                                                      alarmCodes.Contains(AlarmCodes.Handshake_Fail_EQ_READY_LOW) ||
                                                                      alarmCodes.Contains(AlarmCodes.Precheck_IO_Fail_EQ_GO) ||
                                                                      alarmCodes.Contains(AlarmCodes.Handshake_Timeout_TA1_EQ_U_REQ_Not_On) ||
                                                                      alarmCodes.Contains(AlarmCodes.Handshake_Timeout_TA1_EQ_L_REQ_Not_On) ||
                                                                      alarmCodes.Contains(AlarmCodes.Handshake_Timeout_TA2_EQ_READY_Not_On);
                if (IsAlarmHappedWhenTaskExecuting || IsAGVNowIsDown)
                {
                    AGVC.EmergencyStop();
                    _current_alarm_codes = AlarmManager.CurrentAlarms.Values.Where(al => !al.IsRecoverable).Select(al => al.EAlarmCode);
                    if (alarmCodes.Contains(AlarmCodes.AGV_State_Cant_do_this_Action))
                    {
                        alarmCodes.RemoveAll(al => al == AlarmCodes.AGV_State_Cant_do_this_Action);
                        await Task.Delay(200);
                    }

                    if (_isNeedHandshaking && IsAGVAbnormal_when_handshaking && !alarmCodes.Contains(AlarmCodes.Handshake_Fail_AGV_DOWN))
                    {
                        alarmCodes.Add(AlarmCodes.Handshake_Fail_AGV_DOWN);
                    }
                    try
                    {
                        await Task.Delay(1000, LaserObsMonitorCancel.Token); //因為有可能雷射還在偵測中，導致後續狀態會被改成 RUN 或 WARNING或 ALARM,因此等待一段時間 
                    }
                    catch (TaskCanceledException ex)
                    {
                        logger.LogTrace($"Laser OBS Monitor Process end. |{ex.Message}");
                    }
                    finally
                    {
                        SetSub_Status(SUB_STATUS.DOWN);
                    }
                    alarmCodes.ForEach(alarm =>
                    {
                        AlarmManager.AddAlarm(alarm, false);
                    });
                    _current_alarm_codes = AlarmManager.CurrentAlarms.Values.Where(al => !al.IsRecoverable).Select(al => al.EAlarmCode);
                    logger.LogError($"{action} 任務失敗:Alarm:{string.Join(",", _current_alarm_codes)}");

                    TaskDispatchStatus = TASK_DISPATCH_STATUS.IDLE;
                }
                else
                {
                    AlarmManager.ClearAlarm();
                    try
                    {
                        await Task.Delay(1000, LaserObsMonitorCancel.Token); //因為有可能雷射還在偵測中，導致後續狀態會被改成 RUN 或 WARNING或 ALARM,因此等待一段時間 
                    }
                    catch (TaskCanceledException ex)
                    {
                        logger.LogTrace($"Laser OBS Monitor Process end. |{ex.Message}");
                    }
                    finally
                    {
                        SetSub_Status(action == ACTION_TYPE.Charge ? SUB_STATUS.Charging : SUB_STATUS.IDLE);
                    }
                }
                AGVC.OnAGVCActionChanged = null;

                FeedbackTaskStatus(TASK_RUN_STATUS.ACTION_FINISH, alarms_tracking: IsAlarmHappedWhenTaskExecuting ? _current_alarm_codes?.ToList() : null);

                if (IsHandShakeFailByEQPIOStatusErrorBeforeAGVBusy && !_RunTaskData.IsLocalTask)
                {
                    //自動復歸並上線
                    _ = Task.Run(async () =>
                    {
                        await Task.Delay(2000);
                        await AutoInitializeAndOnline();
                    });
                }
            });


        }

        /// <summary>
        /// 嘗試進行初始化並上線
        /// </summary>
        /// <returns></returns>
        protected async Task AutoInitializeAndOnline()
        {
            (bool confirm, string message) = await Initialize();
            if (confirm)
            {
                logger.LogInformation($"嘗試自動初始化完成");
                (bool success, RETURN_CODE return_code) = await Online_Mode_Switch(REMOTE_MODE.ONLINE);
                if (success)
                    logger.LogInformation($"嘗試自動初始化完成且上線成功");
                else
                    logger.LogError($"嘗試自動初始化完成但上線失敗({return_code})");
            }
            else
            {
                logger.LogError($"嘗試自動初始化但失敗({message})");
            }
        }

        private async Task EndLaserObsMonitorAsync()
        {
            Laser.ModeSwitch(LASER_MODE.Bypass, true);
            await Task.Delay(100);
            EndLaserObstacleMonitor();
            var currentStatus = GetSub_Status();
            if (currentStatus == SUB_STATUS.IDLE || currentStatus == SUB_STATUS.Charging)
                BuzzerPlayer.Stop();
        }

        private TaskBase CreateTaskBasedOnDownloadedData(clsTaskDownloadData taskDownloadData)
        {
            TaskBase? _ExecutingTaskEntity = null;
            var action = taskDownloadData.Action_Type;
            if (action == ACTION_TYPE.None)
            {
                _ExecutingTaskEntity = new NormalMoveTask(this, taskDownloadData);
            }
            else
            {
                if (taskDownloadData.CST.Length == 0 && Remote_Mode == REMOTE_MODE.OFFLINE)
                    taskDownloadData.CST = new clsCST[1] { new clsCST { CST_ID = $"TAEMU{DateTime.Now.ToString("mmssfff")}" } };
                if (action == ACTION_TYPE.Charge)
                    _ExecutingTaskEntity = new ChargeTask(this, taskDownloadData);
                else if (action == ACTION_TYPE.Discharge)
                    _ExecutingTaskEntity = new DischargeTask(this, taskDownloadData);
                else if (action == ACTION_TYPE.Load)
                {
                    _ExecutingTaskEntity = new LoadTask(this, taskDownloadData);
                    (_ExecutingTaskEntity as LoadTask).lduld_record.TaskName = _RunTaskData.Task_Name;

                    if (_RunTaskData.CST.Length > 0)
                    {
                        (_ExecutingTaskEntity as LoadTask).lduld_record.CargoID_FromAGVS = _RunTaskData.CST.First().CST_ID;
                    }

                }
                else if (action == ACTION_TYPE.Unload)
                {
                    _ExecutingTaskEntity = new UnloadTask(this, taskDownloadData);
                    (_ExecutingTaskEntity as UnloadTask).lduld_record.TaskName = _RunTaskData.Task_Name;

                    if (_RunTaskData.CST.Length > 0)
                    {
                        (_ExecutingTaskEntity as UnloadTask).lduld_record.CargoID_FromAGVS = _RunTaskData.CST.First().CST_ID;
                    }
                }
                else if (action == ACTION_TYPE.Park)
                    _ExecutingTaskEntity = new ParkTask(this, taskDownloadData);
                else if (action == ACTION_TYPE.Unpark)
                    _ExecutingTaskEntity = new UnParkTask(this, taskDownloadData);
                else if (action == ACTION_TYPE.Measure)
                    _ExecutingTaskEntity = new MeasureTask(this, taskDownloadData);
                else if (action == ACTION_TYPE.ExchangeBattery)
                    _ExecutingTaskEntity = new ExchangeBatteryTask(this, taskDownloadData);
                else
                {
                    _ExecutingTaskEntity = null;
                }
            }
            return _ExecutingTaskEntity;
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
                            logger.LogWarning($"Cannot Download Transfer Infomation From CIM");
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
                        logger.LogInformation($"Download TransferTask= {orderInfoViewModel.ToJson()}");

                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, ex.Message);
                }
            });
        }

        private void WriteTaskNameToFile(string task_Name)
        {
            TaskName = task_Name;
            try
            {
                File.WriteAllText("task_name.txt", task_Name);
                logger.LogTrace($"任務ID站存寫入檔案成功({task_Name})");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, $"任務ID站存寫入檔案失敗.");
                Task.Run(() => WriteTaskNameToFile(task_Name));
            }
        }

        private void ReadTaskNameFromFile()
        {
            if (File.Exists("task_name.txt"))
                TaskName = File.ReadAllText("task_name.txt");
        }

        internal virtual void HandleLastVisitedTagChanged(object? sender, int newVisitedNodeTag)
        {
            Task.Run(async () =>
            {
                UpdateLastVisitedTagOfParam(newVisitedNodeTag);

                if (Operation_Mode == OPERATOR_MODE.MANUAL)
                    return;
                if (ExecutingTaskEntity == null)
                    return;


                var _newTagPoint = _RunTaskData.ExecutingTrajecory.FirstOrDefault(pt => pt.Point_ID == newVisitedNodeTag);


                if (_RunTaskData.Action_Type == ACTION_TYPE.None && _newTagPoint != null)
                {

                    if (AGVC.ActionStatus == RosSharp.RosBridgeClient.Actionlib.ActionStatus.ACTIVE)
                    {
                        TryControlAutoDoor(newVisitedNodeTag);
                    }

                    var laser_mode = _newTagPoint.Laser;
                    await Laser.ModeSwitch(laser_mode, true);
                }

                if (_RunTaskData.TagsOfTrajectory.Last() != Navigation.LastVisitedTag)
                {
                    FeedbackTaskStatus(TASK_RUN_STATUS.NAVIGATING);
                }
            });
        }

        internal void TryControlAutoDoor(int newVisitedNodeTag)
        {
            if (ExecutingTaskEntity == null)
                return;

            clsMapPoint[] trajectory = ExecutingTaskEntity.RunningTaskData.ExecutingTrajecory;

            List<int> autoDoorStationTags = NavingMap.Points.Values.Where(pt => pt.StationType == AGVSystemCommonNet6.MAP.MapPoint.STATION_TYPE.Auto_Door)
                                                            .Select(pt => pt.TagNumber)
                                                            .ToList();

            logger.LogInformation($"Try Control Auto Door in Tag {newVisitedNodeTag}(Full Traj: {string.Join(",", trajectory.Select(pt => pt.Point_ID))})");
            //剩餘路徑包含自動門 則將 IO ON著 反之 OFF
            clsMapPoint? currentPt = trajectory.FirstOrDefault(pt => pt.Point_ID == newVisitedNodeTag);
            if (currentPt == null)
            {
                CloseAutoDoor();
                return;
            }
            //0,1,2,3
            int indexOfCurrentPt = trajectory.ToList().IndexOf(currentPt);

            if (indexOfCurrentPt + 1 == trajectory.Length) //最後一點
            {
                CloseAutoDoor();
                return;
            }

            IEnumerable<clsMapPoint> autoDoorPoints = trajectory.Skip(indexOfCurrentPt).Where(pt => _IsAutoDoor(pt));
            IEnumerable<int> autoDoorTags = autoDoorPoints.Select(pt => pt.Point_ID);
            logger.LogTrace($"Auto Door Points in remain trajectory? {string.Join(",", autoDoorTags)}");
            bool _isAutoDoorInRemainTraj = autoDoorPoints.Count() >= 2 && autoDoorPoints.Any(pt => autoDoorStationTags.Contains(pt.Point_ID));
            if (_isAutoDoorInRemainTraj)
            {
                int index_of_first_auto_door_pt = trajectory.ToList().FindIndex(pt => pt.Point_ID == autoDoorPoints.First().Point_ID);
                if (indexOfCurrentPt == index_of_first_auto_door_pt || indexOfCurrentPt == index_of_first_auto_door_pt - 1)
                    OpenAutoDoor();
            }
            else
                CloseAutoDoor();

            bool _IsAutoDoor(clsMapPoint traj_point)
            {
                KeyValuePair<int, AGVSystemCommonNet6.MAP.MapPoint> mapPt = NavingMap.Points.FirstOrDefault(p => p.Value.TagNumber == traj_point.Point_ID);
                if (mapPt.Value == null)
                    return false;

                return mapPt.Value.IsAutoDoor;
            }
        }

        protected virtual bool GetAutoDoorOpenControl()
        {
            return WagoDO.GetState(DO_ITEM.Infrared_Door_1);
        }

        protected virtual async Task OpenAutoDoor()
        {
            logger.LogInformation($"Open Auto Door OUPUT ON(Tag:{lastVisitedMapPoint.TagNumber})");
            bool success = await WagoDO.SetState(DO_ITEM.Infrared_Door_1, true);
            if (!success)
            {
                AlarmManager.AddWarning(AlarmCodes.Auto_Door_Ouput_Not_Defined);
            }
        }
        protected virtual async Task CloseAutoDoor()
        {
            logger.LogInformation($"Open Auto Door OUPUT OFF(Tag:{lastVisitedMapPoint.TagNumber})");
            bool success = await WagoDO.SetState(DO_ITEM.Infrared_Door_1, false);
            //if (!success)
            //    AlarmManager.AddWarning(AlarmCodes.Auto_Door_Ouput_Not_Defined);
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
                logger.LogError(ex, ex.Message);
                AlarmManager.AddWarning(AlarmCodes.Measure_Result_Data_Report_Fail);
            }
        }
        private bool IsActionFinishTaskFeedbackExecuting = false;
        private CancellationTokenSource taskfeedbackCanceTokenSoruce = new CancellationTokenSource();
        public clsTaskDownloadData.clsOrderInfo orderInfoViewModel { get; set; } = new clsTaskDownloadData.clsOrderInfo();

        /// <summary>
        /// 上報任務狀態
        /// </summary>
        /// <param name="status"></param>
        /// <param name="delay">延遲毫秒數</param>
        /// <returns></returns>
        internal async Task FeedbackTaskStatus(TASK_RUN_STATUS status, List<AlarmCodes> alarms_tracking = null, bool IsTaskCancel = false)
        {
            logger.LogWarning($"{_RunTaskData.Task_Name}-開始向派車上報任務狀態 ({status})");

            if (status == TASK_RUN_STATUS.ACTION_FINISH)
                orderInfoViewModel.ActionName = ACTION_TYPE.NoAction;

            if (_RunTaskData.IsLocalTask)
            {
                _RunTaskData.IsActionFinishReported = status == TASK_RUN_STATUS.ACTION_FINISH;
                logger.LogWarning($"{_RunTaskData.Task_Name}-本地任務不需要向派車系統回報任務狀態!({status})");
                return;
            }


            logger.LogWarning($"嘗試向派車系統上報任務狀態(狀態=>{status},是否因為派車系統取消任務回報=>{IsTaskCancel},異常碼追蹤=>{(alarms_tracking == null ? "" : string.Join(",", alarms_tracking))})");
            var _task_namae = _RunTaskData.Task_Name;
            var _task_simplex = _RunTaskData.Task_Simplex;
            var _task_sequence = _RunTaskData.Task_Sequence;
            int currentPosIndexInTrajectory = GetCurrentTagIndexOfTrajectory();

            try
            {
                bool needReOnline = false;
                if ((!AGVS.IsConnected() || AGVS.IsGetOnlineModeTrying))
                {
                    needReOnline = status == TASK_RUN_STATUS.ACTION_FINISH;
                }

                IsActionFinishTaskFeedbackExecuting = status == TASK_RUN_STATUS.ACTION_FINISH;
                if (status == TASK_RUN_STATUS.ACTION_FINISH && !IsTaskCancel)
                {
                    try
                    {
                        IsWaitForkNextSegmentTask = !AGVSResetCmdFlag && _RunTaskData.IsSegmentTask;
                    }
                    catch (Exception ex)
                    {
                        IsWaitForkNextSegmentTask = false;
                        logger.LogWarning($"[FeedbackTaskStatus] Exception Occur when determine 'IsWaitForkNextSegmentTask' value. Forcing set as FALSE{ex.Message}");
                    }

                }
                taskfeedbackCanceTokenSoruce?.Cancel(); //Raise取消請求,若前一次回報請求還沒完成則會取消回報
                taskfeedbackCanceTokenSoruce = new CancellationTokenSource();
                await Task.Delay(10);
                CurrentTaskRunStatus = status;
                if (alarms_tracking != null)
                {
                    await WaitAlarmCodeReported(alarms_tracking);
                }
                bool feedback_success = await AGVS.TryTaskFeedBackAsync(_task_namae, _task_simplex, _task_sequence, currentPosIndexInTrajectory, status, Navigation.LastVisitedTag, Navigation.CurrentCoordination, taskfeedbackCanceTokenSoruce.Token, IsTaskCancel);

                if (status == TASK_RUN_STATUS.ACTION_FINISH)
                {
                    _RunTaskData.IsActionFinishReported = feedback_success;
                    CurrentTaskRunStatus = TASK_RUN_STATUS.WAIT;
                    if (ExecutingTaskEntity != null)
                    {
                        ExecutingTaskEntity?.Abort();
                        ExecutingTaskEntity?.Dispose();
                        ExecutingTaskEntity = null;
                    }
                    if (needReOnline || (!_RunTaskData.IsLocalTask && RemoteModeSettingWhenAGVsDisconnect == REMOTE_MODE.ONLINE))
                    {
                        //到這AGVs連線已恢復
                        _ = Task.Factory.StartNew(async () =>
                        {
                            await Task.Delay(1000);
                            while (GetSub_Status() != SUB_STATUS.IDLE)
                                await Task.Delay(1000);
                            logger.LogWarning($"[{GetSub_Status()}] Raise ONLINE Request . Because Action_Finish_Feedback is proccessed before.");
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
                logger.LogError(ex, ex.Message);
            }
            IsActionFinishTaskFeedbackExecuting = false;
        }

        private async Task WaitAlarmCodeReported(List<AlarmCodes> alarms_tracking)
        {
            string _alarm_codes_str = string.Join(",", alarms_tracking);
            logger.LogWarning($"Before TaskFeedback, AlarmCodes({_alarm_codes_str}) reported tracking ");
            bool alarm_reported()
            {
                List<int> alarm_codes_reported = new List<int>();
                if (AGVS.UseWebAPI)
                    alarm_codes_reported = AGVS.previousRunningStatusReport_via_WEBAPI.Alarm_Code.Select(al => al.Alarm_ID).ToList();
                else
                    alarm_codes_reported = AGVS.previousRunningStatusReport_via_TCPIP.Alarm_Code.Select(al => al.Alarm_ID).ToList();
                return alarms_tracking.All(alarm => alarm_codes_reported.Contains((int)alarm));
            }

            CancellationTokenSource cancel_wait = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            while (!alarm_reported())
            {
                await Task.Delay(1);
                if (cancel_wait.IsCancellationRequested)
                {
                    logger.LogTrace($"AlarmCodes({alarms_tracking}) not_reported ,, timeout(10 sec) ");
                    return;
                }
            }
            logger.LogWarning($"All AlarmCodes ({_alarm_codes_str}) are reported to AGVS ! ");

        }

        internal int GetCurrentTagIndexOfTrajectory()
        {
            try
            {
                clsMapPoint? currentPt = _RunTaskData.ExecutingTrajecory.FirstOrDefault(pt => pt.Point_ID == Navigation.LastVisitedTag);
                if (currentPt == null)
                {
                    logger.LogError("計算目前點位在移動路徑中的INDEX過程發生錯誤 !");
                    return -1;
                }
                else
                {
                    return _RunTaskData.ExecutingTrajecory.ToList().IndexOf(currentPt);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "GetCurrentTagIndexOfTrajectory exception occur !");
                throw new NullReferenceException();
            }

        }
        private CancellationTokenSource LaserObsMonitorCancel = new CancellationTokenSource();
        private SemaphoreSlim _StartLaserMonitorSemaphore = new SemaphoreSlim(1, 1);
        public bool IsLaserMonitoring { get; private set; } = false;

        /// <summary>
        /// 結束雷射障礙物監控
        /// </summary>
        public void EndLaserObstacleMonitor()
        {
            IsLaserMonitoring = false;
        }
        /// <summary>
        /// 雷射障礙物監控
        /// </summary>
        public async void StartLaserObstacleMonitor()
        {
            try
            {
                await _StartLaserMonitorSemaphore.WaitAsync();

                if (IsLaserMonitoring)
                    return;

                ROBOT_CONTROL_CMD _CurrentRobotControlCmd = ROBOT_CONTROL_CMD.SPEED_Reconvery;
                AlarmCodes[] _CurrentAlarmCodeCollection = new AlarmCodes[0];
                LaserObsMonitorCancel = new CancellationTokenSource();
                SemaphoreSlim _SpeedRecoveryHandleSemaphoreSlim = new SemaphoreSlim(1, 1);
                await Task.Run(async () =>
                {
                    IsLaserMonitoring = true;
                    while (!CheckMonitorCancel() && !CheckAGVCActionDone())
                    {
                        try
                        {
                            if (CheckMonitorCancel() || CheckAGVCActionDone())
                                return;

                            var cmdGet = GetSpeedControlCmdByLaserState(out AlarmCodes[] alarmCodeCollection);
                            if (_CurrentRobotControlCmd != cmdGet || (alarmCodeCollection.Length != 0 && !_CurrentAlarmCodeCollection.SequenceEqual(alarmCodeCollection)))
                            {
                                if (CheckMonitorCancel() || CheckAGVCActionDone())
                                    return;

                                await Task.Delay(10, LaserObsMonitorCancel.Token);
                                if (cmdGet == ROBOT_CONTROL_CMD.SPEED_Reconvery || cmdGet == ROBOT_CONTROL_CMD.DECELERATE)
                                {
                                    SetSub_Status(cmdGet == ROBOT_CONTROL_CMD.SPEED_Reconvery ? SUB_STATUS.RUN : SUB_STATUS.WARNING);
                                    if (_RunTaskData.Action_Type == ACTION_TYPE.None)
                                        BuzzerPlayer.Move();
                                    else if (_RunTaskData.Action_Type == ACTION_TYPE.Charge)
                                        BuzzerPlayer.Play(SOUNDS.GoToChargeStation);
                                    else
                                        BuzzerPlayer.Action();
                                }
                                else
                                {
                                    SetSub_Status(SUB_STATUS.ALARM);
                                    BuzzerPlayer.Alarm();
                                }

                                //已無異常清空所有雷射異常
                                if (!alarmCodeCollection.Any())
                                    AlarmManager.ClearAlarm(_CurrentAlarmCodeCollection);
                                else
                                {
                                    HandleLaserAlarmCodes(_CurrentAlarmCodeCollection, alarmCodeCollection);
                                }
                                if (cmdGet == ROBOT_CONTROL_CMD.SPEED_Reconvery)
                                {
                                    DecreaseSpeedAndRecovery();
                                }
                                else
                                    await AGVC.CarSpeedControl(cmdGet);


                                _CurrentRobotControlCmd = cmdGet;
                                _CurrentAlarmCodeCollection = alarmCodeCollection;
                            }
                            await Task.Delay(10, LaserObsMonitorCancel.Token);

                            //減速後恢復
                            async Task DecreaseSpeedAndRecovery()
                            {
                                try
                                {
                                    await _SpeedRecoveryHandleSemaphoreSlim.WaitAsync();
                                    await AGVC.CarSpeedControl(ROBOT_CONTROL_CMD.DECELERATE);
                                    Stopwatch timer = Stopwatch.StartNew();
                                    while (timer.Elapsed.Seconds < 1)
                                    {
                                        await Task.Delay(1);
                                        if (_CurrentRobotControlCmd != ROBOT_CONTROL_CMD.SPEED_Reconvery)
                                            return;
                                    }

                                    await AGVC.CarSpeedControl(ROBOT_CONTROL_CMD.SPEED_Reconvery);
                                }
                                catch (Exception ex)
                                {
                                    logger.LogError(ex, ex.Message);
                                }
                                finally
                                {
                                    _SpeedRecoveryHandleSemaphoreSlim.Release();
                                }

                            }

                        }
                        catch (TaskCanceledException ex)
                        {
                            logger.LogInformation("雷射偵測流程已取消");
                            return;
                        }
                    }

                    bool CheckMonitorCancel()
                    {
                        return LaserObsMonitorCancel.IsCancellationRequested || !IsLaserMonitoring;
                    }

                    bool CheckAGVCActionDone()
                    {
                        return AGVC.ActionStatus == ActionStatus.SUCCEEDED;
                    }
                });

            }
            catch (Exception ex)
            {
                logger.LogError(ex, ex.Message);
            }
            finally
            {
                _StartLaserMonitorSemaphore.Release();
                LaserObsMonitorCancel.Cancel();
            }
        }

        private static void HandleLaserAlarmCodes(AlarmCodes[] _CurrentAlarmCodeCollection, AlarmCodes[] alarmCodeCollection)
        {
            //把已不存在的異常清除，並添加新的異常
            //Find not exist alarm
            var alarmsNotExist = _CurrentAlarmCodeCollection.Where(alarm => !alarmCodeCollection.Contains(alarm));
            if (alarmsNotExist.Any())
                AlarmManager.ClearAlarm(alarmsNotExist.ToArray());
            //Find new alarm
            var alarmsNewCreated = alarmCodeCollection.Where(alarm => !_CurrentAlarmCodeCollection.Contains(alarm));

            if (alarmsNewCreated.Any())
            {
                bool isAlarm(AlarmCodes alarm)
                {
                    return alarm == AlarmCodes.RightProtection_Area3 || alarm == AlarmCodes.LeftProtection_Area3 || alarm == AlarmCodes.BackProtection_Area3 || alarm == AlarmCodes.FrontProtection_Area3;
                }
                var newAlarms = alarmsNewCreated.Where(alarm => isAlarm(alarm));
                AlarmManager.AddWarning(newAlarms);
                var newWarnings = alarmsNewCreated.Where(alarm => !isAlarm(alarm));
                AlarmManager.AddWarning(newWarnings);
            }
        }

        private ROBOT_CONTROL_CMD GetSpeedControlCmdByLaserState(out AlarmCodes[] alarmCodes)
        {
            List<AlarmCodes> alarmcodesList = new List<AlarmCodes>();

            bool frontBypass = WagoDO.GetState(clsDOModule.DO_ITEM.Front_LsrBypass);
            bool backBypass = WagoDO.GetState(clsDOModule.DO_ITEM.Back_LsrBypass);
            bool rightSideBypass = WagoDO.GetState(clsDOModule.DO_ITEM.Right_LsrBypass);
            bool leftSideBypass = WagoDO.GetState(clsDOModule.DO_ITEM.Left_LsrBypass);

            bool rightSideOn = !WagoDI.GetState(clsDIModule.DI_ITEM.RightProtection_Area_Sensor_3);
            bool leftSideOn = !WagoDI.GetState(clsDIModule.DI_ITEM.LeftProtection_Area_Sensor_3);

            bool frontDecreaseOn = !WagoDI.GetState(clsDIModule.DI_ITEM.FrontProtection_Area_Sensor_1);
            bool frontStopOn = !WagoDI.GetState(clsDIModule.DI_ITEM.FrontProtection_Area_Sensor_2) || !WagoDI.GetState(clsDIModule.DI_ITEM.FrontProtection_Area_Sensor_3);
            bool backDecreaseOn = !WagoDI.GetState(clsDIModule.DI_ITEM.BackProtection_Area_Sensor_1);
            bool backStopOn = !WagoDI.GetState(clsDIModule.DI_ITEM.BackProtection_Area_Sensor_2) || !WagoDI.GetState(clsDIModule.DI_ITEM.BackProtection_Area_Sensor_3);

            bool rightNearObs = (rightSideOn && !rightSideBypass);
            bool leftNearObs = (leftSideOn && !leftSideBypass);
            bool frontNearObs = (frontStopOn && !frontBypass);
            bool backNearObs = (backStopOn && !backBypass);

            bool frontFarObs = (frontDecreaseOn && !frontBypass);
            bool backFarObs = (backDecreaseOn && !backBypass);

            if (rightNearObs || leftNearObs || frontNearObs || backNearObs)
            {

                if (rightNearObs)
                    alarmcodesList.Add(AlarmCodes.RightProtection_Area3);
                if (leftNearObs)
                    alarmcodesList.Add(AlarmCodes.LeftProtection_Area3);
                if (frontNearObs)
                    alarmcodesList.Add(AlarmCodes.FrontProtection_Area3);
                if (backNearObs)
                    alarmcodesList.Add(AlarmCodes.BackProtection_Area3);

                alarmCodes = alarmcodesList.ToArray();
                return ROBOT_CONTROL_CMD.STOP;
            }
            else if (frontFarObs || backFarObs)
            {
                if (frontFarObs)
                    alarmcodesList.Add(AlarmCodes.FrontProtection_Area2);
                if (backFarObs)
                    alarmcodesList.Add(AlarmCodes.BackProtection_Area2);
                alarmCodes = alarmcodesList.ToArray();
                return ROBOT_CONTROL_CMD.DECELERATE;
            }
            else
            {
                alarmCodes = alarmcodesList.ToArray();
                return ROBOT_CONTROL_CMD.SPEED_Reconvery;
            }
        }

    }
}
