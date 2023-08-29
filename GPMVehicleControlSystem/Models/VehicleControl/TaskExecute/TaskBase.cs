using AGVSystemCommonNet6.AGVDispatch.Messages;
using AGVSystemCommonNet6.Alarm;
using AGVSystemCommonNet6.Alarm.VMS_ALARM;
using AGVSystemCommonNet6.Log;
using AGVSystemCommonNet6.MAP;
using GPMVehicleControlSystem.Models.Buzzer;
using GPMVehicleControlSystem.Models.VehicleControl.VehicleComponent;
using GPMVehicleControlSystem.Models.VehicleControl.Vehicles;
using GPMVehicleControlSystem.Models.WorkStation;
using RosSharp.RosBridgeClient.Actionlib;
using System.Diagnostics;
using static AGVSystemCommonNet6.clsEnums;
using static GPMVehicleControlSystem.Models.VehicleControl.VehicleComponent.clsLaser;

namespace GPMVehicleControlSystem.Models.VehicleControl.TaskExecute
{
    public abstract class TaskBase : IDisposable
    {
        public Vehicle Agv { get; }
        private clsTaskDownloadData _RunningTaskData = new clsTaskDownloadData();
        public Action<string> OnTaskFinish;
        protected CancellationTokenSource TaskCancelCTS = new CancellationTokenSource();
        private bool disposedValue;

        public Action<ActionStatus>? AGVCActionStatusChaged
        {
            get => Agv.AGVC.OnAGVCActionChanged;
            set => Agv.AGVC.OnAGVCActionChanged = value;
        }
        public clsTaskDownloadData RunningTaskData
        {
            get => _RunningTaskData;
            set
            {

                if (_RunningTaskData == null | value.Task_Name != _RunningTaskData?.Task_Name)
                {
                    TrackingTags = value.TagsOfTrajectory;
                }
                else
                {
                    List<int> newTrackingTags = value.TagsOfTrajectory;

                    if (TrackingTags.First() != newTrackingTags.First())
                    {
                        //1 2 3 
                        //5
                    }
                    else
                    {
                        TrackingTags = newTrackingTags;

                    }
                }
                _RunningTaskData = value;
            }
        }
        public abstract ACTION_TYPE action { get; set; }
        public List<int> TrackingTags { get; private set; } = new List<int>();
        public clsForkLifter ForkLifter { get; internal set; }
        public int destineTag => _RunningTaskData == null ? -1 : _RunningTaskData.Destination;
        public MapPoint? lastPt => Agv.NavingMap.Points.Values.FirstOrDefault(pt => pt.TagNumber == RunningTaskData.Destination);
        protected AlarmCodes FrontendSecondarSensorTriggerAlarmCode
        {
            get
            {
                if (Agv.AgvType == AGV_TYPE.SUBMERGED_SHIELD)
                {
                    if (action == ACTION_TYPE.Load)
                        return AlarmCodes.EQP_LOAD_BUT_EQP_HAS_OBSTACLE;
                    else
                        return AlarmCodes.EQP_UNLOAD_BUT_EQP_HAS_NO_CARGO;
                }
                else
                {
                    return AlarmCodes.Fork_Frontend_has_Obstacle;
                }

            }
        }

        public TaskBase(Vehicle Agv, clsTaskDownloadData taskDownloadData)
        {
            this.Agv = Agv;
            RunningTaskData = taskDownloadData;
            LOG.INFO($"New Task : \r\nTask Name:{taskDownloadData.Task_Name}\r\n Task_Simplex:{taskDownloadData.Task_Simplex}\r\nTask_Sequence:{taskDownloadData.Task_Sequence}");

        }

