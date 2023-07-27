using AGVSystemCommonNet6.Abstracts;
using AGVSystemCommonNet6.AGVDispatch;
using AGVSystemCommonNet6.AGVDispatch.Messages;
using AGVSystemCommonNet6.AGVDispatch.Model;
using AGVSystemCommonNet6.Alarm.VMS_ALARM;
using AGVSystemCommonNet6.GPMRosMessageNet.Messages;
using AGVSystemCommonNet6.GPMRosMessageNet.Messages.SickMsg;
using AGVSystemCommonNet6.GPMRosMessageNet.SickSafetyscanners;
using AGVSystemCommonNet6.Log;
using AGVSystemCommonNet6.MAP;
using GPMVehicleControlSystem.Models.Buzzer;
using GPMVehicleControlSystem.Models.Emulators;
using GPMVehicleControlSystem.Models.NaviMap;
using GPMVehicleControlSystem.Models.VCSSystem;
using GPMVehicleControlSystem.Models.VehicleControl.AGVControl;
using GPMVehicleControlSystem.Models.VehicleControl.VehicleComponent;
using GPMVehicleControlSystem.VehicleControl.DIOModule;
using System.Diagnostics;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using static AGVSystemCommonNet6.clsEnums;
using static GPMVehicleControlSystem.Models.VehicleControl.VehicleComponent.clsLaser;
using static GPMVehicleControlSystem.VehicleControl.DIOModule.clsDOModule;

namespace GPMVehicleControlSystem.Models.VehicleControl.Vehicles
{
    /// <summary>
    /// 車子
    /// </summary>
    public abstract partial class Vehicle
    {

        public enum VMS_PROTOCOL
        {
            KGS,
            GPM_VMS
        }

        public abstract clsDirectionLighter DirectionLighter { get; set; }
        public clsStatusLighter StatusLighter { get; set; }
        public clsAGVSConnection AGVS;
        public VMS_PROTOCOL VmsProtocol = VMS_PROTOCOL.GPM_VMS;
        public clsDOModule WagoDO;
        public clsDIModule WagoDI;
        public CarController AGVC;
        public clsLaser Laser;
        public string CarName { get; set; }
        public string SID { get; set; }

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

        public MAIN_STATUS Main_Status { get; internal set; } = MAIN_STATUS.DOWN;
        public bool AGV_Reset_Flag { get; internal set; }

        public MoveControl ManualController => AGVC.ManualController;

