using AGVSystemCommonNet6;
using AGVSystemCommonNet6.AGVDispatch;
using AGVSystemCommonNet6.AGVDispatch.Messages;
using AGVSystemCommonNet6.GPMRosMessageNet.Messages;
using AGVSystemCommonNet6.GPMRosMessageNet.SickSafetyscanners;
using AGVSystemCommonNet6.Log;
using AGVSystemCommonNet6.MAP;
using AGVSystemCommonNet6.Vehicle_Control;
using AGVSystemCommonNet6.Vehicle_Control.Models;
using AGVSystemCommonNet6.Vehicle_Control.VCS_ALARM;
using AGVSystemCommonNet6.Vehicle_Control.VCSDatabase;
using GPMVehicleControlSystem.Models.Buzzer;

using GPMVehicleControlSystem.Models.VehicleControl.AGVControl;
using GPMVehicleControlSystem.Models.VehicleControl.VehicleComponent;
using GPMVehicleControlSystem.Models.VehicleControl.Vehicles.Params;
using GPMVehicleControlSystem.Models.WebsocketMiddleware;
using GPMVehicleControlSystem.Models.WorkStation;
using GPMVehicleControlSystem.VehicleControl.DIOModule;
using Newtonsoft.Json;
using RosSharp.RosBridgeClient;
using RosSharp.RosBridgeClient.Actionlib;
using RosSharp.RosBridgeClient.MessageTypes.Geometry;
using System.Diagnostics;
using System.Drawing;
using System.Net.Sockets;
using System.Reflection.Metadata;
using static AGVSystemCommonNet6.AGVDispatch.Messages.clsVirtualIDQu;
using static AGVSystemCommonNet6.clsEnums;
using static GPMVehicleControlSystem.Models.VehicleControl.VehicleComponent.clsLaser;
using static GPMVehicleControlSystem.VehicleControl.DIOModule.clsDIModule;
using static GPMVehicleControlSystem.VehicleControl.DIOModule.clsDOModule;

namespace GPMVehicleControlSystem.Models.VehicleControl.Vehicles
{
    /// <summary>
    /// 車子
    /// </summary>
    public abstract partial class Vehicle
    {
        public enum CARGO_STATUS
        {
            /// <summary>
            /// 沒有貨物(通常為所有在席訊號皆ON)
            /// </summary>
            NO_CARGO,
            /// <summary>
            /// 有貨物且正常裝載(通常為所有在席訊號皆OFF)
            /// </summary>
            HAS_CARGO_NORMAL,
            /// <summary>
            /// 有貨物但傾斜(部分在席訊號OFF/部分ON)
            /// </summary>
            HAS_CARGO_BUT_BIAS,
            /// <summary>
            /// 無載物功能(如巡檢AGV)
            /// </summary>
            NO_CARGO_CARRARYING_CAPABILITY
        }
        public enum VMS_PROTOCOL
        {
            KGS,
            GPM_VMS
        }

        public enum ORDER_INFO_FETCH_SOURCE
        {
            FROM_TASK_DOWNLOAD_CONTENT,
            FROM_CIM_POST_IN
        }
        public abstract clsDirectionLighter DirectionLighter { get; set; }
        public clsStatusLighter StatusLighter { get; set; }
        public clsAGVSConnection AGVS;
        public clsDOModule WagoDO;
        public clsDIModule WagoDI;
        public CarController AGVC;
        public clsLaser Laser;
        //public AGVPILOT Pilot { get; set; }
        public clsNavigation Navigation = new clsNavigation();
        public abstract Dictionary<ushort, clsBattery> Batteries { get; set; }
        public clsIMU IMU = new clsIMU();
        public clsGuideSensor GuideSensor = new clsGuideSensor();
        public clsBarcodeReader BarcodeReader = new clsBarcodeReader();
        public abstract clsCSTReader CSTReader { get; set; }

        public clsDriver VerticalDriverState = new clsDriver()
        {
            location = clsDriver.DRIVER_LOCATION.FORK
        };

        /// <summary>
        /// 工位數據
        /// </summary>
        public virtual clsWorkStationModel WorkStations { get; set; } = new clsWorkStationModel();
        public virtual clsForkLifter ForkLifter { get; set; }

        public clsDriver[] WheelDrivers = new clsDriver[] {
             new clsDriver{ location = clsDriver.DRIVER_LOCATION.LEFT},
             new clsDriver{ location = clsDriver.DRIVER_LOCATION.RIGHT},
             new clsDriver{ location = clsDriver.DRIVER_LOCATION.RIGHT},
             new clsDriver{ location = clsDriver.DRIVER_LOCATION.RIGHT}
        };
        public clsSick SickData = new clsSick();
        public Map NavingMap = new Map()
        {
            Name = "No_load",
            Points = new Dictionary<int, MapPoint>()
        };
        public bool IsHasCSTReader => CSTReader != null;
        internal bool IsWaitForkNextSegmentTask = false;
        internal bool IsHandshaking = false;
        private string _HandshakeStatusText = "";
        internal string HandshakeStatusText
        {
            get => _HandshakeStatusText;
            set
            {
                if (_HandshakeStatusText != value)
                {
                    _HandshakeStatusText = value;
                    //LOG.TRACE(_HandshakeStatusText);
                }
            }
        }
        internal string InitializingStatusText = "";
        /// <summary>
        /// 里程數
        /// </summary>
        public double Odometry;
        public virtual CARGO_STATUS CargoStatus
        {
            get
            {
                return CARGO_STATUS.NO_CARGO_CARRARYING_CAPABILITY;
            }
        }
        protected virtual List<CarComponent> CarComponents
        {
            get
            {
                var ls = new List<CarComponent>()
                {
                    Navigation,IMU,GuideSensor, BarcodeReader
                };
                ls.AddRange(Batteries.Values.ToArray());
                ls.AddRange(WheelDrivers);
                return ls;
            }
        }

        /// <summary>
        /// 手動/自動模式
        /// </summary>
        public OPERATOR_MODE Operation_Mode { get; internal set; } = OPERATOR_MODE.MANUAL;


        /// <summary>
        /// 與 AGVS Reset Command 相關，若收到 AGVS 下 AGVS Reset
        /// Command 給 AGV
        ///⚫ AGVC 停下後為 true
        ///⚫ 重新收到 0301 任務後為 fals
        /// </summary>
        public bool AGV_Reset_Flag { get; internal set; }

