using Modbus.Data;
using Modbus.Device;
using Modbus.Message;
using System.IO.Ports;
using System.Reflection.Emit;

namespace GPMVehicleControlSystem.Models.VehicleControl.VehicleComponent
{
    public partial class clsModbusTunnel
    {
        private SerialPort serialPort = new SerialPort();
        //AGV行走狀態位址
        public int AGVLocStateAddress = 30018;
        //將G-4514暫存區數值寫回AGV車(40000~40045)。
        public int G4514MesDataStartAddress = 40000;
        public int G4514MesDataLength = 46;

        private ModbusSerialSlave modbusRTUSlave;
        public byte ModbusID = 1;
        public bool Initialize(string PortName = "COM11")
        {
            try
            {
                serialPort.PortName = PortName;
                serialPort.BaudRate = 9600;
                serialPort.DataBits = 8;
                serialPort.Parity = Parity.None;
                serialPort.StopBits = StopBits.One;
                serialPort.Open();
                modbusRTUSlave = ModbusSerialSlave.CreateRtu(ModbusID, serialPort);
                modbusRTUSlave.Transport.Retries = 1;
                modbusRTUSlave.Transport.ReadTimeout = 1200;
                modbusRTUSlave.Transport.WriteTimeout = 1200;
                modbusRTUSlave.DataStore = DataStoreFactory.CreateDefaultDataStore();
                modbusRTUSlave.ListenAsync();
                modbusRTUSlave.WriteComplete += ModbusRTUSlave_WriteComplete;
                return true;
            }
            catch (Exception ex)
            {
                return false;
            }
        }

        private void ModbusRTUSlave_WriteComplete(object? sender, ModbusSlaveRequestEventArgs e)
        {
            ushort s1 = modbusRTUSlave.DataStore.HoldingRegisters[G4514MesDataStartAddress];
            ushort[] buffer = new ushort[G4514MesDataLength];
            Array.Copy(modbusRTUSlave.DataStore.HoldingRegisters.ToArray(), G4514MesDataStartAddress+1, buffer, 0, buffer.Length);
        }

        public void UpdateAGVPosition30018(bool IsReachMeasureLocation)
        {
            modbusRTUSlave.DataStore.InputRegisters[AGVLocStateAddress] = (ushort)(IsReachMeasureLocation ? 1 : 0);
        }
    }
}
