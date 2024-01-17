using AGVSystemCommonNet6.AGVDispatch.Messages;
using AGVSystemCommonNet6.GPMRosMessageNet.Messages;
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

        public override bool IsFrontendSideHasObstacle => !WagoDI.GetState(DI_ITEM.Fork_Frontend_Abstacle_Sensor);
        public ForkAGV() : base()
        {
            ForkLifter = new clsForkLifter(this);
            ForkLifter.Driver = VerticalDriverState;
            ForkLifter.DIModule = WagoDI;
            ForkLifter.DOModule = WagoDO;
            ForkMovingProtectedProcess();
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
            if (Sub_Status == SUB_STATUS.IDLE)
                BuzzerPlayer.Stop();
        }

        private void _fork_car_controller_OnForkStartMove(object? sender, string command)
        {
            //throw new NotImplementedException();
            LOG.TRACE($"Fork Star Run (Started by:{command})");
            _ForkSaftyProtectFlag = true;
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
                    if (_ForkSaftyProtectFlag)
                    {
                        if (_isLsrTrigger && !_isStopped)
                        {
                            LOG.TRACE("Side Laser Trigger, Stop Fork");
                            ForkLifter.ForkStopAsync();
                            _isStopped = true;
                            AGVStatusChangeToAlarmWhenLaserTrigger();

                        }
                    }
                    if (!_isLsrTrigger && _isStopped)
                    {
                        LOG.TRACE("Side Laser Reconvery, Resume Fork Action");
                        ForkLifter.ForkResumeAction();
                        _isStopped = false;
                        if (ForkLifter.IsInitialing || _RunTaskData.IsActionFinishReported)
                        {
                            BuzzerPlayer.Stop();
                        }
                        else
                        {
                            if (_RunTaskData.Action_Type == ACTION_TYPE.None)
                                BuzzerPlayer.Move();
                            else
                                BuzzerPlayer.Action();
                        }
                    }
                }

            });
            _thread.Start();
            LOG.TRACE("Start Fork Safty Protect Process Thread");
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
                        return (false, home_action_response.alarm_code.ToString());
                    }
                }

                if (!Parameters.SensorBypass.BeltSensorBypass)
                    await WagoDO.SetState(DO_ITEM.Vertical_Belt_SensorBypass, false);

                AlarmManager.ClearAlarm(AlarmCodes.Fork_Has_Cargo_But_Initialize_Running);
                return (forkInitizeResult.done, forkInitizeResult.alarm_code.ToString());
            }
            else
            {
                AlarmManager.AddWarning(AlarmCodes.Fork_Disabled);
            }
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

                if (Sub_Status == SUB_STATUS.RUN)
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
