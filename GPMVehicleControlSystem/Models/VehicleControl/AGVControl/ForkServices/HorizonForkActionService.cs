using NLog;
using RosSharp.RosBridgeClient;

namespace GPMVehicleControlSystem.Models.VehicleControl.AGVControl.ForkServices
{
    public class HorizonForkActionService : ForkActionServiceBase
    {
        public HorizonForkActionService(RosSocket rosSocket) : base(rosSocket)
        {
            logger = LogManager.GetCurrentClassLogger();
        }
        protected override string CommandActionServiceName { get; set; } = "/horizon_command_action";
        protected override string DoneActionServiceName { get; set; } = "/horizon_done_action";
    }
}
