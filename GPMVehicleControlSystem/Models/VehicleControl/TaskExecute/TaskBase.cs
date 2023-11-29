using AGVSystemCommonNet6.AGVDispatch.Messages;
using AGVSystemCommonNet6.Alarm;
using AGVSystemCommonNet6.Log;
using AGVSystemCommonNet6.MAP;
using AGVSystemCommonNet6.Vehicle_Control.Models;
using AGVSystemCommonNet6.Vehicle_Control.VCSDatabase;
using AGVSystemCommonNet6.Vehicle_Control.VMS_ALARM;
using GPMVehicleControlSystem.Models.Buzzer;
using GPMVehicleControlSystem.Models.VehicleControl.VehicleComponent;
using GPMVehicleControlSystem.Models.VehicleControl.Vehicles;
using GPMVehicleControlSystem.Models.WorkStation;
using GPMVehicleControlSystem.VehicleControl.DIOModule;
using RosSharp.RosBridgeClient.Actionlib;
using System.Diagnostics;
using static AGVSystemCommonNet6.clsEnums;
using static GPMVehicleControlSystem.Models.VehicleControl.AGVControl.CarController;
using static GPMVehicleControlSystem.Models.VehicleControl.VehicleComponent.clsForkLifter;
using static GPMVehicleControlSystem.Models.VehicleControl.Vehicles.Params.clsObstacleDetection;
using static GPMVehicleControlSystem.VehicleControl.DIOModule.clsDIModule;

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

        public Action<ActionStatus> AGVCActionStatusChaged
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

        protected bool IsNeedWaitForkHome = false;
        protected Task forkGoHomeTask = null;
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
        public int ModBusTcpPort
        {
            get
            {
                if (Agv.WorkStations.Stations.TryGetValue(destineTag, out var data))
                {
                    return data.ModbusTcpPort;
                }
                else
                {
                    return -1;
                }
            }
        }
        public WORKSTATION_HS_METHOD eqHandshakeMode
        {
            get
            {
                if (Agv.WorkStations.Stations.TryGetValue(destineTag, out var data))
                {
                    WORKSTATION_HS_METHOD mode = data.HandShakeModeHandShakeMode;
                    LOG.WARN($"[{action}] Tag_{destineTag} Handshake Mode:{mode}({(int)mode})");
                    return mode;
                }
                else
                {
                    LOG.WARN($"[{action}] Tag_{destineTag} Handshake Mode Not Defined! Forcing Handsake to Safty Protection. ");
                    return WORKSTATION_HS_METHOD.HS;
                }
            }
        }
        public CARGO_TRANSFER_MODE CargoTransferMode
        {
            get
            {
                if (Agv.WorkStations.Stations.TryGetValue(destineTag, out var data))
                {
                    return data.CargoTransferMode;
                }
                else
                    return CARGO_TRANSFER_MODE.EQ_Pick_and_Place;
            }
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

                if (ForkLifter != null)
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
                    if (action != ACTION_TYPE.Unpark && action != ACTION_TYPE.Discharge && ForkLifter != null)
                    {
                        if (!Agv.Parameters.LDULD_Task_No_Entry | action == ACTION_TYPE.Charge)
                        {

                            var forkGoTeachPositionResult = await ChangeForkPositionInSecondaryPtOfWorkStation(CargoTransferMode == CARGO_TRANSFER_MODE.AGV_Pick_and_Place ? (action == ACTION_TYPE.Load ? FORK_HEIGHT_POSITION.UP_ : FORK_HEIGHT_POSITION.DOWN_) : FORK_HEIGHT_POSITION.DOWN_);
                            if (!forkGoTeachPositionResult.success)
                            {
                                return forkGoTeachPositionResult.alarm_code;
                            }
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
                //await Agv.WagoDO.SetState(DO_ITEM.Horizon_Motor_Free, true);
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
                        await Task.Delay(1000);
                        if (Agv.AGVC.ActionStatus == ActionStatus.SUCCEEDED)
                            HandleAGVActionChanged(ActionStatus.SUCCEEDED);
                        else if (Agv.AGVC.ActionStatus == ActionStatus.ACTIVE | Agv.AGVC.ActionStatus == ActionStatus.PENDING)
                        {
                            if (action == ACTION_TYPE.Load | action == ACTION_TYPE.Unload)
                            {
                                #region 前方障礙物預檢
                                var _triggerLevelOfOBSDetected = Agv.Parameters.LOAD_OBS_DETECTION.AlarmLevelWhenTrigger;
                                bool isNoObstacle = StartFrontendObstcleDetection(_triggerLevelOfOBSDetected);
                                if (!isNoObstacle)
                                    if (_triggerLevelOfOBSDetected == ALARM_LEVEL.ALARM)
                                        return FrontendSecondarSensorTriggerAlarmCode;
                                    else
                                        AlarmManager.AddWarning(FrontendSecondarSensorTriggerAlarmCode);
                                #endregion
                            }

                            AGVCActionStatusChaged += HandleAGVActionChanged;
                            await Agv.AGVC.CarSpeedControl(ROBOT_CONTROL_CMD.SPEED_Reconvery, SPEED_CONTROL_REQ_MOMENT.NEW_TASK_START_EXECUTING, false);
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
                    AlarmManager.AddAlarm(AlarmCodes.Cst_Slope_Error, false);
                    Agv.Sub_Status = SUB_STATUS.DOWN;
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
                    LOG.INFO($"[{_RunningTaskData.Action_Type}] Tag-[{Agv.BarcodeReader.CurrentTag}] AGVC Action Status is success, {(_RunningTaskData.Action_Type != ACTION_TYPE.None ? $"Do Action in/out of Station defined!" : "Park done")}");
                    Agv.DirectionLighter.CloseAll();
                    Agv.lastParkingAccuracy = StoreParkingAccuracy();
                    var result = await HandleAGVCActionSucceess();
                    if (!result.success)
                    {
                        var alarm_code = result.alarmCode;
                        if (alarm_code != AlarmCodes.None)
                        {
                            Agv.AlarmCodeWhenHandshaking = alarm_code;
                            AlarmManager.AddAlarm(alarm_code, false);
                        }
                        Agv.Sub_Status = SUB_STATUS.DOWN;
                    }
                    else
                    {
                    }
                }
            });
        }

        /// <summary>
        /// 儲存停車精度
        /// </summary>
        private clsParkingAccuracy StoreParkingAccuracy()
        {
            var parkingAccqData = new clsParkingAccuracy
            {
                ParkingLocation = Agv.lastVisitedMapPoint.Graph.Display,
                ParkingTag = Agv.BarcodeReader.CurrentTag,
                Slam_X = Agv.Navigation.Data.robotPose.pose.position.x,
                Slam_Y = Agv.Navigation.Data.robotPose.pose.position.y,
                Slam_Theta = Agv.Navigation.Angle,
                X = Agv.BarcodeReader.CurrentX,
                Y = Agv.BarcodeReader.CurrentY,
                Time = DateTime.Now,
                TaskName = this.RunningTaskData.Task_Name,
                IsGoodParkingLoaction = true
            };
            DBhelper.InsertParkingAccuracy(parkingAccqData);
            return parkingAccqData;
        }

        protected virtual async Task<(bool success, AlarmCodes alarmCode)> HandleAGVCActionSucceess()
        {
            Agv.Sub_Status = SUB_STATUS.IDLE;
            await Agv.FeedbackTaskStatus(TASK_RUN_STATUS.ACTION_FINISH);
            return (true, AlarmCodes.None);
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
        protected AlarmCodes ForkGoHomeResultAlarmCode = AlarmCodes.None;
        protected async Task ForkHomeProcess(bool need_reach_secondary = true)
        {
            if (ForkLifter == null)
            {
                IsNeedWaitForkHome = false;
                return;
            }
            if (!Agv.Parameters.LDULD_Task_No_Entry | (action == ACTION_TYPE.Discharge | action == ACTION_TYPE.Unpark))
            {
                IsNeedWaitForkHome = true;
                forkGoHomeTask = await Task.Factory.StartNew(async () =>
                {
                    if (need_reach_secondary)
                    {
                        LOG.TRACE($"Wait Reach Tag {RunningTaskData.Destination}, Fork Will Start Go Home.");

                        while (Agv.BarcodeReader.CurrentTag != RunningTaskData.Destination)
                        {
                            await Task.Delay(1);
                            if (Agv.Sub_Status == SUB_STATUS.DOWN)
                            {
                                IsNeedWaitForkHome = false;
                                return;
                            }
                        }
                    }
                    LOG.TRACE($"Reach Tag {RunningTaskData.Destination}!, Fork Start Go Home NOW!!!");
                    (bool confirm, AlarmCodes alarm_code) ForkGoHomeActionResult = (Agv.ForkLifter.CurrentForkLocation == FORK_LOCATIONS.HOME, AlarmCodes.None);
                    await Agv.Laser.SideLasersEnable(true);
                    await RegisterSideLaserTriggerEvent();
                    while (Agv.ForkLifter.CurrentForkLocation != FORK_LOCATIONS.HOME)
                    {
                        await Task.Delay(1);
                        if (Agv.Sub_Status == SUB_STATUS.DOWN)
                        {
                            IsNeedWaitForkHome = false;
                            return;
                        }
                        ForkGoHomeActionResult = await ForkLifter.ForkGoHome();
                        LOG.TRACE($"[Fork Home Process At Secondary]ForkHome Confirm= {ForkGoHomeActionResult.confirm}/Z-Axis Position={Agv.ForkLifter.CurrentForkLocation}");
                        if (ForkGoHomeActionResult.confirm && Agv.ForkLifter.CurrentForkLocation != FORK_LOCATIONS.HOME)
                        {
                            AlarmManager.AddWarning(AlarmCodes.Fork_Go_Home_But_Home_Sensor_Signal_Error);
                            break;
                        }
                    }
                    await UnRegisterSideLaserTriggerEvent();
                    await Task.Delay(500);
                    await Agv.Laser.SideLasersEnable(false);
                    if (!ForkGoHomeActionResult.confirm)
                    {
                        Agv.Sub_Status = SUB_STATUS.DOWN;
                        ForkGoHomeResultAlarmCode = ForkGoHomeActionResult.alarm_code;
                        AlarmManager.AddAlarm(ForkGoHomeResultAlarmCode, false);
                    }
                });

            }
            else
                IsNeedWaitForkHome = false;
        }

        public async Task<(bool success, AlarmCodes alarm_code)> ChangeForkPositionInSecondaryPtOfWorkStation(FORK_HEIGHT_POSITION position)
        {
            LOG.WARN($"Before In Work Station, Fork Pose Change ,Tag:{destineTag},{position}");
            await RegisterSideLaserTriggerEvent();
            (bool success, AlarmCodes alarm_code) result = ForkLifter.ForkGoTeachedPoseAsync(destineTag, 0, position, 1).Result;
            await UnRegisterSideLaserTriggerEvent();
            return result;
        }

        protected async Task RegisterSideLaserTriggerEvent()
        {
            Agv.WagoDI.SubsSignalStateChange(DI_ITEM.RightProtection_Area_Sensor_3, LaserTriggerWhenForkLiftMove);
            Agv.WagoDI.SubsSignalStateChange(DI_ITEM.LeftProtection_Area_Sensor_3, LaserTriggerWhenForkLiftMove);
            await Task.Delay(500);
            await Agv.Laser.SideLasersEnable(true);//開啟左右雷射
        }
        protected async Task UnRegisterSideLaserTriggerEvent()
        {
            Agv.WagoDI.UnRegistSignalStateChange(DI_ITEM.RightProtection_Area_Sensor_3, LaserTriggerWhenForkLiftMove);
            Agv.WagoDI.UnRegistSignalStateChange(DI_ITEM.LeftProtection_Area_Sensor_3, LaserTriggerWhenForkLiftMove);
            await Agv.Laser.SideLasersEnable(false);
        }
        private bool IsSideLsrFlickBefore = false;
        private async void LaserTriggerWhenForkLiftMove(object? sender, bool active)
        {
            clsIOSignal input = (clsIOSignal)sender;
            AlarmCodes alarm_code = input.Input == DI_ITEM.RightProtection_Area_Sensor_3 ? AlarmCodes.RightProtection_Area3 : AlarmCodes.LeftProtection_Area3;

            if (!active)
            {
                await Task.Delay(300);
                if (Agv.WagoDI.GetState(input.Input))
                {
                    IsSideLsrFlickBefore = true;
                    return;
                }
                IsSideLsrFlickBefore = false;
                await Agv.ForkLifter.ForkStopAsync(false);
                AlarmManager.AddAlarm(alarm_code);
                await Task.Delay(100);
                BuzzerPlayer.Alarm();
            }
            else
            {
                if (Agv.WagoDI.GetState(DI_ITEM.LeftProtection_Area_Sensor_3) && Agv.WagoDI.GetState(DI_ITEM.RightProtection_Area_Sensor_3))
                {
                    AlarmManager.ClearAlarm(alarm_code);
                    Agv.ForkLifter.fork_ros_controller.IsZAxisActionDone = true;
                    await Task.Delay(100);
                    BuzzerPlayer.Action();
                }
            }
        }

        /// <summary>
        /// 車頭二次檢Sensor檢察功能
        /// </summary>
        protected virtual bool StartFrontendObstcleDetection(ALARM_LEVEL alarmLevel)
        {
            var IsObstacleDetected = Agv.WagoDI.GetState(Agv.Parameters.AgvType == AGV_TYPE.SUBMERGED_SHIELD ? DI_ITEM.FrontProtection_Obstacle_Sensor : DI_ITEM.Fork_Frontend_Abstacle_Sensor);
            var options = Agv.Parameters.LOAD_OBS_DETECTION;
            bool Enable = action == ACTION_TYPE.Load ? options.Enable_Load : options.Enable_UnLoad;
            if (!Enable)
                return true;
            if (IsObstacleDetected)
            {
                LOG.Critical($"前方障礙物預檢知觸發[等級={alarmLevel}]");
                return false;
            }
            if (options.Detection_Method == FRONTEND_OBS_DETECTION_METHOD.BEGIN_ACTION)
            {
                LOG.INFO($"前方障礙物預檢知Sensor Pass , No Obstacle", color: ConsoleColor.Green);
                return true;
            }
            int DetectionTime = options.Duration;
            LOG.WARN($"前方障礙物預檢知偵側開始[{options.Detection_Method}]==> 偵測持續時間={DetectionTime} 秒)");
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
                    LOG.Critical($"前方障礙物預檢知觸發[等級={alarmLevel}](在第 {stopwatch.ElapsedMilliseconds / 1000.0} 秒)");
                    if (alarmLevel == ALARM_LEVEL.ALARM)
                        EMO_STOP_AGV();
                    else
                        AlarmManager.AddWarning(FrontendSecondarSensorTriggerAlarmCode);
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
                    LOG.INFO($"前方障礙物預檢知Sensor Pass , No Obstacle", color: ConsoleColor.Green);
                }
                Agv.WagoDI.OnFrontSecondObstacleSensorDetected -= FrontendObsSensorDetectAction;
            });
            void EMO_STOP_AGV()
            {
                try
                {
                    Agv.AGVC.EMOHandler(this, EventArgs.Empty);
                    Agv.ExecutingTaskModel.Abort();
                    Agv.Sub_Status = SUB_STATUS.DOWN;
                    AlarmManager.AddAlarm(FrontendSecondarSensorTriggerAlarmCode, false);
                }
                catch (Exception ex)
                {
                    throw ex;
                }
            }
            return true;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: 處置受控狀態 (受控物件)
                }
                UnRegisterSideLaserTriggerEvent();
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
