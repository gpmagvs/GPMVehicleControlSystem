//#define YM_4FAOI
using AGVSystemCommonNet6.AGVDispatch.Messages;
using AGVSystemCommonNet6.Alarm;
using AGVSystemCommonNet6.MAP;
using AGVSystemCommonNet6.Vehicle_Control.Models;
using AGVSystemCommonNet6.Vehicle_Control.VCSDatabase;
using AGVSystemCommonNet6.Vehicle_Control.VCS_ALARM;
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
using RosSharp.RosBridgeClient.MessageTypes.Geometry;
using RosSharp.RosBridgeClient.MessageTypes.Sensor;
using static GPMVehicleControlSystem.Models.VehicleControl.Vehicles.Vehicle;
using AGVSystemCommonNet6;
using Newtonsoft.Json.Linq;
using Microsoft.AspNetCore.Mvc.ActionConstraints;
using static AGVSystemCommonNet6.MAP.MapPoint;
using WebSocketSharp;
using NLog;

namespace GPMVehicleControlSystem.Models.VehicleControl.TaskExecute
{
    public abstract class TaskBase : IDisposable
    {
        public Vehicle Agv { get; }
        private clsTaskDownloadData _RunningTaskData = new clsTaskDownloadData();
        public bool isSegmentTask = false;

        public Action<string> OnTaskFinish;
        protected CancellationTokenSource TaskCancelCTS = new CancellationTokenSource();
        public CancellationTokenSource TaskCancelByReplan = new CancellationTokenSource();
        private bool disposedValue;
        protected AlarmCodes task_abort_alarmcode = AlarmCodes.None;
        protected double ExpectedForkPostionWhenEntryWorkStation = 0;
        protected NLog.Logger logger;
        public MapPoint DestineMapPoint
        {
            get
            {
                return Agv.NavingMap.Points.Values.FirstOrDefault(pt => pt.TagNumber == destineTag);
            }
        }

        public STATION_TYPE DestineStationType => DestineMapPoint == null ? STATION_TYPE.Unknown : DestineMapPoint.StationType;

        public bool IsJustAGVPickAndPlaceAtWIPPort
        {
            get
            {
                bool isAGVSKGBase = Agv.Parameters.VMSParam.Protocol == VMS_PROTOCOL.KGS;
                if (Agv.Parameters.AgvType != AGV_TYPE.FORK)
                    return false;

                if (DestineStationType == STATION_TYPE.Unknown)
                    return false;

                if (DestineStationType != STATION_TYPE.Buffer_EQ &&
                    DestineStationType != STATION_TYPE.Buffer &&
                    DestineStationType != STATION_TYPE.Charge_Buffer)
                    return false;

                return DestineStationType == STATION_TYPE.Buffer || (DestineStationType != STATION_TYPE.Buffer && height > 0);
            }
        }
        public Action<ActionStatus> AGVCActionStatusChaged
        {
            get => Agv.AGVC.OnAGVCActionChanged;
            set => Agv.AGVC.OnAGVCActionChanged = value;
        }
        internal bool isMoveToChargeStationTask
        {
            get
            {
                try
                {
                    return _RunningTaskData.Task_Name.ToUpper().Contains("CHARGE");
                }
                catch (Exception ex)
                {
                    logger.Error(ex, ex.Message);
                    return false;
                }
            }
        }
        public clsTaskDownloadData RunningTaskData
        {
            get => _RunningTaskData;
            set
            {
                try
                {
                    if (_RunningTaskData == null || value.Task_Name != _RunningTaskData?.Task_Name)
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
                    logger.Error(ex, ex.Message);
                }
                _RunningTaskData = value;
            }
        }
        public abstract ACTION_TYPE action { get; set; }
        public List<int> TrackingTags { get; private set; } = new List<int>();
        public clsForkLifter ForkLifter { get; internal set; }
        public clsPin PinHardware => (Agv as ForkAGV).PinHardware;
        public int destineTag => _RunningTaskData == null ? -1 : _RunningTaskData.Destination;
        public int height { get; internal set; } = 0;
        public MapPoint? lastPt => Agv.NavingMap.Points.Values.FirstOrDefault(pt => pt.TagNumber == RunningTaskData.Destination);
        public bool IsNeedHandshake = false;

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

        public TaskBase()
        {

        }

