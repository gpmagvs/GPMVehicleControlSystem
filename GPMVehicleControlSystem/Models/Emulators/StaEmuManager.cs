using AGVSystemCommonNet6.Log;

namespace GPMVehicleControlSystem.Models.Emulators
{
    public class StaEmuManager
    {
        // Add services to the container.
        public static WagoEmulator wagoEmu = new WagoEmulator();
        public static AGVROSEmulator agvRosEmu;
        public static MeasureServiceEmulator measureEmu;
        public static void StartWagoEmu(GPMVehicleControlSystem.VehicleControl.DIOModule.clsDIModule wagoDI)
        {
            wagoEmu.WagoDI = wagoDI;
            bool emu_actived = wagoEmu.Connect();
            if (emu_actived)
            {
                LOG.INFO("WAGO EMU Start(ModbusTCP Server :  tcp://127.0.0.1:9999)");
            }
        }

        public static void StartAGVROSEmu()
        {
            agvRosEmu = new AGVROSEmulator();
            LOG.INFO("AGVC(ROS) EMU Start");
        }

        public static void StartMeasureROSEmu()
        {
            measureEmu = new  MeasureServiceEmulator();
            LOG.INFO("AGVC(ROS) EMU Start");
        }

    }
}
