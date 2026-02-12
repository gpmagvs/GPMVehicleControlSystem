using AGVSystemCommonNet6;
using AGVSystemCommonNet6.AGVDispatch.Messages;
using AGVSystemCommonNet6.Alarm;
using AGVSystemCommonNet6.GPMRosMessageNet.Services;
using AGVSystemCommonNet6.Vehicle_Control.VCS_ALARM;
using GPMVehicleControlSystem.Models.Buzzer;
using GPMVehicleControlSystem.Models.TaskExecute;
using GPMVehicleControlSystem.Models.VehicleControl.AGVControl;
using GPMVehicleControlSystem.Models.VehicleControl.AGVControl.ForkServices;
using GPMVehicleControlSystem.Models.VehicleControl.VehicleComponent;
using GPMVehicleControlSystem.Models.VehicleControl.VehicleComponent.Forks;
using GPMVehicleControlSystem.Models.VehicleControl.Vehicles.Params;
using GPMVehicleControlSystem.Models.WorkStation;
using GPMVehicleControlSystem.Service;
using GPMVehicleControlSystem.VehicleControl.DIOModule;
using Newtonsoft.Json;
using RosSharp.RosBridgeClient.Actionlib;
using System.Diagnostics;
using static AGVSystemCommonNet6.clsEnums;
using static AGVSystemCommonNet6.MAP.MapPoint;
using static GPMVehicleControlSystem.Models.VehicleControl.AGVControl.ForkServices.ForkActionServiceBase;
using static GPMVehicleControlSystem.Models.VehicleControl.VehicleComponent.Forks.clsForkLifter;
using static GPMVehicleControlSystem.VehicleControl.DIOModule.clsDIModule;
using static GPMVehicleControlSystem.VehicleControl.DIOModule.clsDOModule;

namespace GPMVehicleControlSystem.Models.VehicleControl.Vehicles
{
    /// <summary>
    /// 叉車AGV
    /// </summary>
    public partial class ForkAGV : SubmarinAGV
    {

        public enum FORK_SAFE_STRATEGY
        {
            AT_HOME_POSITION,
            UNDER_SAFTY_POSITION
        }
        public bool IsVerticalForkInitialized => ForkLifter.IsVerticalForkInitialized;
        public bool IsForkWorking => (AGVC as ForkAGVController).verticalActionService.WaitActionDoneFlag;

        public override clsWorkStationModel WorkStations { get; set; } = new clsWorkStationModel();
        public override clsForkLifter ForkLifter { get; set; }

        public clsPin PinHardware { get; set; }

        public bool IsForkHorizonDriverBase => WagoDI.Indexs.TryGetValue(DI_ITEM.Fork_Home_Pose, out _);
        public ForkAGV(clsVehicelParam param, VehicleServiceAggregator vehicleServiceAggregator) : base(param, vehicleServiceAggregator)
        {

        }

        protected override List<clsDriver> driverList
        {
            get
            {
                var list = base.driverList;
                list.Add(VerticalDriverState);
                return list;
            }
        }
        private bool _isForkInitBypass = false;
        public async Task<(bool confirm, string message)> Initialize(bool isForkInitBypass)
        {
            _isForkInitBypass = isForkInitBypass;
            return await base.Initialize();
        }

        internal override async Task CreateAsync()
        {
            await base.CreateAsync();
            ForkLifter = IsForkHorizonDriverBase ? new clsForkLifterWithDriverBaseExtener(this) : new clsForkLifter(this);
            ForkLifter.Driver = VerticalDriverState;
            ForkLifter.DIModule = WagoDI;
            ForkLifter.DOModule = WagoDO;
            if (Parameters.ForkAGV.IsPinMounted)
            {
                bool _isIOBase = WagoDO.VCSOutputs.FirstOrDefault(o => o.Output == DO_ITEM.Fork_Floating) != null;
                PinHardware = _isIOBase ? new clsPinIOBase(WagoDO) : new clsPin();
            }

            logger.LogInformation($"FORK AGV 搭載Pin模組?{PinHardware != null}");
        }
        protected internal override async Task InitAGVControl(string RosBridge_IP, int RosBridge_Port)
        {
            await base.InitAGVControl(RosBridge_IP, RosBridge_Port);
            if (PinHardware != null)
                PinHardware.rosSocket = AGVC.rosSocket;

            ForkAGVController forkAGVC = (AGVC as ForkAGVController);
            forkAGVC.verticalActionService = new VerticalForkActionService(this, AGVC.rosSocket);
            forkAGVC.HorizonActionService = new HorizonForkActionService(this, AGVC.rosSocket);
            forkAGVC.verticalActionService.AdertiseRequiredService();
            forkAGVC.HorizonActionService.AdertiseRequiredService();

        }

