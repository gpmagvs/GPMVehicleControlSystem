using AGVSystemCommonNet6.AGVDispatch;
using GPMVehicleControlSystem.Models.VehicleControl.Vehicles;
using GPMVehicleControlSystem.Models.VehicleControl.Vehicles.Params;
using Modbus.Device;
using NLog;
using System.Net.Sockets;
using static GPMVehicleControlSystem.Models.VehicleControl.Vehicles.Params.clsModbusDIOParams;
using static GPMVehicleControlSystem.Models.VehicleControl.Vehicles.Vehicle;

namespace GPMVehicleControlSystem.Models.VehicleControl.VehicleComponent
{
    public class clsEQHandshakeModbusTcp : IDisposable
    {
        private ModbusIpMaster master;
        private bool disposedValue;
        private clsAGVSConnection.clsEQOptions eqOptions;
        public static CancellationTokenSource HandshakingModbusTcpProcessCancel;
        public string IP { get; private set; } = "";
        public int Port { get; private set; }
        public clsModbusDIOParams options { get; } = new clsModbusDIOParams();
        public int EQTag { get; }
        public clsAGVSConnection AGVs { get; private set; }

        private Dictionary<Vehicle.AGV_HSSIGNAL, bool> aGVHsSignalStates;
        private Dictionary<Vehicle.EQ_HSSIGNAL, clsHandshakeSignalState> eQHsSignalStates;
        public bool Connected { get; private set; } = false;

