using AGVSystemCommonNet6.GPMRosMessageNet.Messages;
using NLog;
using RosSharp.RosBridgeClient;

namespace GPMVehicleControlSystem.Models.VehicleControl.AGVControl.ForkServices
{
    public class VerticalForkActionService : ForkActionServiceBase
    {
        private DriverState driverState;
        public override double CurrentDriverSpeed => driverState?.speed ?? 0.0;
        public override double CurrentPosition => driverState?.position ?? 0.0;

        public VerticalForkActionService(RosSocket rosSocket) : base(rosSocket)
        {
            logger = LogManager.GetCurrentClassLogger();

            rosSocket.Subscribe<ModuleInformation>("/module_information", callback);
        }

        private void callback(ModuleInformation t)
        {
            driverState = t.Action_Driver;
        }
    }
}
