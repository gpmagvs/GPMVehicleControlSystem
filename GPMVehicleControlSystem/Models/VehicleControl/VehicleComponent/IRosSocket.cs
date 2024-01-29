using RosSharp.RosBridgeClient;

namespace GPMVehicleControlSystem.Models.VehicleControl.VehicleComponent
{
    public interface IRosSocket
    {
        public RosSocket rosSocket { get; set; }
    }
}
