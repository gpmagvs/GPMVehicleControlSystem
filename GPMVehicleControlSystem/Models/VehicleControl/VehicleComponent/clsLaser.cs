using AGVSystemCommonNet6;
using AGVSystemCommonNet6.AGVDispatch.Messages;
using AGVSystemCommonNet6.GPMRosMessageNet.Messages;
using AGVSystemCommonNet6.GPMRosMessageNet.SickSafetyscanners;
using AGVSystemCommonNet6.Vehicle_Control.VCS_ALARM;
using GPMVehicleControlSystem.Models.VehicleControl.DIOModule;
using GPMVehicleControlSystem.Tools;
using GPMVehicleControlSystem.VehicleControl.DIOModule;
using NLog;
using RosSharp.RosBridgeClient;
using static GPMVehicleControlSystem.VehicleControl.DIOModule.clsDOModule;

namespace GPMVehicleControlSystem.Models.VehicleControl.VehicleComponent
{
    public class clsLaser : IDIOUsagable, IRosSocket
    {
        public enum LASER_MODE
        {
            Unknow = 444,
            Bypass = 0,
            Normal = 1,
            Secondary = 2,
            Move_Short = 3,
            Unknow_4 = 4,
            Turning = 5,
            Unknow_6 = 6,
            Loading = 7,
            Unknow_8 = 8,
            Unknow_9 = 9,
            Special = 10,
            Unknow_11 = 11,
            Narrow = 12,
            Narrow_Long = 13,
            Unknow_14 = 14,
            Unknow_15 = 15,
            Bypass16 = 16,
        }

        /// <summary>
        /// AGVS站點雷射預設定植
        /// </summary>
        public enum AGVS_LASER_SETTING_ORDER
        {
            BYPASS,
            NORMAL,
        }

        public class LaserModeSwitchCheckArgs : EventArgs
        {
            public bool accept;
            public string message;
        }

        public GeneralSystemStateMsg SickSsystemState { get; set; } = new GeneralSystemStateMsg();
        public LASER_MODE Spin_Laser_Mode = LASER_MODE.Turning;
        protected SemaphoreSlim modeSwitchSemaphoresSlim = new SemaphoreSlim(1, 1);
        private int _CurrentLaserModeOfSick = -1;

        private DateTime lastDiagnosicsMsgReceiveTime = DateTime.MinValue;
        private DiagnosticArray diagnosticArray = new DiagnosticArray();

        public event EventHandler OnSickApplicationError;
        public event EventHandler<LaserModeSwitchCheckArgs> BeforeLaserModeBypassSwitch;
        public event EventHandler<LASER_MODE> OnLaserModeChanged;

        private bool _IsSickApplicationError = false;
        public bool IsSickApplicationError
        {
            get => _IsSickApplicationError;
            private set
            {
                if (_IsSickApplicationError != value)
                {
                    _IsSickApplicationError = value;
                    if (value)
                    {
                        OnSickApplicationError?.Invoke(this, EventArgs.Empty);
                    }
                }
            }
        }

        internal int CurrentLaserModeOfSick
        {
            get => _CurrentLaserModeOfSick;
            set
            {
                if (_CurrentLaserModeOfSick != value)
                {
                    _CurrentLaserModeOfSick = value;
                    logger.Info($"[From Sick Topic] Laser Mode Chaged To : {value}({GetLaserModeEnum(value)})", true);
                }
            }
        }
        public delegate void LsrModeSwitchDelegate(int mode);
        public LsrModeSwitchDelegate OnLsrModeSwitchRequest;
        public delegate (bool leftBypass, bool rightBypass) SideLaserBypassSettingDelagete();
        /// <summary>
        /// 回傳值為true時,強制設定左右雷射Bypass
        /// </summary>
        public SideLaserBypassSettingDelagete OnSideLaserBypassSetting;


        public clsDOModule DOModule { get; set; }
        public clsDIModule DIModule { get; set; }


        LinuxTools.ROS_VERSION rosVersion = LinuxTools.ROS_VERSION.ROS1;

        protected Logger logger;

        private Dictionary<int, int> _agvsTrajectoryLaserSettingStore = new Dictionary<int, int>();

