using AGVSystemCommonNet6;
using AGVSystemCommonNet6.AGVDispatch.Messages;
using AGVSystemCommonNet6.GPMRosMessageNet.Services;
using AGVSystemCommonNet6.Vehicle_Control.VCS_ALARM;
using GPMVehicleControlSystem.Models.Buzzer;
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
        public override bool IsFrontendSideHasObstacle => !WagoDI.GetState(DI_ITEM.Fork_Frontend_Abstacle_Sensor);
        public bool IsForkHorizonDriverBase => WagoDI.Indexs.TryGetValue(DI_ITEM.Fork_Home_Pose, out _);
        public ForkAGV(clsVehicelParam param, VehicleServiceAggregator vehicleServiceAggregator) : base(param, vehicleServiceAggregator)
        {

        }
        internal override async Task CreateAsync()
        {
            await base.CreateAsync();
            ForkLifter = IsForkHorizonDriverBase ? new clsForkLifterWithDriverBaseExtener(this) : new clsForkLifter(this);
            ForkLifter.Driver = VerticalDriverState;
            ForkLifter.DIModule = WagoDI;
            ForkLifter.DOModule = WagoDO;
            if (Parameters.ForkAGV.IsPinMounted)
                PinHardware = new clsPin();

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
            ForkLifter.Driver.OnAlarmHappened += async (alarm_code) =>
            {
                if (alarm_code != AlarmCodes.None)
                {
                    if (StaSysControl.isAGVCRestarting)
                        return false;
                    Task<bool> state = await Task.Factory.StartNew(async () =>
                    {
                        await Task.Delay(1000);
                        bool isEMOING = WagoDI.GetState(DI_ITEM.EMO) == false;
                        if (isEMOING)
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
                if (Laser.Mode == clsLaser.LASER_MODE.Bypass || Laser.Mode == clsLaser.LASER_MODE.Bypass16)
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
            const int durationMs = 250;

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
                        else if (triggerTimer.ElapsedMilliseconds >= durationMs)
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
                            await ForkLifter.ForkStopAsync();
                            _fork_car_controller.verticalActionService.OnActionDone += _fork_car_controller_OnForkStopMove;
                            LogDebugMessage($"雷射組數觸發，牙叉停止動作!", false);

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
                        else if (triggerTimer.ElapsedMilliseconds >= durationMs)
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
                                await ForkLifter.ForkResumeAction(lastVerticalForkActionCmd);
                                ForkLifter.IsStopByObstacleDetected = false;
                                LogDebugMessage($"雷射復原，牙叉恢復動作!", false);
                            }
                        }

                    }

                    lastLsrTriggerState = isAnySideLaserTriggering;

                }
                catch (TaskCanceledException ex)
                {
                    LogDebugMessage($"牙叉安全偵測任務取消!!", false);
                    break;
                }
            }

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

            ForkLifter?.EarlyMoveUpState.Reset();
            (bool forklifer_init_done, string message) _forklift_vertical_init_result = (false, "");
            (bool forklifer_init_done, string message) _forklift_horizon_init_result = (false, "");
            (bool pin_init_done, string message) _pin_init_result = (false, "");

            List<Task> _actions = new List<Task>();

            Task<(bool pin_init_done, string message)> forkFloatHarwarePinInitTask = ForkFloatHardwarePinInitProcess(cancellation.Token);
            Task<(bool forklifer_init_done, string message)> forkVerticalInitTask = VerticalForkInitProcess(cancellation.Token);
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

            return await Task.Run(async () =>
            {
                using var initMsgUpdater = CreateInitMsgUpdater();
                (bool pin_init_done, string message) _pin_init_result = (false, "");
                await initMsgUpdater.Update("PIN-模組初始化中...");
                try
                {
                    await PinHardware.Init();
                    await initMsgUpdater.Update("PIN-Lock 中...");
                    await PinHardware.Lock();
                    _pin_init_result = (true, "");
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

        internal async Task<(bool, string)> VerticalForkInitProcess(CancellationToken token)
        {
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
                    if (forkInitizeResult.done)
                    {
                        (bool confirm, AlarmCodes alarm_code) home_action_response = (false, AlarmCodes.None);
                        //self test Home action 
                        if (Parameters.ForkAGV.HomePoseUseStandyPose)
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
                Laser.OnLaserModeChanged -= HandleLaserModeChangedWhenForkVerticalMoving;
                await Task.Delay(1);
                ForkLifter.ForkARMStop();
                ForkLifter.ForkStopAsync(true);
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


        protected override async void HandleDriversStatusErrorAsync(object? sender, bool status)
        {
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
            if (!WagoDI.GetState(DI_ITEM.Vertical_Motor_Switch))
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
    }
}
