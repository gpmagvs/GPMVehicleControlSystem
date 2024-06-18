namespace GPMVehicleControlSystem.Models.VehicleControl.Vehicles.Params
{
    public class clsSensorBypass
    {
        public bool BeltSensorBypass { get; set; } = true;
        public bool LeftSideLaserBypass { get; set; } = false;
        public bool RightSideLaserBypass { get; set; } = false;
    }
}
