using AGVSystemCommonNet6.Vehicle_Control.VCS_ALARM;
using GPMVehicleControlSystem.Models.VehicleControl.AGVControl;
using GPMVehicleControlSystem.Models.VehicleControl.VehicleComponent.Forks;
using GPMVehicleControlSystem.Models.VehicleControl.Vehicles;
using GPMVehicleControlSystem.Models.WorkStation;
using GPMVehicleControlSystem.VehicleControl.DIOModule;
using System.Diagnostics;
using static AGVSystemCommonNet6.clsEnums;
using static GPMVehicleControlSystem.VehicleControl.DIOModule.clsDIModule;
using static GPMVehicleControlSystem.VehicleControl.DIOModule.clsDOModule;
using static SQLite.SQLite3;

namespace GPMVehicleControlSystem.Models.VehicleControl.VehicleComponent.Forks
{
    public partial class clsForkLifter : CarComponent, IDIOUsagable
    {
        public enum FORK_LOCATIONS
        {
            UP_HARDWARE_LIMIT,
            UP_POSE,
            HOME,
            DOWN_POSE,
            DOWN_HARDWARE_LIMIT,
            UNKNOWN
        }

        /// <summary>
        /// 牙叉伸縮位置
        /// </summary>
        public enum FORK_ARM_LOCATIONS
        {
            HOME,
            END,
            UNKNOWN
        }

        public clsForkLifter(ForkAGV forkAGV)
        {
            this.forkAGV = forkAGV;
        }

        public double CurrentHeightPosition => Math.Round(Driver.CurrentPosition, 3);

        public bool IsStopByObstacleDetected = false;

        public FORK_LOCATIONS CurrentForkLocation
        {
            //TODO 
            get
            {
                if (DIModule.GetState(DI_ITEM.Vertical_Home_Pos))
                    return FORK_LOCATIONS.HOME;
                else if (!DIModule.GetState(DI_ITEM.Vertical_Up_Hardware_limit))
                    return FORK_LOCATIONS.UP_HARDWARE_LIMIT;
                else if (DIModule.GetState(DI_ITEM.Vertical_Up_Pose))
                    return FORK_LOCATIONS.UP_POSE;
                else if (!DIModule.GetState(DI_ITEM.Vertical_Down_Hardware_limit))
                    return FORK_LOCATIONS.DOWN_HARDWARE_LIMIT;
                else if (DIModule.GetState(DI_ITEM.Vertical_Down_Pose))
                    return FORK_LOCATIONS.DOWN_POSE;
                else return FORK_LOCATIONS.UNKNOWN;
            }
        }
        public virtual FORK_ARM_LOCATIONS CurrentForkARMLocation
        {
            get
            {
                bool IsForkAtHome = !DIModule.GetState(DI_ITEM.Fork_Short_Exist_Sensor);
                bool IsForkAtEnd = !DIModule.GetState(DI_ITEM.Fork_Extend_Exist_Sensor);
                if (IsForkAtHome)
                    return FORK_ARM_LOCATIONS.HOME;
                else if (IsForkAtEnd)
                    return FORK_ARM_LOCATIONS.END;
                else
                    return FORK_ARM_LOCATIONS.UNKNOWN;
            }
        }

        public virtual bool IsForkArmExtendLocationCorrect
        {
            get
            {
                return CurrentForkARMLocation == FORK_ARM_LOCATIONS.END;
            }
        }

        public virtual bool IsForkArmShortLocationCorrect
        {
            get
            {
                return CurrentForkARMLocation == FORK_ARM_LOCATIONS.HOME;
            }
        }

        private SUB_STATUS Sub_Status = SUB_STATUS.IDLE;
        /// <summary>
        /// 是否以初始化
        /// </summary>
        public bool IsVerticalForkInitialized { get; internal set; } = false;
        public bool IsInitialing { get; private set; } = false;
        /// <summary>
        /// 可以走的上極限位置
        /// </summary>


        public override COMPOENT_NAME component_name => COMPOENT_NAME.VERTIVAL_DRIVER;
        public clsDriver Driver { get; set; }
        public override string alarm_locate_in_name => "FORK";
        public clsDOModule DOModule { get; set; }
        private clsDIModule _DIModule;
        private double InitForkSpeed = 1.0;

