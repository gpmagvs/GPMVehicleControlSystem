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
using System.Threading;
using System.Xml.Linq;
using static AGVSystemCommonNet6.Vehicle_Control.CarComponent;
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

                int.TryParse(iniHelper.GetValue("OUTPUT", "ShiftStart"), out ShiftStart);
                int.TryParse(iniHelper.GetValue("OUTPUT", "ShiftSize"), out ShiftSize);

                LOG.INFO($"DO Shift Start = {ShiftStart}, Shift Size = {ShiftSize}");

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

                VCSOutputs[Indexs[signal]].OnStateChanged += handler;
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
            int reconnect_cnt = 0;
            DateTime lastWriteTime = DateTime.MinValue;

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
                        lastWriteTime = DateTime.Now;
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


        private SemaphoreSlim semaphore = new SemaphoreSlim(1, 1);

        private ConcurrentQueue<clsWriteRequest> OutputWriteRequestQueue = new ConcurrentQueue<clsWriteRequest>();
        private async Task WriteToDeviceAsync(clsWriteRequest to_handle_obj)
        {


            ushort startAddress = (ushort)(Start + to_handle_obj.signal.index);

            if (to_handle_obj.signal.index >= ShiftStart)
            {
                startAddress += (ushort)ShiftSize;
            }

            bool[] writeStates = to_handle_obj.writeStates;
            bool[] rollback = writeStates.Select(s => !s).ToArray();
            ushort count = (ushort)writeStates.Length;
            CancellationTokenSource tim = new CancellationTokenSource(TimeSpan.FromSeconds(5));

            async Task<bool[]> ReadFromModule(ushort _startAddress, ushort _count)
            {
                return await master?.ReadCoilsAsync(_startAddress, _count);
            }

            while (!(rollback = await ReadFromModule(startAddress, count)).SequenceEqual(writeStates))
            {
                await Task.Delay(1);
                if (tim.IsCancellationRequested)
                {
                    LOG.ERROR($"Connected:{Connected} DO-" + to_handle_obj.signal.index + "Wago_IO_Write_Fail Error.");
                    throw new Exception($"DO Write Timeout.");
                }
                master?.WriteMultipleCoils(startAddress, writeStates);
            }
            for (int i = 0; i < writeStates.Length; i++)
            {
                clsIOSignal? _DO = VCSOutputs.FirstOrDefault(k => k.index == to_handle_obj.signal.index + i);
                if (_DO != null)
                {
                    _DO.State = writeStates[i];
                }
            }
            if (to_handle_obj.OnWriteDone != null)
                to_handle_obj.OnWriteDone(rollback);
        }

        public async Task<bool> SetState(string address, bool state)
        {
            clsIOSignal? DO = VCSOutputs.FirstOrDefault(k => k.Address == address);
            if (DO.Output == null)
            {
                LOG.WARN($"Output-{address} not defined, no write to IO module");
                return false;
            }
            return await SetState(DO.Output, new bool[] { state });
        }

        public async Task<bool> SetState(DO_ITEM signal, bool state)
        {
            return await SetState(signal, new bool[] { state });
        }
        internal async Task<bool> SetState(DO_ITEM start_signal, bool[] writeStates)
        {
            await semaphore.WaitAsync();
            try
            {
                if (Current_Alarm_Code != AlarmCodes.None)
                    return false;
                clsIOSignal? DO = VCSOutputs.FirstOrDefault(k => k.Name == start_signal.ToString());
                if (DO != null)
                {
                    bool writeDone = false;
                    bool[] DOCurrentSTate = new bool[writeStates.Length];
                    void sdafasdf(bool[] currentState)
                    {
                        DOCurrentSTate = currentState;
                        writeDone = true;
                    }

                    clsWriteRequest writeState = new clsWriteRequest(DO, writeStates);
                    writeState.OnWriteDone += sdafasdf;
                    OutputWriteRequestQueue.Enqueue(writeState);
                    while (!writeDone)
                    {
                        await Task.Delay(1);
                    }
                    bool success = DOCurrentSTate.SequenceEqual(writeStates);

                    writeState.Dispose();

                    return success;
                }
                else
                {
                    //Current_Alarm_Code = AlarmCodes.Wago_IO_Write_Fail;
                    return false;
                }
            }
            catch (Exception ex)
            {
                LOG.ERROR($"DO Start From {start_signal} Write {(string.Join(",", writeStates))} Fail Error.{ex.Message + ex.StackTrace}");
                Current_Alarm_Code = AlarmCodes.Wago_IO_Write_Fail;
                return false;
            }
            finally
            {
                semaphore.Release();
            }
        }


        class clsWriteRequest : IDisposable
        {
            public readonly clsIOSignal signal;
            public readonly bool[] writeStates;
            public clsWriteRequest(clsIOSignal signal, bool[] writeStates)
            {
                this.signal = signal;
                this.writeStates = writeStates;
            }
            public delegate void OnWriteDoneDelegate(bool[] states);
            public OnWriteDoneDelegate OnWriteDone;
            private bool disposedValue;

            protected virtual void Dispose(bool disposing)
            {
                if (!disposedValue)
                {
                    if (disposing)
                    {
                    }
                    OnWriteDone = null;
                    disposedValue = true;
                }
            }

            public void Dispose()
            {
                // 請勿變更此程式碼。請將清除程式碼放入 'Dispose(bool disposing)' 方法
                Dispose(disposing: true);
                GC.SuppressFinalize(this);
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
