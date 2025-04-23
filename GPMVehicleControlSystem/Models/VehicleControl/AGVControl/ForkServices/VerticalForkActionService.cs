using NLog;
using RosSharp.RosBridgeClient;

namespace GPMVehicleControlSystem.Models.VehicleControl.AGVControl.ForkServices
{
    public class VerticalForkActionService : ForkActionServiceBase
    {
        public VerticalForkActionService(RosSocket rosSocket) : base(rosSocket)
        {
            logger = LogManager.GetCurrentClassLogger();
        }
    }
}