        public Dictionary<int, int> agvsTrajectoryLaserSettingStore
        {
            get => _agvsTrajectoryLaserSettingStore;
            set
            {
                _agvsTrajectoryLaserSettingStore = value;
                logger.Info($"更新雷射組數 Map: {string.Join(", ", value.Select(kvp => $"Tag: {kvp.Key}, 雷射設定: {kvp.Value}"))}");
            }
        }

        public clsLaser(clsDOModule DOModule, clsDIModule DIModule, LinuxTools.ROS_VERSION rosVersion = LinuxTools.ROS_VERSION.ROS1)
        {
            logger = LogManager.GetCurrentClassLogger();
            this.DOModule = DOModule;
            this.DIModule = DIModule;
            this.rosVersion = rosVersion;
        }
        /// <summary>
        /// 側邊雷射是否可切段數
        /// </summary>
        internal bool IsSideLaserModeChangable = false;
        /// <summary>
        /// 前後雷射是否共用IO
        /// </summary>
        internal bool IsFrontBackLaserIOShare = false;

        internal (bool, string, bool, string) IsSideLaserAbnormal
        {
            get
            {
                if (!IsSideLaserModeChangable)
                    return (false, "", false, "");
                bool rightSideAbn = !DIModule.GetState(clsDIModule.DI_ITEM.RightProtection_Area_Sensor_4);
                string right_msg = rightSideAbn ? "Right Side Laser Abnormal" : "";
                bool leftSideAbn = !DIModule.GetState(clsDIModule.DI_ITEM.LeftProtection_Area_Sensor_4);
                string left_msg = leftSideAbn ? "Left Side Laser Abnormal" : "";
                return (rightSideAbn, right_msg, leftSideAbn, left_msg);
            }
        }

        private LASER_MODE _currentMode = LASER_MODE.Bypass;
        public LASER_MODE currentMode
        {
            get => _currentMode;
            private set
            {
                if (_currentMode != value)
                {
                    _currentMode = value;
                    OnLaserModeChanged?.Invoke(this, _currentMode);
                }
            }
        }

        public void UpdateAGVSTrajectoryLaser(clsTaskDownloadData orderData)
        {
            lock (agvsTrajectoryLaserSettingStore)
            {
                agvsTrajectoryLaserSettingStore = orderData.ExecutingTrajecory.ToDictionary(pt => pt.Point_ID, pt => pt.Laser);
            }
        }
        public virtual LASER_MODE LaserModeOfDigitalOutput
        {
            get
            {
                try
                {
                    //從目前輸出給 wago 的 output 轉換成 LASER_MODE
                    DO_ITEM lsrOutputStartDO = IsFrontBackLaserIOShare ? DO_ITEM.FrontBack_Protection_Sensor_IN_1 : DO_ITEM.Front_Protection_Sensor_IN_1;

                    bool[] bools = DOModule.GetStates(lsrOutputStartDO, 8);
                    //取出奇數 index
                    //bool[] bits = new bool[4] { bools[0], bools[2], bools[4], bools[6] };
                    bool[] bits = new bool[4] { bools[6], bools[4], bools[2], bools[0] };
                    int currentModeInt = 0;
                    for (int i = 0; i < bits.Length; i++)
                    {
                        if (bits[i])
                        {
                            currentModeInt |= (1 << i); // i 是 bit 的位置，從最低位開始
                        }
                    }

                    return Enum.GetValues(typeof(LASER_MODE)).Cast<LASER_MODE>().FirstOrDefault(mo => (int)mo == currentModeInt);
                }
                catch (Exception)
                {
                    return LASER_MODE.Unknow;
                }
            }
        }

        private RosSocket _rosSocket = null;
        private string _output_paths_subscribe_id;
        private string _diagnostic_topic_id;