        public TaskBase(Vehicle Agv, clsTaskDownloadData taskDownloadData)
        {
            this.Agv = Agv;
            RunningTaskData = taskDownloadData;
            logger = LogManager.GetLogger("TaskLog");
            logger.Info($"New Task : " +
                $"\r\nTask Name:{taskDownloadData.Task_Name}" +
                $"\r\nTask_Simplex:{taskDownloadData.Task_Simplex}" +
                $"\r\nTask_Sequence:{taskDownloadData.Task_Sequence}" +
                $"\r\nTrajecory :{(taskDownloadData.ExecutingTrajecory.Length == 0 ? "empty" : string.Join("->", taskDownloadData.ExecutingTrajecory.Select(pt => pt.Point_ID)))}" +
                $"\r\nOrder Info : {taskDownloadData.OrderInfo.ToJson()}");

        }
        public int ModBusTcpPort
        {
            get
            {
                if (Agv.WorkStations.Stations.TryGetValue(destineTag, out clsWorkStationData? data))
                {
                    return data.ModbusTcpPort;
                }
                else
                {
                    return -1;
                }
            }
        }

        public EQ_HS_METHOD HandshakeProtocol
        {
            get
            {
                if (Agv.WorkStations.Stations.TryGetValue(destineTag, out clsWorkStationData? data))
                {
                    return data.HandShakeConnectionMode;
                }
                else
                {
                    return EQ_HS_METHOD.PIO;
                }
            }
        }

        public WORKSTATION_HS_METHOD eqHandshakeMode { get; set; } = WORKSTATION_HS_METHOD.HS;

#if YM_4FAOI
        public CARGO_TRANSFER_MODE CargoTransferMode => CARGO_TRANSFER_MODE.EQ_Pick_and_Place;
#else
        public CARGO_TRANSFER_MODE CargoTransferMode
        {
            get
            {
                if (IsJustAGVPickAndPlaceAtWIPPort || action == ACTION_TYPE.Park)
                    return CARGO_TRANSFER_MODE.AGV_Pick_and_Place;

                if (Agv.WorkStations.Stations.TryGetValue(destineTag, out clsWorkStationData? data))
                {
                    if (data.CargoTransferMode == CARGO_TRANSFER_MODE.ONLY_FIRST_SLOT_EQ_Pick_and_Place && height > 0)
                        return CARGO_TRANSFER_MODE.AGV_Pick_and_Place;
                    return data.CargoTransferMode;
                }
                else
                    return CARGO_TRANSFER_MODE.EQ_Pick_and_Place;
            }
        }
#endif


        public bool IsBackToSecondaryPt { get; internal set; } = false;

