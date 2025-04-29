using AGVSystemCommonNet6.AGVDispatch;
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
using Microsoft.AspNetCore.SignalR;
using Newtonsoft.Json;
using RosSharp.RosBridgeClient.Actionlib;
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
            ForkMovingProtectedProcess();
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
                IsMotorReseting = true;
                if (!await SetMotorStateAndDelay(DO_ITEM.Vertical_Motor_Stop, true, 100)) throw new Exception($"Vertical_Motor_Stop set true fail");
                if (!await SetMotorStateAndDelay(DO_ITEM.Vertical_Motor_Reset, true, 100)) throw new Exception($"Vertical_Motor_Reset set true fail");
                if (!await SetMotorStateAndDelay(DO_ITEM.Vertical_Motor_Reset, false, 100)) throw new Exception($"Vertical_Motor_Reset set false fail");
                if (!await SetMotorStateAndDelay(DO_ITEM.Vertical_Motor_Stop, false, 100)) throw new Exception($"Vertical_Motor_Stop set false fail");
                return true;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, ex.Message);
                IsMotorReseting = false;
                return false;
            }
        }
        public override async Task<bool> ResetMotor(bool triggerByResetButtonPush, bool bypass_when_motor_busy_on = true)
        {
            try
            {
                await base.ResetMotor(triggerByResetButtonPush, bypass_when_motor_busy_on);

                if (WagoDI.GetState(DI_ITEM.Vertical_Motor_Busy))
                    return true;
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
            _fork_car_controller.verticalActionService.OnActionDone += _fork_car_controller_OnForkStopMove;
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
        }
        private bool _ForkSaftyProtectFlag = false;
        private void _fork_car_controller_OnForkStopMove(object? sender, EventArgs e)
        {
            //throw new NotImplementedException();
            logger.LogTrace("Fork Stop");
            _ForkSaftyProtectFlag = false;
            if (GetSub_Status() == SUB_STATUS.IDLE)
                BuzzerPlayer.Stop($"_fork_car_controller_OnForkStopMove");
        }

        private void _fork_car_controller_OnForkStartMove(object? sender, VerticalCommandRequest request)
        {
            bool isForkMoveInWorkStationByLduld = GetSub_Status() == SUB_STATUS.RUN && lastVisitedMapPoint.StationType != STATION_TYPE.Normal;
            if (isForkMoveInWorkStationByLduld)
            {
                _ForkSaftyProtectFlag = false;
                DebugMessageBrocast("注意! 目前在設備或儲格中移動牙叉，牙叉將不會因側邊雷射觸發而停止");
                return;
            }

            ForkAGVController agvc = sender as ForkAGVController;
            bool isInitializing = agvc.IsInitializing;
            //throw new NotImplementedException();
            logger.LogTrace($"Fork Star Run (Started by:{request.command})");
            bool isGoUpAction = request.command == "pose" && request.target > ForkLifter.CurrentHeightPosition;
            bool isGoUpByLdUldAction = (isGoUpAction && _isLoadUnloadTaskRunning);
            _ForkSaftyProtectFlag = !isGoUpByLdUldAction;
        }
        private void ForkMovingProtectedProcess()
        {
            Thread _thread = new Thread(() =>
            {
                bool _isStopped = false;
                WagoDI.SubsSignalStateChange(DI_ITEM.LeftProtection_Area_Sensor_3, HandlerSideLaserStateChange);
                WagoDI.SubsSignalStateChange(DI_ITEM.RightProtection_Area_Sensor_3, HandlerSideLaserStateChange);

                void HandlerSideLaserStateChange(object? sender, bool state)
                {
                    bool _isLsrTrigger = !state;
                    bool _isLaserTriggerAndForkIsMoving = _isLsrTrigger && !_isStopped;
                    bool _isLaserSafeAndForkIsStopping = !_isLsrTrigger && _isStopped;
                    if (_ForkSaftyProtectFlag)
                    {
                        if (_isLaserTriggerAndForkIsMoving)
                        {
                            logger.LogWarning("Side Laser Trigger, Stop Fork");
                            ForkLifter.ForkStopAsync();
                            _isStopped = true;
                            BuzzerPlayer.Alarm();
                            //AGVStatusChangeToAlarmWhenLaserTrigger();

                        }
                    }
                    if (_isLaserSafeAndForkIsStopping)
                    {
                        logger.LogInformation("Side Laser Reconvery, Resume Fork Action");
                        ChangeSubStatusAndLighterBuzzerWhenLaserRecoveryInForkRunning();
                        ForkLifter.ForkResumeAction();
                        _isStopped = false;
                    }
                }

            });
            _thread.Start();
            logger.LogTrace("Start Fork Safty Protect Process Thread");
        }
        private bool _isLoadUnloadTaskRunning => _RunTaskData.IsLDULDAction() && !_RunTaskData.IsActionFinishReported;
        private void ChangeSubStatusAndLighterBuzzerWhenLaserRecoveryInForkRunning()
        {
            if (GetSub_Status() == SUB_STATUS.DOWN)
                return;


            bool _isForkRunningPreActionAndNoObstacleArround = ForkLifter.EarlyMoveUpState.IsHeightPreSettingActionRunning && IsAllLaserNoTrigger();

            if (_isLoadUnloadTaskRunning || _isForkRunningPreActionAndNoObstacleArround)
            {
                _Sub_Status = SUB_STATUS.RUN;
                StatusLighter.RUN();
                if (_RunTaskData.Action_Type == ACTION_TYPE.None)
                    BuzzerPlayer.Move();
                else
                    BuzzerPlayer.Action();
            }
            if (ForkLifter.IsInitialing || ForkLifter.IsManualOperation || ForkLifter.CurrentHeightPosition <= Parameters.ForkAGV.SaftyPositionHeight)
            {
                BuzzerPlayer.Stop($"ChangeSubStatusAndLighterBuzzerWhenLaserRecoveryInForkRunning");
                if (ForkLifter.IsInitialing)
                {
                    //_Sub_Status = SUB_STATUS.Initializing;
                    StatusLighter.AbortFlash();
                    StatusLighter.FlashAsync(DO_ITEM.AGV_DiractionLight_Y, 600);
                }
            }
        }


        protected override async Task<(bool, string)> PreActionBeforeInitialize()
        {
            (bool, string) baseInitiazedResutl = await base.PreActionBeforeInitialize();
            if (!baseInitiazedResutl.Item1)
                return baseInitiazedResutl;

            if (!Parameters.CheckObstacleWhenForkInit)
                return (true, "");


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
            (bool forklifer_init_done, string message) _forklift_vertical_init_result = (false, "");
            (bool forklifer_init_done, string message) _forklift_horizon_init_result = (false, "");
            (bool pin_init_done, string message) _pin_init_result = (false, "");

            List<Task> _actions = new List<Task>();

            Task<(bool pin_init_done, string message)> forkFloatHarwarePinInitTask = ForkFloatHardwarePinInitProcess();
            Task<(bool forklifer_init_done, string message)> forkVerticalInitTask = VerticalForkInitProcess();
            Task<(bool forklifer_init_done, string message)> forkHorizonInitTask = HorizonForkInitProcess();

            _actions.Add(forkFloatHarwarePinInitTask);
            _actions.Add(forkVerticalInitTask);
            _actions.Add(forkHorizonInitTask);

            Task.WaitAll(_actions.ToArray());

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


        internal async Task<(bool, string)> ForkFloatHardwarePinInitProcess()
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
                (bool pin_init_done, string message) _pin_init_result = (false, "");
                InitializingStatusText = "PIN-模組初始化";
                try
                {
                    await PinHardware.Init();
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
                return _pin_init_result;
            });
        }

        internal async Task<(bool, string)> VerticalForkInitProcess()
        {
            if (GetSub_Status() != SUB_STATUS.Initializing)
                SetSub_Status(SUB_STATUS.Initializing);
            return await Task.Run(async () =>
            {
                (bool forklifer_init_done, string message) _forklift_init_result = (false, "");
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
                        forkInitizeResult = await ForkLifter.VerticalForkInitialize(_speed_of_init);
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

        internal async Task<(bool, string)> HorizonForkInitProcess()
        {
            if (!Parameters.ForkAGV.IsForkIsExtendable)
            {
                DebugMessageBrocast("Fork Is Not Extendable,Horizon Fork arm initialize is bypassed!");
                return (true, "Fork Is Not Extendable");
            }
            if (Parameters.ForkAGV.IsHorizonExtendDisabledTemptary)
            {
                SendNotifyierToFrontend($"注意! 伸縮牙叉暫時被禁用中");
                return (true, "伸縮牙叉已暫時禁用");
            }
            return await Task.Run(async () =>
            {
                try
                {
                    var resetResult = await ForkLifter.ForkHorizonResetAsync();
                    if (!resetResult.success)
                        return resetResult;

                    return await ForkLifter.ForkShortenInAsync();
                }
                catch (Exception ex)
                {
                    return (false, ex.Message);
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
            if (!status)
                return;
            base.HandleDriversStatusErrorAsync(sender, status);
            var signal = (sender as clsIOSignal);
            if (signal.Input == DI_ITEM.Vertical_Motor_Alarm)
            {
                await Task.Delay(1000);
                if (!WagoDI.GetState(DI_ITEM.EMO) || IsResetAlarmWorking)
                    return;
                AlarmManager.AddAlarm(AlarmCodes.Vertical_Motor_IO_Error, false);
            }
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
                DebugMessageBrocast($"{signal.Name} Now is ON!");
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
                BuzzerPlayer.Alarm();
                return (false, "Z軸解煞車旋鈕尚未復歸");
            }
            return base.CheckHardwareStatus();
        }

        protected override async Task DOSettingWhenEmoTrigger()
        {
            await base.DOSettingWhenEmoTrigger();
            await WagoDO.SetState(DO_ITEM.Vertical_Motor_Stop, true);
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