        /// <summary>
        /// 是否啟用牙叉功能
        /// </summary>
        internal bool Enable => forkAGV.Parameters.ForkAGV.ForkLifer_Enable;
        public Dictionary<int, clsWorkStationData> StationDatas
        {
            get
            {
                return forkAGV.WorkStations.Stations;
            }
        }

        public clsDIModule DIModule
        {
            get => _DIModule;
            set
            {
                _DIModule = value;
                _DIModule.SubsSignalStateChange(DI_ITEM.Fork_Under_Pressing_Sensor, OnForkLifterSensorsStateChange);
                _DIModule.SubsSignalStateChange(DI_ITEM.Vertical_Down_Hardware_limit, OnForkLifterSensorsStateChange);
                _DIModule.SubsSignalStateChange(DI_ITEM.Vertical_Up_Hardware_limit, OnForkLifterSensorsStateChange);
                _DIModule.SubsSignalStateChange(DI_ITEM.Fork_Short_Exist_Sensor, OnForkLifterSensorsStateChange);
                _DIModule.SubsSignalStateChange(DI_ITEM.Fork_Extend_Exist_Sensor, OnForkLifterSensorsStateChange);

                if (!forkAGV.Parameters.SensorBypass.BeltSensorBypass)
                    _DIModule.SubsSignalStateChange(DI_ITEM.Vertical_Belt_Sensor, OnForkLifterSensorsStateChange);
            }
        }


        private void OnForkLifterSensorsStateChange(object? sender, bool state)
        {
            Task.Factory.StartNew(() =>
            {
                try
                {
                    clsIOSignal? DI = sender as clsIOSignal;
                    if (!state && DI?.Input == DI_ITEM.Fork_Under_Pressing_Sensor)
                        Current_Alarm_Code = AlarmCodes.Fork_Bumper_Error;

                    else if (!state && DI?.Input == DI_ITEM.Vertical_Down_Hardware_limit)
                    {
                        if (!IsInitialing)
                        {
                            fork_ros_controller?.verticalActionService.Stop();
                            Current_Alarm_Code = AlarmCodes.Zaxis_Down_Limit;
                        }
                    }
                    else if (!state && DI?.Input == DI_ITEM.Vertical_Up_Hardware_limit)
                    {
                        if (!IsInitialing)
                        {
                            fork_ros_controller?.verticalActionService.Stop();
                            Current_Alarm_Code = AlarmCodes.Zaxis_Up_Limit;
                        }
                    }
                    else if (!state && DI?.Input == DI_ITEM.Vertical_Belt_Sensor)
                    {
                        if (!DOModule.GetState(DO_ITEM.Vertical_Belt_SensorBypass))
                        {
                            fork_ros_controller?.verticalActionService.Stop();
                            Current_Alarm_Code = AlarmCodes.Belt_Sensor_Error;
                        }
                    }
                    else if (!state && (DI?.Input == DI_ITEM.Fork_Short_Exist_Sensor || DI?.Input == DI_ITEM.Fork_Extend_Exist_Sensor)) //牙叉伸縮極限Sensor
                    {
                        ForkARMStop();
                    }
                }
                catch (Exception ex)
                {
                    logger.Error($"{OnForkLifterSensorsStateChange} code error", ex);
                }
            });
        }

        internal ForkAGVController fork_ros_controller => forkAGV.AGVC as ForkAGVController;

        internal clsForkEarlyMoveUpState EarlyMoveUpState { get; set; } = new clsForkEarlyMoveUpState();

        public class clsForkEarlyMoveUpState
        {
            public bool IsHeightPreSettingActionRunning { get; private set; }
            public double GoalHeight { get; private set; } = 0;
            public void Reset()
            {
                IsHeightPreSettingActionRunning = false;
            }

            public void SetHeightPreSettingActionRunning(double goalHeight)
            {
                IsHeightPreSettingActionRunning = true;
                GoalHeight = goalHeight;
            }


        }

        public bool IsManualOperation { get; internal set; } = false;

        protected ForkAGV forkAGV;

        public override bool CheckStateDataContent()
        {
            return base.CheckStateDataContent();
        }

