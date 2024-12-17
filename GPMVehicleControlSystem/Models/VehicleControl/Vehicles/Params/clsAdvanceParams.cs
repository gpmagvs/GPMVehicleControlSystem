namespace GPMVehicleControlSystem.Models.VehicleControl.Vehicles.Params
{
    public class clsAdvanceParams
    {
        public bool ShutDownPCWhenLowBatteryLevel { get; set; } = false;
        public bool AutoInitAndOnlineWhenMoveWithCargo { get; set; } = false;
        public bool AutoInitAndOnlineWhenMoveWithoutCargo { get; set; } = false;
    }
}
