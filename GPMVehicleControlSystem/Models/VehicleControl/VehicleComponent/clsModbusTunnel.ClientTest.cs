using Modbus.Data;
using Modbus.Device;
using Modbus.Message;
using System.IO.Ports;
using System.Reflection.Emit;

namespace GPMVehicleControlSystem.Models.VehicleControl.VehicleComponent
{
    public partial class clsModbusTunnel
    {
        private ModbusSerialMaster clientMaster;
        public bool InitializeTestClient()
        {
            try
            {
                SerialPort port = new SerialPort()
                {
                    PortName = "COM12",
                    BaudRate = 9600,
                    DataBits = 8,
                    Parity = Parity.None,
                    StopBits = StopBits.One
                };
                port.Open();
                clientMaster = ModbusSerialMaster.CreateRtu(port);

                Task.Factory.StartNew(async () =>
                {
                    while (true)
                    {
                        await Task.Delay(100);
                        ushort agvLocState = ReadAGVLocState30018();
                        Console.WriteLine(agvLocState.ToString());
                    }
                });

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public ushort ReadAGVLocState30018()
        {
            ushort[] agv_outputs_registers = clientMaster.ReadInputRegisters(ModbusID, (ushort)(AGVLocStateAddress-1), 1);
            if (agv_outputs_registers.Length > 0)
            {
                return agv_outputs_registers.First();
            }
            else
                return 404;
        }
    }
}