        /// <summary>
        /// Fork升降動作暫停
        /// </summary>
        /// <param name="IsEMS"></param>
        /// <returns></returns>
        public async Task<(bool confirm, string message)> ForkStopAsync(bool IsEMS = false, bool waitSpeedZero = false)
        {
            if (IsEMS)
            {
                fork_ros_controller.verticalActionService.BeforeStopActionRequesting = new AGVSystemCommonNet6.GPMRosMessageNet.Services.VerticalCommandRequest();
                fork_ros_controller.verticalActionService.wait_action_down_cts.Cancel();
                logger.Trace("Call fork_ros_controller.wait_action_down_cts.Cancel()");
            }
            if (!waitSpeedZero)
                return await fork_ros_controller.verticalActionService.Stop();
            else
            {
                var _result = await fork_ros_controller.verticalActionService.Stop();
                if (!_result.confirm)
                    return _result;

                CancellationTokenSource _cancel = new CancellationTokenSource(TimeSpan.FromSeconds(20));
                while (fork_ros_controller.verticalActionService.driverState.speed != 0)
                {
                    if (_cancel.IsCancellationRequested)
                        return (false, "Wait Fork Vertical Stop->Speed Zero Timeout!");
                    await Task.Delay(100);
                }
                return (true, "");
            }
        }

        /// <summary>
        /// 繼續動作
        /// </summary>
        /// <returns></returns>
        internal async Task<(bool confirm, string message)> ForkResumeAction(AGVSystemCommonNet6.GPMRosMessageNet.Services.VerticalCommandRequest lastVerticalForkActionCmd)
        {
            return await fork_ros_controller.verticalActionService.ZAxisResume(lastVerticalForkActionCmd);
        }


        /// <summary>
        /// 繼續動作
        /// </summary>
        /// <returns></returns>
        internal async Task<(bool confirm, string message)> ForkResumeAction()
        {
            return await fork_ros_controller.verticalActionService.ZAxisResume();
        }

        public async Task<(bool confirm, string message)> ForkPositionInit()
        {
            await Task.Delay(300);
            return await fork_ros_controller.verticalActionService.Init();
        }

        public async Task<(bool confirm, AlarmCodes alarm_code)> ForkGoHome(double speed = 1, bool wait_done = true)
        {
            if (!forkAGV.ZAxisGoHomingCheck())
            {
                return (false, AlarmCodes.Fork_Cannot_Go_Home_At_Non_Normal_Point);
                AlarmManager.AddAlarm(AlarmCodes.Fork_Cannot_Go_Home_At_Non_Normal_Point, false);
            }

            (bool confirm, string message) response = await fork_ros_controller.verticalActionService.Home(speed, wait_done);
            if (!response.confirm)
                return (false, AlarmCodes.Action_Timeout);
            return (true, AlarmCodes.None);
            //if (!DIModule.GetState(DI_ITEM.Vertical_Home_Pos))
            //    return (false, AlarmCodes.Fork_Go_Home_But_Home_Sensor_Signal_Error);
            //else
        }
        public async Task<(bool confirm, string message)> ForkPose(double pose, double speed = 0.1, bool wait_done = true, bool bypassCheck = false, bool invokeActionStart = true)
        {
            if (!bypassCheck)
            {
                if (pose < forkAGV.Parameters.ForkAGV.DownlimitPose)
                    pose = forkAGV.Parameters.ForkAGV.DownlimitPose;
                else if (pose > forkAGV.Parameters.ForkAGV.UplimitPose)
                    pose = forkAGV.Parameters.ForkAGV.UplimitPose;
            }
            return await fork_ros_controller.verticalActionService.Pose(pose, speed, wait_done, startActionInvoke: invokeActionStart);
        }

        public async Task<(bool confirm, string message)> ForkUpSearchAsync(double speed = 0.1)
        {
            return await fork_ros_controller.verticalActionService.UpSearch(speed);
        }

