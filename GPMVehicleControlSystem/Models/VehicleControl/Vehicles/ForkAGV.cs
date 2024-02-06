using AGVSystemCommonNet6.AGVDispatch.Messages;
using AGVSystemCommonNet6.GPMRosMessageNet.Messages;
using AGVSystemCommonNet6.GPMRosMessageNet.Services;
using AGVSystemCommonNet6.Log;
using AGVSystemCommonNet6.Vehicle_Control.VCS_ALARM;
using GPMVehicleControlSystem.Models.Buzzer;
using GPMVehicleControlSystem.Models.VehicleControl.AGVControl;
using GPMVehicleControlSystem.Models.VehicleControl.VehicleComponent;
using GPMVehicleControlSystem.Models.WorkStation;
using GPMVehicleControlSystem.VehicleControl.DIOModule;
using Newtonsoft.Json;
using System.Diagnostics;
using System.Reflection;
using System.Reflection.Metadata.Ecma335;
using static AGVSystemCommonNet6.clsEnums;
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
        public bool IsForkInitialized => ForkLifter.IsInitialized;
        public bool IsForkWorking => (AGVC as ForkAGVController).WaitActionDoneFlag;

        public override clsWorkStationModel WorkStations { get; set; } = new clsWorkStationModel();
        public override clsForkLifter ForkLifter { get; set; } = new clsForkLifter();

        public clsPin PinHardware { get; set; }
        public override bool IsFrontendSideHasObstacle => !WagoDI.GetState(DI_ITEM.Fork_Frontend_Abstacle_Sensor);
        public ForkAGV() : base()
        {
            ForkLifter = new clsForkLifter(this);
            ForkLifter.Driver = VerticalDriverState;
            ForkLifter.DIModule = WagoDI;
            ForkLifter.DOModule = WagoDO;
            if (Parameters.ForkAGV.IsPinMounted)
                PinHardware = new clsPin();

            LOG.INFO($"FORK AGV 搭載Pin模組?{PinHardware != null}");
            ForkMovingProtectedProcess();
        }

        protected internal override async Task InitAGVControl(string RosBridge_IP, int RosBridge_Port)
        {
            await base.InitAGVControl(RosBridge_IP, RosBridge_Port);
            if (PinHardware != null)
                PinHardware.rosSocket = AGVC.rosSocket;
        }

        public override CARGO_STATUS CargoStatus
        {
            get
            {
                return GetCargoStatus();
            }
        }
        protected override CARGO_STATUS GetCargoStatus()
        {
            bool existSensor_1 = !WagoDI.GetState(DI_ITEM.Fork_RACK_Left_Exist_Sensor);
            bool existSensor_2 = !WagoDI.GetState(DI_ITEM.Fork_RACK_Right_Exist_Sensor);
            if (existSensor_1 && existSensor_2)
                return CARGO_STATUS.HAS_CARGO_NORMAL;
            if (!existSensor_1 && !existSensor_2)
                return CARGO_STATUS.NO_CARGO;
            if ((!existSensor_1 && existSensor_2) || (existSensor_1 && !existSensor_2))
                return CARGO_STATUS.HAS_CARGO_BUT_BIAS;
            return CARGO_STATUS.NO_CARGO;
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
                LOG.ERROR(ex);
                IsMotorReseting = false;
                return false;
            }
        }
        public override async Task<bool> ResetMotor(bool bypass_when_motor_busy_on = true)
        {
            try
            {
                await base.ResetMotor(bypass_when_motor_busy_on);

                if (WagoDI.GetState(DI_ITEM.Vertical_Motor_Busy))
                    return true;
                return await ResetVerticalDriver();
            }
            catch (Exception ex)
            {
                LOG.Critical(ex.Message, ex);
                return false;
            }
        }
        protected override void CommonEventsRegist()
        {
            base.CommonEventsRegist();
            var _fork_car_controller = (AGVC as ForkAGVController);
            _fork_car_controller.OnForkStartGoHome += () => { return Parameters.ForkAGV.SaftyPositionHeight; };
            _fork_car_controller.OnForkStartMove += _fork_car_controller_OnForkStartMove;
            _fork_car_controller.OnForkStopMove += _fork_car_controller_OnForkStopMove;
            ForkLifter.Driver.OnAlarmHappened += async (alarm_code) =>
            {
                if (alarm_code != AlarmCodes.None)
                {
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
            LOG.TRACE("Fork Stop");
            _ForkSaftyProtectFlag = false;
            if (GetSub_Status() == SUB_STATUS.IDLE)
                BuzzerPlayer.Stop();
        }

        private void _fork_car_controller_OnForkStartMove(object? sender, VerticalCommandRequest request)
        {
            //throw new NotImplementedException();
            LOG.TRACE($"Fork Star Run (Started by:{request.command})");
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
                            LOG.WARN("Side Laser Trigger, Stop Fork");
                            ForkLifter.ForkStopAsync();
                            _isStopped = true;
                            BuzzerPlayer.Alarm();
                            //AGVStatusChangeToAlarmWhenLaserTrigger();

                        }
                    }
                    if (_isLaserSafeAndForkIsStopping)
                    {
                        LOG.INFO("Side Laser Reconvery, Resume Fork Action");
                        ChangeSubStatusAndLighterBuzzerWhenLaserRecoveryInForkRunning();
                        ForkLifter.ForkResumeAction();
                        _isStopped = false;
                    }
                }

            });
            _thread.Start();
            LOG.TRACE("Start Fork Safty Protect Process Thread");
        }
        private bool _isLoadUnloadTaskRunning => _RunTaskData.IsLDULDAction() && !_RunTaskData.IsActionFinishReported;
        private void ChangeSubStatusAndLighterBuzzerWhenLaserRecoveryInForkRunning()
        {
            if (GetSub_Status() == SUB_STATUS.DOWN)
                return;


            bool _isForkRunningPreActionAndNoObstacleArround = ForkLifter.IsHeightPreSettingActionRunning && IsAllLaserNoTrigger().Result;

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
                BuzzerPlayer.Stop();
                if (ForkLifter.IsInitialing)
                {
                    //_Sub_Status = SUB_STATUS.Initializing;
                    StatusLighter.AbortFlash();
                    StatusLighter.Flash(DO_ITEM.AGV_DiractionLight_Y, 600);
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

            bool RightLaserAbnormal = !WagoDI.GetState(DI_ITEM.RightProtection_Area_Sensor_3);
            if (RightLaserAbnormal)
                return (false, "無法在障礙物入侵的狀態下進行初始化(右方障礙物檢出)");
            bool LeftLaserAbnormal = !WagoDI.GetState(DI_ITEM.LeftProtection_Area_Sensor_3);
            if (LeftLaserAbnormal)
                return (false, "無法在障礙物入侵的狀態下進行初始化(左方障礙物檢出)");

            await WagoDO.SetState(DO_ITEM.Vertical_Motor_Stop, false);
            await WagoDO.SetState(DO_ITEM.Fork_Under_Pressing_SensorBypass, false);
            await WagoDO.SetState(DO_ITEM.Vertical_Hardware_limit_bypass, false);
            await WagoDO.SetState(DO_ITEM.Vertical_Belt_SensorBypass, false);
            return (true, "");
        }
        protected override async Task<(bool confirm, string message)> InitializeActions(CancellationTokenSource cancellation)
        {
            (bool forklifer_init_done, string message) _forklift_init_result = (false, "");
            (bool pin_init_done, string message) _pin_init_result = (false, "");

            List<Task> _actions = new List<Task>();
            if (PinHardware != null)
            {
                Task Pin_Init_Task = await Task.Factory.StartNew(async () =>
                {
                    InitializingStatusText = "PIN-模組初始化";
                    try
                    {
                        await PinHardware.Init();
                        _pin_init_result = (true, "");
                    }
                    catch (TimeoutException)
                    {
                        _pin_init_result = (false, "Pin Action Timeout");
                    }
                });
                _actions.Add(Pin_Init_Task);
            }
            else
                _pin_init_result = (true, "Pin is not mounted");

            Task ForkLift_Init_Task = await Task.Factory.StartNew(async () =>
            {
                await Task.Delay(700);
                InitializingStatusText = "牙叉初始化動作中";
                ForkLifter.fork_ros_controller.CurrentForkActionRequesting = new AGVSystemCommonNet6.GPMRosMessageNet.Services.VerticalCommandRequest();
                if (ForkLifter.Enable)
                {
                    ForkLifter.ForkShortenInAsync();
                    if (HasAnyCargoOnAGV())
                    {
                        AlarmManager.AddWarning(AlarmCodes.Fork_Has_Cargo_But_Initialize_Running);
                    }
                    InitializingStatusText = "牙叉原點覆歸...";
                    await WagoDO.SetState(DO_ITEM.Vertical_Belt_SensorBypass, true);

                    bool isForkAllowNoDoInitializeAction = Parameters.SimulationMode || Parameters.ForkNoInitializeWhenPoseIsHome && ForkLifter.CurrentForkLocation == clsForkLifter.FORK_LOCATIONS.HOME;

                    (bool done, AlarmCodes alarm_code) forkInitizeResult = (false, AlarmCodes.None);
                    if (isForkAllowNoDoInitializeAction)
                    {
                        (bool confirm, string message) ret = await ForkLifter.ForkPositionInit();
                        forkInitizeResult = (ret.confirm, AlarmCodes.Fork_Initialized_But_Driver_Position_Not_ZERO);
                        ForkLifter.IsInitialized = true;
                    }
                    else
                    {
                        double _speed_of_init = HasAnyCargoOnAGV() ? Parameters.ForkAGV.InitParams.ForkInitActionSpeedWithCargo : Parameters.ForkAGV.InitParams.ForkInitActionSpeedWithoutCargo;
                        forkInitizeResult = await ForkLifter.ForkInitialize(_speed_of_init);
                    }
                    if (forkInitizeResult.done)
                    {
                        //self test Home action 
                        (bool confirm, AlarmCodes alarm_code) home_action_response = await ForkLifter.ForkGoHome();
                        if (!home_action_response.confirm)
                        {
                            _forklift_init_result = (false, home_action_response.alarm_code.ToString());
                        }
                    }

                    if (!Parameters.SensorBypass.BeltSensorBypass)
                        await WagoDO.SetState(DO_ITEM.Vertical_Belt_SensorBypass, false);

                    AlarmManager.ClearAlarm(AlarmCodes.Fork_Has_Cargo_But_Initialize_Running);
                    _forklift_init_result = (forkInitizeResult.done, forkInitizeResult.alarm_code.ToString());
                }
                else
                {
                    AlarmManager.AddWarning(AlarmCodes.Fork_Disabled);
                    _forklift_init_result = (true, "");
                }
            });
            _actions.Add(ForkLift_Init_Task);
            Task.WaitAll(_actions.ToArray());

            if (!_pin_init_result.pin_init_done)
                return _pin_init_result;

            if (!_forklift_init_result.forklifer_init_done)
                return _forklift_init_result;

            return await base.InitializeActions(cancellation);
        }

        protected override void CreateAGVCInstance(string RosBridge_IP, int RosBridge_Port)
        {
            AGVC = new ForkAGVController(RosBridge_IP, RosBridge_Port);
            (AGVC as ForkAGVController).OnCSTReaderActionDone += CSTReader.UpdateCSTIDDataHandler;
            LOG.TRACE($"(AGVC as ForkAGVController).OnCSTReaderActionDone += CSTReader.UpdateCSTIDDataHandler;");
        }

        protected internal override void SoftwareEMO(AlarmCodes alarmCode)
        {
            Task.Factory.StartNew(async () =>
            {
                LOG.Critical($"SW EMS Trigger, Fork Action STOP!!!!!!(LIFER AND ARM)");
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

                if (GetSub_Status() == SUB_STATUS.RUN)
                {
                    AlarmManager.AddAlarm(AlarmCodes.Vertical_Motor_IO_Error, false);
                    return;
                }
                AlarmManager.AddWarning(AlarmCodes.Vertical_Motor_IO_Error);
                #region 嘗試Reset馬達
                _ = Task.Factory.StartNew(async () =>
                {
                    while (signal.State)
                    {
                        await Task.Delay(1000);
                        await ResetMotorWithWait(TimeSpan.FromSeconds(5), signal, AlarmCodes.Vertical_Motor_IO_Error);
                    }
                });
                #endregion
            }
        }
        protected override void DIOStatusChangedEventRegist()
        {
            base.DIOStatusChangedEventRegist();
            WagoDI.SubsSignalStateChange(DI_ITEM.Vertical_Motor_Alarm, HandleDriversStatusErrorAsync);

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
        internal override bool HasAnyCargoOnAGV()
        {
            try
            {
                return !WagoDI.GetState(DI_ITEM.Fork_RACK_Left_Exist_Sensor) || !WagoDI.GetState(DI_ITEM.Fork_RACK_Right_Exist_Sensor);
            }
            catch (Exception)
            {
                return false;
            }
        }


        protected override int GetCargoType()
        {
            var rack_sensor1 = WagoDI.GetState(DI_ITEM.Fork_RACK_Left_Exist_Sensor);
            var rack_sensor2 = WagoDI.GetState(DI_ITEM.Fork_RACK_Right_Exist_Sensor);
            if (rack_sensor2 || rack_sensor1)
                return 1;
            else return 0;
        }
        protected override async Task DOSettingWhenEmoTrigger()
        {
            await base.DOSettingWhenEmoTrigger();
            await WagoDO.SetState(DO_ITEM.Vertical_Motor_Stop, true);
        }
    }
}
