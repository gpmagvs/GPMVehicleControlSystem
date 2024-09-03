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
using System.Diagnostics;
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
        private SemaphoreSlim _writeStateSemaphoreSlim = new SemaphoreSlim(1, 1);
        public clsDOModule() : base()
        {

        }
        public clsDOModule(string IP, int Port) : base(IP, Port)
        {
            //ReadCurrentDOStatus();
        }
        public override bool Connected { get => _Connected; set => _Connected = value; }
        public override string alarm_locate_in_name => "DO Module";
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


        public override void SubsSignalStateChange(Enum signal, EventHandler<bool> handler)
        {
            try
            {
                void _HandleInputStateChanged(object? sender, bool e)
                {
                    Task.Run(() =>
                    {
                        handler.Invoke(sender, e);
                    });
                }
                VCSOutputs[Indexs[signal]].OnStateChanged += _HandleInputStateChanged;
            }
            catch (Exception ex)
            {
                LOG.ERROR("DO-" + signal + "Sbuscribe Error.", ex, show_console: false);
            }

        }
        private Task OutputWriteWorkTask;
        public override async Task<bool> Connect()
        {
            bool connected = _Connected = await base.Connect();
            if (OutputWriteWorkTask == null)
                OutputWriteWorkTask = Task.Run(() => OutputWriteWorker());
            return connected;
        }
        private async void OutputWriteWorker()
        {
            while (true)
            {
                await Task.Delay(50); // 使用非阻塞的方式等待
                if (!_Connected)
                {
                    LOG.WARN("DO Module try reconnecting..");
                    Disconnect();
                    _Connected = await Connect();
                    continue;
                }

                try
                {
                    master?.ReadCoils(0, 1);
                    Current_Alarm_Code = AlarmCodes.None;
                }
                catch (Modbus.SlaveException ex)
                {
                    Current_Alarm_Code = AlarmCodes.Wago_IO_Disconnect;
                    break;
                }
                catch (Exception ex)
                {
                    LOG.ERROR($"DO Read Coils Fail: {ex.Message}");
                    _Connected = false;
                    continue;
                }

                if (OutputWriteRequestQueue.IsEmpty)
                    continue;

                if (OutputWriteRequestQueue.TryDequeue(out var to_handle_obj))
                {
                    try
                    {
                        await WriteToDeviceAsync(to_handle_obj);
                    }
                    catch (Exception ex)
                    {
                        OutputWriteRequestQueue.Enqueue(to_handle_obj);
                        LOG.ERROR($"Error writing to device: {ex.Message}", ex);
                        _Connected = false;
                    }
                }
            }
        }


        private async Task WriteToDeviceAsync(clsWriteRequest to_handle_obj)
        {
            ushort startAddress = (ushort)(Start + to_handle_obj.signal.index);
            bool[] writeStates = to_handle_obj.writeStates;
            bool[] rollback = writeStates.Select(s => !s).ToArray();
            ushort count = (ushort)writeStates.Length;
            CancellationTokenSource tim = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            while (!(rollback = master?.ReadCoils(startAddress, count)).SequenceEqual(writeStates))
            {
                if (tim.IsCancellationRequested)
                {
                    LOG.ERROR($"Connected:{Connected} DO-" + to_handle_obj.signal.index + "Wago_IO_Write_Fail Error.");
                    throw new Exception($"DO Write Timeout.");
                }
                master?.WriteMultipleCoils(startAddress, writeStates);
                await Task.Delay(50); // 使用非阻塞的方式等待
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
                await _writeStateSemaphoreSlim.WaitAsync();
                if (Current_Alarm_Code != AlarmCodes.None)
                    return false;
                clsIOSignal? DO = VCSOutputs.FirstOrDefault(k => k.Address == address);
                if (DO != null)
                {
                    OutputWriteRequestQueue.Enqueue(new clsWriteRequest(DO, new bool[] { state }));

                    CancellationTokenSource _timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                    while (DO.State != state)
                    {
                        if (_timeout.IsCancellationRequested)
                        {
                            Current_Alarm_Code = AlarmCodes.Wago_IO_Write_Fail;
                            return false;
                        }
                        await Task.Delay(10);

                    }
                    return true;
                }
                else
                {
                    LOG.ERROR("DO-" + address + $"Wago_IO_Write_Fail Error.{address}-Not found ");
                    Current_Alarm_Code = AlarmCodes.Wago_IO_Write_Fail;
                    return false;
                }
            }
            catch (Exception ex)
            {
                LOG.ERROR("DO-" + address + $"Wago_IO_Write_Fail Error.{ex.Message + ex.StackTrace}");
                Current_Alarm_Code = AlarmCodes.Wago_IO_Write_Fail;
                return false;
            }
            finally
            {
                _writeStateSemaphoreSlim.Release();
            }
        }

        public async Task<bool> SetState(DO_ITEM signal, bool state)
        {
            try
            {
                await _writeStateSemaphoreSlim.WaitAsync();
                if (Current_Alarm_Code != AlarmCodes.None)
                    return false;
                clsIOSignal? DO = VCSOutputs.FirstOrDefault(k => k.Name == signal + "");
                if (DO != null)
                {
                    OutputWriteRequestQueue.Enqueue(new clsWriteRequest(DO, new bool[] { state }));
                    CancellationTokenSource _timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));

                    while (GetState(signal) != state)
                    {
                        await Task.Delay(1);
                        if (_timeout.IsCancellationRequested)
                        {
                            Current_Alarm_Code = AlarmCodes.Wago_IO_Write_Fail;
                            return false;
                        }
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
                LOG.Critical(ex);
                Current_Alarm_Code = AlarmCodes.Wago_IO_Write_Fail;
                return false;
            }
            finally
            {
                _writeStateSemaphoreSlim.Release();
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
                await _writeStateSemaphoreSlim.WaitAsync();
                if (Current_Alarm_Code != AlarmCodes.None)
                    return false;
                clsIOSignal? DO_Start = VCSOutputs.FirstOrDefault(k => k.Name == start_signal + "");
                if (DO_Start == null)
                {
                    LOG.ERROR($"Connected:{Connected} DO-" + start_signal + "Wago_IO_Write_Fail Error.");
                    Current_Alarm_Code = AlarmCodes.Wago_IO_Write_Fail;
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
                        if (_intime_detector.IsCancellationRequested)
                        {
                            Current_Alarm_Code = AlarmCodes.Wago_IO_Write_Fail;
                            return false;
                        }
                        await Task.Delay(10);
                    }

                    return true;
                }
            }
            catch (Exception ex)
            {
                LOG.ERROR("DO-" + start_signal + $"Wago_IO_Write_Fail Error {ex.Message}");
                LOG.Critical(ex);
                Current_Alarm_Code = AlarmCodes.Wago_IO_Write_Fail;
                return false;
            }
            finally
            {
                _writeStateSemaphoreSlim.Release();
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
            var do_writen_confirm = await SetState(DO_ITEM.Safety_Relays_Reset, true).ConfigureAwait(false);
            await Task.Delay(200);
            do_writen_confirm = await SetState(DO_ITEM.Safety_Relays_Reset, false).ConfigureAwait(false);
            return true;
        }

    }
}
