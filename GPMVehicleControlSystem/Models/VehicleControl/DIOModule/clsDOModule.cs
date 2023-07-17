using AGVSystemCommonNet6.Alarm.VMS_ALARM;
using AGVSystemCommonNet6.Log;
using GPMVehicleControlSystem.Tools;
using Modbus.Device;
using System.Net.Sockets;

namespace GPMVehicleControlSystem.VehicleControl.DIOModule
{
    public class clsDOModule : clsDIModule
    {
        public enum DO_ITEM : byte
        {
            Unknown,
            EMU_EQ_L_REQ,
            EMU_EQ_U_REQ,
            EMU_EQ_READY,
            EMU_EQ_UP_READY,
            EMU_EQ_LOW_READY,
            EMU_EQ_BUSY,
            Recharge_Circuit,
            Motor_Safety_Relay,
            Safety_Relays_Reset,
            Horizon_Motor_Stop,
            Horizon_Motor_Free,
            Horizon_Motor_Reset,
            Horizon_Motor_Brake,
            Vertical_Motor_Stop,
            Vertical_Motor_Free,
            Vertical_Motor_Reset,

            Front_LsrBypass,
            Back_LsrBypass,
            Left_LsrBypass,
            Right_LsrBypass,
            Fork_Under_Pressing_SensorBypass,
            Vertical_Belt_SensorBypass,

            AGV_DiractionLight_Front,
            AGV_DiractionLight_Back,
            AGV_DiractionLight_R,
            AGV_DiractionLight_Y,
            AGV_DiractionLight_G,
            AGV_DiractionLight_B,
            AGV_DiractionLight_Left,
            AGV_DiractionLight_Right,
            AGV_DiractionLight_Left_2,
            AGV_DiractionLight_Right_2,
            Vertical_Hardware_limit_bypass,

            AGV_VALID,
            AGV_READY,
            AGV_TR_REQ,
            AGV_BUSY,
            AGV_COMPT,
            AGV_L_REQ,
            AGV_U_REQ,
            AGV_CS_0,
            AGV_CS_1,
            AGV_Check_REQ,
            TO_EQ_Low,
            TO_EQ_Up,
            CMD_reserve_Up,
            CMD_reserve_Low,
            Front_Protection_Sensor_IN_1,
            Front_Protection_Sensor_CIN_1,
            Front_Protection_Sensor_IN_2,
            Front_Protection_Sensor_CIN_2,
            Front_Protection_Sensor_IN_3,
            Front_Protection_Sensor_CIN_3,
            Front_Protection_Sensor_IN_4,
            Front_Protection_Sensor_CIN_4,

            Back_Protection_Sensor_IN_1,
            Back_Protection_Sensor_CIN_1,
            Back_Protection_Sensor_IN_2,
            Back_Protection_Sensor_CIN_2,
            Back_Protection_Sensor_IN_3,
            Back_Protection_Sensor_CIN_3,
            Back_Protection_Sensor_IN_4,
            Back_Protection_Sensor_CIN_4,
            Left_Protection_Sensor_IN_1,
            Left_Protection_Sensor_IN_2,
            Left_Protection_Sensor_IN_3,
            Left_Protection_Sensor_IN_4,
            Ultrasound_Bypass,
            N2_Open,
            Instrument_Servo_On,
            Battery_2_Lock,
            Battery_2_Unlock,
            Battery_1_Lock,
            Battery_1_Unlock,
            Infrared_Door_1,
            Infrared_PW_2,
            Infrared_PW_1,
            Infrared_PW_0,
            Infrared_Door_2,

            /// <summary>
            /// 牙叉電動肛伸出
            /// </summary>
            Fork_Extend,
            /// <summary>
            /// 牙叉電動肛縮回
            /// </summary>
            Fork_Shortend,

        }
        Dictionary<DO_ITEM, int> OUTPUT_INDEXS = new Dictionary<DO_ITEM, int>();
        public event EventHandler OnDisonnected;
        public List<clsIOSignal> VCSOutputs = new List<clsIOSignal>();
        public clsDOModule() : base()
        {

        }
        public clsDOModule(string IP, int Port) : base(IP, Port)
        {

        }
        public clsDOModule(string IP, int Port, clsDOModule DoModuleRef) : base(IP, Port, DoModuleRef)
        {
        }

        protected override void RegistSignalEvents()
        {
        }
        public override void ReadIOSettingsFromIniFile()
        {
            IniHelper iniHelper = new IniHelper(Path.Combine(Environment.CurrentDirectory, "param/IO_Wago.ini"));

            try
            {
                Start = ushort.Parse(iniHelper.GetValue("OUTPUT", "Start"));
                Size = ushort.Parse(iniHelper.GetValue("OUTPUT", "Size"));
                var do_names = Enum.GetValues(typeof(DO_ITEM)).Cast<DO_ITEM>().Select(i => i.ToString()).ToList();
                for (ushort i = 0; i < Size; i++)
                {
                    var Address = $"Y{i.ToString("X4")}";
                    var RigisterName = iniHelper.GetValue("OUTPUT", Address);
                    var reg = new clsIOSignal(RigisterName, Address);
                    reg.index = i;
                    reg.State = false;
                    if (RigisterName != "")
                    {
                        if (do_names.Contains(RigisterName))
                        {
                            var do_item = Enum.GetValues(typeof(DO_ITEM)).Cast<DO_ITEM>().FirstOrDefault(di => di.ToString() == RigisterName);
                            if (!OUTPUT_INDEXS.TryAdd(do_item, i))
                            {
                                throw new Exception("WAGO DO名稱重複");
                            }
                        }
                        else
                        {

                        }
                    }

                    VCSOutputs.Add(reg);
                }
            }
            catch (Exception ex)
            {

            }
        }