        /// <summary>
        /// 執行任務
        /// </summary>
        public async Task<AlarmCodes> Execute()
        {
            try
            {

                TaskCancelCTS = new CancellationTokenSource();
                DirectionLighterSwitchBeforeTaskExecute();
                LaserSettingBeforeTaskExecute();

                (bool confirm, AlarmCodes alarm_code) checkResult = await BeforeTaskExecuteActions();
                if (!checkResult.confirm)
                {
                    return checkResult.alarm_code;
                }
                LOG.WARN($"Do Order_ {RunningTaskData.Task_Name}:Action:{action}\r\n起始角度{RunningTaskData.ExecutingTrajecory.First().Theta}, 終點角度 {RunningTaskData.ExecutingTrajecory.Last().Theta}");
                if (action == ACTION_TYPE.None)
                {
                    if (ForkLifter != null)
                    {

                        var forkGoHomeResult = await ForkLifter.ForkGoHome();
                        if (!forkGoHomeResult.confirm)
                        {
                            return AlarmCodes.Fork_Arm_Pose_Error;
                        }
                    }
                }
                else
                {
                    if (action != ACTION_TYPE.Unpark && action != ACTION_TYPE.Discharge && ForkLifter != null)
                    {
                        var forkGoTeachPositionResult = await ChangeForkPositionBeforeGoToWorkStation(action == ACTION_TYPE.Load ? FORK_HEIGHT_POSITION.UP_ : FORK_HEIGHT_POSITION.DOWN_);
                        if (!forkGoTeachPositionResult.success)
                        {
                            return forkGoTeachPositionResult.alarm_code;
                        }
                    }
                }
                if (AGVCActionStatusChaged != null)
                    AGVCActionStatusChaged = null;
                AGVCActionStatusChaged += HandleAGVActionChanged;
                (bool agvc_executing, string message) agvc_response = await Agv.AGVC.AGVSTaskDownloadHandler(RunningTaskData);
                if (!agvc_response.agvc_executing)
                {
                    return AlarmCodes.Cant_TransferTask_TO_AGVC;
                }
                else
                {
                    if (action == ACTION_TYPE.Load | action == ACTION_TYPE.Unload)
                    {
                        StartFrontendObstcleDetection();
                    }

                }

                return AlarmCodes.None;
            }
            catch (Exception ex)
            {
                throw ex;
            }

        }

        protected async void HandleAGVActionChanged(ActionStatus status)
        {
            if (Agv.Sub_Status == SUB_STATUS.DOWN)
            {
                AGVCActionStatusChaged -= HandleAGVActionChanged;
                return;
            }
            LOG.WARN($"[ {RunningTaskData.Task_Simplex} -{action}] AGVC Action Status Changed: {status}.");
            if (status == ActionStatus.ACTIVE)
            {
                //Agv.FeedbackTaskStatus(action == ACTION_TYPE.None ? TASK_RUN_STATUS.NAVIGATING : TASK_RUN_STATUS.ACTION_START);
            }
            else
            {
                if (status == ActionStatus.SUCCEEDED)
                {
                    AGVCActionStatusChaged -= HandleAGVActionChanged;
                    if (Agv.Sub_Status == SUB_STATUS.DOWN)
                    {
                        return;
                    }
                    LOG.INFO($"AGVC Action Status is success,Do Work defined!");
                    var result = await HandleAGVCActionSucceess();

                    if (!result.success)
                    {
                        AlarmManager.AddAlarm(result.alarmCode, false);
                        Agv.Sub_Status = SUB_STATUS.DOWN;
                    }

                }
                if (status == ActionStatus.ABORTED)
                {
                    AGVCActionStatusChaged -= HandleAGVActionChanged;
                    Agv.Sub_Status = SUB_STATUS.DOWN;
                    Agv.FeedbackTaskStatus(TASK_RUN_STATUS.FAILURE);
                }
            }
        }

        protected virtual async Task<(bool success, AlarmCodes alarmCode)> HandleAGVCActionSucceess()
        {
            Agv.Sub_Status = SUB_STATUS.IDLE;
            Agv.FeedbackTaskStatus(TASK_RUN_STATUS.ACTION_FINISH);
            return (true, AlarmCodes.None);
        }

        internal async Task AGVSPathExpand(clsTaskDownloadData taskDownloadData)
        {
            _ = Task.Run(async () =>
            {
                string new_path = string.Join("->", taskDownloadData.TagsOfTrajectory);
                string ori_path = string.Join("->", RunningTaskData.TagsOfTrajectory);
                LOG.INFO($"AGV導航路徑變更\r\n-原路徑：{ori_path}\r\n新路徑:{new_path}");
                RunningTaskData = taskDownloadData;
                if (Agv.BarcodeReader.Data.tagID == 0)
                {

                    LOG.INFO($"AGV導航路徑變更-當前Tag為0,等待AGV抵達下一個目的地");
                    while (Agv.BarcodeReader.Data.tagID == 0)
                    {
                        await Task.Delay(1);
                    }
                }
                LOG.INFO($"AGV導航路徑變更-發送新的導航資訊TO車控");
                Agv.AGVC.Replan(taskDownloadData);

            });
        }

