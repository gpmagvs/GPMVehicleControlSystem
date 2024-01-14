namespace GPMVehicleControlSystem.Models.VehicleControl.Vehicles.Params
{
    public class clsImpactDetectionParams
    {
        public bool Enabled { get; set; } = false;
        /// <summary>
        /// 碰撞偵測閥值(單位:G)
        /// </summary>
        public double ThresHold { get; set; } = 2;

        public bool PitchErrorDetection { get; set; } = false;
    }
}
