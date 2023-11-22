using AGVSystemCommonNet6.Abstracts;
using AGVSystemCommonNet6.Alarm.VMS_ALARM;
using AGVSystemCommonNet6.Log;
using GPMVehicleControlSystem.Tools;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Modbus.Device;
using System.Diagnostics;
using System.Net.Sockets;
using System.Security.AccessControl;
using static AGVSystemCommonNet6.clsEnums;
using static GPMVehicleControlSystem.Models.VehicleControl.AGVControl.CarController;
using static GPMVehicleControlSystem.VehicleControl.DIOModule.clsDOModule;

namespace GPMVehicleControlSystem.VehicleControl.DIOModule
{
    public partial class clsDIModule : Connection
    {
        TcpClient client;
        protected ModbusIpMaster? master;

        private AGV_TYPE _AgvType = AGV_TYPE.UNKNOWN;
        public AGV_TYPE AgvType
        {
            get => _AgvType;
            set
            {
                _AgvType = value;
                RegistSignalEvents();
            }
        }
        public ManualResetEvent PauseSignal = new ManualResetEvent(true);

        bool isFrontLaserA1Trigger => !GetState(DI_ITEM.FrontProtection_Area_Sensor_1);
        bool isFrontLaserA2Trigger => !GetState(DI_ITEM.FrontProtection_Area_Sensor_2);
        bool isFrontLaserA3Trigger => !GetState(DI_ITEM.FrontProtection_Area_Sensor_3);
        bool isFrontLaserA4Trigger => !GetState(DI_ITEM.FrontProtection_Area_Sensor_4);

        bool isBackLaserA1Trigger => !GetState(DI_ITEM.BackProtection_Area_Sensor_1);
        bool isBackLaserA2Trigger => !GetState(DI_ITEM.BackProtection_Area_Sensor_2);
        bool isBackLaserA3Trigger => !GetState(DI_ITEM.BackProtection_Area_Sensor_3);
        bool isBackLaserA4Trigger => !GetState(DI_ITEM.BackProtection_Area_Sensor_4);