        /// <summary>
        /// 執行任務前動作
        /// </summary>
        /// <returns></returns>
        public virtual async Task<(bool confirm, AlarmCodes alarm_code)> BeforeTaskExecuteActions()
        {
            Agv.FeedbackTaskStatus(action == ACTION_TYPE.None ? TASK_RUN_STATUS.NAVIGATING : TASK_RUN_STATUS.ACTION_START);
            return (true, AlarmCodes.None);
        }

        /// <summary>
        /// 任務開始前的方向燈切換
        /// </summary>
        public abstract void DirectionLighterSwitchBeforeTaskExecute();

        /// <summary>
        /// 任務開始前的雷射設定
        /// </summary>
        public abstract void LaserSettingBeforeTaskExecute();

        internal virtual void Abort()
        {
            AGVCActionStatusChaged = null;
            TaskCancelCTS.Cancel();
            
        }

        public async Task<(bool success, AlarmCodes alarm_code)> ChangeForkPositionBeforeGoToWorkStation(FORK_HEIGHT_POSITION position)
        {
            LOG.WARN($"Before In Work Station, Fork Pose Change ,Tag:{destineTag},{position}");
            (bool success, AlarmCodes alarm_code) result = ForkLifter.ForkGoTeachedPoseAsync(destineTag, 0, position, 1.0).Result;
            return result;
        }
        /// <summary>
        /// 車頭二次檢Sensor檢察功能
        /// </summary>
        protected virtual void StartFrontendObstcleDetection()
        {
            bool Enable = AppSettingsHelper.GetValue<bool>($"VCS:LOAD_OBS_DETECTION:Enable_{action}");

            if (!Enable)
                return;

            int DetectionTime = AppSettingsHelper.GetValue<int>("VCS:LOAD_OBS_DETECTION:Duration");
            LOG.WARN($"前方二次檢Sensor 偵側開始 (偵測持續時間={DetectionTime} s)");
            CancellationTokenSource cancelDetectCTS = new CancellationTokenSource(TimeSpan.FromSeconds(DetectionTime));
            Stopwatch stopwatch = Stopwatch.StartNew();
            bool detected = false;

            void FrontendObsSensorDetectAction(object sender, EventArgs e)
            {
                detected = true;
                if (!cancelDetectCTS.IsCancellationRequested)
                {
                    cancelDetectCTS.Cancel();
                    stopwatch.Stop();
                    LOG.Critical($" 前方二次檢Sensor觸發(第 {stopwatch.ElapsedMilliseconds / 1000.0} 秒)");
                    try
                    {
                        Agv.AGVC.EMOHandler(this, EventArgs.Empty);
                        Agv.ExecutingTask.Abort();
                        Agv.Sub_Status = SUB_STATUS.DOWN;
                        AlarmManager.AddAlarm(FrontendSecondarSensorTriggerAlarmCode, false);
                    }
                    catch (Exception ex)
                    {
                        throw ex;
                    }
                }
            }
            Agv.WagoDI.OnFrontSecondObstacleSensorDetected += FrontendObsSensorDetectAction;
            Task.Run(() =>
            {
                while (!cancelDetectCTS.IsCancellationRequested)
                {
                    Thread.Sleep(1);
                }
                if (!detected)
                {
                    LOG.WARN($"前方二次檢Sensor Pass. ");
                }
                Agv.WagoDI.OnFrontSecondObstacleSensorDetected -= FrontendObsSensorDetectAction;
            });
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: 處置受控狀態 (受控物件)
                }
                AGVCActionStatusChaged = null;
                TaskCancelCTS.Cancel();
                disposedValue = true;
            }
        }

        // // TODO: 僅有當 'Dispose(bool disposing)' 具有會釋出非受控資源的程式碼時，才覆寫完成項
        // ~TaskBase()
        // {
        //     // 請勿變更此程式碼。請將清除程式碼放入 'Dispose(bool disposing)' 方法
        //     Dispose(disposing: false);
        // }

        public void Dispose()
        {
            // 請勿變更此程式碼。請將清除程式碼放入 'Dispose(bool disposing)' 方法
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