        public async Task<(bool confirm, string message)> ForkDownSearchAsync(double speed = 0.1)
        {
            return await fork_ros_controller.verticalActionService.DownSearch(speed);
        }
        /// <summary>
        /// 牙叉伸出
        /// </summary>
        /// <returns></returns>
        public virtual async Task<(bool confirm, AlarmCodes)> ForkExtendOutAsync(bool wait_reach_end = true)
        {
            bool _checked = await ForkARMStop();
            if (!_checked)
                return (true, AlarmCodes.None);
            if (CurrentForkARMLocation == FORK_ARM_LOCATIONS.END)
                return (true, AlarmCodes.None);

            try
            {
                DO_ITEM _DO_USE_TO_EXTEND_FORK = DO_ITEM.Fork_Shortend;
                CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                await DOModule.SetState(_DO_USE_TO_EXTEND_FORK, true);
                logger.Warn($"Wait Fork Extent DO ON..({_DO_USE_TO_EXTEND_FORK})");
                while (!DOModule.GetState(_DO_USE_TO_EXTEND_FORK))
                {
                    await Task.Delay(1);
                    if (cts.IsCancellationRequested)
                        return (false, AlarmCodes.Fork_Arm_Action_Timeout);
                }

                logger.Warn($"Fork Extent DO ON..!!({_DO_USE_TO_EXTEND_FORK})");
                if (wait_reach_end)
                {
                    cts.TryReset();
                    while (CurrentForkARMLocation != FORK_ARM_LOCATIONS.END)
                    {
                        await Task.Delay(1);
                        bool isStopState = !DOModule.GetState(DO_ITEM.Fork_Extend) && !DOModule.GetState(DO_ITEM.Fork_Shortend);
                        if (isStopState)
                            return (true, AlarmCodes.None);
                        if (!DIModule.GetState(DI_ITEM.Fork_Frontend_Abstacle_Sensor))
                        {
                            ForkARMStop();
                            return (false, AlarmCodes.Fork_Frontend_has_Obstacle);
                        }
                        if (cts.IsCancellationRequested)
                            return (false, AlarmCodes.Fork_Arm_Action_Timeout);
                    }
                }
                return (true, AlarmCodes.None);
            }
            catch (Exception ex)
            {
                return (false, AlarmCodes.Fork_Arm_Action_Error);
            }
        }

        /// <summary>
        /// 牙叉縮回
        /// </summary>
        /// <returns></returns>
        public virtual async Task<(bool confirm, string message)> ForkShortenInAsync(bool wait_reach_home = true, CancellationToken cancellationToken = default)
        {
            try
            {

                ForkARMStop();
                await Task.Delay(400, cancellationToken);
                if (CurrentForkARMLocation == FORK_ARM_LOCATIONS.HOME)
                    return (true, "");
                await DOModule.SetState(DO_ITEM.Fork_Extend, true);
                if (wait_reach_home)
                {
                    CancellationTokenSource cts = new CancellationTokenSource();
                    cts.CancelAfter(TimeSpan.FromSeconds(30));
                    while (CurrentForkARMLocation != FORK_ARM_LOCATIONS.HOME)
                    {
                        await Task.Delay(1, cancellationToken);
                        bool isStopState = !DOModule.GetState(DO_ITEM.Fork_Extend) && !DOModule.GetState(DO_ITEM.Fork_Shortend);

                        if (isStopState)
                            return (true, "");
                        if (cts.IsCancellationRequested)
                            return (false, "Timeout");
                    }
                }
                return (true, "");

            }
            catch (Exception ex)
            {
                return (false, ex.Message); ;
            }
        }

        public virtual async Task<(bool success, string message)> ForkHorizonResetAsync()
        {
            return (true, "");
        }
        /// <summary>
        /// 牙叉伸縮停止動作
        /// </summary>
        /// <returns></returns>
        public virtual async Task<bool> ForkARMStop()
        {
            if (DOModule.VCSOutputs.Any(it => it.Output == DO_ITEM.Fork_Extend))
            {
                await DOModule.SetState(DO_ITEM.Fork_Extend, false);
                await Task.Delay(100);
                await DOModule.SetState(DO_ITEM.Fork_Shortend, false);
                return true;
            }
            else
                return false;
        }


        private enum SEARCH_DIRECTION
        {
            DOWN,
            UP
        }