        private Logger logger = LogManager.GetLogger("HandshakeWithModbusTcpLog");
        public clsEQHandshakeModbusTcp()
        {
            StaStored.ConnectingEQHSModbus = this;
        }
        public clsEQHandshakeModbusTcp(clsModbusDIOParams options, int EQTag, int ModbusPort = -1)
        {
            this.options = options;
            this.EQTag = EQTag;
            this.Port = ModbusPort;
            StaStored.ConnectingEQHSModbus = this;
            logger.Info($"clsEQHandshakeModbusTcp instance created! EQTag={EQTag},ModbusPort= {ModbusPort}");
        }
        public bool Start(clsAGVSConnection AGVS, Dictionary<AGV_HSSIGNAL, bool> aGVHsSignalStates, Dictionary<EQ_HSSIGNAL, clsHandshakeSignalState> eQHsSignalStates)
        {
            try
            {
                this.AGVs = AGVS;
                this.aGVHsSignalStates = aGVHsSignalStates;
                this.eQHsSignalStates = eQHsSignalStates;
                IP = AGVS.IP;
                logger.Info($"Start running. AGVS PC IP(Modbus Server IP)={IP}");
                if (Port == -1)
                {
                    GetEQInfoFromAGVs();
                    Port = eqOptions.AGVModbusGatewayPort;
                    logger.Warn($"ModbusServer Port not assign. Get EQ Info. from AGVs => Port ={Port}");
                }
                bool connected = Connected = Connect();
                if (!connected)
                {
                    return false;
                }
                logger.Info($"Modbus Server Connected! IOSignalExchangeProcess Start!");
                IOSignalExchangeProcess();
                return true;
            }
            catch (Exception ex)
            {
                logger.Error(ex);
                return false;
            }

        }
        private void GetEQInfoFromAGVs()
        {
            this.eqOptions = AGVs.GetEQInfo(EQTag).Result;
        }
        private async void IOSignalExchangeProcess()
        {
            await Task.Run(async () =>
            {
                HandshakingModbusTcpProcessCancel = new CancellationTokenSource();
                logger.Warn($"Handshake Signal excahge via ModbusTcp Process START");
                bool[] lastAGVOutputs = new bool[8];
                bool[] lastEQInputs = new bool[8];
                while (true)
                {
                    await Task.Delay(100);
                    if (HandshakingModbusTcpProcessCancel.IsCancellationRequested)
                    {
                        if (Connected)
                            WriteAGVOutputs(new bool[5]);
                        break;
                    }
                    if (!Connected)
                    {
                        await Task.Delay(1000);
                        Connected = Connect();
                        continue;
                    }
                    try
                    {
                        bool[] outputs = new bool[8];
                        outputs[0] = aGVHsSignalStates[AGV_HSSIGNAL.AGV_VALID];
                        outputs[1] = aGVHsSignalStates[AGV_HSSIGNAL.AGV_TR_REQ];
                        outputs[2] = aGVHsSignalStates[AGV_HSSIGNAL.AGV_BUSY];
                        outputs[3] = aGVHsSignalStates[AGV_HSSIGNAL.AGV_READY];
                        outputs[4] = aGVHsSignalStates[AGV_HSSIGNAL.AGV_COMPT];


                        string currentOuputsStr = string.Join(",", outputs);
                        string lastOuputsStr = string.Join(",", lastAGVOutputs);

                        if (currentOuputsStr != lastOuputsStr)
                        {
                            logger.Trace($"AGV HS Signal Changed=>AGV_VALID={outputs[0]},AGV_TR_REQ={outputs[1]},AGV_BUSY={outputs[2]},AGV_READY={outputs[3]},AGV_COMPT={outputs[4]}");
                        }
                        WriteAGVOutputs(outputs);
                        lastAGVOutputs = outputs.ToArray();
                        bool[] inputs = ReadEQOutputs();
                        eQHsSignalStates[EQ_HSSIGNAL.EQ_L_REQ].State = inputs[0];
                        eQHsSignalStates[EQ_HSSIGNAL.EQ_U_REQ].State = inputs[1];
                        eQHsSignalStates[EQ_HSSIGNAL.EQ_READY].State = inputs[2];
                        eQHsSignalStates[EQ_HSSIGNAL.EQ_BUSY].State = inputs[3];
                        string currentEQInputsStr = string.Join(",", inputs);
                        string lastEQInputsStr = string.Join(",", lastEQInputs);

                        if (currentEQInputsStr != lastEQInputsStr)
                        {
                            logger.Trace($"EQ HS Signal Changed=>EQ_L_REQ={inputs[0]},EQ_U_REQ={inputs[1]},EQ_READY={inputs[2]},EQ_BUSY={inputs[3]}");
                        }
                        lastEQInputs = inputs.ToArray();
                    }
                    catch (Exception ex)
                    {
                        logger.Error("clsEQHandshakeModbusTcp-IOSignalExchangeProcess Error", ex);
                        Disconnect();
                    }
                }
                Disconnect();
                logger.Warn($"Handshake Signal excahge via ModbusTcp Process END");
            });
        }
        private bool Connect()
        {
            try
            {
                logger.Trace($"[Modbus Tcp] Try connect to {IP}:{Port}");
                var client = new TcpClient(IP, Port);
                master = ModbusIpMaster.CreateIp(client);
                master.Transport.ReadTimeout = 1000;
                master.Transport.WriteTimeout = 1000;
                master.Transport.Retries = 2;
                return true;
            }
            catch (Exception ex)
            {
                return false;
            }
        }
        private void Disconnect()
        {
            try
            {
                master?.Transport.Dispose();
                master?.Dispose();
            }
            catch (Exception)
            {
                //Nothing
            }
            Connected = false;
        }
        private void WriteAGVOutputs(bool[] outputs)
        {
            if (options.IO_VAL_TYPE == IO_VALUE_TYPE.INPUT)
            {
                master.WriteMultipleCoils(options.Input_Write_Start, outputs);
            }
            else
            {
                var agv_handshake_ouputs = new bool[16]
                {
                    false,
                    false,
                    false,
                    false,
                    false,
                    false,
                    false,
                    false,
                     outputs[0],
                     outputs[1],
                     outputs[2],
                     outputs[3],
                     outputs[4],
                     outputs[5],
                     outputs[6],
                     outputs[7],
                };
                ushort WriteInValue = GetUshort(agv_handshake_ouputs);
                master.WriteSingleRegister(options.InputRegister_Write_Start, WriteInValue); //EasyModbus從0開始計算
            }
        }
        private bool[] ReadEQOutputs()
        {
            if (options.IO_VAL_TYPE == IO_VALUE_TYPE.INPUT)
            {
                bool[] inputs = master.ReadInputs(options.Input_Read_Start, options.Input_Read_Num);
                return inputs;
            }
            else
            {
                ushort[] input_registers = master.ReadInputRegisters(options.InputRegister_Read_Start, options.InputRegister_Read_Num);
                var _inputs = GetBoolArray(input_registers[0]);
                var eq_handshake_ouputs = new bool[8]
                {
                     _inputs[8],
                     _inputs[9],
                     _inputs[10],
                     _inputs[11],
                     _inputs[12],
                     _inputs[13],
                     _inputs[14],
                     _inputs[15],
                };

                return eq_handshake_ouputs;
            }
        }
        private ushort GetUshort(bool[] BoolArray)
        {
            bool[] NewSwitchArray = new bool[16];
            Array.Copy(BoolArray, 8, NewSwitchArray, 0, 8);
            Array.Copy(BoolArray, 0, NewSwitchArray, 8, 8);
            ushort ReturnData = 0;
            for (int i = 0; i < NewSwitchArray.Length; i++)
            {
                ReturnData += (ushort)(Convert.ToUInt16(Math.Pow(2, i)) * Convert.ToUInt16(NewSwitchArray[i]));
            }
            return ReturnData;

        }
        private bool[] GetBoolArray(ushort InputValue)
        {
            bool[] OutputData = new bool[16];
            bool[] OriginBoolArray = new bool[16];
            int BitInt = 0;
            while (InputValue > 0)
            {
                OriginBoolArray[BitInt] = InputValue % 2 == 1;
                BitInt++;
                InputValue /= 2;
            }
            Array.Copy(OriginBoolArray, 8, OutputData, 0, 8);
            Array.Copy(OriginBoolArray, 0, OutputData, 8, 8);
            return OutputData;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: 處置受控狀態 (受控物件)
                }

                // TODO: 釋出非受控資源 (非受控物件) 並覆寫完成項
                // TODO: 將大型欄位設為 Null
                master.Dispose();
                disposedValue = true;
            }
        }

        // // TODO: 僅有當 'Dispose(bool disposing)' 具有會釋出非受控資源的程式碼時，才覆寫完成項
        // ~clsModbusTcp()
        // {
        //     // 請勿變更此程式碼。請將清除程式碼放入 'Dispose(bool disposing)' 方法
        //     Dispose(disposing: false);
        // }

        public void Dispose()
        {
            // 請勿變更此程式碼。請將清除程式碼放入 'Dispose(bool disposing)' 方法
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
