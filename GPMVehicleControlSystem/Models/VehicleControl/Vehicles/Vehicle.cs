using AGVSystemCommonNet6.AGVDispatch;
using AGVSystemCommonNet6.AGVDispatch.Messages;
using AGVSystemCommonNet6.AGVDispatch.Model;
using AGVSystemCommonNet6.GPMRosMessageNet.Messages;
using AGVSystemCommonNet6.GPMRosMessageNet.SickSafetyscanners;
using AGVSystemCommonNet6.Log;
using AGVSystemCommonNet6.MAP;
using AGVSystemCommonNet6.Vehicle_Control;
using AGVSystemCommonNet6.Vehicle_Control.VMS_ALARM;
using GPMVehicleControlSystem.Models.Buzzer;
using GPMVehicleControlSystem.Models.Emulators;
using GPMVehicleControlSystem.Models.NaviMap;

using GPMVehicleControlSystem.Models.VehicleControl.AGVControl;
using GPMVehicleControlSystem.Models.VehicleControl.TaskExecute;
using GPMVehicleControlSystem.Models.VehicleControl.VehicleComponent;
using GPMVehicleControlSystem.Models.WebsocketMiddleware;
using GPMVehicleControlSystem.Models.WorkStation;
using GPMVehicleControlSystem.VehicleControl.DIOModule;
using Newtonsoft.Json;
using RosSharp.RosBridgeClient;
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
                if (!IsInitialized)
                    return MAIN_STATUS.DOWN;
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
        protected bool IsResetAlarmWorking = false;
        protected bool IsMotorReseting = false;
        internal SUB_STATUS _Sub_Status = SUB_STATUS.DOWN;
        public MapPoint lastVisitedMapPoint { get; private set; } = new MapPoint();
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
                try
                {
                    var _point = NavingMap.Points.Values.FirstOrDefault(pt => pt.TagNumber == _RunTaskData.Destination);
                    return _point == null ? new MapPoint { Name = _RunTaskData.Destination.ToString(), TagNumber = _RunTaskData.Destination } : _point;
                }
                catch (Exception)
                {
                    return new MapPoint { Name = _RunTaskData.Destination.ToString(), TagNumber = _RunTaskData.Destination };
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
                    try
                    {
                        if (value == SUB_STATUS.DOWN | value == SUB_STATUS.ALARM | value == SUB_STATUS.Initializing)
                        {
                            if (value == SUB_STATUS.DOWN)
                                SetAGV_TR_REQ(false);
                            if (value != SUB_STATUS.Initializing && _Sub_Status != SUB_STATUS.Charging && Operation_Mode == OPERATOR_MODE.AUTO)
                                BuzzerPlayer.Alarm();
                            DirectionLighter.CloseAll(1000);
                            StatusLighter.DOWN();
                        }
                        else if (value == SUB_STATUS.IDLE)
                        {
                            BuzzerPlayer.Stop();
                            StatusLighter.IDLE();
                            DirectionLighter.CloseAll(1000);
                        }
                        else if (value == SUB_STATUS.RUN)
                        {
                            StatusLighter.RUN();
                        }

                    }
                    catch (Exception ex)
                    {
                    }
                    _Sub_Status = value;
                }
            }
        }

        public Vehicle()
        {
            try
            {
                Parameters = LoadParameters(watch_file_change: true);
                IMU.Options = Parameters.ImpactDetection;

                CIMConnectionInitialize();
                LoadWorkStationConfigs();
                LOG.INFO($"{GetType().Name} Start create instance...");
                ReadTaskNameFromFile();
                IsSystemInitialized = false;
                string Wago_IP = Parameters.Connections["Wago"].IP;
                int Wago_Port = Parameters.Connections["Wago"].Port;
                int Wago_Protocol_Interval_ms = Parameters.Connections["Wago"].Protocol_Interval_ms;
                int LastVisitedTag = Parameters.LastVisitedTag;
                string RosBridge_IP = Parameters.Connections["RosBridge"].IP;
                int RosBridge_Port = Parameters.Connections["RosBridge"].Port;
                WagoDO = new clsDOModule(Wago_IP, Wago_Port, null)
                {
                    AgvType = Parameters.AgvType
                };
                WagoDI = new clsDIModule(Wago_IP, Wago_Port, WagoDO, Wago_Protocol_Interval_ms)
                {
                    AgvType = Parameters.AgvType
                };
                DirectionLighter.DOModule = WagoDO;

                StatusLighter = new clsStatusLighter(WagoDO);
                Laser = new clsLaser(WagoDO, WagoDI)
                {
                    Spin_Laser_Mode = Parameters.Spin_Laser_Mode
                };

                Task RosConnTask = new Task(async () =>
                {
                    await Task.Delay(1).ContinueWith(async t =>
                    {
                        await InitAGVControl(RosBridge_IP, RosBridge_Port);
                        if (AGVC?.rosSocket != null)
                        {
                            AGVC.OnRosSocketReconnected += AGVC_OnRosSocketReconnected;
                            BuzzerPlayer.rossocket = AGVC.rosSocket;
                            AlarmManager.Active = false;

                            lastVisitedMapPoint = new MapPoint(LastVisitedTag + "", LastVisitedTag);
                            Navigation.StateData = new NavigationState() { lastVisitedNode = new RosSharp.RosBridgeClient.MessageTypes.Std.Int32(LastVisitedTag) };
                            BarcodeReader.StateData = new BarcodeReaderState() { tagID = (uint)LastVisitedTag };

                            CommonEventsRegist();
                            //TrafficMonitor();
                            LOG.INFO($"設備交握通訊方式:{Parameters.EQHandshakeMethod}");
                            await Task.Delay(3000);
                            BuzzerPlayer.Alarm();
                            IsSystemInitialized = true;
                            AlarmManager.Active = true;
                            AlarmManager.AddAlarm(AlarmCodes.None);


                            if (AGVS.UseWebAPI)
                            {
                                var eqinfomations = await GetWorkStationEQInformation();
                                if (eqinfomations != null)
                                {
                                    WorkStations.SyncInfo(eqinfomations);
                                    SaveTeachDAtaSettings();
                                }
                            }

                        }
                    }
                    );
                });

                AGVSInit();
                EmulatorInitialize();
                Task WagoDIConnTask = WagoDIInit();
                WagoDIConnTask.Start();
                WebsocketAgent.StartViewDataCollect();
                RosConnTask.Start();
                StartConfigChangedWatcher();
                Task.Factory.StartNew(async () =>
                {
                    ReloadLocalMap();
                    await Task.Delay(1000);
                    await DownloadMapFromServer();
                });
            }
            catch (Exception ex)
            {
                IsSystemInitialized = false;
                string msg = $"車輛實例化時於建構函式發生錯誤 : {ex.Message}:{ex.StackTrace}";
                throw ex;
            }

        }

        private void AGVC_OnRosSocketReconnected(object? sender, EventArgs e)
        {
            BuzzerPlayer.rossocket = (RosSocket)sender;
        }




        private void EmulatorInitialize()
        {
            if (Parameters.SimulationMode)
            {
                try
                {
                    StaEmuManager.StartAGVROSEmu();
                    StaEmuManager.agvRosEmu.SetInitTag(Parameters.LastVisitedTag);
                }
                catch (SocketException)
                {
                    LOG.ERROR("模擬器無法啟動 (無法建立服務器): 請嘗試使用系統管理員權限開啟程式");
                }
                catch (Exception ex)
                {
                    LOG.ERROR("\"模擬器無法啟動 : 異常訊息\" + ex.Message"); ;
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
                    LOG.ERROR("量測服務模擬器無法啟動 (無法建立服務器): 請嘗試使用系統管理員權限開啟程式");
                }
                catch (Exception ex)
                {
                    LOG.ERROR("\"量測服務模擬器無法啟動 : 異常訊息\" + ex.Message");
                }
            }

            if (Parameters.WagoSimulation)
            {
                StaEmuManager.StartWagoEmu(WagoDI);
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

        private Task WagoDIInit()
        {
            return new Task(async () =>
            {
                try
                {
                    DIOStatusChangedEventRegist();
                    while (!await WagoDI.Connect())
                    {
                        await Task.Delay(1000);
                    }
                    WagoDI.StartAsync();
                    DOSignalDefaultSetting();
                    ResetMotor();
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

        internal async Task<bool> IsAllLaserNoTrigger()
        {
            await Task.Delay(10);
            var FrontArea1 = WagoDI.GetState(DI_ITEM.FrontProtection_Area_Sensor_1);
            var FrontArea2 = WagoDI.GetState(DI_ITEM.FrontProtection_Area_Sensor_2);
            var FrontArea3 = WagoDI.GetState(DI_ITEM.FrontProtection_Area_Sensor_3);

            var BackArea1 = WagoDI.GetState(DI_ITEM.BackProtection_Area_Sensor_1);
            var BackArea2 = WagoDI.GetState(DI_ITEM.BackProtection_Area_Sensor_2);
            var BackArea3 = WagoDI.GetState(DI_ITEM.BackProtection_Area_Sensor_3);

            var RightArea = WagoDI.GetState(DI_ITEM.RightProtection_Area_Sensor_3);
            var LeftArea = WagoDI.GetState(DI_ITEM.LeftProtection_Area_Sensor_3);

            LOG.INFO($"雷射狀態檢查(IsAllLaserNoTrigger)\r\n" +
                        $"Front_Area 1->3 ={FrontArea1.ToSymbol("O", "X")}|{FrontArea2.ToSymbol("O", "X")}|{FrontArea3.ToSymbol("O", "X")}\r\n" +
                        $"Back_Area  1->3 ={BackArea1.ToSymbol("O", "X")}|{BackArea2.ToSymbol("O", "X")}|{BackArea3.ToSymbol("O", "X")}\r\n" +
                        $"Right_Area      ={RightArea.ToSymbol("O", "X")}\r\n" +
                        $"Left_Area       ={LeftArea.ToSymbol("O", "X")}");

            return FrontArea1 && FrontArea2 && FrontArea3 && BackArea1 && BackArea2 && BackArea3 && RightArea | LeftArea;
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

            if (Sub_Status == SUB_STATUS.RUN)
            {
                return (false, $"當前狀態不可進行初始化(任務執行中)");
            }

            if (Sub_Status != SUB_STATUS.DOWN && (AGVC.ActionStatus == ActionStatus.ACTIVE | Sub_Status == SUB_STATUS.Initializing))
            {
                string reason_string = Sub_Status != SUB_STATUS.RUN ? (Sub_Status == SUB_STATUS.Initializing ? "初始化程序執行中" : "任務進行中") : "AGV狀態為RUN";
                return (false, $"當前狀態不可進行初始化({reason_string})");
            }
            orderInfoViewModel.ActionName = ACTION_TYPE.NoAction;

            if ((Parameters.AgvType == AGV_TYPE.FORK | Parameters.AgvType == AGV_TYPE.SUBMERGED_SHIELD))
            {
                if (Parameters.Auto_Cleaer_CST_ID_Data_When_Has_Data_But_NO_Cargo && !HasAnyCargoOnAGV() && CSTReader.ValidCSTID != "")
                {
                    CSTReader.ValidCSTID = "";
                    LOG.WARN($"偵測到AGV有帳無料，已完成自動清帳");
                }
                if (Parameters.Auto_Read_CST_ID_When_No_Data_But_Has_Cargo && HasAnyCargoOnAGV() && CSTReader.ValidCSTID == "")
                {
                    (bool request_success, bool action_done) result = await AGVC.TriggerCSTReader();
                    if (result.request_success)
                        LOG.WARN($"偵測到AGV無帳有料，已完成自動建帳");
                }
            }
            IsWaitForkNextSegmentTask = false;
            InitializeCancelTokenResourece = new CancellationTokenSource();

            AlarmManager.ClearAlarm();
            return await Task.Run(async () =>
            {
                StopAllHandshakeTimer();
                StatusLighter.Flash(DO_ITEM.AGV_DiractionLight_Y, 600);
                try
                {
                    IsMotorReseting = false;
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

                    await Laser.ModeSwitch(LASER_MODE.Bypass);
                    await Laser.AllLaserDisable();
                    await Task.Delay(Parameters.AgvType == AGV_TYPE.SUBMERGED_SHIELD ? 500 : 1000);
                    StatusLighter.AbortFlash();
                    DirectionLighter.CloseAll();
                    Sub_Status = SUB_STATUS.IDLE;

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
            if (ExecutingTaskModel != null)
            {
                ExecutingTaskModel.AGVCActionStatusChaged = null;
            }
            AGVC.OnAGVCActionChanged = null;
            await AGVC.SendGoal(new AGVSystemCommonNet6.GPMRosMessageNet.Actions.TaskCommandGoal());
            ExecutingTaskModel = null;
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
            AlarmCodeWhenHandshaking = AlarmCodes.None;
            if (!WagoDI.Connected)
                return (false, $"DIO 模組連線異常");
            return (true, "");
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

        protected internal async Task InitAGVControl(string RosBridge_IP, int RosBridge_Port)
        {
            CreateAGVCInstance(RosBridge_IP, RosBridge_Port);
            AGVC.Throttle_rate_of_Topic_ModuleInfo = Parameters.ModuleInfoTopicRevHandlePeriod;
            AGVC.QueueSize_of_Topic_ModuleInfo = Parameters.ModuleInfoTopicRevQueueSize;
            await AGVC.Connect();
            AGVC.ManualController.vehicle = this;
            AGVC.OnModuleInformationUpdated += ModuleInformationHandler;
            AGVC.OnSickLocalicationDataUpdated += HandleSickLocalizationStateChanged;
            AGVC.OnSickRawDataUpdated += SickRawDataHandler;
            AGVC.OnSickLaserModeSettingChanged += (sender, active_monitoring_case) => Laser.CurrentLaserModeOfSick = active_monitoring_case;
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

        protected internal virtual async void SoftwareEMO(AlarmCodes alarmCode)
        {
            LOG.WARN($"Software EMO!!! {alarmCode}");
            AlarmManager.AddAlarm(alarmCode);
            _Sub_Status = SUB_STATUS.DOWN;
            BuzzerPlayer.Alarm();
            SetAGV_TR_REQ(false);
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
            AGVSTaskFeedBackReportAndOffline(alarmCode);

            if (AGVC.ActionStatus != ActionStatus.NO_GOAL)
            {
                AGVC._ActionStatus = ActionStatus.NO_GOAL;
                AGVC.OnAGVCActionChanged += RaiseActionStatusNoGoal;
                AGVC.SendGoal(new AGVSystemCommonNet6.GPMRosMessageNet.Actions.TaskCommandGoal());//下空任務清空
            }
            void RaiseActionStatusNoGoal(ActionStatus status)
            {
                LOG.WARN($"[Software EMO] AGVC Status changed to {status}");
                if (status == ActionStatus.SUCCEEDED)
                {
                    Task.Factory.StartNew(async () =>
                    {
                        await Task.Delay(1000);
                        AGVC._ActionStatus = ActionStatus.NO_GOAL;
                        LOG.WARN($"[Software EMO] Set AGVC Status changed to {ActionStatus.NO_GOAL}");
                        AGVC.OnAGVCActionChanged -= RaiseActionStatusNoGoal;
                        ExecutingTaskModel?.Dispose();
                        ExecutingTaskModel = null;
                    });
                }
            }
        }


        private async void AGVSTaskFeedBackReportAndOffline(AlarmCodes alarmCode)
        {
            if (!_RunTaskData.IsLocalTask && !_RunTaskData.IsActionFinishReported)
            {
                if ((_RunTaskData.Action_Type != ACTION_TYPE.Load && _RunTaskData.Action_Type != ACTION_TYPE.Unload) | !_RunTaskData.IsEQHandshake) //非取放貨任務 一律上報任務完成
                    FeedbackTaskStatus(TASK_RUN_STATUS.ACTION_FINISH, alarm_tracking: alarmCode);
                else //取放貨任務僅等交握異常的AlarmCode觸發才上報任務完成
                {
                    if (IsHandshakeFailAlarmCode(alarmCode))
                    {
                        LOG.TRACE($"FeedbackTaskStatus Action Finish with alarm code-{alarmCode}");
                        FeedbackTaskStatus(TASK_RUN_STATUS.ACTION_FINISH, alarm_tracking: alarmCode);
                    }

                }
            }
            if (Remote_Mode == REMOTE_MODE.ONLINE)
            {
                LOG.INFO($"UnRecoveralble Alarm Happened, 自動請求OFFLINE");
                await Online_Mode_Switch(REMOTE_MODE.OFFLINE);
            }
        }

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

        internal async Task ResetAlarmsAsync(bool IsTriggerByButton)
        {
            if (IsResetAlarmWorking)
                return;

            IsResetAlarmWorking = true;
            BuzzerPlayer.Stop();
            AlarmManager.ClearAlarm();
            AGVAlarmReportable.ResetAlarmCodes();
            IsMotorReseting = false;
            await ResetMotor();
            _ = Task.Factory.StartNew(async () =>
            {
                await Task.Delay(1000);
                bool isObstacle = !WagoDI.GetState(DI_ITEM.BackProtection_Area_Sensor_2) | !WagoDI.GetState(DI_ITEM.FrontProtection_Area_Sensor_2) | !WagoDI.GetState(DI_ITEM.RightProtection_Area_Sensor_3) | !WagoDI.GetState(DI_ITEM.LeftProtection_Area_Sensor_3);
                if (AGVC.ActionStatus == ActionStatus.ACTIVE)
                {
                    if (isObstacle)
                    {
                        BuzzerPlayer.Alarm();
                        return;
                    }
                    else
                    {
                        if (ExecutingTaskModel.action == ACTION_TYPE.None)
                            BuzzerPlayer.Move();
                        else
                            BuzzerPlayer.Action();
                        return;
                    }
                }

            });
            await Task.Delay(500);
            IsResetAlarmWorking = false;
            return;
        }
        protected private async Task<bool> SetMotorStateAndDelay(DO_ITEM item, bool state, int delay = 10)
        {
            bool success = await WagoDO.SetState(item, state);
            if (!success) return false;
            await Task.Delay(delay);
            return true;
        }
        public virtual async Task<bool> ResetMotor(bool bypass_when_motor_busy_on = true)
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
                await WagoDO.ResetSaftyRelay();
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

                if (Parameters.SimulationMode)
                {
                    StaEmuManager.wagoEmu.SetState(DI_ITEM.Horizon_Motor_Busy_1, true);
                    StaEmuManager.wagoEmu.SetState(DI_ITEM.Horizon_Motor_Busy_2, true);

                    StaEmuManager.wagoEmu.SetState(DI_ITEM.Horizon_Motor_Alarm_1, false);
                    StaEmuManager.wagoEmu.SetState(DI_ITEM.Horizon_Motor_Alarm_2, false);
                    if (Parameters.AgvType == AGV_TYPE.INSPECTION_AGV)
                    {
                        StaEmuManager.wagoEmu.SetState(DI_ITEM.Horizon_Motor_Busy_3, true);
                        StaEmuManager.wagoEmu.SetState(DI_ITEM.Horizon_Motor_Busy_4, true);

                        StaEmuManager.wagoEmu.SetState(DI_ITEM.Horizon_Motor_Alarm_3, false);
                        StaEmuManager.wagoEmu.SetState(DI_ITEM.Horizon_Motor_Alarm_4, false);
                    }
                    StaEmuManager.agvRosEmu.ClearDriversErrorCodes();
                }

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