        public RosSocket rosSocket
        {
            get => _rosSocket;
            set
            {
                try
                {
                    UnSubscribeSickSaftySacnnerOutputPathsTopic();
                }
                catch (Exception ex)
                {
                    logger.Error(ex, ex.Message);
                }
                _rosSocket = value;
                SubscribeSickSaftyScannerOuputPathsTopic();
            }
        }
        internal string SubscribeSickSaftyScannerOuputPathsTopic()
        {
            string topic = rosVersion == LinuxTools.ROS_VERSION.ROS1 ? "/sick_safetyscanners/output_paths" : "/sick_safetyscanners2/output_paths";

            var _output_paths_subscribe_id = _rosSocket.Subscribe<OutputPathsMsg>(topic, SickSaftyScannerOutputDataCallback);
            logger.Trace($"Subscribe {topic}. topic id= ({_output_paths_subscribe_id})");
            this._output_paths_subscribe_id = _output_paths_subscribe_id;
            return this._output_paths_subscribe_id;
        }
        internal string SubscribeDiagnosticsTopic()
        {
            var _diagnostic_topic_id = _rosSocket.Subscribe<DiagnosticArray>("/diagnostics", DiagnosticsMsgCallBack);
            logger.Trace($"Subscribe /diagnostics ({_diagnostic_topic_id})");
            this._diagnostic_topic_id = _diagnostic_topic_id;
            return this._diagnostic_topic_id;
        }

        internal void UnSubscribeSickSaftySacnnerOutputPathsTopic()
        {
            if (_output_paths_subscribe_id != null)
                _rosSocket?.Unsubscribe(_output_paths_subscribe_id);
        }
        internal void UnSubscribeDiagnosticsTopic()
        {
            if (_diagnostic_topic_id != null)
                _rosSocket?.Unsubscribe(_diagnostic_topic_id);
        }

        internal void ResetSickApplicationError()
        {
            this.IsSickApplicationError = false;
        }
        private void DiagnosticsMsgCallBack(DiagnosticArray value)
        {
            List<DiagnosticStatus> sickStates = value.status.Where(status => status.name == "sick_safetyscanners/sick_safetyscanners: State").ToList();


            if ((DateTime.Now - lastDiagnosicsMsgReceiveTime).TotalSeconds > 1)
            {
                //logger.Trace($"Sick Diagnostic:\r\n{value.ToJson()}");
                lastDiagnosicsMsgReceiveTime = DateTime.Now;
                diagnosticArray = value;

                if (diagnosticArray.status.Any())
                {
                    TrGetApplicationStatus(diagnosticArray.status, out bool _IsSickApplicationError);
                    if (_IsSickApplicationError && !IsSickApplicationError)
                        IsSickApplicationError = _IsSickApplicationError;
                }
            }
        }

        private void TrGetApplicationStatus(DiagnosticStatus[] status, out bool isError)
        {
            isError = false;
            var allvalues = status.SelectMany(s => s.values).ToList();
            List<KeyValue> laserApplicationErrors = allvalues.Where(keypair => keypair.key == "Application error").ToList();

            if (!laserApplicationErrors.Any())
                return;
            isError = laserApplicationErrors.Any(kp => kp.value.ToLower().StartsWith("true"));
        }

        private DateTime lastSickOutputPathDataUpdateTime = DateTime.MinValue;
        private bool isSickOutputPathDataNotUpdate
        {
            get
            {
                double period = (DateTime.Now - lastSickOutputPathDataUpdateTime).TotalSeconds;
                logger.Trace($"{period},lastSickOutputPathDataUpdateTime:{lastSickOutputPathDataUpdateTime.ToString("yyyy-MM-dd HH:mm:ss.ffff")}");
                return period > 1;
            }
        }
        private void SickSaftyScannerOutputDataCallback(OutputPathsMsg sick_scanner_out_data)
        {
            CurrentLaserModeOfSick = sick_scanner_out_data.active_monitoring_case;
            lastSickOutputPathDataUpdateTime = DateTime.Now;
        }

