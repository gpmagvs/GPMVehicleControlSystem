using AGVSystemCommonNet6.Abstracts;
using AGVSystemCommonNet6.AGVDispatch;
using AGVSystemCommonNet6.AGVDispatch.Messages;
using AGVSystemCommonNet6.AGVDispatch.Model;
using AGVSystemCommonNet6.Alarm.VMS_ALARM;
using AGVSystemCommonNet6.GPMRosMessageNet.Messages;
using AGVSystemCommonNet6.GPMRosMessageNet.SickSafetyscanners;
using AGVSystemCommonNet6.Log;
using AGVSystemCommonNet6.MAP;
using GPMVehicleControlSystem.Models.Buzzer;
using GPMVehicleControlSystem.Models.Emulators;
using GPMVehicleControlSystem.Models.NaviMap;
using GPMVehicleControlSystem.Models.VCSSystem;
using GPMVehicleControlSystem.Models.VehicleControl.AGVControl;
using GPMVehicleControlSystem.Models.VehicleControl.VehicleComponent;
using GPMVehicleControlSystem.Models.WorkStation;
using GPMVehicleControlSystem.VehicleControl.DIOModule;
using Newtonsoft.Json;
using RosSharp.RosBridgeClient.Actionlib;
using System.Diagnostics;
using System.Net.Sockets;
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
            /// 沒有貨物
            /// </summary>
            NO_CARGO,
            /// <summary>
            /// 有貨物且正常裝載
            /// </summary>
            HAS_CARGO_NORMAL,
            /// <summary>
            /// 有貨物但傾斜
            /// </summary>
            HAS_CARGO_BUT_BIAS,
            /// <summary>
            /// 無載物功能
            /// </summary>
            NO_CARGO_CARRARYING_CAPABILITY
        }
        public enum VMS_PROTOCOL
        {
            KGS,
            GPM_VMS
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
        /// <summary>
        /// 里程數
        /// </summary>
        public double Odometry;
        VehicleEmu emulator;
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

        public MAIN_STATUS Main_Status
        {
            get
            {
                switch (_Sub_Status)
                {
                    case SUB_STATUS.IDLE:
                        return MAIN_STATUS.IDLE;
                    case SUB_STATUS.RUN:
                        return MAIN_STATUS.RUN;
                    case SUB_STATUS.DOWN:
                        return MAIN_STATUS.DOWN;
                    case SUB_STATUS.Charging:
                        return MAIN_STATUS.Charging;
                    case SUB_STATUS.Initializing:
                        return MAIN_STATUS.DOWN;
                    case SUB_STATUS.ALARM:
                        if (AGVC.ActionStatus == ActionStatus.ACTIVE)
                            return MAIN_STATUS.RUN;
                        else
                            return MAIN_STATUS.IDLE;
                    case SUB_STATUS.WARNING:
                        if (AGVC.ActionStatus == ActionStatus.ACTIVE)
                            return MAIN_STATUS.RUN;
                        else
                            return MAIN_STATUS.IDLE;
                    case SUB_STATUS.STOP:
                        return MAIN_STATUS.IDLE;
                    default:
                        return MAIN_STATUS.DOWN;
                }
            }
        }

        /// <summary>
        /// 與 AGVS Reset Command 相關，若收到 AGVS 下 AGVS Reset
        /// Command 給 AGV
        ///⚫ AGVC 停下後為 true
        ///⚫ 重新收到 0301 任務後為 fals
        /// </summary>
        public bool AGV_Reset_Flag { get; internal set; }

        internal bool AGVSResetCmdFlag = false;
        public MoveControl ManualController => AGVC.ManualController;

        public bool IsInitialized { get; internal set; }
        public bool IsSystemInitialized { get; internal set; }
        internal SUB_STATUS _Sub_Status = SUB_STATUS.DOWN;
        public MapPoint lastVisitedMapPoint { get; private set; }
        public bool _IsCharging = false;
        public bool IsCharging
        {
            get => _IsCharging;
            set
            {
                bool isRechargeCircuitOpened = WagoDO.GetState(DO_ITEM.Recharge_Circuit);
                if (isRechargeCircuitOpened && Batteries.Any(bat => bat.Value.Data.Voltage >= Parameters.CutOffChargeRelayVoltageThreshodlval))
                {
                    LOG.WARN($"Battery voltage  lower than threshold ({Parameters.CutOffChargeRelayVoltageThreshodlval}) mV, cut off recharge circuit ! ");
                    WagoDO.SetState(DO_ITEM.Recharge_Circuit, false);
                    Sub_Status = IsInitialized ? AGVC.ActionStatus == ActionStatus.ACTIVE ? SUB_STATUS.RUN : SUB_STATUS.IDLE : SUB_STATUS.DOWN;
                    _IsCharging = false;
                    return;
                }
                if (_IsCharging != value)
                {
                    if (value)
                    {
                        BeforeChargingSubStatus = _Sub_Status;
                        _Sub_Status = SUB_STATUS.Charging;
                        StatusLighter.ActiveGreen();
                    }
                    else
                        Sub_Status = IsInitialized ? AGVC.ActionStatus == ActionStatus.ACTIVE ? SUB_STATUS.RUN : SUB_STATUS.IDLE : SUB_STATUS.DOWN;
                    _IsCharging = value;
                }
            }
        }

        public MapPoint DestinationMapPoint
        {
            get
            {
                if (ExecutingActionTask == null)
                    return new MapPoint { Name = "" };
                else
                {
                    try
                    {
                        var _point = NavingMap.Points.Values.FirstOrDefault(pt => pt.TagNumber == ExecutingActionTask.RunningTaskData.Destination);
                        return _point == null ? new MapPoint { Name = ExecutingActionTask.RunningTaskData.Destination.ToString(), TagNumber = ExecutingActionTask.RunningTaskData.Destination } : _point;
                    }
                    catch (Exception)
                    {
                        return new MapPoint { Name = ExecutingActionTask.RunningTaskData.Destination.ToString(), TagNumber = ExecutingActionTask.RunningTaskData.Destination };

                    }
                }
            }
        }

        private SUB_STATUS BeforeChargingSubStatus;

        public SUB_STATUS Sub_Status
        {
            get => _Sub_Status;
            set
            {
                if (_Sub_Status != value)
                {
                    _Sub_Status = value;
                    try
                    {
                        if (value == SUB_STATUS.DOWN | value == SUB_STATUS.ALARM | value == SUB_STATUS.Initializing)
                        {
                            if (value == SUB_STATUS.DOWN)
                                SetAGV_TR_REQ(false);
                            if (value != SUB_STATUS.Initializing)
                                BuzzerPlayer.Alarm();
                            DirectionLighter.CloseAll();
                            StatusLighter.DOWN();
                        }
                        else if (value == SUB_STATUS.IDLE)
                        {
                            BuzzerPlayer.Stop();
                            StatusLighter.IDLE();
                            DirectionLighter.CloseAll();
                        }
                        else if (value == SUB_STATUS.RUN)
                        {
                            StatusLighter.RUN();
                        }

                    }
                    catch (Exception ex)
                    {
                    }
                }
            }
        }

        public Vehicle()
        {
            try
            {
                Parameters = LoadParameters();
                CIMConnectionInitialize();
                LoadWorkStationConfigs();
                LOG.INFO($"{GetType().Name} Start create instance...");
                ReadTaskNameFromFile();
                IsSystemInitialized = false;
                string Wago_IP = Parameters.Connections["Wago"].IP;
                int Wago_Port = Parameters.Connections["Wago"].Port;
                int LastVisitedTag = Parameters.LastVisitedTag;
                string RosBridge_IP = Parameters.Connections["RosBridge"].IP;
                int RosBridge_Port = Parameters.Connections["RosBridge"].Port;
                AGVSMessageFactory.Setup(Parameters.SID, Parameters.VehicleName);
                WagoDO = new clsDOModule(Wago_IP, Wago_Port, null)
                {
                    AgvType = Parameters.AgvType
                };
                WagoDI = new clsDIModule(Wago_IP, Wago_Port, WagoDO)
                {
                    AgvType = Parameters.AgvType
                };
                DirectionLighter.DOModule = WagoDO;

                StatusLighter = new clsStatusLighter(WagoDO);
                Laser = new clsLaser(WagoDO, WagoDI)
                {
                    Spin_Laser_Mode = Parameters.Spin_Laser_Mode
                };

                EmulatorInitialize();
                Task RosConnTask = new Task(async () =>
                {
                    await Task.Delay(1).ContinueWith(async t =>
                    {
                        InitAGVControl(RosBridge_IP, RosBridge_Port);
                        if (AGVC?.rosSocket != null)
                        {
                            BuzzerPlayer.rossocket = AGVC.rosSocket;
                            await Task.Delay(1000);
                            BuzzerPlayer.Alarm();
                        }
                    }
                    );
                });

                Task WagoDIConnTask = WagoDIInit();
                RosConnTask.Start();
                WagoDIConnTask.Start();
                lastVisitedMapPoint = new MapPoint(LastVisitedTag + "", LastVisitedTag);
                Navigation.StateData = new NavigationState() { lastVisitedNode = new RosSharp.RosBridgeClient.MessageTypes.Std.Int32(LastVisitedTag) };
                BarcodeReader.StateData = new BarcodeReaderState() { tagID = (uint)LastVisitedTag };
                AGVSInit();
                CommonEventsRegist();
                //TrafficMonitor();
                LOG.INFO($"設備交握通訊方式:{Parameters.EQHandshakeMethod}");
                IsSystemInitialized = true;
            }
            catch (Exception ex)
            {
                IsSystemInitialized = false;
                string msg = $"車輛實例化時於建構函式發生錯誤 : {ex.Message}:{ex.StackTrace}";
                StaSysMessageManager.AddNewMessage(msg, 2);
                throw ex;
            }

        }

        private void EmulatorInitialize()
        {
            if (Parameters.SimulationMode)
            {
                try
                {
                    StaEmuManager.StartAGVROSEmu();
                }
                catch (SocketException)
                {
                    StaSysMessageManager.AddNewMessage("模擬器無法啟動 (無法建立服務器): 請嘗試使用系統管理員權限開啟程式", 1);
                }
                catch (Exception ex)
                {
                    StaSysMessageManager.AddNewMessage("\"模擬器無法啟動 : 異常訊息\" + ex.Message", 1); ;
                }
            }
            if (Parameters.MeasureServiceSimulator)
            {
                try
                {
                    StaEmuManager.StartMeasureROSEmu();
                }
                catch (SocketException)
                {
                    StaSysMessageManager.AddNewMessage("量測服務模擬器無法啟動 (無法建立服務器): 請嘗試使用系統管理員權限開啟程式", 1);
                }
                catch (Exception ex)
                {
                    StaSysMessageManager.AddNewMessage("\"量測服務模擬器無法啟動 : 異常訊息\" + ex.Message", 1); ;
                }
            }

            if (Parameters.WagoSimulation)
            {
                StaEmuManager.StartWagoEmu(WagoDI);
            }
        }


        /// <summary>
        /// 生成支援WebAPI的RunningStatus Model
        /// </summary>
        /// <returns></returns>
        public virtual clsRunningStatus HandleWebAPIProtocolGetRunningStatus()
        {
            clsCoordination Corrdination = new clsCoordination();
            MAIN_STATUS _Main_Status = Main_Status;
            //if (Parameters.SimulationMode)
            //    emulator.Runstatus.AGV_Status = _Main_Status;

            Corrdination.X = Math.Round(Navigation.Data.robotPose.pose.position.x, 3);
            Corrdination.Y = Math.Round(Navigation.Data.robotPose.pose.position.y, 3);
            Corrdination.Theta = Math.Round(Navigation.Angle, 3);
            //gen alarm codes 

            AGVSystemCommonNet6.AGVDispatch.Model.clsAlarmCode[] alarm_codes = GetAlarmCodesUserReportToAGVS_WebAPI();
            try
            {
                double[] batteryLevels = Batteries.ToList().FindAll(bt => bt.Value != null).Select(battery => (double)battery.Value.Data.batteryLevel).ToArray();
                var status = new clsRunningStatus
                {
                    Cargo_Status = HasAnyCargoOnAGV() ? 1 : 0,
                    CargoType = GetCargoType(),
                    AGV_Status = _Main_Status,
                    Electric_Volume = batteryLevels,
                    Last_Visited_Node = Navigation.Data.lastVisitedNode.data,
                    Coordination = Corrdination,
                    Odometry = Odometry,
                    AGV_Reset_Flag = AGV_Reset_Flag,
                    Alarm_Code = alarm_codes,
                    Escape_Flag = ExecutingActionTask == null ? false : ExecutingActionTask.RunningTaskData.Escape_Flag,
                };
                return status;
            }
            catch (Exception ex)
            {
                //LOG.ERROR("GenRunningStateReportData ", ex);
                return new clsRunningStatus();
            }
        }

        private static AGVSystemCommonNet6.AGVDispatch.Messages.clsAlarmCode[] GetAlarmCodesUserReportToAGVS()
        {
            return AlarmManager.CurrentAlarms.ToList().FindAll(alarm => alarm.Value.EAlarmCode != AlarmCodes.None).Select(alarm => new AGVSystemCommonNet6.AGVDispatch.Messages.clsAlarmCode
            {
                Alarm_ID = alarm.Value.Code,
                Alarm_Level = alarm.Value.IsRecoverable ? 0 : 1,
                Alarm_Description = alarm.Value.CN,
                Alarm_Description_EN = alarm.Value.Description,
                Alarm_Category = alarm.Value.IsRecoverable ? 0 : 1,
            }).DistinctBy(alarm => alarm.Alarm_ID).ToArray();
        }
        private AGVSystemCommonNet6.AGVDispatch.Model.clsAlarmCode[] GetAlarmCodesUserReportToAGVS_WebAPI()
        {
            return AlarmManager.CurrentAlarms.ToList().FindAll(alarm => alarm.Value.EAlarmCode != AlarmCodes.None).Select(alarm => new AGVSystemCommonNet6.AGVDispatch.Model.clsAlarmCode
            {
                Alarm_ID = alarm.Value.Code,
                Alarm_Level = alarm.Value.IsRecoverable ? 0 : 1,
                Alarm_Description = alarm.Value.CN,
                Alarm_Description_EN = alarm.Value.Description,
                Alarm_Category = alarm.Value.IsRecoverable ? 0 : 1,
            }).DistinctBy(alarm => alarm.Alarm_ID).ToArray();
        }

        /// <summary>
        /// 生成支援TCPIP通訊的RunningStatus Model
        /// </summary>
        /// <returns></returns>
        protected virtual RunningStatus HandleTcpIPProtocolGetRunningStatus()
        {
            clsCoordination Corrdination = new clsCoordination();
            MAIN_STATUS _Main_Status = Main_Status;
            //if (Parameters.SimulationMode)
            //    emulator.Runstatus.AGV_Status = _Main_Status;

            Corrdination.X = Math.Round(Navigation.Data.robotPose.pose.position.x, 3);
            Corrdination.Y = Math.Round(Navigation.Data.robotPose.pose.position.y, 3);
            Corrdination.Theta = Math.Round(Navigation.Angle, 3);
            //gen alarm codes 

            AGVSystemCommonNet6.AGVDispatch.Messages.clsAlarmCode[] alarm_codes = GetAlarmCodesUserReportToAGVS();

            try
            {
                double[] batteryLevels = Batteries.ToList().FindAll(bky => bky.Value != null).Select(battery => (double)battery.Value.Data.batteryLevel).ToArray();
                var status = new RunningStatus
                {
                    Cargo_Status = HasAnyCargoOnAGV() ? 1 : 0,
                    CargoType = GetCargoType(),
                    AGV_Status = _Main_Status,
                    Electric_Volume = batteryLevels,
                    Last_Visited_Node = lastVisitedMapPoint.IsVirtualPoint ? lastVisitedMapPoint.TagNumber : Navigation.Data.lastVisitedNode.data,
                    Coordination = Corrdination,
                    Odometry = Odometry,
                    AGV_Reset_Flag = AGV_Reset_Flag,
                    Alarm_Code = alarm_codes,
                    Escape_Flag = ExecutingActionTask == null ? false : ExecutingActionTask.RunningTaskData.Escape_Flag,
                };
                return status;
            }
            catch (Exception ex)
            {
                //LOG.ERROR("GenRunningStateReportData ", ex);
                return new RunningStatus();
            }
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
                    StaSysMessageManager.AddNewMessage("Load Fork Teach Data Fail...Read Json Null", 2);
                    return;
                }
                WorkStations = DeserializeWorkStationJson(json);


            }
            catch (Exception ex)
            {
                StaSysMessageManager.AddNewMessage($"Load Fork Teach Data Fail...{ex.Message}", 2);
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
                return true;
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }
        internal async Task DownloadMapFromServer()
        {
            await Task.Run(async () =>
            {
                try
                {
                    NavingMap = await MapStore.GetMapFromServer();
                    if (NavingMap != null)
                    {
                        MapStore.SaveCurrentMap(NavingMap);
                        LOG.INFO($"Map Downloaded. Map Name : {NavingMap.Name}, Version: {NavingMap.Note}");
                    }
                    else
                    {
                        if (File.Exists(Parameters.MapParam.LocalMapFileFullName))
                        {
                            LOG.WARN($"Try load map from local : {Parameters.MapParam.LocalMapFileFullName}");
                            NavingMap = MapStore.GetMapFromFile(Parameters.MapParam.LocalMapFileFullName);
                            if (NavingMap.Note != "empty")
                                LOG.WARN($"Local Map data load success: {NavingMap.Name}({NavingMap.Note})");
                        }
                        else
                        {
                            LOG.ERROR($"Cannot download map from server.({MapStore.GetMapUrl}) and not any local map file exist");
                        }
                    }
                }
                catch (Exception ex)
                {
                    LOG.WARN($"Map Download Fail....{ex.Message}");
                }
            });
        }

        private void AGVSInit()
        {
            string vms_ip = Parameters.Connections["AGVS"].IP;
            int vms_port = Parameters.Connections["AGVS"].Port;
            //AGVS
            AGVS = new clsAGVSConnection(vms_ip, vms_port, Parameters.VMSParam.LocalIP);
            AGVS.SetLogFolder(Path.Combine(Parameters.LogFolder, "AGVS_Message_Log"));
            AGVS.UseWebAPI = Parameters.VMSParam.Protocol == VMS_PROTOCOL.GPM_VMS;
            AGVS.OnRemoteModeChanged = HandleRemoteModeChangeReq;
            AGVS.OnTaskDownload += AGVSTaskDownloadConfirm;
            AGVS.OnTaskResetReq = HandleAGVSTaskCancelRequest;
            AGVS.OnTaskDownloadFeekbackDone += ExecuteAGVSTask;
            AGVS.Start();
        }

        private Task WagoDIInit()
        {
            return new Task(async () =>
            {
                try
                {
                    DIOStatusChangedEventRegist();
                    while (!WagoDI.Connect())
                    {
                        await Task.Delay(1000);
                    }
                    WagoDI.StartAsync();
                    DOSignalDefaultSetting();
                }
                catch (SocketException ex)
                {
                    LOG.Critical($"初始化Wago 連線的過程中發生Socket 例外 , {ex.Message}", ex);
                }
                catch (Exception ex)
                {
                    LOG.Critical($"初始化Wago 連線的過程中發生例外 , {ex.Message}", ex);
                }
            });
        }




        internal bool IsAllLaserNoTrigger()
        {
            return WagoDI.GetState(DI_ITEM.FrontProtection_Area_Sensor_1) && WagoDI.GetState(DI_ITEM.BackProtection_Area_Sensor_1) && WagoDI.GetState(DI_ITEM.LeftProtection_Area_Sensor_3) && WagoDI.GetState(DI_ITEM.RightProtection_Area_Sensor_3);
        }

        protected virtual async void DOSignalDefaultSetting()
        {
            WagoDO.AllOFF();
            await WagoDO.SetState(DO_ITEM.AGV_DiractionLight_R, true);
            await WagoDO.SetState(DO_ITEM.Back_LsrBypass, false);
            await WagoDO.SetState(DO_ITEM.Left_LsrBypass, false);
            await WagoDO.SetState(DO_ITEM.Right_LsrBypass, true);
            await WagoDO.SetState(DO_ITEM.Left_LsrBypass, true);
            await Laser.ModeSwitch(0);
        }
        protected CancellationTokenSource InitializeCancelTokenResourece = new CancellationTokenSource();

        /// <summary>
        /// 初始化AGV
        /// </summary>
        /// <returns></returns>
        public async Task<(bool confirm, string message)> Initialize()
        {
            if (Sub_Status == SUB_STATUS.RUN | Sub_Status == SUB_STATUS.Initializing)
                return (false, $"當前狀態不可進行初始化({Sub_Status})");


            if ((Parameters.AgvType == AGV_TYPE.FORK | Parameters.AgvType == AGV_TYPE.SUBMERGED_SHIELD) && !HasAnyCargoOnAGV() && CSTReader.ValidCSTID != "")
            {
                CSTReader.ValidCSTID = "";
                LOG.WARN($"偵測到AGV有帳無料，已完成自動清帳");
            }

            InitializeCancelTokenResourece = new CancellationTokenSource();
            return await Task.Run(async () =>
            {
                StopAllHandshakeTimer();
                StatusLighter.Flash(DO_ITEM.AGV_DiractionLight_Y, 600);
                try
                {
                    await ResetMotor();
                    (bool, string) result = await PreActionBeforeInitialize();
                    if (!result.Item1)
                    {
                        StatusLighter.AbortFlash();
                        return result;
                    }

                    Sub_Status = SUB_STATUS.Initializing;
                    IsInitialized = false;

                    result = await InitializeActions(InitializeCancelTokenResourece);
                    if (!result.Item1)
                    {
                        Sub_Status = SUB_STATUS.STOP;
                        IsInitialized = false;
                        StatusLighter.AbortFlash();
                        return result;
                    }
                    LOG.INFO("Init done. Laser mode chaged to Bypass");
                    await Laser.ModeSwitch(LASER_MODE.Bypass);
                    await Task.Delay(Parameters.AgvType == AGV_TYPE.SUBMERGED_SHIELD ? 500 : 1000);
                    StatusLighter.AbortFlash();
                    DirectionLighter.CloseAll();
                    Sub_Status = SUB_STATUS.IDLE;
                    IsInitialized = true;
                    LOG.INFO("Init done");
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
            if (ExecutingActionTask != null)
            {
                ExecutingActionTask.AGVCActionStatusChaged = null;
            }
            AGVC.OnAGVCActionChanged = null;
            ExecutingActionTask = null;
            BuzzerPlayer.Stop();
            DirectionLighter.CloseAll();
            if (EQAlarmWhenEQBusyFlag && WagoDI.GetState(DI_ITEM.EQ_BUSY))
            {
                return (false, $"端點設備({lastVisitedMapPoint.Name})尚未進行復歸，AGV禁止復歸");
            }

            AGVAlarmWhenEQBusyFlag = false;
            EQAlarmWhenEQBusyFlag = false;
            ResetHandshakeSignals();
            await WagoDO.SetState(DO_ITEM.Horizon_Motor_Stop, false);
            var hardware_status_check_reuslt = CheckHardwareStatus();
            if (!hardware_status_check_reuslt.confirm)
                return (false, hardware_status_check_reuslt.message);
            //if (Sub_Status == SUB_STATUS.Charging)
            //    return (false, "無法在充電狀態下進行初始化");
            //bool forkRackExistAbnormal = !WagoDI.GetState(DI_ITEM.Fork_RACK_Right_Exist_Sensor) | !WagoDI.GetState(DI_ITEM.Fork_RACK_Left_Exist_Sensor);
            //if (forkRackExistAbnormal)
            //    return (false, "無法在有Rack的狀態下進行初始化");

            //if (lastVisitedMapPoint.StationType !=STATION_TYPE.Normal)
            //    return (false, $"無法在非一般點位下進行初始化({lastVisitedMapPoint.StationType})");



            return (true, "");
        }

        /// <summary>
        /// Reset交握訊號
        /// </summary>
        protected virtual async void ResetHandshakeSignals()
        {

            await WagoDO.SetState(DO_ITEM.AGV_COMPT, false);
            await WagoDO.SetState(DO_ITEM.AGV_BUSY, false);
            await WagoDO.SetState(DO_ITEM.AGV_READY, false);
            await WagoDO.SetState(DO_ITEM.AGV_TR_REQ, false);
            await WagoDO.SetState(DO_ITEM.AGV_VALID, false);
        }

        public virtual (bool confirm, string message) CheckHardwareStatus()
        {
            AlarmCodes alarmo_code = AlarmCodes.None;
            string error_message = "";
            if (WagoDI.GetState(DI_ITEM.Horizon_Motor_Alarm_1))
            {
                error_message = "走行軸馬達IO異常";
                alarmo_code = AlarmCodes.Wheel_Motor_IO_Error;
            }

            if (!WagoDI.GetState(DI_ITEM.EMO))
            {
                error_message = "EMO 按鈕尚未復歸";
                alarmo_code = AlarmCodes.EMO_Button;
            }

            if (!WagoDI.GetState(DI_ITEM.Horizon_Motor_Switch))
            {
                error_message = "解煞車旋鈕尚未復歸";
                alarmo_code = AlarmCodes.Switch_Type_Error;
            }
            if (alarmo_code == AlarmCodes.None)
                return (true, "");
            else
            {
                AlarmManager.AddAlarm(alarmo_code, false);
                BuzzerPlayer.Alarm();
                return (false, error_message);
            }
        }

        protected abstract Task<(bool confirm, string message)> InitializeActions(CancellationTokenSource cancellation);
        private (int tag, double locx, double locy, double theta) CurrentPoseReqCallback()
        {
            var tag = Navigation.Data.lastVisitedNode.data;
            var x = Navigation.Data.robotPose.pose.position.x;
            var y = Navigation.Data.robotPose.pose.position.y;
            var theta = BarcodeReader.Data.theta;
            return new(tag, x, y, theta);
        }

        protected internal void InitAGVControl(string RosBridge_IP, int RosBridge_Port)
        {
            CreateAGVCInstance(RosBridge_IP, RosBridge_Port);
            AGVC.Connect();
            AGVC.ManualController.vehicle = this;
            AGVC.OnModuleInformationUpdated += ModuleInformationHandler;
            AGVC.OnSickLocalicationDataUpdated += CarController_OnSickDataUpdated;
            AGVC.OnSickRawDataUpdated += SickRawDataHandler;
            AGVC.OnSickOutputPathsDataUpdated += (sender, OutputPaths) => Laser.CurrentLaserMonitoringCase = OutputPaths.active_monitoring_case;
        }


        private void SickRawDataHandler(object? sender, RawMicroScanDataMsg RawData)
        {
            Task.Factory.StartNew(() =>
            {
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

        protected internal virtual async void SoftwareEMO(AlarmCodes alarmCode)
        {
            LOG.TRACE($"IsSystemInitialized {IsSystemInitialized}");
            AGVSResetCmdFlag = true;
            Task.Factory.StartNew(() => BuzzerPlayer.Alarm());
            Sub_Status = SUB_STATUS.DOWN;
            InitializeCancelTokenResourece.Cancel();
            SetAGV_TR_REQ(false);
            if (AGVC.ActionStatus != ActionStatus.NO_GOAL)
                AGVC.AbortTask();
            if ((DateTime.Now - previousSoftEmoTime).TotalSeconds > 2)
            {
                AlarmManager.AddAlarm(alarmCode);
                ExecutingActionTask?.Abort();
                if (!_RunTaskData.IsLocalTask)
                {
                    await FeedbackTaskStatus(TASK_RUN_STATUS.ACTION_FINISH, alarm_tracking: alarmCode);
                    if (Remote_Mode == REMOTE_MODE.ONLINE)
                    {
                        LOG.INFO($"UnRecoveralble Alarm Happened, 自動請求OFFLINE");
                        await Online_Mode_Switch(REMOTE_MODE.OFFLINE);
                    }
                }
                DirectionLighter.CloseAll();
                DOSettingWhenEmoTrigger();
                StatusLighter.DOWN();
            }
            IsInitialized = false;
            ExecutingActionTask = null;
            AGVC._ActionStatus = ActionStatus.NO_GOAL;
            previousSoftEmoTime = DateTime.Now;
        }
        protected internal virtual void SoftwareEMO()
        {
            SoftwareEMO(AlarmCodes.SoftwareEMS);
        }
        private bool IsResetAlarmWorking = false;
        internal async Task ResetAlarmsAsync(bool IsTriggerByButton)
        {
            if (IsResetAlarmWorking)
                return;

            await ResetMotor();
            if (AlarmManager.CurrentAlarms.Values.Count == 0)
            {
                IsResetAlarmWorking = false;
                return;
            }
            BuzzerPlayer.Stop();
            AlarmManager.ClearAlarm();
            AGVAlarmReportable.ResetAlarmCodes();
            StaSysMessageManager.Clear();
            IsResetAlarmWorking = true;

            if (AGVC.ActionStatus == ActionStatus.ACTIVE)
            {
                if (_RunTaskData.Action_Type == ACTION_TYPE.None)
                {
                    BuzzerPlayer.Stop();
                    BuzzerPlayer.Move();
                }
                else
                {
                    BuzzerPlayer.Stop();
                    BuzzerPlayer.Action();
                }
            }
            IsResetAlarmWorking = false;
            return;
        }

        public virtual async Task<bool> ResetMotor()
        {
            try
            {

                await WagoDO.ResetSaftyRelay();
                if (WagoDI.GetState(DI_ITEM.Horizon_Motor_Busy_1) && WagoDI.GetState(DI_ITEM.Horizon_Motor_Busy_2))
                    return true;

                Console.WriteLine("Reset Motor Process Start");
                await WagoDO.SetState(DO_ITEM.Horizon_Motor_Stop, true);
                await WagoDO.SetState(DO_ITEM.Horizon_Motor_Free, true);
                await Task.Delay(100);
                await WagoDO.SetState(DO_ITEM.Horizon_Motor_Reset, true);
                await Task.Delay(100);
                await WagoDO.SetState(DO_ITEM.Horizon_Motor_Reset, false);
                await Task.Delay(100);
                await WagoDO.SetState(DO_ITEM.Horizon_Motor_Stop, false);
                await WagoDO.SetState(DO_ITEM.Horizon_Motor_Free, false);
                Console.WriteLine("Reset Motor Process End");
                return true;
            }
            catch (SocketException ex)
            {
                return false;
            }
            catch (Exception ex)
            {
                return false;
            }

        }


        internal bool HandleRemoteModeChangeReq(REMOTE_MODE mode, bool IsAGVSRequest = false)
        {
            if (mode != Remote_Mode)
            {
                string request_user_name = IsAGVSRequest ? "AGVS" : "車載用戶";
                LOG.WARN($"{request_user_name} 請求變更Online模式為:{mode}");
                (bool success, RETURN_CODE return_code) result = Online_Mode_Switch(mode).Result;
                if (result.success)
                    LOG.WARN($"{request_user_name} 請求變更Online模式為 {mode}---成功");
                else
                    LOG.ERROR($"{request_user_name} 請求變更Online模式為{mode}---失敗 Return Code = {(int)result.return_code}-{result.return_code})");
                return result.success;
            }
            else
            {
                return true;
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
            if (mode == OPERATOR_MODE.AUTO)
            {
                await Laser.AllLaserActive();
            }
            else
            {
                await Laser.AllLaserDisable();
            }
            return true;
        }

        internal virtual bool HasAnyCargoOnAGV()
        {
            try
            {
                return !WagoDI.GetState(DI_ITEM.Cst_Sensor_1) | !WagoDI.GetState(DI_ITEM.Cst_Sensor_2);
            }
            catch (Exception)
            {
                return false;
            }
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

                if (Sub_Status != SUB_STATUS.IDLE)
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
    }
}
