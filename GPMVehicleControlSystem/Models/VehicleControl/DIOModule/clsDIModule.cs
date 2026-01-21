using AGVSystemCommonNet6.Abstracts;
using AGVSystemCommonNet6.Vehicle_Control.VCS_ALARM;
using GPMVehicleControlSystem.Models.VehicleControl.Vehicles;
using GPMVehicleControlSystem.Models.VehicleControl.Vehicles.Params;
using GPMVehicleControlSystem.Tools;
using Modbus.Device;
using NLog;
using System.Net.Sockets;
using static AGVSystemCommonNet6.clsEnums;
using static AGVSystemCommonNet6.DATABASE.DatabaseCaches;
using static GPMVehicleControlSystem.Models.VehicleControl.AGVControl.CarController;
using static GPMVehicleControlSystem.VehicleControl.DIOModule.clsDOModule;
using Vehicle = GPMVehicleControlSystem.Models.VehicleControl.Vehicles.Vehicle;

namespace GPMVehicleControlSystem.VehicleControl.DIOModule
{
    public partial class clsDIModule : Connection
    {
        protected Vehicle vehicle;
        protected IniHelper iniHelper = new IniHelper(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), $"param/IO_Wago.ini"));

        TcpClient client;
        public int IO_Interval_ms { get; }
        protected ModbusIpMaster? master;

        private AGV_TYPE _AgvType = AGV_TYPE.FORK;
        public AGV_TYPE AgvType
        {
            get => _AgvType;
            set
            {
                _AgvType = value;
            }
        }
        public int Version { get; internal set; }

        public ManualResetEvent PauseSignal = new ManualResetEvent(true);

        public event EventHandler OnDisonnected;
        public event EventHandler OnReConnected;
        public override string alarm_locate_in_name => "DI Module";

        public event EventHandler<ROBOT_CONTROL_CMD> OnLaserDIRecovery;
        public event EventHandler OnFarLaserDITrigger;
        public event EventHandler OnNearLaserDiTrigger;


        /// <summary>
        /// EMO觸發
        /// </summary>
        public event EventHandler OnEMO;

        /// <summary>
        /// EMO按鈕按壓觸發
        /// </summary>
        public event EventHandler OnEMOButtonPressed;

        /// <summary>
        /// Bump Sensor觸發
        /// </summary>
        public event EventHandler OnBumpSensorPressed;

        public event EventHandler OnResetButtonPressed;

        public event EventHandler OnFrontSecondObstacleSensorDetected;
        protected bool _Connected = false;
        public virtual bool Connected
        {
            get => _Connected;
            set
            {
                if (_Connected != value)
                {
                    _Connected = value;

                    if (!value)
                        Task.Factory.StartNew(async () =>
                        {
                            logger.Info($"Wago Module Disconect(After Read I/O Timeout 5000ms,Still Disconnected.)");
                            Current_Alarm_Code = AlarmCodes.Wago_IO_Disconnect;
                        });
                    else
                    {
                        OnReConnected?.Invoke(this, null);
                        Current_Alarm_Code = AlarmCodes.None;
                        logger.Info($"Wago Module Reconnected");
                    }
                }
            }
        }

        internal Dictionary<Enum, int> Indexs = new Dictionary<Enum, int>();

        public List<clsIOSignal> VCSInputs = new List<clsIOSignal>();
        public ushort Start { get; set; }
        public ushort Size { get; set; }
        public int ShiftStart = 0;
        public int ShiftSize = 0;

        protected Logger logger = LogManager.GetCurrentClassLogger();
        public clsDIModule()
        {
        }

        public clsDIModule(Vehicle vehicle, string IP, int Port, int IO_Interval_ms = 5)
        {
            this.vehicle = vehicle;
            this.IP = IP;
            this.VMSPort = Port;
            this.IO_Interval_ms = IO_Interval_ms;
            logger = LogManager.GetLogger("DIModule");
            logger.Trace($"Wago IO_ IP={IP},Port={Port},Inputs Read Interval={IO_Interval_ms} ms");

            AgvType = vehicle.Parameters.AgvType;
            Version = vehicle.Parameters.Version;

            ReadIOSettingsFromIniFile();

        }

        virtual public void ReadIOSettingsFromIniFile()
        {
            DI_ITEM di_item;
            var di_names = Enum.GetValues(typeof(DI_ITEM)).Cast<DI_ITEM>().Select(i => i.ToString()).ToList();
            try
            {
                Start = ushort.Parse(iniHelper.GetValue("INPUT", "Start"));
                Size = ushort.Parse(iniHelper.GetValue("INPUT", "Size"));
                for (ushort i = 0; i < Size; i++)
                {
                    var Address = $"X{i.ToString("X4")}";
                    var RigisterName = iniHelper.GetValue("INPUT", Address);
                    var reg = new clsIOSignal(RigisterName, Address);
                    if (RigisterName != "")
                    {
                        if (di_names.Contains(RigisterName))
                        {
                            var do_item = Enum.GetValues(typeof(DI_ITEM)).Cast<DI_ITEM>().FirstOrDefault(di => di.ToString() == RigisterName);
                            if (!Indexs.TryAdd(do_item, i))
                            {
                                throw new Exception("WAGO DI 名稱重複");
                            }
                        }
                        else
                        {

                        }
                    }
                    VCSInputs.Add(reg);
                }
            }
            catch (Exception ex)
            {

            }

        }
        public override async Task<bool> Connect()
        {
            if (IP == null | VMSPort <= 0)
                throw new SocketException((int)SocketError.AddressNotAvailable);
            try
            {
                client = new TcpClient(IP, VMSPort);
                master = ModbusIpMaster.CreateIp(client);
                master.Transport.ReadTimeout = 800;
                master.Transport.WriteTimeout = 300;
                master.Transport.Retries = 5;
                master.Transport.WaitToRetryMilliseconds = 100;
                Current_Warning_Code = AlarmCodes.None;
                logger.Info($"[{this.GetType().Name}]Wago Modbus TCP Connected!");
                Connected = true;
                return true;
            }
            catch (Exception ex)
            {
                Connected = false;
                Current_Alarm_Code = AlarmCodes.Wago_IO_Disconnect;
                OnDisonnected?.Invoke(this, EventArgs.Empty);
                client = null;
                master = null;
                return false;
            }
        }

        public override void Disconnect()
        {
            try
            {
                client?.Close();
                master?.Transport.Dispose();
                master?.Dispose();
            }
            catch (Exception ex)
            {
                logger.Error(ex);
            }
            finally
            {
                client = null;
                master = null;
            }
        }

        public override bool IsConnected()
        {
            return Connected;
        }

        internal void SetState(string address, bool state)
        {
            VCSInputs.FirstOrDefault(k => k.Address == address).State = state;
        }

        public bool GetState(DI_ITEM signal)
        {
            clsIOSignal di = VCSInputs.FirstOrDefault(k => k.Name == signal + "");
            return di == null ? false : di.State;
        }
        public bool[] GetStates(DI_ITEM signal, int number)
        {
            if (Indexs.TryGetValue(signal, out int index))
            {
                if (index < 0 || index + number > VCSInputs.Count)
                {
                    logger.Warn($"GetStates failed for {signal}, index out of range.");
                    return new bool[0];
                }
                return VCSInputs.Skip(index).Take(number).Select(i => i.State).ToArray();
            }
            else
            {
                logger.Warn($"{signal} not defined in INPUT map.");
            }
            return new bool[0];
        }


        //監視某個輸入的變化事件??

        public virtual bool SubsSignalStateChange(Enum signal, EventHandler<bool> handler)
        {
            try
            {
                if (!Indexs.TryGetValue(signal, out int index))
                {
                    logger.Warn($"{signal} 註冊事件失敗因為未定義該訊號的INPUT位置");
                    return false;
                }
                clsIOSignal inputSignal = VCSInputs[Indexs[signal]];
                inputSignal.AddEvent(handler);
                return true;

            }
            catch (Exception ex)
            {
                logger.Error("Digital Input - [" + signal + "] Sbuscribe Error.", ex);
                return false;
            }
        }

        internal virtual void RegistSignalEvents(out string _notifyText)
        {
            _notifyText = "";

            VCSInputs[Indexs[DI_ITEM.Panel_Reset_PB]].OnSignalON -= HandlePanelResetPBStatusON;
            VCSInputs[Indexs[DI_ITEM.Panel_Reset_PB]].OnSignalON += HandlePanelResetPBStatusON;

            if (AgvType != AGV_TYPE.INSPECTION_AGV)
            {
                VCSInputs[Indexs[DI_ITEM.EMO]].OnStateChanged -= HandleEMOBStatusChanged;
                VCSInputs[Indexs[DI_ITEM.EMO]].OnStateChanged += HandleEMOBStatusChanged;
                VCSInputs[Indexs[DI_ITEM.Bumper_Sensor]].OnStateChanged -= HandleBumpSensorChanged;
                VCSInputs[Indexs[DI_ITEM.Bumper_Sensor]].OnStateChanged += HandleBumpSensorChanged;
            }
            else
            {
                VCSInputs[Indexs[DI_ITEM.Bumper_Sensor]].OnSignalON -= HandleBumperSensorOnOfInspectionAGV;
                VCSInputs[Indexs[DI_ITEM.Bumper_Sensor]].OnSignalON += HandleBumperSensorOnOfInspectionAGV;
                VCSInputs[Indexs[DI_ITEM.EMO_Button]].OnSignalON -= HandleEMOButtonStatusOnOfInspectionAGV;
                VCSInputs[Indexs[DI_ITEM.EMO_Button]].OnSignalON += HandleEMOButtonStatusOnOfInspectionAGV;

                if (Version == 2)
                {
                    VCSInputs[Indexs[DI_ITEM.EMO_Button_2]].OnSignalON -= HandleEMOButton2OfInspectionAGV;
                    VCSInputs[Indexs[DI_ITEM.EMO_Button_2]].OnSignalON += HandleEMOButton2OfInspectionAGV;
                }
            }


            bool isIODefined = Indexs.TryGetValue(DI_ITEM.FrontProtection_Obstacle_Sensor, out int val);
            if (isIODefined)
            {
                VCSInputs[val].OnSignalOFF -= HandleFrontProtection_ObstacleSensorOFF;
                VCSInputs[val].OnSignalOFF += HandleFrontProtection_ObstacleSensorOFF;
            }
            else if (this.AgvType == AGV_TYPE.SUBMERGED_SHIELD)
                _notifyText += $"FrontProtection_Obstacle_Sensor 未定義;";

            isIODefined = Indexs.TryGetValue(DI_ITEM.Fork_Frontend_Abstacle_Sensor, out val);
            if (isIODefined)
            {
                VCSInputs[val].OnSignalOFF -= HandleForkFrontendAbstacleOFF;
                VCSInputs[val].OnSignalOFF += HandleForkFrontendAbstacleOFF;
            }
            else if (this.AgvType == AGV_TYPE.FORK || this.AgvType == AGV_TYPE.FORK_XL)
                _notifyText += $"Fork_Frontend_Abstacle_Sensor 未定義;";


        }

        private void HandleForkFrontendAbstacleOFF(object? sender, EventArgs e)
        {
            OnFrontSecondObstacleSensorDetected?.Invoke(sender, e);
        }

        private void HandleFrontProtection_ObstacleSensorOFF(object? sender, EventArgs e)
        {
            OnFrontSecondObstacleSensorDetected?.Invoke(sender, e);
        }

        private void HandleEMOButton2OfInspectionAGV(object? sender, EventArgs e)
        {
            OnEMOButtonPressed?.Invoke(sender, e);
        }

        private void HandleEMOButtonStatusOnOfInspectionAGV(object? sender, EventArgs e)
        {
            OnEMOButtonPressed?.Invoke(sender, e);
        }

        private void HandleBumperSensorOnOfInspectionAGV(object? sender, EventArgs e)
        {
            OnBumpSensorPressed?.Invoke(sender, e);
        }

        private void HandlePanelResetPBStatusON(object? sender, EventArgs e)
        {
            OnResetButtonPressed?.Invoke(sender, EventArgs.Empty);
        }

        private void HandleEMOBStatusChanged(object? sender, bool e)
        {
            IO_CONTACT_TYPE emoContactType = vehicle.Parameters.GetContactType(DI_ITEM.EMO);
            if (emoContactType == IO_CONTACT_TYPE.A && e || emoContactType == IO_CONTACT_TYPE.B && !e)
                OnEMO?.Invoke(sender, EventArgs.Empty);
        }

        private void HandleBumpSensorChanged(object? sender, bool e)
        {
            IO_CONTACT_TYPE bumperSensorContactType = vehicle.Parameters.GetContactType(DI_ITEM.Bumper_Sensor);
            if (bumperSensorContactType == IO_CONTACT_TYPE.A && e || bumperSensorContactType == IO_CONTACT_TYPE.B && !e)
                OnBumpSensorPressed?.Invoke(sender, EventArgs.Empty);
        }

        private bool IsTimeout = false;


        private DateTime lastReadTime = DateTime.MaxValue;
        private async void ConnectionWatchDog()
        {
            Thread thread = new Thread(() =>
            {
                while (true)
                {
                    Thread.Sleep(10);
                    var period = (DateTime.Now - lastReadTime).TotalMilliseconds;
                    if (period > 5000)
                    {
                        logger.Fatal($"Wago Module Read Timeout!! ({period} ms) ");
                        lastReadTime = DateTime.Now;
                        Disconnect();
                        Connected = false;
                    }
                }

            });
            thread.Start();
        }
        public virtual async void StartAsync()
        {
            ConnectionWatchDog();
            int error_cnt = 0;
            await Task.Delay(100);
            Task.Run(async () =>
            {
                while (true)
                {
                    await Task.Delay(IO_Interval_ms);
                    if (!Connected)
                    {
                        OnDisonnected?.Invoke(this, EventArgs.Empty);
                        await Connect();
                        await Task.Delay(1000); // 使用 Task.Delay 而不是 Thread.Sleep
                        continue;
                    }

                    try
                    {
                        bool[]? input = master?.ReadInputs(1, Start, Size);
                        if (input == null)
                        {
                            logger.Fatal("DI Read inputs but null return, disconnect connection.");
                            Disconnect();
                            Connected = false;
                            continue;
                        }

                        for (int i = 0; i < input.Length; i++)
                        {
                            VCSInputs[i].State = input[i];
                        }

                        lastReadTime = DateTime.Now;
                        error_cnt = 0;
                    }
                    catch (Exception ex)
                    {
                        logger.Error($"Wago IO Read Exception...{ex.Message}");
                        error_cnt++;

                        if (error_cnt >= 2)
                        {
                            Disconnect();
                            Connected = false;
                        }
                    }
                }

            });
        }

        internal virtual (bool confirm, string message) UpdateSignalMap(Dictionary<int, string> newInputMap)
        {
            var newCoolection = newInputMap.Select(x => new clsIOSignal(x.Value, $"X{x.Key.ToString("X4")}")
            {
                index = (ushort)x.Key
            }).ToList();
            VCSInputs = newCoolection;
            UpdateIniFile();
            return (true, "");
        }

        protected virtual bool UpdateIniFile()
        {
            for (int i = 0; i < VCSInputs.Count; i++)
            {
                var input = VCSInputs[i];
                if (input.Name == "")
                    iniHelper.RemoveKey("INPUT", input.Address, out string errMsg);
                else
                    iniHelper.SetValue("INPUT", input.Address, input.Name, out string errMsg);
            }
            return true;
        }
    }
}
