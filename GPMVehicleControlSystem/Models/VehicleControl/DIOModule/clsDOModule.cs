using AGVSystemCommonNet6.Alarm.VMS_ALARM;
using AGVSystemCommonNet6.Log;
using GPMVehicleControlSystem.Tools;
using Modbus.Device;
using System.Net.Http;
using System.Net.Sockets;

namespace GPMVehicleControlSystem.VehicleControl.DIOModule
{
    public partial class clsDOModule : clsDIModule
    {
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
            IniHelper iniHelper = new IniHelper(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), $"param/IO_Wago.ini"));
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
                            if (!Indexs.TryAdd(do_item, i))
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

        private async Task<(bool connected, TcpClient tcpclient, ModbusIpMaster modbusMaster)> TryConnectAsync()
        {
            TcpClient tcpclient;
            ModbusIpMaster modbusMaster;
            int retry_cnt = 0;
            while (!Connect(out tcpclient, out modbusMaster))
            {
                retry_cnt += 1;
                if (retry_cnt >= 5)
                {
                    OnDisonnected?.Invoke(this, EventArgs.Empty);
                    Current_Alarm_Code = AlarmCodes.Wago_IO_Read_Fail;
                    return (false, null, null);
                }
                await Task.Delay(1000);
            }
            Current_Alarm_Code = AlarmCodes.None;
            return (true, tcpclient, modbusMaster);
        }

        public async void SetState(string address, bool state)
        {
            try
            {
                clsIOSignal? DO = VCSOutputs.FirstOrDefault(k => k.Address == address);
                if (DO != null)
                {
                    DO.State = state;

                    (bool connected, TcpClient tcpclient, ModbusIpMaster modbusMaster) connresult = await TryConnectAsync();
                    if (connresult.connected)
                    {
                        connresult.modbusMaster?.WriteSingleCoil((ushort)(Start + DO.index), DO.State);
                        Disconnect(connresult.tcpclient, connresult.modbusMaster);
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
                return false;
            }
        }
        public override void SubsSignalStateChange(Enum signal, EventHandler<bool> handler)
        {
            VCSOutputs[Indexs[signal]].OnStateChanged += handler;

        }
        public async Task<bool> SetState(DO_ITEM signal, bool state)
        {
            try
            {
                clsIOSignal? DO = VCSOutputs.FirstOrDefault(k => k.Name == signal + "");
                if (DO != null)
                {
                    DO.State = state;
                    (bool connected, TcpClient tcpclient, ModbusIpMaster modbusMaster) connresult = await TryConnectAsync();
                    if (connresult.connected)
                    {
                        connresult.modbusMaster?.WriteSingleCoil((ushort)(Start + DO.index), DO.State);
                        Disconnect(connresult.tcpclient, connresult.modbusMaster);
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
        internal async void AllOFF()
        {
            (bool connected, TcpClient tcpclient, ModbusIpMaster modbusMaster) connresult = await TryConnectAsync();
            if (connresult.connected)
            {
                connresult.modbusMaster?.WriteMultipleCoils(Start, new bool[Size]);
                Disconnect(connresult.tcpclient, connresult.modbusMaster);
            }
        }
        internal async void SetState(DO_ITEM start_signal, bool[] writeStates)
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
                (bool connected, TcpClient tcpclient, ModbusIpMaster modbusMaster) connresult = await TryConnectAsync();
                if (connresult.connected)
                {
                    connresult.modbusMaster?.WriteMultipleCoils((ushort)(Start + DO_Start.index), writeStates);
                    Disconnect(connresult.tcpclient, connresult.modbusMaster);
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

        public async Task<bool> ResetSaftyRelay()
        {
            //安全迴路RELAY
            bool RelayON = false;
            while (true)
            {
                await Task.Delay(300);
                bool do_writen_confirm = false;
                if (!RelayON)
                {
                    do_writen_confirm = await SetState(DO_ITEM.Safety_Relays_Reset, true);
                    RelayON = do_writen_confirm;
                }
                else
                {
                    do_writen_confirm = await SetState(DO_ITEM.Safety_Relays_Reset, false);
                    break;
                }
                if (!do_writen_confirm)
                    return false;
            }
            return true;
        }
        public override async void StartAsync()
        {
            if (!IsConnected())
                Connect();
        }

    }
}
