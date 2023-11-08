using AGVSystemCommonNet6.AGVDispatch;
using AGVSystemCommonNet6.HttpTools;
using AGVSystemCommonNet6.Log;
using GPMVehicleControlSystem.Models.VehicleControl.Vehicles;
using GPMVehicleControlSystem.Models.VehicleControl.Vehicles.Params;
using Modbus.Device;
using Modbus.Extensions.Enron;
using System.Diagnostics.Metrics;
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
        private Dictionary<Vehicle.EQ_HSSIGNAL, bool> eQHsSignalStates;

        public clsEQHandshakeModbusTcp(clsModbusDIOParams options, int EQTag, int ModbusPort = -1)
        {
            this.options = options;
            this.EQTag = EQTag;
            this.Port = ModbusPort;
        }
        public bool Start(clsAGVSConnection AGVS, Dictionary<AGV_HSSIGNAL, bool> aGVHsSignalStates, Dictionary<EQ_HSSIGNAL, bool> eQHsSignalStates)
        {
            try
            {
                this.AGVs = AGVS;
                this.aGVHsSignalStates = aGVHsSignalStates;
                this.eQHsSignalStates = eQHsSignalStates;
                IP = AGVS.IP;
                if (Port == -1)
                {
                    GetEQInfoFromAGVs();
                    Port = eqOptions.AGVModbusGatewayPort;
                }
                bool connected = Connect();
                if (!connected)
                {
                    return false;
                }
                IOSignalExchangeProcess();
                return true;
            }
            catch (Exception ex)
            {
                LOG.ERROR(ex);
                return false;
            }

        }
        private void GetEQInfoFromAGVs()
        {
            this.eqOptions = AGVs.GetEQInfo(EQTag).Result;
        }
        private async void IOSignalExchangeProcess()
        {
            await Task.Factory.StartNew(async () =>
            {
                HandshakingModbusTcpProcessCancel = new CancellationTokenSource();
                LOG.WARN($"Handshake Signal excahge via ModbusTcp Process START");
                while (true)
                {
                    await Task.Delay(100);
                    if (HandshakingModbusTcpProcessCancel.IsCancellationRequested)
                        break;
                    bool[] outputs = new bool[8];
                    outputs[0] = aGVHsSignalStates[AGV_HSSIGNAL.AGV_VALID];
                    outputs[1] = aGVHsSignalStates[AGV_HSSIGNAL.AGV_TR_REQ];
                    outputs[2] = aGVHsSignalStates[AGV_HSSIGNAL.AGV_BUSY];
                    outputs[3] = aGVHsSignalStates[AGV_HSSIGNAL.AGV_READY];
                    outputs[4] = aGVHsSignalStates[AGV_HSSIGNAL.AGV_COMPT];
                    WriteAGVOutputs(outputs);
                    var inputs = ReadEQOutputs();
                    eQHsSignalStates[EQ_HSSIGNAL.EQ_L_REQ] = inputs[0];
                    eQHsSignalStates[EQ_HSSIGNAL.EQ_U_REQ] = inputs[1];
                    eQHsSignalStates[EQ_HSSIGNAL.EQ_READY] = inputs[2];
                    eQHsSignalStates[EQ_HSSIGNAL.EQ_BUSY] = inputs[3];

                }
                LOG.WARN($"Handshake Signal excahge via ModbusTcp Process END");
            });
        }
        private bool Connect()
        {
            try
            {
                LOG.TRACE($"[Modbus Tcp] Try connect to {IP}:{Port}");
                var client = new TcpClient(IP, Port);
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