        /// <summary>
        /// Replan訊號
        /// </summary>
        private bool _AGVSResetCmdFlag = false;
        internal bool AGVSResetCmdFlag
        {
            get => _AGVSResetCmdFlag;
            set
            {
                if (_AGVSResetCmdFlag != value)
                {
                    _AGVSResetCmdFlag = value;
                    LOG.WARN($"AGVSResetCmdFlag changed to :{value}");
                }
            }
        }
        public MoveControl ManualController => AGVC.ManualController;

        public bool IsInitialized { get; internal set; }
        public bool IsSystemInitialized { get; internal set; }
        protected bool IsResetAlarmWorking = false;
        protected bool IsMotorReseting = false;
        internal SUB_STATUS _Sub_Status = SUB_STATUS.DOWN;
        public MapPoint lastVisitedMapPoint { get; private set; } = new MapPoint();
        public bool _IsCharging = false;
        public virtual bool IsFrontendSideHasObstacle => WagoDI.GetState(DI_ITEM.FrontProtection_Obstacle_Sensor);
        public MapPoint DestinationMapPoint
        {
            get
            {
                var _mapPoint_default = new MapPoint { Name = _RunTaskData.Destination.ToString(), TagNumber = _RunTaskData.Destination, Graph = new Graph() { Display = _RunTaskData.Destination + "" } };
                try
                {
                    var _point = NavingMap.Points.Values.FirstOrDefault(pt => pt.TagNumber == _RunTaskData.Destination);
                    return _point == null ? _mapPoint_default : _point;
                }
                catch (Exception)
                {
                    return _mapPoint_default;
                }
            }
        }

        private clsAGVStatusTrack status_data_store;

        public bool IsCstReaderMounted => CSTReader != null;

        /// <summary>
        /// 是否為有料無帳的狀態
        /// </summary>
        public bool IsNoCargoButIDExist => !HasAnyCargoOnAGV() && CSTReader.ValidCSTID != "";
        /// <summary>
        /// 是否為有帳無料的狀態
        /// </summary>
        public bool IsCargoExistButNoID => CSTReader.ValidCSTID == "" && HasAnyCargoOnAGV();

        private async Task StoreStatusToDataBase()
        {

            await Task.Delay(1);
            if (status_data_store == null)
                status_data_store = new clsAGVStatusTrack { Status = SUB_STATUS.STOP };


            try
            {
                List<clsBattery> batterys = Batteries.Values.ToList();
                double _bat1_level = batterys.Count >= 1 ? batterys[0].Data.batteryLevel : -1;
                double _bat2_level = batterys.Count >= 2 ? batterys[0].Data.batteryLevel : -1;
                double _bat1_voltage = batterys.Count >= 1 ? batterys[0].Data.Voltage : -1;
                double _bat2_voltage = batterys.Count >= 2 ? batterys[0].Data.Voltage : -1;


                string _Task_Name = "";
                string _Task_Simplex = "";
                ACTION_TYPE _TaskAction = ACTION_TYPE.NoAction;
                int _DestineTag = Navigation.LastVisitedTag;
                bool IsRunStatus = GetSub_Status() == SUB_STATUS.RUN;
                if (IsRunStatus)
                {
                    _Task_Name = _RunTaskData.Task_Name;
                    _Task_Simplex = _RunTaskData.Task_Simplex;
                    _TaskAction = _RunTaskData.Action_Type;
                    _DestineTag = _RunTaskData.Destination;
                }
                clsAGVStatusTrack status_data = new clsAGVStatusTrack
                {
                    Time = DateTime.Now,
                    Status = GetSub_Status(),
                    BatteryLevel1 = _bat1_level,
                    BatteryLevel2 = _bat2_level,
                    BatteryVoltage1 = _bat1_voltage,
                    BatteryVoltage2 = _bat2_voltage,
                    ExecuteTaskName = _Task_Name,
                    ExecuteTaskSimpleName = _Task_Simplex,
                    TaskAction = _TaskAction,
                    CargoID = CSTReader?.ValidCSTID,
                    Odometry = this.Odometry,
                    DestineTag = _DestineTag
                };
                if (status_data_store.Status == status_data.Status)
                {
                    return;
                }
                LOG.TRACE($"Try Write AGV Status to Database \n{status_data.ToJson()}", false);
                status_data_store = status_data;
                DBhelper.AddAgvStatusData(status_data);
            }
            catch (Exception ex)
            {
                LOG.ERROR(ex, false);
            }
        }

        /// <summary>
        /// AGV是否有搭載極限Sensor
        /// </summary>
        public bool IsLimitSwitchSensorMounted
        {
            get
            {
                try
                {
                    var inputs_mounted = WagoDI.VCSInputs.Select(i => i.Input).ToList();
                    return inputs_mounted.Any(input => input is DI_ITEM.Limit_Switch_Sensor);
                }
                catch (Exception)
                {
                    return false;
                }

            }
        }
        public bool IsForkExtenable
        {
            get
            {
                try
                {
                    return WagoDO.VCSOutputs.Any(item => item.Output is DO_ITEM.Fork_Extend);
                }
                catch (Exception)
                {
                    return false;
                }
            }
        }
        public Vehicle()
        {
            try
            {
                Parameters = LoadParameters(watch_file_change: true);
                Parameters._EQHandshakeMethodStore = Parameters.EQHandshakeMethod;
                IMU.Options = Parameters.ImpactDetection;
                CIMConnectionInitialize();
                LoadWorkStationConfigs();
                LOG.INFO($"{GetType().Name} Start create instance...");
                ReadTaskNameFromFile();
                IsSystemInitialized = false;
                Params.clsConnectionParam wago_connection_params = Parameters.Connections[Params.clsConnectionParam.CONNECTION_ITEM.Wago];
                Params.clsConnectionParam rosbridge_connection_params = Parameters.Connections[Params.clsConnectionParam.CONNECTION_ITEM.RosBridge];
                string Wago_IP = wago_connection_params.IP;
                int Wago_Port = wago_connection_params.Port;
                int Wago_Protocol_Interval_ms = wago_connection_params.Protocol_Interval_ms;
                int LastVisitedTag = Parameters.LastVisitedTag;
                string RosBridge_IP = rosbridge_connection_params.IP;
                int RosBridge_Port = rosbridge_connection_params.Port;
                WagoDO = new clsDOModule(Wago_IP, Wago_Port)
                {
                    AgvType = Parameters.AgvType,
                    Version = Parameters.Version,
                };
                WagoDI = new clsDIModule(Wago_IP, Wago_Port, Wago_Protocol_Interval_ms)
                {
                    AgvType = Parameters.AgvType,
                    Version = Parameters.Version
                };

                DirectionLighter.DOModule = WagoDO;

                StatusLighter = new clsStatusLighter(WagoDO);
                CreateLaserInstance();

                WebsocketAgent.Middleware.Initialize();

                List<Task> WagoAndRosInitTasks = new List<Task>();
                WagoAndRosInitTasks.Add(WagoDIInit());
                WagoAndRosInitTasks.Add(RosConnAsync(RosBridge_IP, RosBridge_Port, LastVisitedTag));



                Task.WhenAll(WagoAndRosInitTasks).ContinueWith(async t =>
                {

                    while (!ModuleInformationUpdatedInitState && Parameters.AgvType != AGV_TYPE.SUBMERGED_SHIELD_Parts)
                    {
                        await Task.Delay(1000);
                    }
                    await Startup();

                });
            }
            catch (Exception ex)
            {
                IsSystemInitialized = false;
                string msg = $"車輛實例化時於建構函式發生錯誤 : {ex.Message}:{ex.StackTrace}";
                throw ex;
            }

        }

