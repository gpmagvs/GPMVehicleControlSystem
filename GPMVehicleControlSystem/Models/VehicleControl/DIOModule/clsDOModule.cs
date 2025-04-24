using AGVSystemCommonNet6;
using AGVSystemCommonNet6.Vehicle_Control.VCS_ALARM;
using GPMVehicleControlSystem.Models.VehicleControl.DIOModule;
using Modbus.Device;
using NLog;
using System.Collections.Concurrent;
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
            logger = LogManager.GetLogger("DOModule");
            //ReadCurrentDOStatus();
        }
        public override bool Connected { get => _Connected; set => _Connected = value; }
        public override string alarm_locate_in_name => "DO Module";


        public List<DO_ITEM> laserModeDoBag { get; private set; } = new List<DO_ITEM>() {
                DO_ITEM.FrontBack_Protection_Sensor_CIN_1,
                DO_ITEM.FrontBack_Protection_Sensor_CIN_2,
                DO_ITEM.FrontBack_Protection_Sensor_CIN_3,
                DO_ITEM.FrontBack_Protection_Sensor_CIN_4,
                DO_ITEM.FrontBack_Protection_Sensor_IN_1,
                DO_ITEM.FrontBack_Protection_Sensor_IN_2,
                DO_ITEM.FrontBack_Protection_Sensor_IN_3,
                DO_ITEM.FrontBack_Protection_Sensor_IN_4,
                DO_ITEM.Side_Protection_Sensor_IN_1,
                DO_ITEM.Side_Protection_Sensor_IN_2,
                DO_ITEM.Side_Protection_Sensor_IN_3,
                DO_ITEM.Side_Protection_Sensor_IN_4,
                DO_ITEM.Left_Protection_Sensor_IN_1,
                DO_ITEM.Left_Protection_Sensor_IN_2,
                DO_ITEM.Left_Protection_Sensor_IN_3,
                DO_ITEM.Left_Protection_Sensor_IN_4,
            };


        public List<DO_ITEM> needConfirmWhenSwitchStateManual { get; private set; } = new List<DO_ITEM>() {
                DO_ITEM.FrontBack_Protection_Sensor_CIN_1,
                DO_ITEM.FrontBack_Protection_Sensor_CIN_2,
                DO_ITEM.FrontBack_Protection_Sensor_CIN_3,
                DO_ITEM.FrontBack_Protection_Sensor_CIN_4,
                DO_ITEM.FrontBack_Protection_Sensor_IN_1,
                DO_ITEM.FrontBack_Protection_Sensor_IN_2,
                DO_ITEM.FrontBack_Protection_Sensor_IN_3,
                DO_ITEM.FrontBack_Protection_Sensor_IN_4,
                DO_ITEM.Side_Protection_Sensor_IN_1,
                DO_ITEM.Side_Protection_Sensor_IN_2,
                DO_ITEM.Side_Protection_Sensor_IN_3,
                DO_ITEM.Side_Protection_Sensor_IN_4,
                DO_ITEM.Left_Protection_Sensor_IN_1,
                DO_ITEM.Left_Protection_Sensor_IN_2,
                DO_ITEM.Left_Protection_Sensor_IN_3,
                DO_ITEM.Left_Protection_Sensor_IN_4,
                DO_ITEM.Vertical_Motor_Free,
                DO_ITEM.Front_Protection_Sensor_Reset,
                DO_ITEM.Back_Protection_Sensor_Reset,
            };



        internal override void RegistSignalEvents()
        {
        }
        public override void ReadIOSettingsFromIniFile()
        {
            try
            {
                Start = ushort.Parse(iniHelper.GetValue("OUTPUT", "Start"));
                Size = ushort.Parse(iniHelper.GetValue("OUTPUT", "Size"));

                int.TryParse(iniHelper.GetValue("OUTPUT", "ShiftStart"), out ShiftStart);
                int.TryParse(iniHelper.GetValue("OUTPUT", "ShiftSize"), out ShiftSize);

                logger.Info($"DO Shift Start = {ShiftStart}, Shift Size = {ShiftSize}");

                var do_names = Enum.GetValues(typeof(DO_ITEM)).Cast<DO_ITEM>().Select(i => i.ToString()).ToList();
                for (ushort i = 0; i < Size; i++)
                {
                    var Address = $"Y{i.ToString("X4")}";
                    var RigisterName = iniHelper.GetValue("OUTPUT", Address);
                    var reg = new clsIOSignal(RigisterName, Address);
                    reg.index = i;
                    reg.State = false;
                    reg.manualToggleEnable = !IsLaserModeSwitchIO(reg);
                    reg.manualToggleNeedConfirmed = IsNeedConfirmWhenSwitchStateManual(reg);
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
                if (!Indexs.TryGetValue(signal, out int index))
                {
                    logger.Warn($"{signal} 註冊事件失敗因為未定義該訊號的OUTPUT位置");
                    return;
                }

                VCSOutputs[Indexs[signal]].OnStateChanged += handler;
            }
            catch (Exception ex)
            {
                logger.Error("Digital Output - [" + signal + "] Sbuscribe Error.", ex);
            }

        }
        private Task OutputWriteWorkTask;
        public override async Task<bool> Connect()
        {
            bool connected = _Connected = await base.Connect();
            if (OutputWriteWorkTask == null)
            {
                if (connected)
                    SyncCoilsFromModule();
                OutputWriteWorkTask = Task.Run(() => OutputWriteWorker());
            }
            return connected;
        }

        private void SyncCoilsFromModule()
        {
            try
            {

                logger.Info($"Start Sync Coils From Module | Start-{Start},Size-{Size}");
                bool[] states = master?.ReadCoils(Start, Size);
                int index = 0;
                logger.Trace(string.Join(",", states.Select(b => b ? 1 : 0)));
                foreach (bool _bolState in states)
                {
                    clsIOSignal? _DO = VCSOutputs.FirstOrDefault(k => k.index == index);
                    if (_DO != null)
                    {
                        _DO.State = _bolState;
                    }
                    index += 1;
                }
                logger.Info("Sync Coils From Module done.");
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Sync Coils From Module Fail : " + ex.Message);
            }
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
                    logger.Warn("DO Module try reconnecting..");
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
                    logger.Error($"DO Read Coils Fail: {ex.Message}");
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
                        logger.Error($"Error writing to device: {ex.Message}", ex);
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
                await master?.WriteMultipleCoilsAsync(startAddress, writeStates);
                if (tim.IsCancellationRequested)
                {
                    logger.Error($"Connected:{Connected} DO-" + to_handle_obj.signal.index + "Wago_IO_Write_Fail Error.");
                    throw new Exception($"DO Write Timeout.");
                }
            }
            updateDOState();
            if (to_handle_obj.OnWriteDone != null)
                to_handle_obj.OnWriteDone(rollback);


            async Task updateDOState()
            {

                for (int i = 0; i < writeStates.Length; i++)
                {
                    clsIOSignal? _DO = VCSOutputs.FirstOrDefault(k => k.index == to_handle_obj.signal.index + i);
                    if (_DO != null)
                    {
                        _DO.State = writeStates[i];
                    }
                }
            }

        }

        public async Task<bool> SetState(string address, bool[] writeStates)
        {
            try
            {
                await semaphore.WaitAsync();
                if (Current_Alarm_Code != AlarmCodes.None)
                    return false;
                clsIOSignal? DO = VCSOutputs.FirstOrDefault(k => k.Address == address);
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
                logger.Error($"DO Start From {address} Write {(string.Join(",", writeStates))} Fail Error.{ex.Message + ex.StackTrace}");
                Current_Alarm_Code = AlarmCodes.Wago_IO_Write_Fail;
                return false;
            }
            finally
            {
                semaphore.Release();
            }
        }
        public async Task<bool> SetState(string address, bool state)
        {
            return await SetState(address, new bool[] { state });

            //clsIOSignal? DO = VCSOutputs.FirstOrDefault(k => k.Address == address);
            //if (DO.Output == null)
            //{
            //    logger.Warn($"Output-{address} not defined, no write to IO module");
            //    return false;
            //}
            //return await SetState(DO.Output, new bool[] { state });
        }

        public async Task<bool> SetState(DO_ITEM signal, bool state)
        {
            return await SetState(signal, new bool[] { state });
        }

        public async Task<bool> SetState(DOWriteRequest modifyRequest)
        {
            if (!modifyRequest.isMultiModify)
            {
                return await SetState(modifyRequest.firstModify.signal.Output, modifyRequest.firstModify.state);
            }
            else
            {
                int startIndex = modifyRequest.startIndex;
                int endIndex = modifyRequest.endIndex;

                int totalLen = endIndex - startIndex + 1;
                //取出範圍
                List<clsIOSignal> inRangeOuputs = VCSOutputs.Skip(startIndex).Take(totalLen).ToList().Clone();
                foreach (var item in modifyRequest.toModifyItems)
                {
                    var toModify = inRangeOuputs.FirstOrDefault(i => i.Output == item.signal.Output);
                    if (toModify == null)
                        continue;
                    toModify.State = item.state;
                }
                bool[] currentStsteInRange = inRangeOuputs.Select(v => v.State).ToArray();
                return await SetState(inRangeOuputs.First().Output, currentStsteInRange);
            }
        }

        internal async Task<bool> SetState(DO_ITEM start_signal, bool[] writeStates)
        {
            try
            {
                if (Current_Alarm_Code != AlarmCodes.None)
                    return false;
                clsIOSignal? DO = VCSOutputs.FirstOrDefault(k => k.Name == start_signal.ToString());
                if (DO != null)
                {
                    return await SetState(DO.Address, writeStates);
                }
                else
                {
                    return false;
                }
            }
            catch (Exception ex)
            {
                logger.Error($"DO Start From {start_signal} Write {(string.Join(",", writeStates))} Fail Error.{ex.Message + ex.StackTrace}");
                Current_Alarm_Code = AlarmCodes.Wago_IO_Write_Fail;
                return false;
            }
        }
        internal override (bool confirm, string message) UpdateSignalMap(Dictionary<int, string> newOutputMap)
        {
            var newCoolection = newOutputMap.Select(x => new clsIOSignal(x.Value, $"Y{x.Key.ToString("X4")}")
            {
                index = (ushort)x.Key
            }).ToList();
            VCSOutputs = newCoolection;
            UpdateIniFile();
            return (true, "");
        }
        protected override bool UpdateIniFile()
        {
            for (int i = 0; i < VCSOutputs.Count; i++)
            {
                var input = VCSOutputs[i];
                if (input.Name == "")
                    iniHelper.RemoveKey("OUTPUT", input.Address, out string errMsg);
                else
                    iniHelper.SetValue("OUTPUT", input.Address, input.Name, out string errMsg);
            }
            return true;
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
                logger.Info($"Wago DO All OFF Done.");
            }
        }

        internal bool IsLaserModeSwitchIO(string address)
        {
            return IsLaserModeSwitchIO(VCSOutputs.FirstOrDefault(v => v.Address == address));
        }
        internal bool IsLaserModeSwitchIO(clsIOSignal outputSignalWrapper)
        {
            if (outputSignalWrapper == null)
                return false;
            return laserModeDoBag.Contains(outputSignalWrapper.Output);
        }

        internal bool IsNeedConfirmWhenSwitchStateManual(string address)
        {
            return IsNeedConfirmWhenSwitchStateManual(VCSOutputs.FirstOrDefault(v => v.Address == address));
        }
        internal bool IsNeedConfirmWhenSwitchStateManual(clsIOSignal outputSignalWrapper)
        {
            if (outputSignalWrapper == null)
                return false;
            return needConfirmWhenSwitchStateManual.Contains(outputSignalWrapper.Output);
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