        public async Task<bool> ResetVerticalDriver()
        {
            try
            {
                await WagoDO.SetState(DO_ITEM.Vertical_Motor_Reset, true).ContinueWith(async t =>
                {
                    await WagoDO.SetState(DO_ITEM.Vertical_Motor_Reset, false);
                    await WagoDO.SetState(DO_ITEM.Vertical_Motor_Stop, false);
                });
                return true;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, ex.Message);
                return false;
            }
        }
        public override async Task<bool> ResetMotor(bool triggerByResetButtonPush)
        {
            try
            {
                await base.ResetMotor(triggerByResetButtonPush);
                return await ResetVerticalDriver();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, ex.Message);
                return false;
            }
        }
        protected override void CommonEventsRegist()
        {
            base.CommonEventsRegist();
            var _fork_car_controller = (AGVC as ForkAGVController);
            _fork_car_controller.verticalActionService.OnForkStartGoHome += () => { return Parameters.ForkAGV.SaftyPositionHeight; };
            _fork_car_controller.verticalActionService.OnActionStart += _fork_car_controller_OnForkStartMove;
            _fork_car_controller.verticalActionService.BeforeActionStart += VerticalActionService_BeforeActionStart;
            ForkLifter.BeforeForkArmAction -= ForkLifter_BeforeForkArmAction;
            ForkLifter.BeforeForkArmAction += ForkLifter_BeforeForkArmAction;
            ForkLifter.Driver.OnAlarmHappened += async (alarm_code) =>
            {
                if (alarm_code != AlarmCodes.None)
                {
                    if (StaSysControl.isAGVCRestarting)
                        return false;
                    Task<bool> state = await Task.Factory.StartNew(async () =>
                    {
                        await Task.Delay(1000);
                        if (IsEmoTrigger)
                            return false;
                        bool isResetAlarmProcessing = IsResetAlarmWorking;
                        return !isResetAlarmProcessing;
                    });
                    bool isAlarmNeedAdd = await state;
                    return isAlarmNeedAdd;
                }
                else
                {
                    return true;
                }
            };

            if (this.IsForkHorizonDriverBase)
            {
                WagoDI.SubsSignalStateChange(DI_ITEM.Fork_Extend_Exist_Sensor, HandleHorizonForkLimitSensorStateChanged);
                WagoDI.SubsSignalStateChange(DI_ITEM.Fork_Short_Exist_Sensor, HandleHorizonForkLimitSensorStateChanged);

            }

            TaskBase.BeforeForkGoHomeOrStandyPosWhenNormalMoveStart += HandleForkGoHomeOrStandyPosWhenNormalMoveStart;
        }

        private void HandleForkGoHomeOrStandyPosWhenNormalMoveStart(object? sender, TaskBase.BeforeForkGoHomeOrStandyPosWhenNormalMoveStartEventArgs e)
        {

            try
            {
                var actionService = (AGVC as ForkAGVController).verticalActionService;
                if (actionService != null)
                {
                    string _messg = string.Empty;
                    var _currentAction = actionService.CurrentForkActionRequesting;
                    bool _isForkGoHomeOrGoStandbyNow = actionService.driverState.speed != 0 && (_IsGoHome(out _messg) || _IsGoStandby(out _messg));

                    e.message = _messg;
                    e.isCancel = _isForkGoHomeOrGoStandbyNow;

                    bool _IsGoHome(out string msg)
                    {
                        msg = string.Empty;
                        bool _isgoHome = _currentAction.command == "orig";
                        msg = _isgoHome ? "牙叉動作正在執行回Home位置，取消本次移動動作!" : "";
                        return _isgoHome;
                    }

                    bool _IsGoStandby(out string msg)
                    {
                        double standyPosition = Parameters.ForkAGV.StandbyPose;
                        bool _isgoStandyPose = _currentAction.command == "pose" && (_currentAction.target == standyPosition || _currentAction.target == 0);
                        msg = _isgoStandyPose ? "牙叉動作正在執行前往待命位置，取消本次移動動作!" : "";
                        return _isgoStandyPose;
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "VerticalActionService_BeforeActionStart:" + ex.Message);
            }
        }

        private void VerticalActionService_BeforeActionStart(object? sender, BeforActionStartErgs e)
        {
            try
            {
                var actionService = (AGVC as ForkAGVController).verticalActionService;
                if (actionService != null)
                {
                    bool _isForkRunning = actionService.driverState.speed != 0 || actionService.IsStartRunRequesting(actionService.CurrentForkActionRequesting);
                    if (!_isForkRunning)
                    {
                        e.isNeedWaitDriverStop = false;
                        return;
                    }
                    //Stop First
                    LogDebugMessage($"牙叉動作 {e.currentCommandReg.command} 開始前但偵測到牙叉處於運動狀態-> 首先下發停止指定(stop)", true);
                    actionService.Stop();

                    e.isNeedWaitDriverStop = true;
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "VerticalActionService_BeforeActionStart:" + ex.Message);
            }
        }

        private void ForkLifter_BeforeForkArmAction(object? sender, clsForkLifter.BeforeForkArmActionEventArgs e)
        {
            try
            {
                if (PinHardware == null || Parameters.ForkAGV.IsPinDisabledTemptary)
                    return;

                if (PinHardware.IsReleased)
                    return;

                e.isCancel = true;
                string actionText = e.action == FORK_ARM_ACTION.EXTEND ? "extend" : "shorten";
                e.message = $"浮動牙叉 PIN 尚未 Release，禁止執行 {actionText} 動作";
                logger.LogWarning($"[ForkArmActionGuard] {e.message}(PinState={PinHardware.CurrentPinState})");
            }
            catch (Exception ex)
            {
                e.isCancel = true;
                e.message = $"浮動牙叉 PIN 狀態檢查失敗:{ex.Message}";
                logger.LogError(ex, "ForkLifter_BeforeForkArmAction");
            }
        }

        private void HandleHorizonForkLimitSensorStateChanged(object? sender, bool inputState)
        {
            bool isReachLimit = !inputState;

            if (isReachLimit)
            {
                ForkLifter.ForkARMStop().ContinueWith((t) =>
                {
                    clsIOSignal? signal = sender as clsIOSignal;
                    AlarmCodes alarmCode = AlarmCodes.Fork_Horizon_Extend_Limit;
                    if (signal != null)
                        alarmCode = signal.Input == DI_ITEM.Fork_Extend_Exist_Sensor ? AlarmCodes.Fork_Horizon_Extend_Limit : AlarmCodes.Fork_Horizon_Retract_Limit;
                    AlarmManager.AddAlarm(alarmCode, false);
                });
            }
        }


        private SUB_STATUS _subStatusBeforeForkVerticalStartMove = SUB_STATUS.UNKNOWN;
        private CancellationTokenSource? _forkVerticalMoveObsProcessCancellationTokenSource = null;

        private void _fork_car_controller_OnForkStartMove(object? sender, VerticalCommandRequest request)
        {
            var _fork_car_controller = (AGVC as ForkAGVController);
            LogDebugMessage($"Fork Start Move Event Invoked Handling(command:{request.command}). {request.ToJson(Formatting.None)}", true);

            bool isForkMoveInWorkStationByLduld = GetSub_Status() == SUB_STATUS.RUN && lastVisitedMapPoint.StationType != STATION_TYPE.Normal;
            if (isForkMoveInWorkStationByLduld)
            {
                Laser.OnLaserModeChanged -= HandleLaserModeChangedWhenForkVerticalMoving;
                LogDebugMessage("注意! 目前在設備或儲格中移動牙叉，牙叉將不會因側邊雷射觸發而停止");
                return;
            }


            if (request.command == "orig" && (WagoDI.GetState(DI_ITEM.Vertical_Home_Pos) || (Parameters.ForkAGV.HomePoseUseStandyPose && Math.Abs(Parameters.ForkAGV.StandbyPose - ForkLifter.fork_ros_controller.verticalActionService.driverState.position) <= 0.5)))
            {
                Laser.OnLaserModeChanged -= HandleLaserModeChangedWhenForkVerticalMoving;
                LogDebugMessage("垂直牙叉已在Home位置或已接近待命位置", false);
                return;
            }

            if (request.command == "pose" && Math.Abs(request.target - ForkLifter.fork_ros_controller.verticalActionService.driverState.position) <= 0.5)
            {

                Laser.OnLaserModeChanged -= HandleLaserModeChangedWhenForkVerticalMoving;
                LogDebugMessage("垂直牙叉位置已經接近目標位置", false);
                return;
            }

            _fork_car_controller.verticalActionService.OnActionStart -= _fork_car_controller_OnForkStartMove;

            Laser.SideLasersEnable(lastVisitedMapPoint.StationType != STATION_TYPE.Charge).ContinueWith(async t =>
            {
                if (Laser.currentMode == clsLaser.LASER_MODE.Bypass || Laser.currentMode == clsLaser.LASER_MODE.Bypass16)
                    await Laser.ModeSwitch(clsLaser.LASER_MODE.Turning);

                _forkVerticalMoveObsProcessCancellationTokenSource?.Dispose();
                _forkVerticalMoveObsProcessCancellationTokenSource = new CancellationTokenSource();

                _ = Task.Factory.StartNew(() => StartVerticalForkProtectProcess());

            });
        }

        private async Task StartVerticalForkProtectProcess()
        {
            bool _isStopCmdCalled = false;
            var _fork_car_controller = (AGVC as ForkAGVController);

            Laser.OnLaserModeChanged += HandleLaserModeChangedWhenForkVerticalMoving;
            VerticalCommandRequest? lastVerticalForkActionCmd = null;

            SOUNDS _soundBeforeStop = BuzzerPlayer.SoundPlaying;
            LogDebugMessage("因牙叉升降動作開始監視側邊雷射狀態", false);
            _fork_car_controller.verticalActionService.OnActionDone += _fork_car_controller_OnForkStopMove;
            bool lastLsrTriggerState = false;
            await Task.Delay(500);
            Stopwatch triggerTimer = new Stopwatch();
            const int debounceTimeMs = 250;
            const int resumeActionDebounceTimeMs = 1000;

            while (!_forkVerticalMoveObsProcessCancellationTokenSource.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(10, _forkVerticalMoveObsProcessCancellationTokenSource.Token);

                    bool isRightSideLaserBypass = WagoDO.GetState(DO_ITEM.Right_LsrBypass);
                    bool isLeftSideLaserBypass = WagoDO.GetState(DO_ITEM.Left_LsrBypass);


                    bool _AnySideLaserTrigger()
                    {
                        Dictionary<DO_ITEM, DI_ITEM[]> monitorMap = new Dictionary<DO_ITEM, DI_ITEM[]>() {

                            { DO_ITEM.Right_LsrBypass , new DI_ITEM[] { DI_ITEM.RightProtection_Area_Sensor_3 } },
                            { DO_ITEM.Left_LsrBypass , new DI_ITEM[] { DI_ITEM.LeftProtection_Area_Sensor_3 } }
                        };

                        foreach (var keypair in monitorMap)
                        {
                            DO_ITEM bypassOutput = keypair.Key;
                            bool isBypassed = WagoDO.GetState(bypassOutput);

                            if (isBypassed)
                                continue;

                            foreach (var input in keypair.Value)//觀世input薩??
                            {
                                if (!WagoDI.VCSInputs.Any(i => i.Input == input))
                                    continue;

                                if (!WagoDI.GetState(input))
                                    return true;
                            }
                        }
                        return false;
                    }

                    bool isAnySideLaserTriggering = _AnySideLaserTrigger();

                    if (isAnySideLaserTriggering && !_isStopCmdCalled)
                    {

                        if (!lastLsrTriggerState)
                        {
                            triggerTimer.Restart();
                        }
                        else if (triggerTimer.ElapsedMilliseconds >= debounceTimeMs)
                        {

                            lastVerticalForkActionCmd = _fork_car_controller.verticalActionService.CurrentForkActionRequesting.Clone();
                            _fork_car_controller.verticalActionService.OnActionDone -= _fork_car_controller_OnForkStopMove;
                            _isStopCmdCalled = true;
                            if (!IsLaserMonitoring)
                            {
                                _soundBeforeStop = BuzzerPlayer.SoundPlaying;
                                BuzzerPlayer.SoundPlaying = SOUNDS.Alarm;
                                AlarmManager.AddWarning(AlarmCodes.SideLaserTriggerWhenForkMove);
                            }
                            ForkLifter.IsStopByObstacleDetected = true;
                            await ForkLifter.ForkStopAsync(waitSpeedZero: true);
                            LogDebugMessage($"雷射組數觸發，牙叉停止動作!", false);
                            _fork_car_controller.verticalActionService.OnActionDone += _fork_car_controller_OnForkStopMove;

                            if (ForkLifter.IsManualOperation)
                            {
                                SendNotifyierToFrontend($"注意!在手動操作下牙叉側邊雷射障礙物檢出停止動作!", title: "Fork Action Stop");
                                break;
                            }

                        }
                    }
                    else if (!isAnySideLaserTriggering && _isStopCmdCalled)
                    {

                        if (lastLsrTriggerState)
                        {
                            triggerTimer.Restart();
                        }
                        else if (triggerTimer.ElapsedMilliseconds >= resumeActionDebounceTimeMs)
                        {
                            _isStopCmdCalled = false;
                            //_fork_car_controller.verticalActionService.OnActionDone += _fork_car_controller_OnForkStopMove;
                            if (lastVerticalForkActionCmd != null)
                            {
                                if (!IsLaserMonitoring)
                                {
                                    BuzzerPlayer.SoundPlaying = _soundBeforeStop;
                                    AlarmManager.ClearAlarm(AlarmCodes.SideLaserTriggerWhenForkMove);
                                }

                                const int MAX_RETRY = 3;
                                int retry = 0;
                                bool isResumeActionSuccess = false;
                                while (!isResumeActionSuccess)
                                {

                                    if (_Sub_Status == SUB_STATUS.DOWN || _forkVerticalMoveObsProcessCancellationTokenSource.IsCancellationRequested)
                                        throw new TaskCanceledException();

                                    isResumeActionSuccess = (await ForkLifter.ForkResumeAction(lastVerticalForkActionCmd)).confirm;
                                    if (isResumeActionSuccess)
                                        break;

                                    if (retry >= MAX_RETRY)
                                    {
                                        logger.LogWarning($"嘗試恢復牙叉動作嘗試次數已達{MAX_RETRY}次.");
                                        ForkLifter.IsStopByObstacleDetected = false;
                                        break;
                                    }
                                    logger.LogWarning($"嘗試恢復牙叉動作失敗,一秒後將重新嘗試...");
                                    retry += 1;
                                    await Task.Delay(1000, _forkVerticalMoveObsProcessCancellationTokenSource.Token);
                                }
                                ForkLifter.IsStopByObstacleDetected = false;
                                LogDebugMessage($"雷射復原，牙叉恢復動作!", false);
                            }
                        }

                    }

                    lastLsrTriggerState = isAnySideLaserTriggering;

                }
                catch (TaskCanceledException ex)
                {
                    if (_Sub_Status == SUB_STATUS.DOWN)
                        LogDebugMessage($"牙叉安全偵測任務取消且 AGV 狀態為 DOWN!!", true);
                    else
                        LogDebugMessage($"牙叉安全偵測任務取消!!", false);
                    break;
                }
            }
            if (_Sub_Status == SUB_STATUS.DOWN)
                LogDebugMessage("牙叉升降動作監視側邊雷射狀態且 AGV 狀態為 DOWN!!---[結束]", true);
            else
                LogDebugMessage("牙叉升降動作監視側邊雷射狀態---[結束]", false);
            _fork_car_controller.verticalActionService.OnActionDone -= _fork_car_controller_OnForkStopMove;
            _fork_car_controller.verticalActionService.OnActionStart += _fork_car_controller_OnForkStartMove;

            Laser.OnLaserModeChanged -= HandleLaserModeChangedWhenForkVerticalMoving;

            void _fork_car_controller_OnForkStopMove(object? sender, EventArgs e)
            {
                _fork_car_controller.verticalActionService.OnActionDone -= _fork_car_controller_OnForkStopMove;
                _forkVerticalMoveObsProcessCancellationTokenSource?.Cancel();
                Laser.OnLaserModeChanged -= HandleLaserModeChangedWhenForkVerticalMoving;
                LogDebugMessage($"取消註冊雷射組數切換事件", false);
                logger.LogTrace("Fork Stop");
            }
        }

        private void HandleLaserModeChangedWhenForkVerticalMoving(object? sender, clsLaser.LASER_MODE currentMode)
        {
            Laser.OnLaserModeChanged -= HandleLaserModeChangedWhenForkVerticalMoving;

            if (currentMode == clsLaser.LASER_MODE.Bypass || currentMode == clsLaser.LASER_MODE.Bypass16)
            {
                logger.LogWarning($"Fork Vertical Moving, Laser Mode now changed to {currentMode}(Bypass status),switch laser mode to turning");
                LogDebugMessage($"雷射組數現在為Bypass,但牙叉(Vertical)動作中=> 切換為 Truning 組數!", false);
                Laser.ModeSwitch(clsLaser.LASER_MODE.Turning);
                Laser.SideLasersEnable(true);
            }
        }

        protected override async Task<(bool, string)> PreActionBeforeInitialize()
        {

            Laser.OnLaserModeChanged -= HandleLaserModeChangedWhenForkVerticalMoving;
            (bool, string) baseInitiazedResutl = await base.PreActionBeforeInitialize();

            if (!baseInitiazedResutl.Item1)
                return baseInitiazedResutl;

            if (!Parameters.CheckObstacleWhenForkInit)
                return (true, "");

            InitializingStatusText = "Laser Initizing...";

            #region 雷射組數切換
            await Laser.ModeSwitch(clsLaser.LASER_MODE.Normal);
            await Task.Delay(250);
            await Laser.ModeSwitch(clsLaser.LASER_MODE.Turning);
            await Task.Delay(250);
            await Laser.SideLasersEnable(true);
            #endregion

            if (!Parameters.SensorBypass.RightSideLaserBypass)
            {
                bool RightLaserAbnormal = !WagoDI.GetState(DI_ITEM.RightProtection_Area_Sensor_3);
                if (RightLaserAbnormal)
                    return (false, "無法在障礙物入侵的狀態下進行初始化(右方障礙物檢出)");
            }
            if (!Parameters.SensorBypass.LeftSideLaserBypass)
            {
                bool LeftLaserAbnormal = !WagoDI.GetState(DI_ITEM.LeftProtection_Area_Sensor_3);
                if (LeftLaserAbnormal)
                    return (false, "無法在障礙物入侵的狀態下進行初始化(左方障礙物檢出)");
            }


            await WagoDO.SetState(DO_ITEM.Vertical_Motor_Stop, false);
            await WagoDO.SetState(DO_ITEM.Fork_Under_Pressing_SensorBypass, false);
            await WagoDO.SetState(DO_ITEM.Vertical_Hardware_limit_bypass, false);
            await WagoDO.SetState(DO_ITEM.Vertical_Belt_SensorBypass, false);
            return (true, "");
        }
        protected override async Task<(bool confirm, string message)> InitializeActions(CancellationTokenSource cancellation)
        {
            if (cancellation.IsCancellationRequested)
                throw new TaskCanceledException();

            ForkLifter?.EarlyMoveUpState.Reset();
            (bool forklifer_init_done, string message) _forklift_vertical_init_result = (false, "");
            (bool forklifer_init_done, string message) _forklift_horizon_init_result = (false, "");
            (bool pin_init_done, string message) _pin_init_result = (false, "");

            List<Task> _actions = new List<Task>();

            _resumeForkInitProcessWhenDriverStateIsKnown = false;
            _forkVerticalInitWaitUserConfirm.Reset();

            if (ForkLifter.IsForkDriverStateUnknown)
            {
                InitializingStatusText = "垂直牙叉驅動器狀態未知-等待使用者確認牙叉初始化動作";
                SendNotifyierToFrontend("Fork Vertical Driver Unkonwn, Initialize Action Confirm", (int)AlarmCodes.Fork_Initialize_Process_Interupt);
                bool confirmed = _forkVerticalInitWaitUserConfirm.WaitOne(10000); //等待10秒鐘,如果沒有收到確認則取消初始化
                if (!confirmed)
                {
                    LogDebugMessage("等待使用者確認牙叉初始化動作超時,取消初始化動作");
                    return (false, "等待使用者確認牙叉初始化動作超時,取消初始化動作");
                }
                SendCloseSpeficDialogToFrontend((int)AlarmCodes.Fork_Initialize_Process_Interupt);
                if (!_resumeForkInitProcessWhenDriverStateIsKnown)
                {
                    return (false, "使用者取消牙叉初始化動作");
                }
            }

            Task<(bool pin_init_done, string message)> forkFloatHarwarePinInitTask = ForkFloatHardwarePinInitProcess(cancellation.Token);
            Task<(bool forklifer_init_done, string message)> forkVerticalInitTask = VerticalForkInitProcess(cancellation.Token, ForkLifter.IsForkDriverStateUnknown && _resumeForkInitProcessWhenDriverStateIsKnown);
            Task<(bool forklifer_init_done, string message)> forkHorizonInitTask = HorizonForkInitProcess(cancellation.Token);

            _actions.Add(forkFloatHarwarePinInitTask);

            _actions.Add(forkVerticalInitTask);

            _actions.Add(forkHorizonInitTask);

            Task.WaitAll(_actions.ToArray());

            Laser.OnLaserModeChanged -= HandleLaserModeChangedWhenForkVerticalMoving;
            _forkVerticalMoveObsProcessCancellationTokenSource?.Cancel();


            _pin_init_result = forkFloatHarwarePinInitTask.Result;
            _forklift_vertical_init_result = forkVerticalInitTask.Result;
            _forklift_horizon_init_result = forkHorizonInitTask.Result;


            bool pin_init_success = _pin_init_result.pin_init_done;
            bool fork_vertical_init_success = _forklift_vertical_init_result.forklifer_init_done;
            bool fork_horizon_init_success = _forklift_horizon_init_result.forklifer_init_done;

            if (pin_init_success && fork_vertical_init_success && fork_horizon_init_success)
            {
                return await base.InitializeActions(cancellation);
            }

            string errmsg = "";
            errmsg += pin_init_success ? "" : $"[Pin Driver:{_pin_init_result.message}]";
            errmsg += fork_vertical_init_success ? "" : $"[Fork_Vertical:{_forklift_vertical_init_result.message}]";
            errmsg += fork_horizon_init_success ? "" : $"[Fork_Horizon :{_forklift_horizon_init_result.message}]";
            return (false, errmsg);

        }


        internal async Task<(bool, string)> ForkFloatHardwarePinInitProcess(CancellationToken token)
        {
            if (PinHardware == null)
                return (true, "浮動牙叉定位PIN裝置未裝載");

            if (Parameters.ForkAGV.IsPinDisabledTemptary)
            {
                SendNotifyierToFrontend($"注意! 浮動牙叉暫時被禁用中");
                return (true, "浮動牙叉定位PIN裝置已暫時禁用");
            }
            if (_isForkInitBypass)
            {
                LogDebugMessage("Fork Init Bypass Enabled, Skip Pin Initialize Action", true);
                return (true, "Fork Init Bypass Enabled, Skip Pin Initialize Action");
            }
            return await Task.Run(async () =>
            {
                using var initMsgUpdater = CreateInitMsgUpdater();
                (bool pin_init_done, string message) _pin_init_result = (false, "");
                await initMsgUpdater.Update("PIN-模組初始化中...");

                try
                {
                    await PinHardware.Init(token);
                    await initMsgUpdater.Update("PIN-Lock 中...");
                    await PinHardware.Lock(token);
                    _pin_init_result = (true, "");
                }

                catch (TaskCanceledException ex)
                {
                    _pin_init_result = (false, ex.Message);
                }

                catch (TimeoutException)
                {
                    _pin_init_result = (false, "Pin Action Timeout");
                }
                catch (Exception ex)
                {
                    _pin_init_result = (false, ex.Message);
                }
                finally
                {
                }
                return _pin_init_result;
            });
        }

        private bool _resumeForkInitProcessWhenDriverStateIsKnown = false;
        private ManualResetEvent _forkVerticalInitWaitUserConfirm = new ManualResetEvent(false);

        internal async Task<(bool, string)> VerticalForkInitProcess(CancellationToken token, bool bypass = false)
        {
            if (bypass)
                return (true, "bypass");

            if (GetSub_Status() != SUB_STATUS.Initializing)
                SetSub_Status(SUB_STATUS.Initializing);
            return await Task.Run(async () =>
            {

                (bool forklifer_init_done, string message) _forklift_init_result = (false, "");
                await Laser.ModeSwitch(clsLaser.LASER_MODE.Turning);
                await Laser.SideLasersEnable(true);

                await Task.Delay(700);
                InitializingStatusText = "牙叉初始化動作中";
                ForkLifter.fork_ros_controller.verticalActionService.CurrentForkActionRequesting = new AGVSystemCommonNet6.GPMRosMessageNet.Services.VerticalCommandRequest();
                if (ForkLifter.Enable)
                {
                    InitializingStatusText = "牙叉原點覆歸...";
                    await WagoDO.SetState(DO_ITEM.Vertical_Belt_SensorBypass, true);

                    bool isForkAllowNoDoInitializeAction = Parameters.ForkNoInitializeWhenPoseIsHome && ForkLifter.CurrentForkLocation == FORK_LOCATIONS.HOME;

                    (bool done, AlarmCodes alarm_code) forkInitizeResult = (false, AlarmCodes.None);

                    if (_isForkInitBypass)
                    {
                        LogDebugMessage("Fork Init Bypass Enabled, Skip Fork Search Home Initialize Action", true);
                        forkInitizeResult = (true, AlarmCodes.None);
                        ForkLifter.IsVerticalForkInitialized = true;

                    }
                    else
                    {
                        if (isForkAllowNoDoInitializeAction)
                        {
                            (bool confirm, string message) ret = await ForkLifter.ForkPositionInit();
                            forkInitizeResult = (ret.confirm, AlarmCodes.Fork_Initialized_But_Driver_Position_Not_ZERO);
                            ForkLifter.IsVerticalForkInitialized = true;
                        }
                        else
                        {
                            double _speed_of_init = CargoStateStorer.HasAnyCargoOnAGV(Parameters.LDULD_Task_No_Entry) ? Parameters.ForkAGV.InitParams.ForkInitActionSpeedWithCargo : Parameters.ForkAGV.InitParams.ForkInitActionSpeedWithoutCargo;
                            forkInitizeResult = await ForkLifter.VerticalForkInitialize(_speed_of_init, token);
                        }
                    }

                    if (forkInitizeResult.done)
                    {
                        (bool confirm, AlarmCodes alarm_code) home_action_response = (false, AlarmCodes.None);
                        //self test Home action 
                        if (!_isForkInitBypass && Parameters.ForkAGV.HomePoseUseStandyPose)
                        {
                            InitializingStatusText = $"Fork Height Go To Standby Pose..";
                            (bool confirm, string message) = await ForkLifter.ForkPose(Parameters.ForkAGV.StandbyPose, 1, true);
                            home_action_response.confirm = confirm;
                            home_action_response.alarm_code = confirm ? AlarmCodes.None : AlarmCodes.Fork_Action_Aborted;
                        }
                        else
                        {
                            home_action_response = await ForkLifter.ForkGoHome();
                        }

                        if (!home_action_response.confirm)
                        {
                            _forklift_init_result = (false, home_action_response.alarm_code.ToString());
                        }
                        else
                        {
                            if (!Parameters.ForkAGV.HomePoseUseStandyPose)
                            {

                                await Task.Delay(200);
                                bool isHome = WagoDI.GetState(DI_ITEM.Vertical_Home_Pos);
                                forkInitizeResult = (isHome, isHome ? AlarmCodes.None : AlarmCodes.Fork_Initialized_But_Home_Input_Not_ON);
                            }
                        }
                    }

                    if (!Parameters.SensorBypass.BeltSensorBypass)
                        await WagoDO.SetState(DO_ITEM.Vertical_Belt_SensorBypass, false);
                    _forklift_init_result = (forkInitizeResult.done, forkInitizeResult.alarm_code.ToString());
                }
                else
                {
                    AlarmManager.AddWarning(AlarmCodes.Fork_Disabled);
                    _forklift_init_result = (true, "");
                }
                return _forklift_init_result;
            });
        }

        internal async Task<(bool, string)> HorizonForkInitProcess(CancellationToken token)
        {
            if (!Parameters.ForkAGV.IsForkIsExtendable)
            {
                LogDebugMessage("Fork Is Not Extendable,Horizon Fork arm initialize is bypassed!");
                return (true, "Fork Is Not Extendable");
            }
            if (Parameters.ForkAGV.IsHorizonExtendDisabledTemptary)
            {
                SendNotifyierToFrontend($"注意! 伸縮牙叉暫時被禁用中");
                return (true, "伸縮牙叉已暫時禁用");
            }
            return await Task.Run(async () =>
            {
                using var initMsgUpdater = CreateInitMsgUpdater();
                try
                {
                    await initMsgUpdater.Update("伸縮牙叉 Reset...");
                    var resetResult = await ForkLifter.ForkHorizonResetAsync();
                    if (!resetResult.success)
                        return resetResult;
                    await initMsgUpdater.Update("伸縮牙叉縮回中...");
                    return await ForkLifter.ForkShortenInAsync();
                }
                catch (Exception ex)
                {
                    return (false, ex.Message);
                }
                finally
                {
                }
            });
        }

        protected override void CreateAGVCInstance(string RosBridge_IP, int RosBridge_Port)
        {
            AGVC = new ForkAGVController(RosBridge_IP, RosBridge_Port);
            (AGVC as ForkAGVController).OnCSTReaderActionDone += CSTReader.UpdateCSTIDDataHandler;
            logger.LogTrace($"(AGVC as ForkAGVController).OnCSTReaderActionDone += CSTReader.UpdateCSTIDDataHandler;");
        }

        protected internal override void SoftwareEMO(AlarmCodes alarmCode)
        {
            Task.Run(async () =>
            {
                logger.LogWarning($"SW EMS Trigger, Fork Action STOP!!!!!!(LIFER AND ARM)");
                _forkVerticalMoveObsProcessCancellationTokenSource?.Cancel();
                Laser.OnLaserModeChanged -= HandleLaserModeChangedWhenForkVerticalMoving;
                await Task.Delay(1);
                ForkLifter.ForkARMStop();
                ForkLifter.ForkStopAsync(IsEMS: true);
            });
            base.SoftwareEMO(alarmCode);
        }
        protected override void EMOTriggerHandler(object? sender, EventArgs e)
        {
            base.EMOTriggerHandler(sender, e);
            Task.Factory.StartNew(async () =>
            {
                await Task.Delay(1);
                ForkLifter.ForkARMStop();
                ForkLifter.ForkStopAsync(true);
            });
        }

        protected override async Task DOSignalDefaultSetting()
        {
            await base.DOSignalDefaultSetting();
            await WagoDO.SetState(DO_ITEM.Vertical_Hardware_limit_bypass, false);
            await WagoDO.SetState(DO_ITEM.Vertical_Belt_SensorBypass, false);
            await WagoDO.SetState(DO_ITEM.Fork_Under_Pressing_SensorBypass, false);
        }


        protected override void HandleDriversStatusErrorAsync(object? sender, bool status)
        {
            if (!status)
                return;

            var signal = (sender as clsIOSignal);

            if (signal.Input == DI_ITEM.Vertical_Motor_Alarm)
            {
                (bool needAddAlar, bool recoveryable) = DetermineDriverAlarmNeedAddOrNot(VerticalDriverState, AlarmCodes.Vertical_Motor_IO_Error);
                if (needAddAlar)
                {
                    AlarmManager.AddAlarm(AlarmCodes.Vertical_Motor_IO_Error, false);
                    return;
                }
                if (recoveryable)
                    AlarmManager.AddWarning(AlarmCodes.Vertical_Motor_IO_Error);
            }
            else
                base.HandleDriversStatusErrorAsync(sender, status);
        }
        protected override void DIOStatusChangedEventRegist()
        {
            base.DIOStatusChangedEventRegist();
            WagoDI.SubsSignalStateChange(DI_ITEM.Vertical_Motor_Alarm, HandleDriversStatusErrorAsync);
            WagoDI.SubsSignalStateChange(DI_ITEM.Fork_Home_Pose, HandleHomePoseSensorStateChanged);
            WagoDI.SubsSignalStateChange(DI_ITEM.Vertical_Home_Pos, HandleHomePoseSensorStateChanged);
        }

        private void HandleHomePoseSensorStateChanged(object? sender, bool state)
        {
            if (state)
            {
                clsIOSignal signal = (clsIOSignal)sender;
                LogDebugMessage($"{signal.Name} Now is ON!");
            }
        }

        protected override clsWorkStationModel DeserializeWorkStationJson(string json)
        {
            clsWorkStationModel? dat = JsonConvert.DeserializeObject<clsWorkStationModel>(json);
            foreach (KeyValuePair<int, clsWorkStationData> station in dat.Stations)
            {
                while (station.Value.LayerDatas.Count != 3)
                {
                    station.Value.LayerDatas.Add(station.Value.LayerDatas.Count, new clsStationLayerData
                    {
                        Down_Pose = 0,
                        Up_Pose = 0
                    });
                }
            }
            return dat;
        }

        public override (bool confirm, string message) CheckHardwareStatus()
        {
            bool _hasVerticalBrakeSwitch = WagoDI.VCSInputs.Any(i => i.Input == DI_ITEM.Vertical_Motor_Switch);
            if (_hasVerticalBrakeSwitch && !WagoDI.GetState(DI_ITEM.Vertical_Motor_Switch))
            {
                AlarmManager.AddAlarm(AlarmCodes.Switch_Type_Error_Vertical, false);
                BuzzerPlayer.SoundPlaying = SOUNDS.Alarm;
                return (false, "Z軸解煞車旋鈕尚未復歸");
            }
            return base.CheckHardwareStatus();
        }

        protected override async Task DOSettingWhenEmoTrigger()
        {
            await base.DOSettingWhenEmoTrigger();
            //await WagoDO.SetState(DO_ITEM.Vertical_Motor_Stop, true);
        }

        protected override async Task TryResetMotors()
        {
            await WagoDO.SetState(DO_ITEM.Horizon_Motor_Stop, false);
            await WagoDO.SetState(DO_ITEM.Horizon_Motor_Free, false);
            await WagoDO.SetState(DO_ITEM.Vertical_Motor_Stop, false);

            if (IsAnyHorizonMotorAlarm())
            {
                await WagoDO.SetState(DO_ITEM.Horizon_Motor_Reset, true);
                await Task.Delay(100);
                await WagoDO.SetState(DO_ITEM.Horizon_Motor_Reset, false);

                await WagoDO.SetState(DO_ITEM.Vertical_Motor_Reset, true);
                await Task.Delay(100);
                await WagoDO.SetState(DO_ITEM.Vertical_Motor_Reset, false);

                while (IsAnyHorizonMotorAlarm())
                {
                    await Task.Delay(1);
                }

                await Task.Delay(50);
            }
        }

        protected override void AutoResetHorizonMotor(object? sender, bool alarm)
        {
            //DO Nothing
        }
        protected override bool IsAnyHorizonMotorAlarm()
        {
            bool horizonMotorAlarm = base.IsAnyHorizonMotorAlarm();

            bool verticalMotorAlarm = !WagoDI.GetState(DI_ITEM.Vertical_Motor_Busy);

            return horizonMotorAlarm || verticalMotorAlarm;
        }

        public virtual bool ZAxisGoHomingCheck()
        {
            if (lastVisitedMapPoint.StationType != STATION_TYPE.Normal)
            {
                logger.LogCritical($"Fork want to Home in non-normal point!!!!!!");
                return false;
            }

            bool IsAGVEnteringWorkStationNow()
            {
                var currentAction = _RunTaskData.Action_Type;
                var agvcActionStatus = AGVC.ActionStatus;

                if (currentAction == ACTION_TYPE.None)
                    return false;
                return agvcActionStatus != ActionStatus.SUCCEEDED && lastVisitedMapPoint.StationType == STATION_TYPE.Normal;
            }
            return true;
        }

        internal void AcceptResumeForkInitWhenActionDriverStateUnknown(bool resume)
        {
            _resumeForkInitProcessWhenDriverStateIsKnown = resume;
            _forkVerticalInitWaitUserConfirm.Set();
        }

    }
}