        bool isRightLaserTrigger => !GetState(DI_ITEM.RightProtection_Area_Sensor_3);
        bool isLeftLaserTrigger => !GetState(DI_ITEM.LeftProtection_Area_Sensor_3);


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
        protected bool IOBusy = false;
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
                            LOG.INFO($"Wago Module Disconect(After Read I/O Timeout 5000ms,Still Disconnected.)");
                            AlarmManager.AddAlarm(AlarmCodes.Wago_IO_Disconnect, false);
                        });
                    else
                    {
                        OnReConnected?.Invoke(this, null);
                        AlarmManager.ClearAlarm(AlarmCodes.Wago_IO_Disconnect);
                        LOG.INFO($"Wago Module Reconnected");
                    }
                }
            }
        }


        public Action OnResetButtonPressing { get; set; }

        public Action OnHS_EQ_READY { get; internal set; }

        internal Dictionary<Enum, int> Indexs = new Dictionary<Enum, int>();

        public List<clsIOSignal> VCSInputs = new List<clsIOSignal>();
        public ushort Start { get; set; }
        public ushort Size { get; set; }

        public clsDIModule()
        {
        }
        public clsDIModule(string IP, int Port, int IO_Interval_ms = 5)
        {
            this.IP = IP;
            this.VMSPort = Port;
            this.IO_Interval_ms = IO_Interval_ms;
            ReadIOSettingsFromIniFile();
        }
        public clsDIModule(string IP, int Port, clsDOModule DoModuleRef, int IO_Interval_ms = 5)
        {
            this.IP = IP;
            this.VMSPort = Port;
            this.DoModuleRef = DoModuleRef;
            this.IO_Interval_ms = IO_Interval_ms;
            LOG.TRACE($"Wago IO_ IP={IP},Port={Port},Inputs Read Interval={IO_Interval_ms} ms");
            ReadIOSettingsFromIniFile();
        }

        virtual public void ReadIOSettingsFromIniFile()
        {
            DI_ITEM di_item;
            IniHelper iniHelper = new IniHelper(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), $"param/IO_Wago.ini"));
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
            if (Connected)
                return true;
            try
            {
                client = new TcpClient(IP, VMSPort);
                master = ModbusIpMaster.CreateIp(client);
                master.Transport.ReadTimeout = 500;
                master.Transport.WriteTimeout = 5000;
                master.Transport.Retries = 2;
                Current_Warning_Code = AlarmCodes.None;
                LOG.INFO($"[{this.GetType().Name}]Wago Modbus TCP Connected!");
                Connected = true;
                DoModuleRef.Connected = true;
                return true;
            }
            catch (Exception ex)
            {
                Connected = false;
                DoModuleRef.Connected = false;
                Current_Warning_Code = AlarmCodes.Wago_IO_Disconnect;
                LOG.Critical($"[{this.GetType().Name}]Wago Modbus TCP  Connect FAIL", ex);
                OnDisonnected?.Invoke(this, EventArgs.Empty);
                client = null;
                master = null;
                return false;
            }
        }

        public override void Disconnect()
        {
            Current_Warning_Code = AlarmCodes.Wago_IO_Disconnect;
            try
            {
                client?.Close();
                master?.Transport.Dispose();
                master?.Dispose();
            }
            catch (Exception ex)
            {
                LOG.ERROR(ex);
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


        //監視某個輸入的變化事件??

        public virtual void SubsSignalStateChange(Enum signal, EventHandler<bool> handler)
        {
            try
            {
                VCSInputs[Indexs[signal]].OnStateChanged += handler;
            }
            catch (Exception ex)
            {
                LOG.ERROR("DO-" + signal + "Sbuscribe Error.", ex);
            }
        }

        public virtual void UnRegistSignalStateChange(Enum signal, EventHandler<bool> handler)
        {
            try
            {
                VCSInputs[Indexs[signal]].OnStateChanged -= handler;
            }
            catch (Exception ex)
            {
                LOG.ERROR("DO-" + signal + "Sbuscribe Error.", ex);
            }
        }

        protected virtual void RegistSignalEvents()
        {
            VCSInputs[Indexs[DI_ITEM.EMO]].OnSignalOFF += (s, e) => OnEMO?.Invoke(s, e);

            VCSInputs[Indexs[DI_ITEM.Panel_Reset_PB]].OnSignalON += (s, e) => OnResetButtonPressed?.Invoke(s, e);

            if (AgvType != AGV_TYPE.INSPECTION_AGV)
                VCSInputs[Indexs[DI_ITEM.Bumper_Sensor]].OnSignalOFF += (s, e) => OnBumpSensorPressed?.Invoke(s, e);
            else
            {
                VCSInputs[Indexs[DI_ITEM.Bumper_Sensor]].OnSignalON += (s, e) => OnBumpSensorPressed?.Invoke(s, e);
                VCSInputs[Indexs[DI_ITEM.EMO_Button]].OnSignalON += (s, e) => OnEMOButtonPressed?.Invoke(s, e);
            }

            if (AgvType == AGV_TYPE.SUBMERGED_SHIELD)
            {
                VCSInputs[Indexs[DI_ITEM.FrontProtection_Obstacle_Sensor]].OnSignalON += (s, e) => OnFrontSecondObstacleSensorDetected?.Invoke(s, e);
            }
            else if (AgvType == AGV_TYPE.FORK)
            {
                VCSInputs[Indexs[DI_ITEM.Fork_Frontend_Abstacle_Sensor]].OnSignalOFF += (s, e) => OnFrontSecondObstacleSensorDetected?.Invoke(s, e);
            }
        }

        private bool IsTimeout = false;


        private DateTime lastReadTime = DateTime.MaxValue;
        private async void ConnectionWatchDog()
        {
            _ = Task.Factory.StartNew(async () =>
            {
                while (true)
                {
                    await Task.Delay(10);
                    var period = (DateTime.Now - lastReadTime).TotalMilliseconds;
                    if (period > 5000)
                    {
                        LOG.Critical($"Wago Module Read Timeout!! ({period} ms) ");
                        lastReadTime = DateTime.Now;
                        Disconnect();
                        Connected = false;
                        DoModuleRef.Connected = false;
                    }
                }
            });
        }
        public virtual async void StartAsync()
        {
            ConnectionWatchDog();
            await Task.Run(async () =>
            {
                while (true)
                {
                    Thread.Sleep(IO_Interval_ms);
                    if (!Connected)
                    {
                        OnDisonnected?.Invoke(this, EventArgs.Empty);
                        Thread.Sleep(1000);
                        await Connect();
                        continue;
                    }
                    try
                    {
                        CancellationTokenSource cts = new CancellationTokenSource(3000);
                        while (DoModuleRef.IOBusy)
                        {
                            Thread.Sleep(1);
                            if (cts.IsCancellationRequested)
                            {
                                cts.Dispose();
                                continue;
                            }
                        }
                        cts.Dispose();
                        bool[]? input = master?.ReadInputs(1, Start, Size);
                        if (input == null)
                        {
                            LOG.Critical($"DI Read inputs but null return, disconnect connection.");
                            Disconnect();
                            Connected= false;
                            continue;
                        }

                        for (int i = 0; i < input.Length; i++)
                            VCSInputs[i].State = input[i];

                        lastReadTime = DateTime.Now;
                        input = null;
                    }
                    catch (Exception ex)
                    {
                        LOG.ERROR($"Wago IO Read Exception...{ex.Message}");
                        LOG.Critical(ex.Message, ex);
                        Disconnect();
                        Connected = false;
                    }
                }
            });
        }

    }
}
