using AGVSystemCommonNet6.AGVDispatch.Messages;
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
    public abstract class TaskBase
    {
        public Vehicle Agv { get; }
        private clsTaskDownloadData _RunningTaskData;
        public Action<string> OnTaskFinish;
        protected CancellationTokenSource TaskCancelCTS = new CancellationTokenSource();
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
        public async Task Execute()
        {
            try
            {
                TaskCancelCTS = new CancellationTokenSource();
                Agv.Laser.AllLaserActive();
                Agv.AGVC.IsAGVExecutingTask = true;
                Agv.AGVC.OnTaskActionFinishAndSuccess += AfterMoveFinishHandler;
                DirectionLighterSwitchBeforeTaskExecute();
                LaserSettingBeforeTaskExecute();
                (bool confirm, AlarmCodes alarm_code) checkResult = await BeforeTaskExecuteActions();
                if (!checkResult.confirm)
                {
                    AlarmManager.AddAlarm(checkResult.alarm_code, false);
                    Agv.AGVC.OnTaskActionFinishAndSuccess -= AfterMoveFinishHandler;
                    Agv.Sub_Status = SUB_STATUS.ALARM;
                    return;
                }
                Agv.Laser.AgvsLsrSetting = RunningTaskData.ExecutingTrajecory.First().Laser;

                if (ForkLifter != null)
                {
                    if (action == ACTION_TYPE.None)
                        await ForkLifter.ForkGoHome();
                    else
                        ChangeForkPositionBeforeGoToWorkStation(action == ACTION_TYPE.Load ? FORK_HEIGHT_POSITION.UP_ : FORK_HEIGHT_POSITION.DOWN_);
                }
                (bool agvc_executing, string message) agvc_response = await Agv.AGVC.AGVSTaskDownloadHandler(RunningTaskData);
                if (!agvc_response.agvc_executing)
                {
                    AlarmManager.AddAlarm(AlarmCodes.Cant_TransferTask_TO_AGVC, false);
                    Agv.Sub_Status = SUB_STATUS.ALARM;
                }
                else
                {

                    if (action == ACTION_TYPE.Load | action == ACTION_TYPE.Unload)
                    {
                        StartFrontendObstcleDetection();
                    }

                    Agv.AGVC.CarSpeedControl(AGVControl.CarController.ROBOT_CONTROL_CMD.SPEED_Reconvery);
                    Agv.FeedbackTaskStatus(TASK_RUN_STATUS.NAVIGATING);
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }

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

        private async void AfterMoveFinishHandler(object? sender, clsTaskDownloadData e)
        {
            LOG.INFO($"[{action}] move task done. Reach  Tag = {Agv.Navigation.LastVisitedTag} 目的地:{RunningTaskData.Destination} Agv.AGVC.OnTaskActionFinishAndSuccess -= AfterMoveFinishHandler ");
            if (Agv.Navigation.LastVisitedTag == RunningTaskData.Destination)
            {
                Agv.AGVC.OnTaskActionFinishAndSuccess -= AfterMoveFinishHandler;
            }


            if (Agv.Sub_Status == SUB_STATUS.DOWN)
            {
                LOG.Critical($"AfterMoveFinishHandler BUT AGV STATUS DOWN");
            }
            else
            {
                (bool confirm, AlarmCodes alarm_code) check_result = await AfterMoveDone();
                if (!check_result.confirm)
                {
                    AlarmManager.AddAlarm(check_result.alarm_code, false);
                    Agv.Sub_Status = SUB_STATUS.ALARM;
                }
                else
                    Agv.Sub_Status = SUB_STATUS.IDLE;
            }

            Agv.AGVC.IsAGVExecutingTask = false;
            OnTaskFinish(RunningTaskData.Task_Simplex);
            _ = Task.Factory.StartNew(async () =>
            {
                await Task.Delay(1000);
                Agv.DirectionLighter.CloseAll();
            });
        }

        public virtual async Task<(bool confirm, AlarmCodes alarm_code)> AfterMoveDone()
        {
            Agv.Laser.LeftLaserBypass = false;
            Agv.Laser.RightLaserBypass = false;
            Agv.Sub_Status = SUB_STATUS.IDLE;
            Agv.FeedbackTaskStatus(TASK_RUN_STATUS.ACTION_FINISH);
            return (true, AlarmCodes.None);

        }

        /// <summary>
        /// 執行任務前的各項設定
        /// </summary>
        /// <returns></returns>
        public virtual async Task<(bool confirm, AlarmCodes alarm_code)> BeforeTaskExecuteActions()
        {
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
            TaskCancelCTS.Cancel();
            Agv.AGVC.OnTaskActionFinishAndSuccess -= AfterMoveFinishHandler;
        }


        public async void ChangeForkPositionBeforeGoToWorkStation(FORK_HEIGHT_POSITION position)
        {
            LOG.WARN($"Before In Work Station, Fork Pose Change ,Tag:{destineTag},{position}");
            (bool success, AlarmCodes alarm_code) result = ForkLifter.ForkGoTeachedPoseAsync(destineTag, 0, position, 1.0).Result;
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

    }
}
