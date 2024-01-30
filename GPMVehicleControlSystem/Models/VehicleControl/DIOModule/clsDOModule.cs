using AGVSystemCommonNet6.AGVDispatch.RunMode;
using AGVSystemCommonNet6.Log;
using AGVSystemCommonNet6.Vehicle_Control.VCS_ALARM;
using GPMVehicleControlSystem.Tools;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.EntityFrameworkCore.SqlServer.Query.Internal;
using Microsoft.Extensions.Configuration.EnvironmentVariables;
using Modbus.Device;
using Newtonsoft.Json.Linq;
using System.Collections.Concurrent;
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
            //ReadCurrentDOStatus();
        }
        public clsDOModule(string IP, int Port, clsDOModule DoModuleRef) : base(IP, Port, DoModuleRef)
        {
            //ReadCurrentDOStatus();
        }

        public override bool Connected { get => _Connected; set => _Connected = value; }
        internal override void RegistSignalEvents()
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


        public override bool SubsSignalStateChange(Enum signal, EventHandler<bool> handler)
        {
            try
            {

                VCSOutputs[Indexs[signal]].OnStateChanged += handler;
                return true;
            }
            catch (Exception ex)
            {
                LOG.ERROR("DO-" + signal + "Sbuscribe Error.", ex, show_console: false);
                return false;
            }

        }
        private Task OutputWriteWorkTask;
        public override async Task<bool> Connect()
        {
            bool connected = _Connected = await base.Connect();
            if (OutputWriteWorkTask == null)
                OutputWriteWorkTask = OutputWriteWorker();
            return connected;
        }

        private async Task OutputWriteWorker()
        {
            _ = Task.Factory.StartNew(async () =>
            {
                int reconnect_cnt = 0;
                DateTime lastWriteTime = DateTime.MinValue;
                bool testFlag = false;
                while (true)
                {
                    Thread.Sleep(50);
                    try
                    {
                        if (_Connected)
                        {
                            try
                            {
                                master?.ReadCoils(0, 1);//just keep tcp connection 
                            }
                            catch (Exception ex)
                            {
                                LOG.ERROR($"DO Read Coils Fail.");
                                _Connected = false;
                                continue;
                            }

                            if (OutputWriteRequestQueue.Count == 0)
                                continue;

                            reconnect_cnt = 0;
                            if (OutputWriteRequestQueue.TryDequeue(out var to_handle_obj))
                            {
                                try
                                {
                                    WriteToDevice(to_handle_obj);
                                    lastWriteTime = DateTime.Now;
                                }
                                catch (Exception ex)
                                {
                                    OutputWriteRequestQueue.Enqueue(to_handle_obj);
                                    LOG.ERROR(ex.Message, ex);
                                    _Connected = false;
                                    continue;
                                }
                            }
                        }
                        else
                        {
                            if ((DateTime.Now - lastWriteTime).TotalSeconds < 1)
                            {
                                Current_Warning_Code = AlarmCodes.Wago_IO_Write_Fail;
                                Thread.Sleep(100);
                                Current_Warning_Code = AlarmCodes.Wago_IO_Disconnect;
                            }
                            LOG.WARN($"DO Module try reconnecting..");
                            Disconnect();
                            _Connected = await Connect();
                            continue;
                        }
                    }
                    catch (Exception ex)
                    {

                    }

                }
            });
        }
        private void WriteToDevice(clsWriteRequest to_handle_obj)
        {
            ushort startAddress = (ushort)(Start + to_handle_obj.signal.index);
            bool[] writeStates = to_handle_obj.writeStates;
            bool[] rollback = writeStates.Select(s => !s).ToArray();
            ushort count = (ushort)writeStates.Length;
            CancellationTokenSource tim = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            while (!rollback.SequenceEqual(writeStates))
            {
                Thread.Sleep(1);
                if (tim.IsCancellationRequested)
                {
                    LOG.ERROR($"Connected:{Connected} DO-" + to_handle_obj.signal.index + "Wago_IO_Write_Fail Error.");
                    throw new Exception($"DO Write Timeout.");
                }
                master?.WriteMultipleCoils(startAddress, writeStates);
                rollback = master?.ReadCoils(startAddress, count);
            }
            for (int i = 0; i < writeStates.Length; i++)
            {
                clsIOSignal? _DO = VCSOutputs.FirstOrDefault(k => k.index == to_handle_obj.signal.index + i);
                if (_DO != null)
                    _DO.State = writeStates[i];
            }

        }

        public async Task<bool> SetState(string address, bool state)
        {
            try
            {
                clsIOSignal? DO = VCSOutputs.FirstOrDefault(k => k.Address == address);
                if (DO != null)
                {
                    OutputWriteRequestQueue.Enqueue(new clsWriteRequest(DO, new bool[] { state }));
                    return true;
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
                    OutputWriteRequestQueue.Enqueue(new clsWriteRequest(DO, new bool[] { state }));
                    while (GetState(signal) != state)
                    {
                        Thread.Sleep(1);
                    }
                    return true;
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
                LOG.Critical(ex);
                AlarmManager.AddAlarm(AlarmCodes.Wago_IO_Write_Fail, false);
                return false;
            }
        }
        class clsWriteRequest
        {
            public readonly clsIOSignal signal;
            public readonly bool[] writeStates;
            public clsWriteRequest(clsIOSignal signal, bool[] writeStates)
            {
                this.signal = signal;
                this.writeStates = writeStates;
            }
        }

        private ConcurrentQueue<clsWriteRequest> OutputWriteRequestQueue = new ConcurrentQueue<clsWriteRequest>();
        internal async Task<bool> SetState(DO_ITEM start_signal, bool[] writeStates)
        {
            try
            {


                clsIOSignal? DO_Start = VCSOutputs.FirstOrDefault(k => k.Name == start_signal + "");
                if (DO_Start == null)
                {
                    LOG.ERROR($"Connected:{Connected} DO-" + start_signal + "Wago_IO_Write_Fail Error.");
                    AlarmManager.AddAlarm(AlarmCodes.Wago_IO_Write_Fail, false);
                    return false;
                }
                else
                {
                    bool[] GetCurrentOuputState(ushort startIndex, int length)
                    {
                        bool[] states = new bool[length];

                        for (int i = 0; i < writeStates.Length; i++)
                        {
                            var _index = startIndex + i;
                            states[i] = VCSOutputs.FirstOrDefault(output => output.index == _index).State;
                        }
                        return states;
                    }

                    OutputWriteRequestQueue.Enqueue(new clsWriteRequest(DO_Start, writeStates));
                    CancellationTokenSource _intime_detector = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                    while (!GetCurrentOuputState(DO_Start.index, writeStates.Length).SequenceEqual(writeStates))
                    {
                        Thread.Sleep(1);
                        if (_intime_detector.IsCancellationRequested)
                        {
                            AlarmManager.AddAlarm(AlarmCodes.Wago_IO_Write_Fail, false);
                            return false;
                        }
                    }
                    return true;
                }
            }
            catch (Exception ex)
            {
                LOG.ERROR("DO-" + start_signal + $"Wago_IO_Write_Fail Error {ex.Message}");
                LOG.Critical(ex);
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
            if (_Connected)
            {
                try
                {
                    master?.WriteMultipleCoils(Start, new bool[Size]);
                }
                catch (Exception ex)
                {
                }
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