        internal virtual void CreateLaserInstance()
        {
            Laser = new clsLaser(WagoDO, WagoDI)
            {
                Spin_Laser_Mode = Parameters.Spin_Laser_Mode
            };
        }

        private async Task Startup()
        {
            CommonEventsRegist();
            AGVSInit();
            StartConfigChangedWatcher();

            try
            {
                SyncHandshakeSignalStates();
                WagoDI.RegistSignalEvents();
                await DOSignalDefaultSetting();
                await ResetMotor(false);
                DIOStatusChangedEventRegist();
                BuzzerPlayer.Alarm();
                AlarmManager.Active = true;
                AlarmManager.RecordAlarm(AlarmCodes.None);
                if (HasAnyCargoOnAGV() && CSTReader != null)
                {
                    CSTReader.ReadCSTIDFromLocalStorage();
                }
                LOG.INFO($"AGV 搭載極限Sensor?{IsLimitSwitchSensorMounted}");
                LOG.INFO($"AGV 牙叉可伸縮?{IsForkExtenable}");
                IsSystemInitialized = true;
            }
            catch (Exception ex)
            {
                LOG.ERROR(ex.Message, ex);
            }
        }

        protected virtual void SyncHandshakeSignalStates()
        {
            EQHsSignalStates[EQ_HSSIGNAL.EQ_READY].State = WagoDI.GetState(DI_ITEM.EQ_READY);
            EQHsSignalStates[EQ_HSSIGNAL.EQ_BUSY].State = WagoDI.GetState(DI_ITEM.EQ_BUSY);
            EQHsSignalStates[EQ_HSSIGNAL.EQ_L_REQ].State = WagoDI.GetState(DI_ITEM.EQ_L_REQ);
            EQHsSignalStates[EQ_HSSIGNAL.EQ_U_REQ].State = WagoDI.GetState(DI_ITEM.EQ_U_REQ);
            EQHsSignalStates[EQ_HSSIGNAL.EQ_GO].State = WagoDI.GetState(DI_ITEM.EQ_GO);

            AGVHsSignalStates[AGV_HSSIGNAL.AGV_VALID] = WagoDO.GetState(DO_ITEM.AGV_VALID);
            AGVHsSignalStates[AGV_HSSIGNAL.AGV_READY] = WagoDO.GetState(DO_ITEM.AGV_READY);
            AGVHsSignalStates[AGV_HSSIGNAL.AGV_TR_REQ] = WagoDO.GetState(DO_ITEM.AGV_TR_REQ);
            AGVHsSignalStates[AGV_HSSIGNAL.AGV_BUSY] = WagoDO.GetState(DO_ITEM.AGV_BUSY);
            AGVHsSignalStates[AGV_HSSIGNAL.AGV_COMPT] = WagoDO.GetState(DO_ITEM.AGV_COMPT);
        }
        private async Task RosConnAsync(string RosBridge_IP, int RosBridge_Port, int LastVisitedTag)
        {
            await InitAGVControl(RosBridge_IP, RosBridge_Port);
            if (AGVC?.rosSocket != null)
            {
                AGVC.OnRosSocketReconnected += AGVC_OnRosSocketReconnected;
                AGVC.OnRosSocketDisconnected += AGVC_OnRosSocketDisconnected;
                BuzzerPlayer.rossocket = AGVC.rosSocket;

                lastVisitedMapPoint = new MapPoint(LastVisitedTag + "", LastVisitedTag);
                Navigation.StateData = new NavigationState() { lastVisitedNode = new RosSharp.RosBridgeClient.MessageTypes.Std.Int32(LastVisitedTag) };
                BarcodeReader.StateData = new BarcodeReaderState() { tagID = (uint)LastVisitedTag };


            }
        }

        private void AGVC_OnRosSocketDisconnected(object? sender, EventArgs e)
        {
            ModuleInformationUpdatedInitState = false;
            AlarmManager.AddAlarm(AlarmCodes.Motion_control_Disconnected, false);
        }

        private void AGVC_OnRosSocketReconnected(object? sender, EventArgs e)
        {
            BuzzerPlayer.rossocket = (RosSocket)sender;
        }

        public string WorkStationSettingsJsonFilePath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "param/WorkStation.json");

