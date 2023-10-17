using AGVSystemCommonNet6;
using AGVSystemCommonNet6.AGVDispatch.Messages;
using AGVSystemCommonNet6.Alarm;
using AGVSystemCommonNet6.Alarm.VMS_ALARM;
using AGVSystemCommonNet6.GPMRosMessageNet.Services;
using AGVSystemCommonNet6.Log;
using AGVSystemCommonNet6.MAP;
using AGVSystemCommonNet6.Tools.Database;
using GPMVehicleControlSystem.Models.Buzzer;
using GPMVehicleControlSystem.Models.VehicleControl.VehicleComponent;
using GPMVehicleControlSystem.Models.VehicleControl.Vehicles;
using GPMVehicleControlSystem.Models.WorkStation;
using Newtonsoft.Json;
using RosSharp.RosBridgeClient.Actionlib;
using System.Diagnostics;
using static AGVSystemCommonNet6.clsEnums;
using static GPMVehicleControlSystem.Models.VehicleControl.AGVControl.CarController;
using static GPMVehicleControlSystem.Models.VehicleControl.VehicleComponent.clsLaser;
using static GPMVehicleControlSystem.VehicleControl.DIOModule.clsDOModule;

namespace GPMVehicleControlSystem.Models.VehicleControl.TaskExecute
{
    public abstract class TaskBase : IDisposable
    {
        public Vehicle Agv { get; }
        private clsTaskDownloadData _RunningTaskData = new clsTaskDownloadData();
        public bool isSegmentTask = false;

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
                try
                {

                    if (_RunningTaskData == null | value.Task_Name != _RunningTaskData?.Task_Name)
                    {
                        TrackingTags = value.TagsOfTrajectory;
                    }
                    else
                    {
                        List<int> newTrackingTags = value.TagsOfTrajectory;
                        if (TrackingTags.First() == newTrackingTags.First())
                            TrackingTags = newTrackingTags;
                    }
                }
                catch (Exception ex)
                {
                    LOG.ERROR(ex);
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
                if (Agv.Parameters.AgvType == AGV_TYPE.SUBMERGED_SHIELD)
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
                await Task.Delay(10);
                BuzzerPlayMusic(action);
                TaskCancelCTS = new CancellationTokenSource();
                DirectionLighterSwitchBeforeTaskExecute();
                if (!await LaserSettingBeforeTaskExecute())
                {
                    return AlarmCodes.Laser_Mode_value_fail;
                }
                (bool confirm, AlarmCodes alarm_code) checkResult = await BeforeTaskExecuteActions();
                if (!checkResult.confirm)
                {
                    return checkResult.alarm_code;
                }
                await Task.Delay(10);
                LOG.WARN($"Do Order_ {RunningTaskData.Task_Name}:Action:{action}\r\n起始角度{RunningTaskData.ExecutingTrajecory.First().Theta}, 終點角度 {RunningTaskData.ExecutingTrajecory.Last().Theta}");

                if (ForkLifter != null && !Agv.Parameters.LDULD_Task_No_Entry)
                {
                    if (ForkLifter.CurrentForkARMLocation != clsForkLifter.FORK_ARM_LOCATIONS.HOME)
                    {
                        await ForkLifter.ForkShortenInAsync();
                    }
                }

                if (action == ACTION_TYPE.None)
                {
                    if (ForkLifter != null && !Agv.Parameters.LDULD_Task_No_Entry)
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
                    if (action != ACTION_TYPE.Unpark && action != ACTION_TYPE.Discharge && ForkLifter != null && !Agv.Parameters.LDULD_Task_No_Entry)
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


                if (Agv.Sub_Status == SUB_STATUS.DOWN)
                {
                    LOG.WARN($"車載狀態錯誤:{Agv.Sub_Status}");
                    return AlarmCodes.AGV_State_Cant_do_this_Action;
                }

                (bool agvc_executing, string message) agvc_response = (false, "");
                await Agv.WagoDO.SetState(DO_ITEM.Horizon_Motor_Free, true);
                if ((action == ACTION_TYPE.Load | action == ACTION_TYPE.Unload) && Agv.Parameters.LDULD_Task_No_Entry)
                {
                    agvc_response = (true, "空取空放");
                    HandleAGVActionChanged(ActionStatus.SUCCEEDED);
                }
                else
                {
                    agvc_response = await TransferTaskToAGVC();
                    if (!agvc_response.agvc_executing)
                        return AlarmCodes.Can_not_Pass_Task_to_Motion_Control;
                    else
                    {
                        await Task.Delay(10);
                        await Agv.AGVC.CarSpeedControl(ROBOT_CONTROL_CMD.STOP);
                        await Task.Delay(100);
                        await Agv.WagoDO.SetState(DO_ITEM.Horizon_Motor_Free, false);

                        if (Agv.AGVC.ActionStatus == ActionStatus.SUCCEEDED)
                            HandleAGVActionChanged(ActionStatus.SUCCEEDED);
                        else if (Agv.AGVC.ActionStatus == ActionStatus.ACTIVE)
                        {
                            if (action == ACTION_TYPE.Load | action == ACTION_TYPE.Unload)
                                StartFrontendObstcleDetection();
                            AGVCActionStatusChaged += HandleAGVActionChanged;
                            await Agv.AGVC.CarSpeedControl(ROBOT_CONTROL_CMD.SPEED_Reconvery);
                        }
                    }
                }
                return AlarmCodes.None;
            }
            catch (Exception ex)
            {
                throw ex;
            }

        }

