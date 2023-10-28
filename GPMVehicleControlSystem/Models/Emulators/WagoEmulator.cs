using AGVSystemCommonNet6.Abstracts;
using GPMVehicleControlSystem.VehicleControl.DIOModule;
using Modbus.Device;
using System.Net;
using System.Net.Sockets;
using static GPMVehicleControlSystem.VehicleControl.DIOModule.clsDIModule;

namespace GPMVehicleControlSystem.Models.Emulators
{
    public class WagoEmulator : Connection
    {
        ModbusTcpSlave? slave;

        Dictionary<DI_ITEM, int> INPUT_INDEXS;

        public clsDIModule WagoDI { get; internal set; }

        public override string alarm_locate_in_name => throw new NotImplementedException();

        public WagoEmulator()
        {
            INPUT_INDEXS = Enum.GetValues(typeof(DI_ITEM)).Cast<DI_ITEM>().ToDictionary(e => e, e => (int)e);
        }
        public override async Task<bool> Connect()
        {
            IPAddress iPAddress = IPAddress.Parse("127.0.0.1");
            int port = 9999;
            TcpListener tcpListener = new TcpListener(iPAddress, port);
            tcpListener.Start();
            try
            {
                slave = ModbusTcpSlave.CreateTcp(1, tcpListener);
                InitializeInputState();
                slave.ModbusSlaveRequestReceived += Slave_ModbusSlaveRequestReceived;
                Task.Run(() =>
                {
                    slave.ListenAsync().Wait();
                });
                return true;
            }
            catch (Exception ex)
            {
                slave = null;
                return false;
            }
        }
        private void InitializeInputState()
        {
            SetState(DI_ITEM.EMO, true);
            SetState(DI_ITEM.Bumper_Sensor, true);
            SetState(DI_ITEM.Horizon_Motor_Switch, true);
            SetState(DI_ITEM.FrontProtection_Area_Sensor_1, true);
            SetState(DI_ITEM.FrontProtection_Area_Sensor_2, true);
            SetState(DI_ITEM.FrontProtection_Area_Sensor_3, true);
            SetState(DI_ITEM.FrontProtection_Area_Sensor_4, true);
            SetState(DI_ITEM.BackProtection_Area_Sensor_1, true);
            SetState(DI_ITEM.BackProtection_Area_Sensor_2, true);
            SetState(DI_ITEM.BackProtection_Area_Sensor_3, true);
            SetState(DI_ITEM.BackProtection_Area_Sensor_4, true);
            SetState(DI_ITEM.RightProtection_Area_Sensor_3, true);
            SetState(DI_ITEM.LeftProtection_Area_Sensor_3, true);
            SetState(DI_ITEM.Fork_Frontend_Abstacle_Sensor, true);

            SetState(DI_ITEM.Cst_Sensor_1, true);
            SetState(DI_ITEM.Cst_Sensor_2, true);

            SetState(DI_ITEM.Fork_RACK_Right_Exist_Sensor, true);
            SetState(DI_ITEM.Fork_RACK_Left_Exist_Sensor, true);
        }
        public void SetState(DI_ITEM item, bool state)
        {
            try
            {
                var index = WagoDI.Indexs[item];
                slave.DataStore.InputDiscretes[index + 1] = state;
            }
            catch (Exception ex)
            {
            }
        }
        private void Slave_ModbusSlaveRequestReceived(object? sender, ModbusSlaveRequestEventArgs e)
        {
            var inputs = slave.DataStore.InputDiscretes;
        }

        public override void Disconnect()
        {
            slave.Dispose();
            slave = null;
        }

        public override bool IsConnected()
        {
            return slave != null;
        }
    }
}
