namespace GPMVehicleControlSystem.Models.VehicleControl.Vehicles.Params
{
    public class clsObstacleDetection
    {
        public bool Enable_Load { get; set; } = false;
        public bool Enable_UnLoad { get; set; } = false;
        /// <summary>
        /// 偵測秒數
        /// </summary>
        public int Duration { get; set; } = 4;
    }
}