        /// <summary>
        /// 初始化Fork , 尋找原點
        /// </summary>
        public async Task<(bool done, AlarmCodes alarm_code)> VerticalForkInitialize(double InitForkSpeed = 0.5, CancellationToken token = default)
        {
            logger.Info($"Fork Z軸初始化動作開始，速度={InitForkSpeed}");
            IsInitialing = true;
            bool _isInitializeDone = false;
            (this.forkAGV.AGVC as ForkAGVController).IsInitializing = true;
            fork_ros_controller.verticalActionService.wait_action_down_cts = new CancellationTokenSource();
            try
            {
                VerticalForkHomeSearchHelper vertialForkHomeSearchHelper = new VerticalForkHomeSearchHelper(forkAGV, "Vertical");
                (bool success, AlarmCodes alarmCode) result = await vertialForkHomeSearchHelper.StartSearchAsync(token);
                IsVerticalForkInitialized = CurrentForkLocation == FORK_LOCATIONS.HOME;
                logger.Info($"Fork Initialize Done,Current Position : {Driver.CurrentPosition}_cm");
                return result;
            }
            catch (Exception ex)
            {
                IsInitialing = IsVerticalForkInitialized = false;
                logger.Fatal($"[ForkInitialize] FAIL. {ex.Message}");
                return (false, AlarmCodes.Fork_Initialize_Process_Interupt);
            }
            finally
            {
                DOModule.SetState(DO_ITEM.Vertical_Hardware_limit_bypass, false);
                (this.forkAGV.AGVC as ForkAGVController).IsInitializing = false;
                IsInitialing = false;
            }

        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="tag">工位的TAG</param>
        /// <param name="height">第N層(Zero-base)</param>
        /// <param name="position">該層之上/下位置</param>
        /// <exception cref="NotImplementedException"></exception>
        internal async Task<(double position, bool success, AlarmCodes alarm_code)> ForkGoTeachedPoseAsync(int tag, int height, FORK_HEIGHT_POSITION position, double speed, int timeout = 60, bool bypassFinalCheck = false, bool invokeActionStart = true)
        {
            double target = 0;
            try
            {
                fork_ros_controller.verticalActionService.wait_action_down_cts = new CancellationTokenSource();

                #region 教點數據確認

                if (!StationDatas.TryGetValue(tag, out clsWorkStationData? workStation))
                    return (target, false, AlarmCodes.Fork_WorkStation_Teach_Data_Not_Found_Tag);

                if (!workStation.LayerDatas.TryGetValue(height, out clsStationLayerData? teach))
                    return (target, false, AlarmCodes.Fork_WorkStation_Teach_Data_Not_Found_layer);

                if (teach.Down_Pose == 0 && teach.Up_Pose == 0)
                    return (target, false, AlarmCodes.Fork_Slot_Teach_Data_ERROR);

                #endregion

                #region 從教點數據中取出目標位置

                if (position == FORK_HEIGHT_POSITION.UP_)
                    target = teach.Up_Pose;
                if (position == FORK_HEIGHT_POSITION.DOWN_)
                    target = teach.Down_Pose;
                #endregion

                bool _isForkAlreadyGoingToTarget = EarlyMoveUpState.IsHeightPreSettingActionRunning && EarlyMoveUpState.GoalHeight == target && Driver.Data.speed != 0;

                if (forkAGV.Navigation.LastVisitedTag % 2 != 0)
                    forkAGV.LogDebugMessage($"設備/WIP進入前上升牙叉,牙叉 {(_isForkAlreadyGoingToTarget ? "已提前動作中" : "準備上升")}", true);

                double _errorTorlence = 0.1;
                CancellationTokenSource _waitPoseReachTargetCancellationTokenSource = new CancellationTokenSource();
                Task actionTimeoutDetectTask = Task.Run(async () =>
                {
                    try
                    {
                        Stopwatch sw = Stopwatch.StartNew();
                        while (sw.Elapsed.TotalSeconds < timeout)
                        {
                            forkAGV.HandshakeStatusText = $"等待牙叉移動至設定高度...({CurrentHeightPosition}/{target})..{sw.Elapsed.ToString(@"mm\:ss")}";
                            if (IsStopByObstacleDetected)
                                sw.Stop();
                            else
                                sw.Start();
                            await Task.Delay(100, _waitPoseReachTargetCancellationTokenSource.Token);
                        }
                    }
                    catch (TaskCanceledException)
                    {
                        logger.Trace("Fork Move Timeout Detect Task Cancelled");
                    }
                });
                Task<(bool confirm, string message, double positionError)> poseActionTask = Task.Run(async () =>
                {
                    EarlyMoveUpState.Reset();
                    (bool confirm, string message) forkMoveResult = (false, "");
                    ForkPositionLargeThanTorrlence(CurrentHeightPosition, target, _errorTorlence, out double positionError);
                    if (!_isForkAlreadyGoingToTarget)
                    {
                        await ForkStopAsync(waitSpeedZero: true);
                        await Task.Delay(500);
                        logger.Warn($"Fork Start Goto Height={height},Position={target}(Current Position={Driver.CurrentPosition}cm) at Tag:{tag}.[{position}]");
                        forkMoveResult = await ForkPose(target, speed, false, invokeActionStart: invokeActionStart);
                        if (!forkMoveResult.confirm)
                            return (forkMoveResult.confirm, forkMoveResult.message, 0);
                    }
                    else
                        logger.Info($"Fork Already Going to Target Position={target} cm,Just Waiting reach aim");


                    while (ForkPositionLargeThanTorrlence(CurrentHeightPosition, target, _errorTorlence, out positionError))
                    {
                        try
                        {
                            await Task.Delay(1, _waitPoseReachTargetCancellationTokenSource.Token);
                            if (AGVStatusError())
                            {
                                throw new TaskCanceledException($"因AGV狀態異常取消等待");
                            }
                        }
                        catch (TaskCanceledException)
                        {
                            ForkStopAsync();
                            return (false, "取消等待", positionError);
                        }
                    }
                    return (true, "Fork Move Done", positionError);
                });

                Task taskEnd = await Task.WhenAny(actionTimeoutDetectTask, poseActionTask);

                if (taskEnd == actionTimeoutDetectTask)
                {
                    ForkStopAsync();
                    _waitPoseReachTargetCancellationTokenSource.Cancel();
                    logger.Error($"Fork Move Timeout, 牙叉當前位置={CurrentHeightPosition} cm, 目標位置={target} cm");
                    return (target, false, AlarmCodes.Action_Timeout);
                }
                else
                {
                    _waitPoseReachTargetCancellationTokenSource.Cancel();
                    (bool confirm, string message, double positionError) result = poseActionTask.Result;
                    if (!result.confirm)
                    {
                        logger.Error($"Fork Move Error, 牙叉當前位置={CurrentHeightPosition} cm, 目標位置={target} cm");
                        return (target, false, AlarmCodes.Fork_Arm_Action_Error);
                    }

                    if (!bypassFinalCheck && ForkPositionLargeThanTorrlence(CurrentHeightPosition, target, _errorTorlence, out _))
                        return (target, false, AlarmCodes.Fork_Height_Setting_Error);

                    return (target, true, AlarmCodes.None);
                }
                #region Local Functions
                //計算牙叉當前位置距離目標位置的誤差值
                double GetPositionErrorVal(double currentHeightPosition, double position_to_reach)
                {
                    return Math.Abs(currentHeightPosition - position_to_reach);
                }
                //計算牙叉當前位置距離目標位置的誤差值是否在允許範圍內
                bool ForkPositionLargeThanTorrlence(double currentHeightPosition, double position_to_reach, double errorTorlence, out double positionError)
                {
                    positionError = GetPositionErrorVal(currentHeightPosition, position_to_reach);
                    if (fork_ros_controller.verticalActionService.driverState.speed != 0)
                        return true;
                    return positionError > errorTorlence;
                }
                //皮帶是否有異常
                bool AGVStatusError() => forkAGV.GetSub_Status() == SUB_STATUS.DOWN || forkAGV.GetSub_Status() == SUB_STATUS.Initializing;
                #endregion
            }
            catch (TaskCanceledException ex)
            {
                logger.Error(ex);
                return (target, false, AlarmCodes.Fork_Action_Aborted);
            }
            catch (OperationCanceledException ex)
            {
                logger.Error(ex);
                return (target, false, AlarmCodes.Fork_Action_Aborted);
            }
            catch (Exception ex)
            {
                logger.Fatal(ex);
                return (target, false, AlarmCodes.Code_Error_In_System);
            }

        }

    }
}