        public AGV_TYPE AgvType { get; internal set; } = AGV_TYPE.SUBMERGED_SHIELD;
        public int AgvTypeInt
        {
            get => (int)AgvType;
            private set
            {
                AgvType = Enum.GetValues(typeof(AGV_TYPE)).Cast<AGV_TYPE>().FirstOrDefault(_type => (int)_type == value);
            }
        }
        public bool SimulationMode { get; internal set; } = false;
        public bool IsInitialized { get; internal set; }
        public bool IsSystemInitialized { get; internal set; }
        private SUB_STATUS _Sub_Status = SUB_STATUS.DOWN;
        public MapPoint lastVisitedMapPoint { get; private set; } = new MapPoint { Name = "Unkown" };
        public MapPoint DestinationMapPoint
        {
            get
            {
                if (ExecutingTask == null)
                    return new MapPoint { Name = "" };
                else
                {
                    var _point = NavingMap.Points.Values.FirstOrDefault(pt => pt.TagNumber == ExecutingTask.RunningTaskData.Destination);
                    return _point == null ? new MapPoint { Name = "Unknown" } : _point;
                }
            }
        }

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
                        BuzzerPlayer.Stop();
                        if (value == SUB_STATUS.DOWN | value == SUB_STATUS.ALARM | value == SUB_STATUS.Initializing)
                        {
                            if (value == SUB_STATUS.DOWN | value == SUB_STATUS.Initializing)
                                Main_Status = MAIN_STATUS.DOWN;

                            if (value != SUB_STATUS.Initializing)
                                BuzzerPlayer.Alarm();
                            else
                            {
                                DirectionLighter.WaitPassLights(500);
                            }
                            StatusLighter.DOWN();
                        }
                        else if (value == SUB_STATUS.IDLE)
                        {
                            AGVC.IsAGVExecutingTask = false;
                            Main_Status = MAIN_STATUS.IDLE;
                            StatusLighter.IDLE();
                            DirectionLighter.CloseAll();
                        }
                        else if (value == SUB_STATUS.Charging)
                        {
                            Main_Status = MAIN_STATUS.Charging;
                        }
                        else if (value == SUB_STATUS.RUN)
                        {
                            Main_Status = MAIN_STATUS.RUN;
                            StatusLighter.RUN();
                            if (ExecutingTask != null)
                            {
                                Task.Run(async () =>
                                {
                                    if (ExecutingTask.action == ACTION_TYPE.None)
                                        BuzzerPlayer.Move();
                                    else
                                        BuzzerPlayer.Action();
                                });
                            }
                        }

                    }
                    catch (Exception ex)
                    {
                    }
                }
            }
        }

        public abstract string WagoIOConfigFilePath { get; }
        public Vehicle()
        {
            try
            {
                Navigation.OnDirectionChanged += Navigation_OnDirectionChanged;
                Navigation.OnTagReach += OnTagReachHandler;
                BarcodeReader.OnTagLeave += OnTagLeaveHandler;

                LOG.INFO($"{GetType().Name} Start create instance...");
                ReadTaskNameFromFile();
                IsSystemInitialized = false;
                SimulationMode = AppSettingsHelper.GetValue<bool>("VCS:SimulationMode");


                AgvTypeInt = AppSettingsHelper.GetValue<int>("VCS:AgvType");
                string AGVS_IP = AppSettingsHelper.GetValue<string>("VCS:Connections:AGVS:IP");
                int AGVS_Port = AppSettingsHelper.GetValue<int>("VCS:Connections:AGVS:Port");
                string AGVS_LocalIP = AppSettingsHelper.GetValue<string>("VCS:Connections:AGVS:LocalIP");
                VmsProtocol = AppSettingsHelper.GetValue<int>("VCS:Connections:AGVS:Protocol") == 0 ? VMS_PROTOCOL.KGS : VMS_PROTOCOL.GPM_VMS;
                string Wago_IP = AppSettingsHelper.GetValue<string>("VCS:Connections:Wago:IP");
                int Wago_Port = AppSettingsHelper.GetValue<int>("VCS:Connections:Wago:Port");
                int LastVisitedTag = AppSettingsHelper.GetValue<int>("VCS: LastVisitedTag");

                string RosBridge_IP = AppSettingsHelper.GetValue<string>("VCS:Connections:RosBridge:IP");
                int RosBridge_Port = AppSettingsHelper.GetValue<int>("VCS:Connections:RosBridge:Port");
                SID = AppSettingsHelper.GetValue<string>("VCS:SID");
                CarName = AppSettingsHelper.GetValue<string>("VCS:EQName");
                AGVSMessageFactory.Setup(SID, CarName);
                WagoIOIniSetting();
                WagoDO = new clsDOModule(Wago_IP, Wago_Port, null)
                {
                    AgvType = AgvType
                };
                WagoDI = new clsDIModule(Wago_IP, Wago_Port, WagoDO)
                {
                    AgvType = AgvType
                };
                DirectionLighter.DOModule = WagoDO;

                StatusLighter = new clsStatusLighter(WagoDO);
                Laser = new clsLaser(WagoDO, WagoDI);

                if (SimulationMode)
                {
                    try
                    {
                        emulator = new VehicleEmu(7);
                        StaEmuManager.StartWagoEmu(WagoDI);
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
                Task RosConnTask = new Task(async () =>
                {
                    await Task.Delay(1).ContinueWith(t =>
                    {
                        InitAGVControl(RosBridge_IP, RosBridge_Port);
                        if (AGVC?.rosSocket != null)
                        {
                            BuzzerPlayer.rossocket = AGVC.rosSocket;
                            BuzzerPlayer.Alarm();
                        }
                    }
                    );
                });

                Task WagoDIConnTask = WagoDIInit();

                RosConnTask.Start();
                WagoDIConnTask.Start();
                DownloadMapFromServer();
                AlarmManager.OnUnRecoverableAlarmOccur += AlarmManager_OnUnRecoverableAlarmOccur;
                AGVSMessageFactory.OnVCSRunningDataRequest += GenRunningStateReportData;
                AGVSInit(AGVS_IP, AGVS_Port, AGVS_LocalIP);
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

        private void DownloadMapFromServer()
        {
            Task.Run(async () =>
            {
                try
                {
                    NavingMap = await MapStore.GetMapFromServer();
                    LOG.INFO($"Map Downloaded. Map Name : {NavingMap.Name}, Version: {NavingMap.Note}");
                }
                catch (Exception ex)
                {
                    LOG.WARN($"Map Download Fail....{ex.Message}");
                }
            });
        }

        private void AGVSInit(string AGVS_IP, int AGVS_Port, string AGVS_LocalIP)
        {
            //AGVS
            AGVS = new clsAGVSConnection(AGVS_IP, AGVS_Port, AGVS_LocalIP);
            AGVS.UseWebAPI = VmsProtocol == VMS_PROTOCOL.GPM_VMS;
            AGVS.OnRemoteModeChanged = AGVSRemoteModeChangeReq;
            AGVS.OnTaskDownload += AGVSTaskDownloadConfirm;
            AGVS.OnTaskResetReq = AGVSTaskResetReqHandle;
            AGVS.OnTaskDownloadFeekbackDone += ExecuteAGVSTask;
            AGVS.Start();
        }

        private Task WagoDIInit()
        {
            return new Task(async () =>
            {
                try
                {
                    WagoDIEventRegist();
                    while (!WagoDI.Connect())
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

        protected virtual void WagoDIEventRegist()
        {
            WagoDI.OnEMO += WagoDI_OnEMO;
            WagoDI.OnBumpSensorPressed += WagoDI_OnBumpSensorPressed;
            WagoDI.OnResetButtonPressed += async (s, e) => await ResetAlarmsAsync(true);
            WagoDI.OnLaserDIRecovery += LaserRecoveryHandler;
            WagoDI.OnFarLaserDITrigger += FarLaserTriggerHandler;
            WagoDI.OnNearLaserDiTrigger += NearLaserTriggerHandler;
        }

        private void WagoIOIniSetting()
        {
            string IO_Wago_ini_Use = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "param/IO_Wago.ini");

            if (!File.Exists(WagoIOConfigFilePath))
            {
                StaSysMessageManager.AddNewMessage($"Specfic DI/O Module ini File Not Exist [{WagoIOConfigFilePath}]", 2);
                return;
            }
            File.Copy(WagoIOConfigFilePath, IO_Wago_ini_Use, true);
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

        public async Task<(bool confirm, string message)> Initialize()
        {
            if (Sub_Status == SUB_STATUS.RUN | Sub_Status == SUB_STATUS.Initializing)
                return (false, $"當前狀態不可進行初始化({Sub_Status})");
            try
            {
                (bool, string) result = await PreActionBeforeInitialize();
                if (!result.Item1)
                    return result;

                Sub_Status = SUB_STATUS.Initializing;
                IsInitialized = false;

                result = await InitializeActions();
                if (!result.Item1)
                {
                    Sub_Status = SUB_STATUS.STOP;
                    IsInitialized = false;
                    StatusLighter.AbortFlash();
                    return result;
                }

                StatusLighter.AbortFlash();
                await Laser.ModeSwitch(LASER_MODE.Bypass);
                Sub_Status = SUB_STATUS.IDLE;
                IsInitialized = true;
                return (true, "");
            }
            catch (Exception ex)
            {
                _Sub_Status = SUB_STATUS.DOWN;
                BuzzerPlayer.Alarm();
                IsInitialized = false;
                return (false, $"AGV Initizlize Code Error ! : \r\n{ex.Message}");
            }
        }

        protected virtual async Task<(bool, string)> PreActionBeforeInitialize()
        {
            AGVC.IsAGVExecutingTask = false;
            BuzzerPlayer.Stop();
            DirectionLighter.CloseAll();
            if (EQAlarmWhenEQBusyFlag && WagoDI.GetState(clsDIModule.DI_ITEM.EQ_BUSY))
            {
                return (false, $"端點設備({lastVisitedMapPoint.Name})尚未進行復歸，AGV禁止復歸");
            }

            AGVAlarmWhenEQBusyFlag = false;
            EQAlarmWhenEQBusyFlag = false;
            WagoDO.ResetHandshakeSignals();

            if (!WagoDI.GetState(clsDIModule.DI_ITEM.EMO))
            {
                AlarmManager.AddAlarm(AlarmCodes.EMO_Button, false);
                BuzzerPlayer.Alarm();
                return (false, "EMO 按鈕尚未復歸");
            }

            if (!WagoDI.GetState(clsDIModule.DI_ITEM.Horizon_Motor_Switch))
            {
                AlarmManager.AddAlarm(AlarmCodes.Switch_Type_Error, false);
                BuzzerPlayer.Alarm();
                return (false, "解煞車旋鈕尚未復歸");
            }
            return (true, "");
        }
        protected abstract Task<(bool confirm, string message)> InitializeActions();
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
            AGVC.OnSickOutputPathsDataUpdated += SickOutputPathsDataHandler;
            AGVC.OnTaskActionFinishCauseAbort += AGVCTaskAbortedHandle;

        }

        private void SickOutputPathsDataHandler(object? sender, OutputPathsMsg OutputPaths)
        {
            Laser.CurrentLaserMonitoringCase = OutputPaths.active_monitoring_case;
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

        protected internal virtual void SoftwareEMO()
        {
            BuzzerPlayer.Alarm();
            _Sub_Status = SUB_STATUS.DOWN;
            IsInitialized = false;
            AGVC.EMOHandler("SoftwareEMO", EventArgs.Empty);
            ExecutingTask?.Abort();
            AGVSRemoteModeChangeReq(REMOTE_MODE.OFFLINE);
            AlarmManager.AddAlarm(AlarmCodes.SoftwareEMS, false);


        }
        private bool IsResetAlarmWorking = false;
        internal async Task ResetAlarmsAsync(bool IsTriggerByButton)
        {
            BuzzerPlayer.Stop();
            AlarmManager.ClearAlarm();
            AGVAlarmReportable.ResetAlarmCodes();
            StaSysMessageManager.Clear();

            if (IsResetAlarmWorking)
                return;

            IsResetAlarmWorking = true;
            bool motor_reset_success = await ResetMotor();
            if (!motor_reset_success)
            {
                AlarmManager.AddAlarm(AlarmCodes.Wago_IO_Write_Fail);
                BuzzerPlayer.Alarm();
                _Sub_Status = SUB_STATUS.DOWN;
                IsResetAlarmWorking = false;
                return;
            }

            if (AlarmManager.CurrentAlarms.Values.Count == 0)
            {
                IsResetAlarmWorking = false;
                return;
            }


            _ = Task.Factory.StartNew(async () =>
             {
                 if (AlarmManager.CurrentAlarms.Values.All(alarm => !alarm.IsRecoverable))
                 {
                     FeedbackTaskStatus(TASK_RUN_STATUS.ACTION_FINISH);
                     Sub_Status = SUB_STATUS.STOP;
                 }
                 else
                 {
                     if (AGVC.currentTaskCmdActionStatus == RosSharp.RosBridgeClient.Actionlib.ActionStatus.ACTIVE)
                     {
                         Sub_Status = SUB_STATUS.RUN;
                     }
                     else
                     {
                         if (Sub_Status != SUB_STATUS.IDLE)
                             Sub_Status = SUB_STATUS.STOP;
                     }
                 }

             });
            IsResetAlarmWorking = false;
            return;
        }

        public virtual async Task<bool> ResetMotor()
        {
            try
            {
                await WagoDO.ResetSaftyRelay();
                Console.WriteLine("Reset Motor Process Start");
                await WagoDO.SetState(DO_ITEM.Horizon_Motor_Stop, true);
                await Task.Delay(200);
                await WagoDO.SetState(DO_ITEM.Horizon_Motor_Reset, true);
                await Task.Delay(200);
                await WagoDO.SetState(DO_ITEM.Horizon_Motor_Reset, false);
                await Task.Delay(200);
                await WagoDO.SetState(DO_ITEM.Horizon_Motor_Stop, false);
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

        /// <summary>
        /// 成功完成移動任務的處理
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="taskData"></param>


        private bool AGVSRemoteModeChangeReq(REMOTE_MODE mode)
        {
            if (mode != Remote_Mode)
            {

                Task reqTask = new Task(async () =>
                {
                    if (OnlineModeChangingFlag)
                    {
                        return;
                    }
                    OnlineModeChangingFlag = true;
                    (bool success, RETURN_CODE return_code) result = await Online_Mode_Switch(mode);
                    if (result.success)
                    {
                        Remote_Mode = mode;
                        Console.WriteLine($"[Online Mode Change] 請求 {mode} 成功!");
                    }
                    else
                    {
                        Console.WriteLine($"[Online Mode Change] 請求 {mode} 失敗!(Return Code = {(int)result.return_code}-{result.return_code}) 現在是 {Remote_Mode}");
                    }
                    OnlineModeChangingFlag = false;
                });
                reqTask.Start();
            }
            return true;
        }

        /// <summary>
        /// 當要求取得RunningStates Data的callback function
        /// </summary>
        /// <param name="getLastPtPoseOfTrajectory"></param>
        /// <returns></returns>
        internal virtual RunningStatus GenRunningStateReportData(bool getLastPtPoseOfTrajectory = false)
        {
            clsCoordination clsCorrdination = new clsCoordination();
            MAIN_STATUS _Main_Status = Main_Status;
            if (getLastPtPoseOfTrajectory)
            {
                var lastPt = ExecutingTask.RunningTaskData.ExecutingTrajecory.Last();
                clsCorrdination.X = lastPt.X;
                clsCorrdination.Y = lastPt.Y;
                clsCorrdination.Theta = lastPt.Theta;
                _Main_Status = MAIN_STATUS.IDLE;
            }
            else
            {
                clsCorrdination.X = Math.Round(Navigation.Data.robotPose.pose.position.x, 3);
                clsCorrdination.Y = Math.Round(Navigation.Data.robotPose.pose.position.y, 3);
                clsCorrdination.Theta = Math.Round(BarcodeReader.Data.theta, 3);
            }
            //gen alarm codes 

            RunningStatus.clsAlarmCode[] alarm_codes = AlarmManager.CurrentAlarms.ToList().FindAll(alarm => alarm.Value.EAlarmCode != AlarmCodes.None).Select(alarm => new RunningStatus.clsAlarmCode
            {
                Alarm_ID = alarm.Value.Code,
                Alarm_Level = alarm.Value.IsRecoverable ? 0 : 1,
                Alarm_Description = alarm.Value.Description,
                Alarm_Category = alarm.Value.IsRecoverable ? 0 : 1,


            }).ToArray();

            try
            {
                double[] batteryLevels = Batteries.Select(battery => (double)battery.Value.Data.batteryLevel).ToArray();
                return SimulationMode ? emulator.Runstatus : new RunningStatus
                {
                    Cargo_Status = HasAnyCargoOnAGV() ? 1 : 0,
                    AGV_Status = _Main_Status,
                    Electric_Volume = batteryLevels,
                    Last_Visited_Node = Navigation.Data.lastVisitedNode.data,
                    Coordination = clsCorrdination,
                    Odometry = Odometry,
                    AGV_Reset_Flag = AGV_Reset_Flag,
                    Alarm_Code = alarm_codes,
                    Escape_Flag = ExecutingTask == null ? false : ExecutingTask.RunningTaskData.Escape_Flag
                };
            }
            catch (Exception ex)
            {
                //LOG.ERROR("GenRunningStateReportData ", ex);
                return new RunningStatus();
            }
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
                Laser.AllLaserActive();
            }
            else
            {
                Laser.AllLaserDisable();
            }
            return true;
        }

        internal bool HasAnyCargoOnAGV()
        {
            try
            {

                return !WagoDI.GetState(clsDIModule.DI_ITEM.Cst_Sensor_1) && !WagoDI.GetState(clsDIModule.DI_ITEM.Cst_Sensor_2);
            }
            catch (Exception)
            {
                return false;
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
