using AGVSystemCommonNet6.Abstracts;
using AGVSystemCommonNet6.Alarm.VMS_ALARM;
using AGVSystemCommonNet6.Log;
using GPMVehicleControlSystem.Tools;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Modbus.Device;
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
        public override string alarm_locate_in_name => "DI Module";

        public event EventHandler<ROBOT_CONTROL_CMD> OnLaserDIRecovery;
        public event EventHandler OnFarLaserDITrigger;
        public event EventHandler OnNearLaserDiTrigger;

        /// <summary>
        /// EMO按壓
        /// </summary>
        public event EventHandler OnEMO;
        /// <summary>
        /// Bump Sensor觸發
        /// </summary>
        public event EventHandler OnBumpSensorPressed;

        public event EventHandler OnResetButtonPressed;

        public event EventHandler OnFrontSecondObstacleSensorDetected;


        public Action OnResetButtonPressing { get; set; }

        public Action OnHS_EQ_READY { get; internal set; }

        internal Dictionary<Enum, int> Indexs = new Dictionary<Enum, int>();

        public List<clsIOSignal> VCSInputs = new List<clsIOSignal>();
        public ushort Start { get; set; }
        public ushort Size { get; set; }

        public clsDIModule()
        {
        }
        public clsDIModule(string IP, int Port)
        {
            this.IP = IP;
            this.Port = Port;
            ReadIOSettingsFromIniFile();
        }
        public clsDIModule(string IP, int Port, clsDOModule DoModuleRef)
        {
            this.IP = IP;
            this.Port = Port;
            this.DoModuleRef = DoModuleRef;
            ReadIOSettingsFromIniFile();
        }

        virtual public void ReadIOSettingsFromIniFile()
        {
            DI_ITEM di_item;
            IniHelper iniHelper = new IniHelper(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"param/IO_Wago.ini"));
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
        public override bool Connect()
        {
            if (IP == null | Port <= 0)
                throw new SocketException((int)SocketError.AddressNotAvailable);
            try
            {
                client = new TcpClient(IP, Port);
                master = ModbusIpMaster.CreateIp(client);
                master.Transport.ReadTimeout = 5000;
                master.Transport.WriteTimeout = 5000;
                master.Transport.Retries = 10;
                Current_Alarm_Code = AlarmCodes.None;
                LOG.INFO($"[{this.GetType().Name}]Wago Modbus TCP Connected!");
                return true;
            }
            catch (Exception ex)
            {
                Current_Alarm_Code = AlarmCodes.Wago_IO_Disconnect;
                LOG.Critical($"[{this.GetType().Name}]Wago Modbus TCP  Connect FAIL", ex);
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
                master?.Dispose();
                client = null;
                master = null;
            }
            catch (Exception)
            {
            }
        }

        public override bool IsConnected()
        {
            return client != null;
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
            VCSInputs[Indexs[signal]].OnStateChanged += handler;
        }
        protected virtual void RegistSignalEvents()
        {
            VCSInputs[Indexs[DI_ITEM.EMO]].OnSignalOFF += (s, e) => OnEMO?.Invoke(s, e);
            VCSInputs[Indexs[DI_ITEM.Bumper_Sensor]].OnSignalOFF += (s, e) => OnBumpSensorPressed?.Invoke(s, e);
            VCSInputs[Indexs[DI_ITEM.Panel_Reset_PB]].OnSignalON += (s, e) => OnResetButtonPressed?.Invoke(s, e);

            if (AgvType == AGV_TYPE.SUBMERGED_SHIELD)
            {
                VCSInputs[Indexs[DI_ITEM.FrontProtection_Obstacle_Sensor]].OnSignalOFF += (s, e) => OnFrontSecondObstacleSensorDetected?.Invoke(s, e);
            }
            else if (AgvType == AGV_TYPE.FORK)
            {
                VCSInputs[Indexs[DI_ITEM.Fork_Frontend_Abstacle_Sensor]].OnSignalOFF += (s, e) => OnFrontSecondObstacleSensorDetected?.Invoke(s, e);
            }
        }


        public virtual async void StartAsync()
        {
            await Task.Run(() =>
            {
                while (true)
                {

                    Thread.Sleep(1);

                    if (!IsConnected())
                    {
                        Connect();
                        continue;
                    }

                    try
                    {
                        bool[]? input = master?.ReadInputs(1, Start, Size);
                        if (input == null)
                            continue;

                        for (int i = 0; i < input.Length; i++)
                        {
                            VCSInputs[i].State = input[i];
                        }
                    }
                    catch (Exception ex)
                    {
                        AlarmManager.AddAlarm(AlarmCodes.Wago_IO_Read_Fail, false);
                        Disconnect();
                        Console.WriteLine(ex.Message);
                    }
                }
            });
        }

    }
}