        /// <summary>
        /// 執行任務
        /// </summary>
        public async Task<List<AlarmCodes>> Execute()
        {
            try
            {
                task_abort_alarmcode = AlarmCodes.None;
                await Task.Delay(10);
                if (this.action != ACTION_TYPE.None)
                {
                    Agv.SetSub_Status(SUB_STATUS.RUN);
                    BuzzerPlayMusic(action);
                }
                TaskCancelCTS = new CancellationTokenSource();
                DirectionLighterSwitchBeforeTaskExecute();
                if (!await LaserSettingBeforeTaskExecute())
                {
                    return new List<AlarmCodes> { AlarmCodes.Laser_Mode_value_fail };
                }
                Agv.FeedbackTaskStatus(action == ACTION_TYPE.None ? TASK_RUN_STATUS.NAVIGATING : TASK_RUN_STATUS.ACTION_START);
                (bool confirm, AlarmCodes alarm_code) checkResult = await BeforeTaskExecuteActions();
                if (!checkResult.confirm)
                {
                    return new List<AlarmCodes> { checkResult.alarm_code };
                }
                await Task.Delay(10);
                logger.Trace($"Do Order_ {RunningTaskData.Task_Name}:" +
                    $"\r\nAction:{action}" +
                    $"\r\n起始角度{RunningTaskData.ExecutingTrajecory.First().Theta}, 終點角度 {RunningTaskData.ExecutingTrajecory.Last().Theta}" +
                    $"\r\nHeight:{RunningTaskData.Height}", false);

                if (ForkLifter != null && !Agv.Parameters.LDULD_Task_No_Entry)
                {
                    (bool success, List<AlarmCodes> alarm_codes) forkActionsResult = await ForkLiftActionWhenTaskStart(height, action);
                    if (!forkActionsResult.success)
                    {
                        return forkActionsResult.alarm_codes;
                    }
                }
                if (AGVCActionStatusChaged != null)
                {
                    logger.Warn($"車控 AGVCActionStatusChaged event 註冊狀態未清空=>自動清空");
                    AGVCActionStatusChaged = null;
                }

                if (Agv.GetSub_Status() == SUB_STATUS.DOWN)
                {
                    logger.Error($"車載狀態錯誤:{Agv.GetSub_Status()}");
                    var _task_abort_alarmcode = IsNeedHandshake ? AlarmCodes.Handshake_Fail_AGV_DOWN : AlarmCodes.AGV_State_Cant_do_this_Action;

                    return new List<AlarmCodes> { IsNeedHandshake ? AlarmCodes.Handshake_Fail_AGV_DOWN : _task_abort_alarmcode };
                }

                SendActionCheckResult agvc_response = new SendActionCheckResult(SendActionCheckResult.SEND_ACTION_GOAL_CONFIRM_RESULT.Confirming);
                //await Agv.WagoDO.SetState(DO_ITEM.Horizon_Motor_Free, true);
                if ((action == ACTION_TYPE.Load || action == ACTION_TYPE.Unload) && Agv.Parameters.LDULD_Task_No_Entry)
                {
                    logger.Trace("空取空放!");
                    agvc_response = new SendActionCheckResult(SendActionCheckResult.SEND_ACTION_GOAL_CONFIRM_RESULT.LD_ULD_SIMULATION);
                    if (Agv.Parameters.AgvType == AGV_TYPE.SUBMERGED_SHIELD || Agv.Parameters.AgvType == AGV_TYPE.FORK)
                    {
                        SubmarinAGV? _agv = (Agv as SubmarinAGV);
                        CARGO_STATUS _cargo_status_simulation = action == ACTION_TYPE.Load ? CARGO_STATUS.NO_CARGO : CARGO_STATUS.HAS_CARGO_NORMAL;
                        string _cst_id = action == ACTION_TYPE.Load ? "" : RunningTaskData.CST.FirstOrDefault() == null ? "" : RunningTaskData.CST.First().CST_ID;//取貨[Unload]需模擬拍照=>從派車任務中拿CSTID
                        logger.Trace($"空取空放-貨物在席狀態模擬-{_cargo_status_simulation},CST ID= {_cst_id}");

                        _agv.simulation_cargo_status = _cargo_status_simulation;
                        _agv.CSTReader.ValidCSTID = _cst_id;
                    }
                    await Task.Delay(2000);
                    Agv.SetSub_Status(SUB_STATUS.IDLE);
                    logger.Info($"AGV完成任務[空取空放]---[{action}=>{task_abort_alarmcode}.]");
                    return new List<AlarmCodes>();

                }
                else
                {
                    agvc_response = await TransferTaskToAGVC();
                    if (!agvc_response.Accept)
                    {
                        bool _is_agvs_task_cancel_req_raised = agvc_response.ResultCode == SendActionCheckResult.SEND_ACTION_GOAL_CONFIRM_RESULT.AGVS_CANCEL_TASK_REQ_RAISED;
                        return _is_agvs_task_cancel_req_raised ? new List<AlarmCodes>() { AlarmCodes.Send_Goal_to_AGV_But_AGVS_Cancel_Req_Raised } : new List<AlarmCodes> { AlarmCodes.Can_not_Pass_Task_to_Motion_Control };
                    }
                    else
                    {
                        if (task_abort_alarmcode != AlarmCodes.None)
                            return new List<AlarmCodes> { task_abort_alarmcode };

                        await Agv.Laser.AllLaserDisable();
                        await Task.Delay(100);
                        await LaserSettingBeforeTaskExecute();

                        _wait_agvc_action_done_pause.Reset();

                        if (Agv.AGVC.ActionStatus == ActionStatus.SUCCEEDED)
                        {
                            HandleAGVActionChanged(ActionStatus.SUCCEEDED);
                        }
                        else if (Agv.AGVC.IsRunning)
                        {
                            if (action == ACTION_TYPE.Load || action == ACTION_TYPE.Unload)
                            {
                                #region 前方障礙物預檢
                                var _triggerLevelOfOBSDetected = Agv.Parameters.LOAD_OBS_DETECTION.AlarmLevelWhenTrigger;
                                bool isNoObstacle = StartFrontendObstcleDetection(_triggerLevelOfOBSDetected);
                                if (!isNoObstacle)
                                    if (_triggerLevelOfOBSDetected == ALARM_LEVEL.ALARM)
                                        return new List<AlarmCodes> { FrontendSecondarSensorTriggerAlarmCode };
                                    else
                                        AlarmManager.AddWarning(FrontendSecondarSensorTriggerAlarmCode);
                                #endregion
                            }
                            AGVCActionStatusChaged += HandleAGVActionChanged;
                            await Agv.AGVC.CarSpeedControl(ROBOT_CONTROL_CMD.SPEED_Reconvery, SPEED_CONTROL_REQ_MOMENT.NEW_TASK_START_EXECUTING, false);
                        }
                        await WaitTaskDoneAsync();
                        AGVCActionStatusChaged -= HandleAGVActionChanged;
                        return new List<AlarmCodes>() { task_abort_alarmcode };
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex.Message, ex);
                return new List<AlarmCodes>() { AlarmCodes.Code_Error_In_System };
            }

        }

        protected virtual async Task WaitTaskDoneAsync()
        {
            logger.Trace($"等待AGV完成 [{action}] 任務");
            _wait_agvc_action_done_pause.WaitOne();
            logger.Trace($"AGV完成 [{action}] 任務 ,Alarm Code:=>{task_abort_alarmcode}.]");
        }

        private async Task<(bool success, List<AlarmCodes> alarm_codes)> ForkLiftActionWhenTaskStart(int Height, ACTION_TYPE action)
        {
            List<Task> tasks = new List<Task>();
            List<AlarmCodes> alarmCodes = new List<AlarmCodes>();
            if (ForkLifter.CurrentForkARMLocation != clsForkLifter.FORK_ARM_LOCATIONS.HOME)
            {
                tasks.Add(Task.Run(async () =>
                {
                    Agv.HandshakeStatusText = "AGV動作中-牙叉縮回";
                    await ForkLifter.ForkShortenInAsync();
                }));

            }

            if (action == ACTION_TYPE.None) //一般走行任務
            {
                tasks.Add(Task.Run(async () =>
                {
                    logger.Warn($"一般走行任務-牙叉回HOME");

                    (bool confirm, AlarmCodes alarm_code) forkGoHomeResult = (false, AlarmCodes.Fork_Action_Aborted);
                    if (Agv.Parameters.ForkAGV.HomePoseUseStandyPose)
                    {
                        (bool confirm, string message) = await ForkLifter.ForkPose(Agv.Parameters.ForkAGV.StandbyPose, 1, true);
                        forkGoHomeResult.confirm = confirm;
                        forkGoHomeResult.alarm_code = confirm ? AlarmCodes.None : AlarmCodes.Fork_Action_Aborted;
                    }
                    else
                        forkGoHomeResult = await ForkLifter.ForkGoHome();


                    if (!forkGoHomeResult.confirm)
                        alarmCodes.Add(forkGoHomeResult.alarm_code);
                    else
                        logger.Warn($"一般走行任務-牙叉回HOME-牙叉已位於安全位置({ForkLifter.CurrentHeightPosition} cm)");
                }));
            }
            else if (action == ACTION_TYPE.Charge || action == ACTION_TYPE.Park || action == ACTION_TYPE.Load || action == ACTION_TYPE.Unload || action == ACTION_TYPE.LoadAndPark)
            {
                tasks.Add(Task.Run(async () =>
                {
                    Agv.HandshakeStatusText = "AGV牙叉動作中";
                    logger.Warn($"取貨、放貨、充電任務-牙叉升至設定高度");

                    var _position = CargoTransferMode == CARGO_TRANSFER_MODE.AGV_Pick_and_Place ? (action == ACTION_TYPE.Load ? FORK_HEIGHT_POSITION.UP_ : FORK_HEIGHT_POSITION.DOWN_) : FORK_HEIGHT_POSITION.DOWN_;

                    var forkGoTeachPositionResult = await ChangeForkPositionInSecondaryPtOfWorkStation(Height, _position);
                    if (!forkGoTeachPositionResult.success)
                        alarmCodes.Add(forkGoTeachPositionResult.alarm_code);
                    else
                    {
                        ExpectedForkPostionWhenEntryWorkStation = forkGoTeachPositionResult.position;
                        logger.Warn($"取貨、放貨、充電任務-牙叉升至設定高度({ExpectedForkPostionWhenEntryWorkStation})-牙叉已升至{ForkLifter.CurrentHeightPosition} cm");
                    }
                }));

                //Pin Release
                if (PinHardware != null)
                {
                    tasks.Add(Task.Run(async () =>
                    {
                        await PinHardware.Release();
                    }));
                }
            }

            if (action != ACTION_TYPE.None)
            {
                logger.Info($"等待牙叉動作(動作數:{tasks.Count})...");
                Task.WaitAll(tasks.ToArray());
                logger.Info($"牙叉動作完成(動作數:{tasks.Count}),異常:{alarmCodes.Count}");
            }
            return (alarmCodes.Count == 0, alarmCodes);
        }
        protected virtual void BuzzerPlayMusic(ACTION_TYPE action)
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
        protected virtual async Task<SendActionCheckResult> TransferTaskToAGVC()
        {
            bool IsForkAction()
            {
                return action == ACTION_TYPE.Load || action == ACTION_TYPE.Unload || action == ACTION_TYPE.Charge || action == ACTION_TYPE.LoadAndPark || action == ACTION_TYPE.Park;
            }
            if (Agv.Parameters.AgvType == AGV_TYPE.FORK && IsForkAction())
            {
                _ = ForkPositionSaftyMonitor();
            }
            return await Agv.AGVC.ExecuteTaskDownloaded(RunningTaskData, Agv.Parameters.ActionTimeout);
        }


        private async Task ForkPositionSaftyMonitor()
        {
            await Task.Delay(1);
            logger.Warn($"Start Monitor Fork Position before AGV Reach in WorkStation(Fork Position should be : {ExpectedForkPostionWhenEntryWorkStation})");
            while (Agv.BarcodeReader.CurrentTag != this.RunningTaskData.Homing_Trajectory.Last().Point_ID)
            {
                await Task.Delay(1);
                if (Agv.GetSub_Status() == SUB_STATUS.DOWN)
                    return;

                double _error = 0;
                if ((_error = _ForkHeightErrorWithExpected()) > 0.5)
                {
                    AlarmManager.AddAlarm(AlarmCodes.Fork_Height_Setting_Error, false);
                    logger.Error($"Fork Position Incorrect. Error={_error}cm ({Agv.ForkLifter.CurrentHeightPosition}/{ExpectedForkPostionWhenEntryWorkStation})");
                    return;
                }
            }
            logger.Info($"Fork Position Monitor done=>Safe");

            double _ForkHeightErrorWithExpected()
            {
                return Math.Abs(Agv.ForkLifter.CurrentHeightPosition - ExpectedForkPostionWhenEntryWorkStation);
            }
        }

        protected bool IsAGVCActionNoOperate(ActionStatus status)
        {
            bool isNoOperate = status == ActionStatus.RECALLING || status == ActionStatus.REJECTED || status == ActionStatus.PREEMPTING || status == ActionStatus.PREEMPTED || status == ActionStatus.ABORTED;
            return isNoOperate;
        }
        protected ManualResetEvent _wait_agvc_action_done_pause = new ManualResetEvent(false);
        protected async void HandleAGVActionChanged(ActionStatus status)
        {
            _ = Task.Factory.StartNew(async () =>
            {
                logger.Warn($"[AGVC Action Status Changed-ON-Action Actived][{RunningTaskData.Task_Simplex} -{action}] AGVC Action Status Changed: {status}.");
                if (IsAGVCActionNoOperate(status) || Agv.GetSub_Status() == SUB_STATUS.DOWN)
                {
                    if (Agv.AGVSResetCmdFlag)
                    {
                        Agv.AGV_Reset_Flag = true;
                    }
                    AGVCActionStatusChaged = null;
                    task_abort_alarmcode = IsNeedHandshake ? AlarmCodes.Handshake_Fail_AGV_DOWN : AlarmCodes.AGV_State_Cant_do_this_Action;
                    _wait_agvc_action_done_pause.Set();
                    return;
                }
                if (Agv.IsCargoBiasTrigger && Agv.Parameters.CargoBiasDetectionWhenNormalMoving && !Agv.Parameters.LDULD_Task_No_Entry)
                {
                    AGVCActionStatusChaged = null;
                    logger.Warn($"存在貨物傾倒異常");
                    Agv.IsCargoBiasTrigger = Agv.IsCargoBiasDetecting = false;
                    Agv.SetSub_Status(SUB_STATUS.DOWN);
                    task_abort_alarmcode = AlarmCodes.Cst_Slope_Error;
                    _wait_agvc_action_done_pause.Set();
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
                        //因為
                    }
                    AGVCActionStatusChaged = null;

                    if (Agv.GetSub_Status() == SUB_STATUS.DOWN)
                    {
                        var _task_abort_alarmcode = IsNeedHandshake ? AlarmCodes.Handshake_Fail_AGV_DOWN : AlarmCodes.AGV_State_Cant_do_this_Action;
                        task_abort_alarmcode = IsNeedHandshake ? AlarmCodes.Handshake_Fail_AGV_DOWN : _task_abort_alarmcode;
                        _wait_agvc_action_done_pause.Set();
                        return;
                    }
                    //logger.Info($"[{_RunningTaskData.Action_Type}] Tag-[{Agv.BarcodeReader.CurrentTag}] AGVC Action Status is success, {(_RunningTaskData.Action_Type != ACTION_TYPE.None ? $"Do Action in/out of Station defined!" : "Park done")}");
                    Agv.DirectionLighter.CloseAll();
                    Agv.lastParkingAccuracy = StoreParkingAccuracy();
                    (bool success, AlarmCodes alarmCode) result = await HandleAGVCActionSucceess();
                    task_abort_alarmcode = result.success ? AlarmCodes.None : result.alarmCode;
                    logger.Trace($"HandleAGVCActionSucceess result => {task_abort_alarmcode}");

                    _wait_agvc_action_done_pause.Set();
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

        internal virtual async Task<(bool success, AlarmCodes alarmCode)> HandleAGVCActionSucceess()
        {
            await Task.Delay(10);
            return (true, AlarmCodes.None);
        }


        /// <summary>
        /// 執行任務前動作
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
        public abstract Task<bool> LaserSettingBeforeTaskExecute();

        internal virtual void Abort(AlarmCodes alarm_code = AlarmCodes.None)
        {
            task_abort_alarmcode = alarm_code;
            AGVCActionStatusChaged = null;
            TaskCancelCTS.Cancel();
            _wait_agvc_action_done_pause.Set();
        }
        protected AlarmCodes ForkGoHomeResultAlarmCode = AlarmCodes.None;
        protected async Task ForkHomeProcess(bool need_reach_secondary = true)
        {
            if (ForkLifter == null)
            {
                IsNeedWaitForkHome = false;
                return;
            }
            //check StationType of AGV.lastVisitedMapPoint should be Normal

            IsNeedWaitForkHome = action == ACTION_TYPE.Unload;
            forkGoHomeTask = await Task.Factory.StartNew(async () =>
            {
                if (PinHardware != null)
                {
                    PinHardware.Lock();
                }
                if (need_reach_secondary)
                {
                    logger.Trace($"Wait Reach Tag {RunningTaskData.Destination}, Fork Will Start Go Home.");

                    while (Agv.BarcodeReader.CurrentTag != RunningTaskData.Destination)
                    {
                        await Task.Delay(1);
                        if (Agv.GetSub_Status() == SUB_STATUS.DOWN)
                        {
                            IsNeedWaitForkHome = false;
                            return;
                        }
                    }
                }
                logger.Trace($"Reach Tag {RunningTaskData.Destination}!, Fork Start Go Home NOW!!!");
                (bool confirm, AlarmCodes alarm_code) ForkGoHomeActionResult = (Agv.ForkLifter.CurrentForkLocation == FORK_LOCATIONS.HOME, AlarmCodes.None);
                await Agv.Laser.SideLasersEnable(true);
                var _safty_height = Agv.Parameters.ForkAGV.SaftyPositionHeight;
                bool isCurrentHightAboveSaftyH() => Agv.ForkLifter.CurrentHeightPosition > _safty_height;
                bool _needWaitPoseReach = action != ACTION_TYPE.Load;
                if (isCurrentHightAboveSaftyH())
                {
                    while (isCurrentHightAboveSaftyH())
                    {
                        await Task.Delay(1);
                        if (Agv.GetSub_Status() == SUB_STATUS.DOWN)
                        {
                            IsNeedWaitForkHome = false;
                            return;
                        }
                        if (Agv.Parameters.ForkAGV.HomePoseUseStandyPose)
                        {
                            (bool confirm, string message) _goStandyPoseResult = await ForkLifter.ForkPose(Agv.Parameters.ForkAGV.StandbyPose, 1, _needWaitPoseReach);
                            ForkGoHomeActionResult.confirm = _goStandyPoseResult.confirm;
                        }
                        else
                            ForkGoHomeActionResult = await ForkLifter.ForkGoHome(wait_done: _needWaitPoseReach);
                        logger.Trace($"[Fork Home Process At Secondary]ForkHome Confirm= {ForkGoHomeActionResult.confirm}/Z-Axis Position={Agv.ForkLifter.CurrentForkLocation}");

                        if (ForkGoHomeActionResult.confirm && !_needWaitPoseReach)
                        {
                            logger.Warn("放貨任務牙叉開始下降後就允許射後不理");
                            break; //放貨任務牙叉開始下降後就允許射後不理
                        }
                    }
                    logger.Trace($"[Fork Home Process At Secondary] Fork position now is Under safty height({_safty_height}cm)");

                }
                else
                {
                    bool isForkPoseInCondition()
                    {
                        if (Agv.Parameters.ForkAGV.HomePoseUseStandyPose)
                        {
                            double diff = Math.Abs(Agv.ForkLifter.CurrentHeightPosition - Agv.Parameters.ForkAGV.StandbyPose);
                            return diff < 0.1;
                        }
                        else
                        {
                            return !(Agv.ForkLifter.CurrentHeightPosition > 0.1);
                        }
                    }
                    while (!isForkPoseInCondition())
                    {
                        await Task.Delay(1);
                        if (Agv.GetSub_Status() == SUB_STATUS.DOWN)
                        {
                            IsNeedWaitForkHome = false;
                            return;
                        }
                        double goPose = Agv.Parameters.ForkAGV.HomePoseUseStandyPose ? Agv.Parameters.ForkAGV.StandbyPose : 0;
                        (bool confirm, string message) go = await ForkLifter.ForkPose(goPose, 1, _needWaitPoseReach);
                        logger.Trace($"[Fork Pose to zero Process At Secondary] ForkPose Confirm= {ForkGoHomeActionResult.confirm}/Z-Axis Position={Agv.ForkLifter.CurrentForkLocation}");

                        if (!_needWaitPoseReach)
                        {
                            logger.Warn("放貨任務牙叉開始下降後就允許射後不理");
                            break; //放貨任務牙叉開始下降後就允許射後不理
                        }
                    }
                    await Task.Delay(200);
                    logger.Trace($"[Fork Home Process At Secondary] Fork position now is almost at home({Agv.ForkLifter.CurrentHeightPosition}cm)");

                }
                ForkGoHomeActionResult.confirm = true;
                await Task.Delay(500);
                await Agv.Laser.SideLasersEnable(false);
                if (!ForkGoHomeActionResult.confirm)
                {
                    Agv.SetSub_Status(SUB_STATUS.DOWN);
                    ForkGoHomeResultAlarmCode = ForkGoHomeActionResult.alarm_code;
                    AlarmManager.AddAlarm(ForkGoHomeResultAlarmCode, false);
                }
            });

        }

        public async Task<(double position, bool success, AlarmCodes alarm_code)> ChangeForkPositionInSecondaryPtOfWorkStation(int Height, FORK_HEIGHT_POSITION position)
        {

            CancellationTokenSource _wait_fork_reach_position_cst = new CancellationTokenSource();
            _ = Task.Run(async () =>
            {
                while (!_wait_fork_reach_position_cst.IsCancellationRequested)
                {
                    await Task.Delay(1);
                    Agv.HandshakeStatusText = $"AGV牙叉動作中({ForkLifter.CurrentHeightPosition} cm)";
                }
            });

            logger.Warn($"Before Go Into Work Station_Tag:{destineTag}, Fork Pose need change to {(position == FORK_HEIGHT_POSITION.UP_ ? "Load Pose" : "Unload Pose")}");
            (double position, bool success, AlarmCodes alarm_code) result = ForkLifter.ForkGoTeachedPoseAsync(destineTag, Height, position, 1).Result;
            _wait_fork_reach_position_cst.Cancel();
            return result;
        }

        /// <summary>
        /// 車頭二次檢Sensor檢察功能
        /// </summary>
        protected virtual bool StartFrontendObstcleDetection(ALARM_LEVEL alarmLevel)
        {
            var options = Agv.Parameters.LOAD_OBS_DETECTION;
            bool Enable = action == ACTION_TYPE.Load ? options.Enable_Load : options.Enable_UnLoad;
            if (!Enable)
                return true;
            if (Agv.IsFrontendSideHasObstacle)
            {
                logger.Error($"前方障礙物預檢知觸發[等級={alarmLevel}]");
                return false;
            }
            if (options.Detection_Method == FRONTEND_OBS_DETECTION_METHOD.BEGIN_ACTION)
            {
                logger.Info($"前方障礙物預檢知Sensor Pass , No Obstacle");
                return true;
            }
            int DetectionTime = options.Duration;
            logger.Warn($"前方障礙物預檢知偵側開始[{options.Detection_Method}]==> 偵測持續時間={DetectionTime} 秒)");
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
                    logger.Error($"前方障礙物預檢知觸發[等級={alarmLevel}](在第 {stopwatch.ElapsedMilliseconds / 1000.0} 秒)");
                    if (alarmLevel == ALARM_LEVEL.ALARM)
                        EMO_STOP_AGV();
                    else
                        AlarmManager.AddWarning(FrontendSecondarSensorTriggerAlarmCode);
                }
            }
            Agv.WagoDI.OnFrontSecondObstacleSensorDetected += FrontendObsSensorDetectAction;

            Task.Run(async () =>
            {
                while (!cancelDetectCTS.IsCancellationRequested)
                {
                    await Task.Delay(1);
                }
                if (!detected)
                {
                    logger.Info($"前方障礙物預檢知Sensor Pass , No Obstacle");
                }
                Agv.WagoDI.OnFrontSecondObstacleSensorDetected -= FrontendObsSensorDetectAction;
            });
            void EMO_STOP_AGV()
            {
                try
                {
                    Agv.AGVC.EmergencyStop();
                    Agv.ExecutingTaskEntity.Abort();
                    Agv.SetSub_Status(SUB_STATUS.DOWN);
                    AlarmManager.AddAlarm(FrontendSecondarSensorTriggerAlarmCode, false);
                }
                catch (Exception ex)
                {
                    throw ex;
                }
            }
            return true;
        }

        protected async virtual Task<(bool success, AlarmCodes alarmCode)> ExitPortRequest()
        {
            if (!Agv.Parameters.LDULDParams.LeaveWorkStationNeedSendRequestToAGVS)
                return (true, AlarmCodes.None);
            if (RunningTaskData.IsLocalTask)
            {
                logger.Info($"注意! Local派工不需詢問派車系統是否可退出設備");
                return (true, AlarmCodes.None);
            }

            bool accept = await WaitAGVSAcceptLeaveWorkStationAsync(Agv.Parameters.LDULDParams.LeaveWorkStationRequestTimeout);
            if (accept)
                return (true, AlarmCodes.None);
            else
            {
                return (false, AlarmCodes.AGVS_Leave_Workstation_Response_Timeout);
            }
        }
        protected virtual int ExitPointTag => 0;

        private async Task<bool> WaitAGVSAcceptLeaveWorkStationAsync(int timeout = 300)
        {
            try
            {
                bool accept = false;
                Agv.HandshakeStatusText = $"等待派車允許AGV退出設備..";
                using CancellationTokenSource cancelCts = new CancellationTokenSource(TimeSpan.FromSeconds(timeout));
                Stopwatch stopwatch = Stopwatch.StartNew();
                Agv.DirectionLighter.WaitPassLights(300);
                while (!accept)
                {
                    await Task.Delay(1000);
                    if (cancelCts.IsCancellationRequested)
                        break;
                    if (Agv.GetSub_Status() == SUB_STATUS.DOWN)
                        return false;

                    if (Agv.Parameters.VMSParam.Protocol == Vehicle.VMS_PROTOCOL.GPM_VMS)
                    {
                        accept = await Agv.AGVS.LeaveWorkStationRequest(Agv.Parameters.VehicleName, (int)Agv.BarcodeReader.Data.tagID);
                        Agv.HandshakeStatusText = $"等待派車允許AGV退出設備-{stopwatch.Elapsed}";
                    }
                    else
                    {
                        accept = await Agv.AGVS.Exist_Request(ExitPointTag);
                        Agv.HandshakeStatusText = $"等待派車允許AGV退出至Tag-{ExitPointTag}-{stopwatch.Elapsed}";
                    }
                }

                return accept;
            }
            catch (Exception ex)
            {
                return false;
            }
            finally
            {
                Agv.DirectionLighter.AbortFlash();
            }

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