        internal async Task FrontBackLasersEnable(bool front_active, bool back_active)
        {
            await DOModule.SetState(DO_ITEM.Front_LsrBypass, !front_active);
            await DOModule.SetState(DO_ITEM.Back_LsrBypass, !back_active);
        }
        /// <summary>
        /// 前後雷射Bypass關閉
        /// </summary>
        /// <exception cref="NotImplementedException"></exception>
        internal async Task FrontBackLasersEnable(bool active)
        {
            await DOModule.SetState(DO_ITEM.Front_LsrBypass, !active);
            await DOModule.SetState(DO_ITEM.Back_LsrBypass, !active);
        }
        /// <summary>
        /// 側邊雷射Bypass設定
        /// </summary>
        /// <exception cref="NotImplementedException"></exception>
        internal async Task SideLasersEnable(bool active)
        {
            bool _leftactive = active;
            bool _rightactive = active;
            if (active && OnSideLaserBypassSetting != null)
            {
                (bool leftforcingBypass, bool rightforcingBypass) = OnSideLaserBypassSetting.Invoke();
                _leftactive = leftforcingBypass ? false : active;
                _rightactive = rightforcingBypass ? false : active;
            }


            DOWriteRequest request = new DOWriteRequest(new List<DOModifyWrapper>()
                    {
                        new DOModifyWrapper(DO_ITEM.Right_LsrBypass.GetIOSignalOfModule(), !_rightactive),
                        new DOModifyWrapper(DO_ITEM.Left_LsrBypass.GetIOSignalOfModule(),  !_leftactive),
                    });
            await DOModule.SetState(request);
        }
        /// <summary>
        /// 前後左右雷射Bypass全部關閉
        /// </summary>
        /// <exception cref="NotImplementedException"></exception>
        internal async Task AllLaserActive()
        {
            await SideLasersEnable(true);
            await FrontBackLasersEnable(true);
        }


        /// <summary>
        /// 前後左右雷射Bypass全部開啟
        /// </summary>
        /// <exception cref="NotImplementedException"></exception>
        internal async Task AllLaserDisable()
        {
            DOWriteRequest request = new DOWriteRequest(new List<DOModifyWrapper>()
                    {
                        new DOModifyWrapper(DO_ITEM.Front_LsrBypass.GetIOSignalOfModule(), true),
                        new DOModifyWrapper(DO_ITEM.Back_LsrBypass.GetIOSignalOfModule(),  true),
                        new DOModifyWrapper(DO_ITEM.Right_LsrBypass.GetIOSignalOfModule(),  true),
                        new DOModifyWrapper(DO_ITEM.Left_LsrBypass.GetIOSignalOfModule(),  true),
                    });
            await DOModule.SetState(request);
        }

        public clsNavigation.AGV_DIRECTION agvDirection { get; internal set; } = clsNavigation.AGV_DIRECTION.FORWARD;
        internal virtual async void LaserChangeByAGVDirection(int lastVisitTag, clsNavigation.AGV_DIRECTION direction)
        {
            if (direction == clsNavigation.AGV_DIRECTION.BYPASS)
            {
                logger.Info($"雷射設定組 =Bypass , AGVC Direction 11", true);
                //await FrontBackLasersEnable(false);
                await ModeSwitch(LASER_MODE.Bypass);

                return;
            }
            if (direction == clsNavigation.AGV_DIRECTION.FORWARD)
            {
                await FrontBackLasersEnable(true);
                (bool succeess, int currentModeInt) = await ModeSwitchAGVSMapSettingOfCurrentTag(lastVisitTag);
                if (succeess)
                    logger.Info($"AGV走行方向為前進，雷射設定組切換回任務軌跡設定值 = {currentModeInt}", true);
                else
                    logger.Warn($"AGV走行方向為前進，但無法得知目前 TAG 的雷射設定組數，因此將雷射設定組切換為, {currentModeInt}", true);

            }
            else // 左.右轉
            {
                await FrontBackLasersEnable(true);
                await ModeSwitch(Spin_Laser_Mode);
                logger.Warn($"AGVC Direction = {direction}, Laser Mode Changed to {Spin_Laser_Mode}");
            }
        }
        public virtual async Task<bool> ModeSwitch(LASER_MODE mode, bool isSettingByResetButtonLongPressed = false)
        {
            return await ModeSwitch((int)mode, isSettingByResetButtonLongPressed);
        }

        public virtual async Task<(bool succeess, int currentModeInt)> ModeSwitchAGVSMapSettingOfCurrentTag(int tag)
        {
            if (agvsTrajectoryLaserSettingStore.TryGetValue(tag, out int _laserModeInt))
            {
                logger.Info($"依AGVS圖資設定雷射組數,目前位置Tag:{tag},組數 {_laserModeInt}");
                bool _sucess = await ModeSwitch(_laserModeInt);
                return (_sucess, _laserModeInt);
            }
            else
            {
                logger.Warn($"依AGVS圖資設定雷射組數,目前位置Tag:{tag}. 找不到 Tag {tag} 需要哪一個組數，切換為第 1 組");
                bool _sucess = await ModeSwitch(1);
                return (_sucess, 1);
            }
        }