        private void LoadWorkStationConfigs()
        {
            try
            {

                if (!File.Exists(WorkStationSettingsJsonFilePath))
                {
                    File.Copy(Path.Combine(Environment.CurrentDirectory, "src/WorkStation.json"), WorkStationSettingsJsonFilePath);
                }
                string json = File.ReadAllText(WorkStationSettingsJsonFilePath);
                if (json == null)
                {
                    LOG.ERROR("Load Fork Teach Data Fail...Read Json Null");
                    return;
                }
                WorkStations = DeserializeWorkStationJson(json);


            }
            catch (Exception ex)
            {
                LOG.ERROR($"Load Fork Teach Data Fail...{ex.Message}");
            }
            finally
            {
                SaveTeachDAtaSettings();
            }
        }
        protected virtual clsWorkStationModel DeserializeWorkStationJson(string json)
        {
            return JsonConvert.DeserializeObject<clsWorkStationModel>(json);
        }
        internal bool SaveTeachDAtaSettings()
        {
            try
            {
                string json = JsonConvert.SerializeObject(WorkStations, Formatting.Indented);
                File.WriteAllText(WorkStationSettingsJsonFilePath, json);
                LOG.INFO($"WorkStation Settings Save done");
                return true;
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }


        private void AGVS_OnOnlineStateQueryFail(object? sender, EventArgs e)
        {
        }

        private async Task WagoDIInit()
        {

            try
            {
                LOG.INFO($"DIO Module Connecting...{WagoDI.IP}:{WagoDI.VMSPort}");
                while (!await WagoDI.Connect())
                {
                    await Task.Delay(1000);
                }
                while (!await WagoDO.Connect())
                {
                    await Task.Delay(1000);
                }
                WagoDI.StartAsync();

            }
            catch (SocketException ex)
            {
                LOG.Critical($"初始化Wago 連線的過程中發生Socket 例外 , {ex.Message}", ex);
            }
            catch (Exception ex)
            {
                LOG.Critical($"初始化Wago 連線的過程中發生例外 , {ex.Message}", ex);
            }
        }

        internal async Task<bool> IsAllLaserNoTrigger()
        {
            await Task.Delay(10);

            bool LLsrBypassState = WagoDO.GetState(DO_ITEM.Left_LsrBypass);
            bool RLsrBypassState = WagoDO.GetState(DO_ITEM.Right_LsrBypass);
            bool FLsrBypassState = WagoDO.GetState(DO_ITEM.Front_LsrBypass);
            bool BLsrBypassState = WagoDO.GetState(DO_ITEM.Back_LsrBypass);


            var FrontArea1 = FLsrBypassState ? true : WagoDI.GetState(DI_ITEM.FrontProtection_Area_Sensor_1);
            var FrontArea2 = FLsrBypassState ? true : WagoDI.GetState(DI_ITEM.FrontProtection_Area_Sensor_2);
            var FrontArea3 = FLsrBypassState ? true : WagoDI.GetState(DI_ITEM.FrontProtection_Area_Sensor_3);

            var BackArea1 = BLsrBypassState ? true : WagoDI.GetState(DI_ITEM.BackProtection_Area_Sensor_1);
            var BackArea2 = BLsrBypassState ? true : WagoDI.GetState(DI_ITEM.BackProtection_Area_Sensor_2);
            var BackArea3 = BLsrBypassState ? true : WagoDI.GetState(DI_ITEM.BackProtection_Area_Sensor_3);

            var RightArea = RLsrBypassState ? true : WagoDI.GetState(DI_ITEM.RightProtection_Area_Sensor_3);
            var LeftArea = LLsrBypassState ? true : WagoDI.GetState(DI_ITEM.LeftProtection_Area_Sensor_3);

            LOG.INFO($"雷射狀態檢查(IsAllLaserNoTrigger)\r\n" +
                        $"Front_Area 1->3 ={FrontArea1.ToSymbol("O", "X")}|{FrontArea2.ToSymbol("O", "X")}|{FrontArea3.ToSymbol("O", "X")}\r\n" +
                        $"Back_Area  1->3 ={BackArea1.ToSymbol("O", "X")}|{BackArea2.ToSymbol("O", "X")}|{BackArea3.ToSymbol("O", "X")}\r\n" +
                        $"Right_Area      ={RightArea.ToSymbol("O", "X")}\r\n" +
                        $"Left_Area       ={LeftArea.ToSymbol("O", "X")}", false);

            return FrontArea1 && FrontArea2 && FrontArea3 && BackArea1 && BackArea2 && BackArea3 && RightArea && LeftArea;
        }

        protected virtual async Task DOSignalDefaultSetting()
        {
            //WagoDO.AllOFF();
            await WagoDO.SetState(DO_ITEM.AGV_DiractionLight_R, true);
            await WagoDO.SetState(DO_ITEM.Right_LsrBypass, true);
            await WagoDO.SetState(DO_ITEM.Left_LsrBypass, true);
            await Laser.ModeSwitch(16);
        }
        protected CancellationTokenSource InitializeCancelTokenResourece = new CancellationTokenSource();

        /// <summary>
        /// 初始化AGV
        /// </summary>
        /// <returns></returns>
        public async Task<(bool confirm, string message)> Initialize()
        {

            if (!ModuleInformationUpdatedInitState)
            {
                return (false, $"與車控系統通訊異常，不可進行初始化");
            }

            if (GetSub_Status() == SUB_STATUS.RUN)
            {
                return (false, $"當前狀態不可進行初始化(任務執行中)");
            }

            if (GetSub_Status() != SUB_STATUS.DOWN && (AGVC.ActionStatus == ActionStatus.ACTIVE || GetSub_Status() == SUB_STATUS.Initializing))
            {
                string reason_string = GetSub_Status() != SUB_STATUS.RUN ? (GetSub_Status() == SUB_STATUS.Initializing ? "初始化程序執行中" : "任務進行中") : "AGV狀態為RUN";
                return (false, $"當前狀態不可進行初始化({reason_string})");
            }

            if (lastVisitedMapPoint.IsEquipment)
            {
                return (false, "AGV位於設備內禁止初始化，請將AGV移動至道路Tag上");
            }

            if (WagoDI.Current_Alarm_Code != AlarmCodes.None || WagoDO.Current_Alarm_Code != AlarmCodes.None)
            {
                return (false, "IO 模組異常");
            }

            BuzzerPlayer.Stop();
            DirectionLighter.CloseAll();
            orderInfoViewModel.ActionName = ACTION_TYPE.NoAction;
            IsWaitForkNextSegmentTask = false;
            AGVSResetCmdFlag = false;
            InitializeCancelTokenResourece = new CancellationTokenSource();
            AlarmManager.ClearAlarm();
            clsEQHandshakeModbusTcp.HandshakingModbusTcpProcessCancel?.Cancel();
            _RunTaskData = new clsTaskDownloadData();
            SaveParameters(Parameters);
            HandshakeStatusText = "";
            IsHandshaking = false;
            return await Task.Run(async () =>
            {


                StopAllHandshakeTimer();
                StatusLighter.FlashAsync(DO_ITEM.AGV_DiractionLight_Y, 600);
                try
                {
                    IsMotorReseting = false;
                    await ResetMotor(false);
                    (bool, string) result = await PreActionBeforeInitialize();
                    if (!result.Item1)
                    {
                        IsInitialized = false;
                        _Sub_Status = SUB_STATUS.ALARM;
                        BuzzerPlayer.Alarm();
                        StatusLighter.AbortFlash();
                        return result;
                    }

                    InitializingStatusText = "初始化開始";
                    SetSub_Status(SUB_STATUS.Initializing);
                    IsInitialized = false;

                    result = await InitializeActions(InitializeCancelTokenResourece);
                    if (!result.Item1)
                    {
                        SetSub_Status(SUB_STATUS.STOP);
                        IsInitialized = false;
                        StatusLighter.AbortFlash();
                        return result;
                    }
                    await Task.Delay(500);
                    InitializingStatusText = "雷射模式切換(Bypass)..";
                    await Task.Delay(200);

                    if (Parameters.AgvType == AGV_TYPE.INSPECTION_AGV)
                        await (Laser as clsAMCLaser).ModeSwitch(clsAMCLaser.AMC_LASER_MODE.Bypass16);
                    else
                        await Laser.ModeSwitch(LASER_MODE.Bypass);


                    await Laser.AllLaserDisable();

                    StatusLighter.AbortFlash();
                    DirectionLighter.CloseAll();

                    InitializingStatusText = "初始化完成!";
                    await Task.Delay(500);
                    SetSub_Status(SUB_STATUS.IDLE);
                    AGVC._ActionStatus = ActionStatus.NO_GOAL;
                    IsInitialized = true;
                    LOG.INFO("Init done, and Laser mode chaged to Bypass");
                    return (true, "");
                }
                catch (TaskCanceledException ex)
                {
                    StatusLighter.AbortFlash();
                    _Sub_Status = SUB_STATUS.DOWN;
                    IsInitialized = false;
                    LOG.Critical($"AGV Initizlize Task Canceled! : \r\n{ex.Message}", ex);
                    return (false, $"AGV Initizlize Task Canceled! : \r\n{ex.Message}");
                }
                catch (Exception ex)
                {
                    StatusLighter.AbortFlash();
                    _Sub_Status = SUB_STATUS.DOWN;
                    BuzzerPlayer.Alarm();
                    IsInitialized = false;
                    return (false, $"AGV Initizlize Code Error ! : \r\n{ex.Message}");
                }

            }, InitializeCancelTokenResourece.Token);
        }

        protected virtual async Task<(bool, string)> PreActionBeforeInitialize()
        {
            _Basic_Initialze_ActionsAsync();

            (bool confirm, string message) eq_io_status_check_reuslt = _Try_CheckEQ_IO_Status();
            if (!eq_io_status_check_reuslt.confirm)
                return (false, $"端點設備({lastVisitedMapPoint.Name})尚未進行復歸，AGV禁止復歸");

            ResetHandshakeSignals();
            await WagoDO.SetState(DO_ITEM.Horizon_Motor_Stop, false);
            (bool confirm, string message) hardware_status_check_reuslt = CheckHardwareStatus();
            if (!hardware_status_check_reuslt.confirm)
                return (false, hardware_status_check_reuslt.message);

            AlarmCodeWhenHandshaking = AlarmCodes.None;

            if (!WagoDI.Connected)
                return (false, $"DIO 模組連線異常");

            return (true, "");

            #region Local Methods

            (bool confirm, string message) _Try_CheckEQ_IO_Status()
            {
                if ((IsEQAbnormal_when_handshaking || IsEQBusy_when_AGV_Busy) && EQHsSignalStates[EQ_HSSIGNAL.EQ_BUSY].State)
                {
                    return (false, $"端點設備({lastVisitedMapPoint.Name})尚未進行復歸，AGV禁止復歸");
                }
                else
                {
                    return (true, "");
                }
            }

            async Task _Basic_Initialze_ActionsAsync()
            {
                await Task.Delay(1).ContinueWith(async (tsk) =>
                {

                    if (ExecutingTaskEntity != null)
                    {
                        ExecutingTaskEntity.AGVCActionStatusChaged = null;
                        ExecutingTaskEntity = null;
                    }
                    AGVC.OnAGVCActionChanged = null;
                    IMU.OnAccelermeterDataChanged -= HandleIMUVibrationDataChanged;
                    await AGVC.SendGoal(new AGVSystemCommonNet6.GPMRosMessageNet.Actions.TaskCommandGoal());
                });
            }
            #endregion

        }

        /// <summary>
        /// Reset交握訊號
        /// </summary>
        internal virtual async void ResetHandshakeSignals()
        {
            await WagoDO.SetState(DO_ITEM.AGV_COMPT, false);
            await WagoDO.SetState(DO_ITEM.AGV_BUSY, false);
            await WagoDO.SetState(DO_ITEM.AGV_READY, false);
            await WagoDO.SetState(DO_ITEM.AGV_TR_REQ, false);
            await WagoDO.SetState(DO_ITEM.AGV_VALID, false);
            if (Parameters.EQHandshakeMethod == EQ_HS_METHOD.EMULATION)
            {
                await WagoDO.SetState(DO_ITEM.EMU_EQ_BUSY, false);
                await WagoDO.SetState(DO_ITEM.EMU_EQ_L_REQ, false);
                await WagoDO.SetState(DO_ITEM.EMU_EQ_U_REQ, false);
                await WagoDO.SetState(DO_ITEM.EMU_EQ_READY, false);
            }
        }

        public virtual (bool confirm, string message) CheckHardwareStatus()
        {
            AlarmCodes alarmo_code = AlarmCodes.None;
            string error_message = "";
            if (CheckMotorIOError())
            {
                error_message = "走行軸馬達IO異常";
                alarmo_code = AlarmCodes.Wheel_Motor_IO_Error;
            }

            if (CheckEMOButtonNoRelease())
            {
                error_message = "EMO 按鈕尚未復歸";
                alarmo_code = AlarmCodes.EMO_Button;
            }

            if (!WagoDI.GetState(DI_ITEM.Horizon_Motor_Switch))
            {
                error_message = "解煞車旋鈕尚未復歸";
                alarmo_code = AlarmCodes.Switch_Type_Error;
            }
            if (IMU.PitchState != clsIMU.PITCH_STATES.NORMAL && Parameters.ImpactDetection.PitchErrorDetection)
            {
                error_message = $"AGV姿態異常({(IMU.PitchState == clsIMU.PITCH_STATES.INCLINED ? "傾斜" : "側翻")})";
                alarmo_code = AlarmCodes.IMU_Pitch_State_Error;
            }
            if (alarmo_code == AlarmCodes.None)
                return (true, "");
            else
            {
                AlarmManager.AddAlarm(alarmo_code, true);
                BuzzerPlayer.Alarm();
                return (false, error_message);
            }
        }

        protected virtual bool CheckEMOButtonNoRelease()
        {
            return !WagoDI.GetState(DI_ITEM.EMO);
        }

        protected virtual bool CheckMotorIOError()
        {
            return WagoDI.GetState(DI_ITEM.Horizon_Motor_Alarm_1) || WagoDI.GetState(DI_ITEM.Horizon_Motor_Alarm_2);
        }

        protected virtual async Task<(bool confirm, string message)> InitializeActions(CancellationTokenSource cancellation)
        {
            await AutoCargoIDInit();
            return (true, "");
        }

        private async Task AutoCargoIDInit()
        {
            if (!IsCstReaderMounted)
                return;
            if (Parameters.Auto_Cleaer_CST_ID_Data_When_Has_Data_But_NO_Cargo && IsNoCargoButIDExist)
            {
                CSTReader.ValidCSTID = "";
                LOG.WARN($"偵測到AGV有帳無料，已完成自動清帳");
            }
            else if (Parameters.Auto_Read_CST_ID_When_No_Data_But_Has_Cargo && IsCargoExistButNoID)
            {
                InitializingStatusText = "自動建帳中...";
                (bool request_success, bool action_done) result = await AGVC.TriggerCSTReader();
                if (result.request_success)
                    LOG.WARN($"偵測到AGV有料無帳，已完成自動建帳");
            }
        }

        private (int tag, double locx, double locy, double theta) CurrentPoseReqCallback()
        {
            var tag = Navigation.Data.lastVisitedNode.data;
            var x = Navigation.Data.robotPose.pose.position.x;
            var y = Navigation.Data.robotPose.pose.position.y;
            var theta = BarcodeReader.Data.theta;
            return new(tag, x, y, theta);
        }

        protected virtual internal async Task InitAGVControl(string RosBridge_IP, int RosBridge_Port)
        {
            CreateAGVCInstance(RosBridge_IP, RosBridge_Port);
            AGVC.Throttle_rate_of_Topic_ModuleInfo = Parameters.ModuleInfoTopicRevHandlePeriod;
            AGVC.QueueSize_of_Topic_ModuleInfo = Parameters.ModuleInfoTopicRevQueueSize;
            await AGVC.Connect();
            AGVC.ManualController.vehicle = this;
            AGVC.OnModuleInformationUpdated += ModuleInformationHandler;
            AGVC.OnSickLocalicationDataUpdated += HandleSickLocalizationStateChanged;
            AGVC.OnSickRawDataUpdated += SickRawDataHandler;
            Laser.rosSocket = AGVC.rosSocket;

        }


        private void SickRawDataHandler(object? sender, RawMicroScanDataMsg RawData)
        {
            Task.Factory.StartNew(() =>
            {
                SickData.SickRawData = RawData;
                SickData.LaserModeSettingError = RawData.general_system_state.application_error;
                SickData.SickConnectionError = RawData.general_system_state.contamination_error;
            });
        }

        protected virtual void CreateAGVCInstance(string RosBridge_IP, int RosBridge_Port)
        {
            AGVC = new SubmarinAGVControl(RosBridge_IP, RosBridge_Port);
        }

        internal async Task<bool> CancelInitialize()
        {
            return true;
        }

        protected internal virtual async void SoftwareEMOFromUI()
        {
            _ = Task.Run(() =>
            {
                AGVC.EmergencyStop(bypass_stopped_check: true); //
            });
            LOG.Critical($"Software EMO By User!!!");
            SoftwareEMO(AlarmCodes.SoftwareEMS);
        }

        private SemaphoreSlim _softwareEmoSemaphoreSlim = new SemaphoreSlim(1, 1);
        protected internal virtual async void SoftwareEMO(AlarmCodes alarmCode)
        {
            await _softwareEmoSemaphoreSlim.WaitAsync();
            try
            {
                LOG.Critical($"EMO-{alarmCode}");
                _Sub_Status = SUB_STATUS.DOWN;

                if (ExecutingTaskEntity != null)
                {
                    IsAGVAbnormal_when_handshaking = ExecutingTaskEntity.IsNeedHandshake;
                    ExecutingTaskEntity.Abort(alarmCode);
                }
                else
                    AlarmManager.RecordAlarm(alarmCode);

                TryFeedbackActionFinisInEmoMoment();

                StoreStatusToDataBase();
                HandshakeIOOff();
                BuzzerPlayer.Alarm();
                StatusLighter.CloseAll();
                StatusLighter.DOWN();
                DirectionLighter.CloseAll();
                DOSettingWhenEmoTrigger();
                RemoteModeSettingWhenAGVsDisconnect = REMOTE_MODE.OFFLINE;
                IsWaitForkNextSegmentTask = false;
                InitializeCancelTokenResourece.Cancel();
                IsInitialized = false;
                StopAllHandshakeTimer();
                previousSoftEmoTime = DateTime.Now;

                //AGVSTaskFeedBackReportAndOffline(alarmCode);
                if (Remote_Mode == REMOTE_MODE.ONLINE)
                {
                    _ = Task.Run(async () =>
                    {
                        LOG.INFO($"UnRecoveralble Alarm Happened, 自動請求OFFLINE");
                        await Online_Mode_Switch(REMOTE_MODE.OFFLINE);
                    });
                }
                HandshakeStatusText = IsHandshakeFailAlarmCode(alarmCode) ? $"{alarmCode}" : HandshakeStatusText;
            }
            catch (Exception ex)
            {
                LOG.Critical(ex);
            }
            finally
            {
                _softwareEmoSemaphoreSlim.Release();
            }

        }

        private void TryFeedbackActionFinisInEmoMoment()
        {
            Task.Run(async () =>
            {
                try
                {
                    await FeedbackTaskStatus(TASK_RUN_STATUS.ACTION_FINISH, AlarmManager.CurrentAlarms.Values.Where(al => !al.IsRecoverable).Select(vl => vl.EAlarmCode).ToList());
                }
                catch (Exception ex)
                {
                    LOG.ERROR(ex.Message, ex);
                }
            });
        }

        protected virtual void HandshakeIOOff()
        {
            SetAGV_TR_REQ(false);

        }
        private void RecordVibrationDataToDatabase()
        {
            try
            {
                DBhelper.AddVibrationStatusRecord(new clsVibrationStatusWhenAGVMoving(_RunTaskData.VibrationRecords)
                {
                    Time = DateTime.Now,
                    TaskName = _RunTaskData.Task_Name,
                    DestineTag = _RunTaskData.Destination
                });
            }
            catch (Exception ex)
            {
                LOG.ERROR(ex);
            }
        }

        private void HandleIMUVibrationDataChanged(object? sender, Vector3 e)
        {
            if (GetSub_Status() != SUB_STATUS.RUN)
            {
                IMU.OnAccelermeterDataChanged -= HandleIMUVibrationDataChanged;
                RecordVibrationDataToDatabase();
                return;
            }
            if (IMU.IsAccSensorError)
                return;
            _RunTaskData.VibrationRecords.Add(new clsVibrationRecord
            {
                Time = DateTime.Now,
                AccelermetorValue = e,
                LocX = Navigation.Data.robotPose.pose.position.x,
                LocY = Navigation.Data.robotPose.pose.position.y,
                Theta = Navigation.Angle
            });
        }
        private bool IsCurrentTaskIsHandshaking => _RunTaskData.IsEQHandshake;

        /// <summary>
        /// 是否為交握異常碼
        /// </summary>
        /// <param name="alarmCode"></param>
        /// <returns></returns>
        private bool IsHandshakeFailAlarmCode(AlarmCodes alarmCode)
        {
            string alarcode_str = ((int)alarmCode).ToString();

            if (alarcode_str.Length != 4)
                return false;

            bool isCargoExistStatusAlarmCode = alarcode_str.Substring(0, 2) == "17";
            if (isCargoExistStatusAlarmCode)
                return true;

            bool isHSAlarmCode = alarcode_str.Substring(0, 2) == "32";
            if (isHSAlarmCode)
                LOG.WARN($"HS Alarm-{alarmCode} happend!");

            return isHSAlarmCode;
        }


        protected internal virtual void SoftwareEMO()
        {
            SoftwareEMO(AlarmCodes.SoftwareEMS);
        }
        protected SemaphoreSlim _ResetAlarmSemaphoreSlim = new SemaphoreSlim(1, 1);
        internal virtual async Task ResetAlarmsAsync(bool IsTriggerByButton)
        {
            await _ResetAlarmSemaphoreSlim.WaitAsync();
            try
            {
                if (IsResetAlarmWorking)
                    return;

                IsResetAlarmWorking = true;
                BuzzerPlayer.Stop();
                AlarmManager.ClearAlarm();
                AGVAlarmReportable.ResetAlarmCodes();
                AGVS.ResetErrors();
                IsMotorReseting = false;
                await ResetMotor(IsTriggerByButton);
                _ = Task.Factory.StartNew(async () =>
                {
                    await Task.Delay(1000);
                    if (AGVC.ActionStatus == ActionStatus.ACTIVE && GetSub_Status() != SUB_STATUS.DOWN && GetSub_Status() != SUB_STATUS.IDLE)
                    {
                        bool isObstacle = !WagoDI.GetState(DI_ITEM.BackProtection_Area_Sensor_2) || !WagoDI.GetState(DI_ITEM.FrontProtection_Area_Sensor_2) || !WagoDI.GetState(DI_ITEM.RightProtection_Area_Sensor_3) || !WagoDI.GetState(DI_ITEM.LeftProtection_Area_Sensor_3);
                        if (isObstacle)
                        {
                            BuzzerPlayer.Alarm();
                            return;
                        }
                        else
                        {
                            if (ExecutingTaskEntity.action == ACTION_TYPE.None)
                                BuzzerPlayer.Move();
                            else
                                BuzzerPlayer.Action();
                            return;
                        }
                    }

                });
                await Task.Delay(500);
                IsResetAlarmWorking = false;
            }
            catch (Exception ex)
            {
                LOG.Critical(ex);
            }
            finally
            {
                _ResetAlarmSemaphoreSlim.Release();
            }

        }
        protected private async Task<bool> SetMotorStateAndDelay(DO_ITEM item, bool state, int delay = 10)
        {
            bool success = await WagoDO.SetState(item, state);
            if (!success) return false;
            await Task.Delay(delay);
            return true;
        }
        public virtual async Task<bool> ResetMotor(bool triggerByResetButtonPush, bool bypass_when_motor_busy_on = true)
        {
            try
            {
                var caller_class_name = new StackTrace().GetFrame(1).GetMethod().DeclaringType.Name;
                if (IsMotorReseting)
                {
                    LOG.WARN($"Reset Motor Action is excuting by other process");
                    return false;
                }
                IsMotorReseting = true;
                if (!triggerByResetButtonPush)
                    await WagoDO.ResetSaftyRelay();
                else
                    await Task.Delay(1000);
                if (bypass_when_motor_busy_on & WagoDI.GetState(DI_ITEM.Horizon_Motor_Busy_1) && WagoDI.GetState(DI_ITEM.Horizon_Motor_Busy_2))
                    return true;

                LOG.TRACE($"Reset Motor Process Start (caller:{caller_class_name})");
                if (!await SetMotorStateAndDelay(DO_ITEM.Horizon_Motor_Stop, true, 100)) throw new Exception($"Horizon_Motor_Stop set true fail");
                if (!await SetMotorStateAndDelay(DO_ITEM.Horizon_Motor_Free, true, 100)) throw new Exception($"Horizon_Motor_Free set true fail");
                if (!await SetMotorStateAndDelay(DO_ITEM.Horizon_Motor_Reset, true, 100)) throw new Exception($"Horizon_Motor_Reset set true fail");
                if (!await SetMotorStateAndDelay(DO_ITEM.Horizon_Motor_Reset, false, 100)) throw new Exception($"Horizon_Motor_Reset set false fail");
                if (!await SetMotorStateAndDelay(DO_ITEM.Horizon_Motor_Stop, false)) throw new Exception($"Horizon_Motor_Stop set false  fail");
                if (!await SetMotorStateAndDelay(DO_ITEM.Horizon_Motor_Free, false)) throw new Exception($"Horizon_Motor_Free set false fail");
                LOG.TRACE("Reset Motor Process End");

                IsMotorReseting = false;
                return true;
            }
            catch (SocketException ex)
            {
                IsMotorReseting = false;
                LOG.ERROR(ex);
                return false;
            }
            catch (Exception ex)
            {
                IsMotorReseting = false;
                LOG.ERROR(ex);
                return false;
            }

        }


        /// <summary>
        /// 取得載物的類型 0:tray, 1:rack , 200:tray
        /// </summary>
        /// <returns></returns>
        protected virtual int GetCargoType()
        {
            return 0;
        }

        /// <summary>
        /// Auto/Manual 模式切換
        /// </summary>
        /// <param name="mode"></param>
        /// <returns></returns>
        internal async Task<bool> Auto_Mode_Siwtch(OPERATOR_MODE mode)
        {
            Operation_Mode = mode;
            LOG.TRACE($"手/自動模式已切換為:{mode}");
            if (mode == OPERATOR_MODE.AUTO)
            {
                await Laser.FrontBackLasersEnable(true, true);
                await Laser.SideLasersEnable(false);
            }
            else
            {
                await Laser.AllLaserDisable();
            }
            return true;
        }

        internal virtual bool HasAnyCargoOnAGV()
        {
            bool _hasRack = false;
            bool _hasTray = false;

            if (Parameters.CargoExistSensorParams.RackSensorMounted)
            {
                if (Parameters.CargoExistSensorParams.RackSensorPointType == IO_CONEECTION_POINT_TYPE.A)
                {
                    _hasRack = WagoDI.GetState(DI_ITEM.RACK_Exist_Sensor_1) || WagoDI.GetState(DI_ITEM.RACK_Exist_Sensor_2);
                }
                else
                {
                    _hasRack = !WagoDI.GetState(DI_ITEM.RACK_Exist_Sensor_1) || !WagoDI.GetState(DI_ITEM.RACK_Exist_Sensor_2);
                }
            }
            if (Parameters.CargoExistSensorParams.TraySensorMounted)
            {
                if (Parameters.CargoExistSensorParams.TraySensorPointType == IO_CONEECTION_POINT_TYPE.A)
                    _hasTray = WagoDI.GetState(DI_ITEM.TRAY_Exist_Sensor_1) || WagoDI.GetState(DI_ITEM.TRAY_Exist_Sensor_2);
                else
                    _hasTray = !WagoDI.GetState(DI_ITEM.TRAY_Exist_Sensor_1) || !WagoDI.GetState(DI_ITEM.TRAY_Exist_Sensor_2);
            }
            return _hasRack || _hasTray;
        }

        internal async Task QueryVirtualID(VIRTUAL_ID_QUERY_TYPE QueryType, CST_TYPE CstType)
        {
            LOG.INFO($"Query Virtual ID From AGVS  QueryType={QueryType.ToString()},CstType={CstType.ToString()}");
            (bool result, string virtual_id, string message) results = await AGVS.TryGetVirtualID(QueryType, CstType);
            if (results.result)
            {
                CSTReader.ValidCSTID = results.virtual_id;
                LOG.INFO($"Query Virtual ID From AGVS Success, QueryType={QueryType.ToString()},CstType={CstType.ToString()},Virtual ID={CSTReader.ValidCSTID}");

            }
            else
            {
                LOG.WARN($"Query Virtual ID From AGVS Fail, QueryType={QueryType.ToString()},CstType={CstType.ToString()},Message={results.message}");
                AlarmManager.AddAlarm(AlarmCodes.GetVirtualIDFail, true);
            }
        }

        internal async Task<(bool confirm, string message)> TrackingTagCenter(double finalAngle = 90)
        {

            if (!Debugger.IsAttached)
            {
                if (this.lastVisitedMapPoint == null)
                {
                    return (false, "AGV位於未知的點位,禁止操作自動移動到TAG中心功能");
                }
                if (lastVisitedMapPoint.StationType != STATION_TYPE.Normal)
                    return (false, "AGV位於非一般點位,禁止操作自動移動到TAG中心功能");
                if (BarcodeReader.CurrentTag == 0)
                    return (false, "AGV並未在Tag上,無法操作自動移動到TAG中心功能");

                if (GetSub_Status() != SUB_STATUS.IDLE)
                    return (false, "AGV非IDLE狀態, 無法操作自動移動到TAG中心功能");
            }

            _ = Task.Factory.StartNew(async () =>
            {

                var currentAngle = Convert.ToDouble(BarcodeReader.CurrentAngle + "");
                double distance_to_tag_center = Math.Sqrt(Math.Pow(BarcodeReader.CurrentX, 2) + Math.Pow(BarcodeReader.CurrentY, 2));
                var rotationAngle = currentAngle + Math.Cosh(BarcodeReader.CurrentX / distance_to_tag_center) * 180.0 / Math.PI;

                LOG.INFO($"[Find Tag] Rotation Angle : {rotationAngle}");

                var rotationAngleAim = currentAngle - rotationAngle;

                AGVC.ManualController.TurnLeft();
                while (Math.Abs(BarcodeReader.CurrentAngle - rotationAngleAim) > 1)
                {
                    if (BarcodeReader.CurrentTag == 0)
                        break;
                    await Task.Delay(1);
                }
                AGVC.ManualController.Stop();
                AGVC.ManualController.Forward();
                while (Math.Abs(BarcodeReader.CurrentX - 0) > 5)
                {
                    await Task.Delay(1);
                }
                AGVC.ManualController.Stop();
            });

            return (true, "");
        }

        internal void JudgeIsBatteryCharging()
        {
            if (!lastVisitedMapPoint.IsCharge || AGVC.IsRunning || Parameters.AgvType == AGV_TYPE.INSPECTION_AGV)
            {
                return;
            }

            bool IsFakeCharging = lastVisitedMapPoint.IsCharge && IsInitialized && Parameters.BatteryModule.ChargeWhenLevelLowerThanThreshold && Batteries.All(bat => bat.Value.Data.batteryLevel > Parameters.BatteryModule.ChargeLevelThreshold);
            SetIsCharging(IsFakeCharging ? true : Batteries.Values.Any(battery => battery.IsCharging()));
            //當電量僅在低於閥值才充電的設定下，若AGV在充電站但充電迴路沒開 且電量低於閥值，須將狀態轉成IDLE
            if (!IsChargeCircuitOpened && Parameters.BatteryModule.ChargeWhenLevelLowerThanThreshold && Batteries.Any(bat => bat.Value.Data.batteryLevel < Parameters.BatteryModule.ChargeLevelThreshold))
                SetSub_Status(GetSub_Status() == SUB_STATUS.Charging ? SUB_STATUS.IDLE : GetSub_Status(), false);
        }
    }
}