        private void BuzzerPlayMusic(ACTION_TYPE action)
        {
            if (action == ACTION_TYPE.None)
            {
                BuzzerPlayer.Move();
            }
            else
            {
                BuzzerPlayer.Action();
            }
        }
        public static event EventHandler<clsTaskDownloadData> OnSegmentTaskExecuting2Sec;
        protected virtual async Task<(bool agvc_executing, string message)> TransferTaskToAGVC()
        {
            return await Agv.AGVC.ExecuteTaskDownloaded(RunningTaskData, Agv.Parameters.ActionTimeout);
        }

        internal bool IsCargoBiasDetecting = false;
        internal bool IsCargoBiasTrigger = false;

        protected bool IsAGVCActionNoOperate(ActionStatus status, Action<ActionStatus> actionStatusChangedCallback)
        {
            bool isNoOperate = status == ActionStatus.RECALLING | status == ActionStatus.REJECTED | status == ActionStatus.PREEMPTING | status == ActionStatus.PREEMPTED | status == ActionStatus.ABORTED;
            if (isNoOperate)
            {
                AGVCActionStatusChaged -= actionStatusChangedCallback;
                LOG.WARN($"Task Action狀態錯誤:{status}");
                Agv.SoftwareEMO(AlarmCodes.AGV_State_Cant_do_this_Action);
            }
            return isNoOperate;
        }
        protected async void HandleAGVActionChanged(ActionStatus status)
        {
            _ = Task.Factory.StartNew(async () =>
            {
                LOG.WARN($"[AGVC Action Status Changed-ON-Action Actived][{RunningTaskData.Task_Simplex} -{action}] AGVC Action Status Changed: {status}.");
                if (IsAGVCActionNoOperate(status, HandleAGVActionChanged))
                    return;
                if (Agv.Sub_Status == SUB_STATUS.DOWN)
                {
                    if (Agv.AGVSResetCmdFlag)
                    {
                        Agv.AGV_Reset_Flag = true;
                    }
                    AGVCActionStatusChaged = null;
                    return;
                }

                if (IsCargoBiasTrigger && Agv.Parameters.CargoBiasDetectionWhenNormalMoving)
                {
                    AGVCActionStatusChaged = null;
                    LOG.ERROR($"存在貨物傾倒異常");
                    IsCargoBiasTrigger = IsCargoBiasDetecting = false;
                    AlarmManager.AddAlarm(AlarmCodes.Cst_Slope_Error);
                    Agv.Sub_Status = SUB_STATUS.DOWN;
                    await Agv.FeedbackTaskStatus(TASK_RUN_STATUS.ACTION_FINISH, alarm_tracking: AlarmCodes.Cst_Slope_Error);
                    return;
                }

                if (status == ActionStatus.ACTIVE)
                {
                    //Agv.FeedbackTaskStatus(action == ACTION_TYPE.None ? TASK_RUN_STATUS.NAVIGATING : TASK_RUN_STATUS.ACTION_START);
                }
                else if (status == ActionStatus.SUCCEEDED)
                {
                    if (Agv.AGVSResetCmdFlag)
                    {
                        Agv.AGV_Reset_Flag = true;
                    }
                    AGVCActionStatusChaged = null;


                    if (Agv.Sub_Status == SUB_STATUS.DOWN)
                    {
                        return;
                    }
                    LOG.INFO($"AGVC Action Status is success,Do Work defined!");
                    DBhelper.InsertParkingAccuracy(new AGVSystemCommonNet6.Tools.clsParkingAccuracy
                    {
                        ParkingLocation = Agv.lastVisitedMapPoint.Name,
                        ParkingTag = Agv.BarcodeReader.CurrentTag,
                        X = Agv.BarcodeReader.CurrentX,
                        Y = Agv.BarcodeReader.CurrentY,
                        Time = DateTime.Now,
                        TaskName = this.RunningTaskData.Task_Name
                    });
                    Agv.DirectionLighter.CloseAll();
                    var result = await HandleAGVCActionSucceess();
                    if (!result.success)
                    {
                        AlarmManager.AddAlarm(result.alarmCode, false);
                        Agv.Sub_Status = SUB_STATUS.DOWN;
                    }
                }
            });
        }

        protected virtual async Task<(bool success, AlarmCodes alarmCode)> HandleAGVCActionSucceess()
        {
            Agv.Sub_Status = SUB_STATUS.IDLE;
            await Agv.FeedbackTaskStatus(TASK_RUN_STATUS.ACTION_FINISH);
            return (true, AlarmCodes.None);
        }

        internal async Task AGVSPathExpand(clsTaskDownloadData taskDownloadData)
        {
            RunningTaskData = taskDownloadData;
            LOG.INFO($"AGV導航路徑變更-發送新的導航資訊TO車控");
            Agv.AGVC.Replan(taskDownloadData);
            LOG.INFO($"AGV導航路徑變更-發送新的導航資訊TO車控-已發送");
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
        public abstract Task<bool> LaserSettingBeforeTaskExecute();

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

            bool Enable = action == ACTION_TYPE.Load ? StaStored.CurrentVechicle.Parameters.LOAD_OBS_DETECTION.Enable_Load :
                                                                                        StaStored.CurrentVechicle.Parameters.LOAD_OBS_DETECTION.Enable_UnLoad;

            if (!Enable)
                return;

            int DetectionTime = StaStored.CurrentVechicle.Parameters.LOAD_OBS_DETECTION.Duration;
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
                        Agv.ExecutingActionTask.Abort();
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