        public async Task<bool> ModeSwitch(int mode_int, bool isSettingByResetButtonLongPressed = false)
        {
            try
            {
                await modeSwitchSemaphoresSlim.WaitAsync();


                if (mode_int == 0 || mode_int == 16)
                {
                    LaserModeSwitchCheckArgs checkArgs = new LaserModeSwitchCheckArgs()
                    {
                        accept = true,
                        message = ""
                    };

                    BeforeLaserModeBypassSwitch?.Invoke(this, checkArgs);
                    if (!checkArgs.accept)
                    {
                        logger.Warn($"嘗試將雷射組數切換為 Bypass 組數已遭自檢流程拒絕:{checkArgs.message}");
                        return false;
                    }
                }

                LASER_MODE requestMode = GetLaserModeEnum(mode_int);

                if (!isSettingByResetButtonLongPressed && (agvDirection == clsNavigation.AGV_DIRECTION.RIGHT || agvDirection == clsNavigation.AGV_DIRECTION.LEFT))
                {
                    bool _isReuestNeedReject = requestMode != LASER_MODE.Turning && requestMode != LASER_MODE.Bypass;
                    if (_isReuestNeedReject)
                    {

                        logger.Warn($"AGV旋轉中,雷射切換為 =>{requestMode} 請求已被Bypass");
                        return true;
                    }
                }
                int retry_times_limit = 300;
                int try_count = 0;
                bool[] writeBools = mode_int.ToLaserDOSettingBits();
                bool[] writeBools_SideLaser = IsSideLaserModeChangable ? mode_int.ToSideLaserDOSettingBits() : new bool[0];
                while (true)
                {
                    logger.Info($"Try Laser Output Setting  as {requestMode} --(Try-{try_count})");
                    await Task.Delay(10);
                    bool isNeedChange = IsLaserModeNeedChange(mode_int) || currentMode != requestMode;
                    if (!isNeedChange)
                    {
                        logger.Info($"Laser Mode Now is Already Setting  as {requestMode} -- Not Need Modify Digital Ouput");
                        break;
                    }
                    if (try_count > retry_times_limit)
                        return false;
                    bool writeSuccess = false;

                    if (!IsFrontBackLaserIOShare)
                        writeSuccess = await DOModule.SetState(DO_ITEM.Front_Protection_Sensor_IN_1, writeBools);
                    else
                        writeSuccess = await DOModule.SetState(DO_ITEM.FrontBack_Protection_Sensor_IN_1, writeBools.Take(8).ToArray());

                    bool sideLaserWriteSuccess = false;
                    if (IsSideLaserModeChangable)
                        sideLaserWriteSuccess = await DOModule.SetState(DO_ITEM.Side_Protection_Sensor_IN_1, writeBools_SideLaser);
                    else
                        sideLaserWriteSuccess = true;

                    if (writeSuccess && sideLaserWriteSuccess)
                    {
                        break;
                    }

                    try_count++;
                }
                if (isSickOutputPathDataNotUpdate)
                    logger.Warn("AlarmCodes.Laser_Mode_Switch_But_SICK_OUPUT_NOT_UPDATE");
                logger.Info($"[雷射組數設定結果] Laser Output Setting as {requestMode}({mode_int}) Success.({try_count})");
                currentMode = requestMode;
                return true;
            }
            catch (Exception ex)
            {
                logger.Error(ex);
                return false;
            }
            finally
            {
                modeSwitchSemaphoresSlim.Release();
            }

        }

        private LASER_MODE GetLaserModeEnum(int mode_int)
        {
            if (Enum.IsDefined(typeof(LASER_MODE), mode_int))
            {
                return (LASER_MODE)mode_int;
            }
            else
            {
                return LASER_MODE.Unknow;
            }
        }

        private bool IsLaserModeNeedChange(int toSetMode)
        {
            var currentModeOfIO = LaserModeOfDigitalOutput;
            if (toSetMode == 0 || toSetMode == 16)
            {
                return currentModeOfIO != LASER_MODE.Bypass && currentModeOfIO != LASER_MODE.Bypass16;
            }
            else
            {
                return toSetMode != (int)currentModeOfIO;
            }
        }
    }
}
