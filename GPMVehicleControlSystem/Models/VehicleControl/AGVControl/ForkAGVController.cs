using AGVSystemCommonNet6;
using AGVSystemCommonNet6.AGVDispatch.Messages;
using AGVSystemCommonNet6.GPMRosMessageNet.Services;
using GPMVehicleControlSystem.Models.VehicleControl.AGVControl.ForkServices;
using System.Diagnostics;
using static AGVSystemCommonNet6.GPMRosMessageNet.Services.VerticalCommandRequest;

namespace GPMVehicleControlSystem.Models.VehicleControl.AGVControl
{
    public class ForkAGVController : SubmarinAGVControl
    {
        public ForkActionServiceBase verticalActionService;
        public ForkActionServiceBase HorizonActionService;
        public bool IsInitializing { get; set; }
        public ForkAGVController(string IP, int Port) : base(IP, Port)
        {
        }
        public override void AdertiseROSServices()
        {
            base.AdertiseROSServices();
        }

    }
}
