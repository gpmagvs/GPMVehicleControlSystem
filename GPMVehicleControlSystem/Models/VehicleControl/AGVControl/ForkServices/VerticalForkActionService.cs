using AGVSystemCommonNet6.GPMRosMessageNet.Messages;
using GPMVehicleControlSystem.Models.VehicleControl.Vehicles;
using NLog;
using RosSharp.RosBridgeClient;

namespace GPMVehicleControlSystem.Models.VehicleControl.AGVControl.ForkServices
{
    public class VerticalForkActionService : ForkActionServiceBase
    {
        public override double CurrentDriverSpeed => driverState?.speed ?? 0.0;
        public override double CurrentPosition => driverState?.position ?? 0.0;

        public VerticalForkActionService(Vehicle vehicle, RosSocket rosSocket) : base(vehicle, rosSocket)
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
