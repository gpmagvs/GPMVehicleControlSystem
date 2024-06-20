namespace GPMVehicleControlSystem.Models.VehicleControl.Vehicles.Params
{
    public class clsSensorBypass
    {
        public bool BeltSensorBypass { get; set; } = true;
        public bool LeftSideLaserBypass { get; set; } = false;
        public bool RightSideLaserBypass { get; set; } = false;

        /// <summary>
        /// Bypass安裝在Z柱下方的Sensor(用來偵測車子是否衝過頭卡到設備)
        /// </summary>
        public bool AGVBodyLimitSensorBypass { get; set; } = true;
    }
}
