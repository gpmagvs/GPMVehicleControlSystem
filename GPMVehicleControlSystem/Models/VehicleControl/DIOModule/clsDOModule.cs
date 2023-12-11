using AGVSystemCommonNet6.AGVDispatch.RunMode;
using AGVSystemCommonNet6.Log;
using AGVSystemCommonNet6.Vehicle_Control.VCS_ALARM;
using GPMVehicleControlSystem.Tools;
using Microsoft.Extensions.Configuration.EnvironmentVariables;
using Modbus.Device;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using static GPMVehicleControlSystem.ViewModels.ForkTestVM.clsForkTestState;

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

        public override bool Connected { get => _Connected; set => _Connected = value; }
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
                if (!Connected)
                    return (false, null, null);
                if (retry_cnt >= 5)
                {
                    OnDisonnected?.Invoke(this, EventArgs.Empty);
                    Current_Alarm_Code = AlarmCodes.Wago_IO_WRITE_Disconnect;
                    return (false, null, null);
                }
                await Task.Delay(1000);
            }
            Current_Alarm_Code = AlarmCodes.None;
            return (true, tcpclient, modbusMaster);
        }
        public new bool Connect(out TcpClient client, out ModbusIpMaster master)
        {
            client = null;
            master = null;
            if (IP == null | VMSPort <= 0)
                throw new SocketException((int)SocketError.AddressNotAvailable);
            try
            {
                client = new TcpClient(IP, this.VMSPort);
                master = ModbusIpMaster.CreateIp(client);
                master.Transport.WriteTimeout = 300;
                master.Transport.Retries = 3;
                return true;
            }
            catch (Exception ex)
            {
                LOG.ERROR($"[DOModule] Try Connect to DIO Module={IP}:{VMSPort} FAIL, {ex.Message}");
                return false;
            }
        }
        public override void SubsSignalStateChange(Enum signal, EventHandler<bool> handler)
        {
            try
            {

                VCSOutputs[Indexs[signal]].OnStateChanged += handler;
            }
            catch (Exception ex)
            {
                LOG.ERROR("DO-" + signal + "Sbuscribe Error.", ex, show_console: false);
            }

        }

        public async Task<bool> SetState(string address, bool state)
        {
            try
            {
                clsIOSignal? DO = VCSOutputs.FirstOrDefault(k => k.Address == address);
                if (DO != null)
                {
                    (bool connected, TcpClient tcpclient, ModbusIpMaster modbusMaster) connresult = await TryConnectAsync();
                    if (connresult.connected)
                    {
                        var master = connresult.modbusMaster;
                        var startAddress = (ushort)(Start + DO.index);
                        bool[] rollbacks = new bool[1] { !state };
                        rollbacks = await master?.ReadCoilsAsync(startAddress, 1);
                        if (rollbacks[0] == state)
                            return true;

                        CancellationTokenSource tim = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                        while (rollbacks[0] != state)
                        {
                            await Task.Delay(30);
                            try
                            {
                                master?.WriteSingleCoil(startAddress, state);
                                await Task.Delay(30);
                                rollbacks = await master?.ReadCoilsAsync(startAddress, 1);
                            }
                            catch (Exception)
                            {
                                Disconnect(connresult.tcpclient, connresult.modbusMaster);
                                connresult = await TryConnectAsync();
                                master = connresult.modbusMaster;
                                continue;
                            }
                            if (tim.IsCancellationRequested)
                            {
                                LOG.ERROR("DO-" + address + "Wago_IO_Write_Fail Error.- Timeout");
                                AlarmManager.AddAlarm(AlarmCodes.Wago_IO_Write_Fail, false);
                                Disconnect(connresult.tcpclient, connresult.modbusMaster);
                                return false;
                            }
                        }
                        DO.State = state;
                        Disconnect(connresult.tcpclient, connresult.modbusMaster);
                        return true;
                    }
                    else
                    {
                        LOG.ERROR("DO-" + address + "Wago_IO_Write_Fail Error. connection fail");
                        AlarmManager.AddAlarm(AlarmCodes.Wago_IO_WRITE_Disconnect, false);

                        return false;
                    }
                }
                else
                {
                    LOG.ERROR("DO-" + address + $"Wago_IO_Write_Fail Error.{address}-Not found ");
                    AlarmManager.AddAlarm(AlarmCodes.Wago_IO_Write_Fail, false);

                    return false;
                }
            }
            catch (Exception ex)
            {
                LOG.ERROR("DO-" + address + $"Wago_IO_Write_Fail Error.{ex.Message + ex.StackTrace}");
                AlarmManager.AddAlarm(AlarmCodes.Wago_IO_Write_Fail, false);
                return false;
            }
        }

        public async Task<bool> SetState(DO_ITEM signal, bool state)
        {
            try
            {
                clsIOSignal? DO = VCSOutputs.FirstOrDefault(k => k.Name == signal + "");
                if (DO != null)
                {
                    return await SetState(DO.Address, state);
                }
                else
                {
                    return false;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message + ex.StackTrace);
                Environment.Exit(1);
                IOBusy = false;
                LOG.Critical(ex);
                AlarmManager.AddAlarm(AlarmCodes.Wago_IO_Write_Fail, false);
                return false;
            }
        }

        internal async Task<bool> SetState(DO_ITEM start_signal, bool[] writeStates)
        {
            if (!Connected)
            {
                LOG.ERROR($"Connected:{Connected} DO-" + start_signal + "Wago_IO_Write_Fail Error.");
                AlarmManager.AddAlarm(AlarmCodes.Wago_IO_Write_Fail, false);
                return false;
            }
            try
            {
                clsIOSignal? DO_Start = VCSOutputs.FirstOrDefault(k => k.Name == start_signal + "");
                if (DO_Start == null)
                {
                    LOG.ERROR($"Connected:{Connected} DO-" + start_signal + "Wago_IO_Write_Fail Error.");
                    AlarmManager.AddAlarm(AlarmCodes.Wago_IO_Write_Fail, false);
                    return false;
                }

                (bool connected, TcpClient tcpclient, ModbusIpMaster modbusMaster) connresult = await TryConnectAsync();
                if (connresult.connected)
                {
                    var startAddress = (ushort)(Start + DO_Start.index);
                    bool[] rollback = writeStates.Select(s => !s).ToArray();
                    ushort count = (ushort)writeStates.Length;
                    IOBusy = true;
                    CancellationTokenSource tim = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                    while (string.Join(",", rollback) != string.Join(",", writeStates))
                    {

                        await Task.Delay(50);
                        if (tim.IsCancellationRequested)
                        {
                            LOG.ERROR($"Connected:{Connected} DO-" + start_signal + "Wago_IO_Write_Fail Error.");
                            AlarmManager.AddAlarm(AlarmCodes.Wago_IO_Write_Fail, false);
                            Disconnect(connresult.tcpclient, connresult.modbusMaster);
                            IOBusy = false;
                            return false;
                        }
                        connresult.modbusMaster?.WriteMultipleCoils(startAddress, writeStates);
                        await Task.Delay(50);
                        rollback = connresult.modbusMaster?.ReadCoils(startAddress, count);
                    }
                    for (int i = 0; i < writeStates.Length; i++)
                    {
                        clsIOSignal? _DO = VCSOutputs.FirstOrDefault(k => k.index == DO_Start.index + i);
                        if (_DO != null)
                            _DO.State = writeStates[i];
                    }
                    Disconnect(connresult.tcpclient, connresult.modbusMaster);
                    IOBusy = false;
                    return true;
                }
                else
                {
                    LOG.ERROR($"Connected:{Connected} DO-" + start_signal + "Wago_IO_Write_Fail Error.");
                    AlarmManager.AddAlarm(AlarmCodes.Wago_IO_Write_Fail, false);

                    return false;
                }
            }
            catch (Exception ex)
            {
                LOG.ERROR("DO-" + start_signal + $"Wago_IO_Write_Fail Error {ex.Message}");
                LOG.Critical(ex);
                IOBusy = false;
                AlarmManager.AddAlarm(AlarmCodes.Wago_IO_Write_Fail, false);
                return false;
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
                LOG.INFO($"Wago DO All OFF Done.");
            }
        }

        public async Task<bool> ResetSaftyRelay()
        {
            //安全迴路RELAY
            bool RelayON = false;
            var do_writen_confirm = await SetState(DO_ITEM.Safety_Relays_Reset, true);
            await Task.Delay(200);
            do_writen_confirm = await SetState(DO_ITEM.Safety_Relays_Reset, false);
            return true;
        }

    }
}