        public void SetState(string address, bool state)
        {
            try
            {
                clsIOSignal? DO = VCSOutputs.FirstOrDefault(k => k.Address == address);
                if (DO != null)
                {
                    DO.State = state;
                    bool connected = Connect(out var tcpclient, out var modbusMaster);
                    if (connected)
                    {
                        modbusMaster?.WriteSingleCoil((ushort)(Start + DO.index), DO.State);
                        Disconnect(tcpclient, modbusMaster);
                    }
                }
            }
            catch (Exception ex)
            {
                AlarmManager.AddAlarm(AlarmCodes.Wago_IO_Write_Fail, false);
                throw ex;
            }
        }
        public new bool Connect(out TcpClient client, out ModbusIpMaster master)
        {
            client = null;
            master = null;
            if (IP == null | Port <= 0)
                throw new SocketException((int)SocketError.AddressNotAvailable);
            try
            {
                client = new TcpClient(IP, Port);
                master = ModbusIpMaster.CreateIp(client);
                master.Transport.ReadTimeout = 5000;
                master.Transport.WriteTimeout = 5000;
                master.Transport.Retries = 10;
                return true;
            }
            catch (Exception ex)
            {
                LOG.Critical($"[{this.GetType().Name}]Wago Modbus TCP  Connect FAIL", ex);
                OnDisonnected?.Invoke(this, EventArgs.Empty);
                master = null;
                throw new SocketException((int)SocketError.ConnectionAborted);
            }
        }

        public bool SetState(DO_ITEM signal, bool state)
        {
            try
            {
                clsIOSignal? DO = VCSOutputs.FirstOrDefault(k => k.Name == signal + "");
                if (DO != null)
                {
                    DO.State = state;
                    bool connected = Connect(out var tcpclient, out var modbusMaster);
                    if (connected)
                    {
                        modbusMaster?.WriteSingleCoil((ushort)(Start + DO.index), DO.State);
                        Disconnect(tcpclient, modbusMaster);
                        return true;
                    }
                    else
                        return false;
                }
                else
                    return false;
            }
            catch (Exception ex)
            {
                AlarmManager.AddAlarm(AlarmCodes.Wago_IO_Write_Fail, false);
                return false;
            }
        }

        private bool Disconnect(TcpClient tcpclient, ModbusIpMaster? modbusMaster)
        {
            try
            {
                tcpclient?.Close();
                modbusMaster?.Dispose();
                return true;
            }
            catch (Exception ex)
            {
                return false;
            }
        }

        internal void AllOFF()
        {
            bool connected = Connect(out var tcpclient, out var modbusMaster);
            if (connected)
            {
                modbusMaster?.WriteMultipleCoils(Start, new bool[Size]);
                Disconnect(tcpclient, modbusMaster);
            }
        }
        internal void SetState(DO_ITEM start_signal, bool[] writeStates)
        {
            try
            {
                clsIOSignal? DO_Start = VCSOutputs.FirstOrDefault(k => k.Name == start_signal + "");
                if (DO_Start == null)
                {
                    throw new Exception();
                }
                for (int i = 0; i < writeStates.Length; i++)
                {
                    clsIOSignal? _DO = VCSOutputs.FirstOrDefault(k => k.index == DO_Start.index + i);
                    if (_DO != null)
                        _DO.State = writeStates[i];
                }

                bool connected = Connect(out var tcpclient, out var modbusMaster);
                if (connected)
                {
                    modbusMaster?.WriteMultipleCoils((ushort)(Start + DO_Start.index), writeStates);
                    Disconnect(tcpclient, modbusMaster);
                }
            }
            catch (Exception ex)
            {
                AlarmManager.AddAlarm(AlarmCodes.Wago_IO_Write_Fail, false);
                throw ex;
            }
        }
        public new bool GetState(DO_ITEM signal)
        {
            try
            {
                return VCSOutputs.FirstOrDefault(k => k.Name == signal + "").State;
            }
            catch (Exception)
            {
                return false;
            }
        }
        public void ResetHandshakeSignals()
        {
            SetState(DO_ITEM.AGV_COMPT, false);
            SetState(DO_ITEM.AGV_BUSY, false);
            SetState(DO_ITEM.AGV_READY, false);
            SetState(DO_ITEM.AGV_TR_REQ, false);
            SetState(DO_ITEM.AGV_VALID, false);
        }
        public override async void StartAsync()
        {
            if (!IsConnected())
                Connect();
        }

    }
}
