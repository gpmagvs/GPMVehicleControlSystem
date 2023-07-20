namespace GPMVehicleControlSystem.ViewModels
{
    public class BatteryStateVM
    {
        public int BatteryLevel { get; set; }
        public bool IsCharging { get; set; }
        public int ChargeCurrent { get; set; }
        public bool IsError { get; set; }
        public bool CircuitOpened { get; set; }
        public ushort BatteryID { get; internal set; }

        public BatteryPositionInfoVM SensorInfo { get; set; } = new BatteryPositionInfoVM();
    }
    public class BatteryPositionInfoVM
    {
        public bool IsExistSensor1ON { get; set; }
        public bool IsExistSensor2ON { get; set; }

        public bool IsDockingSensor1ON { get; set; }
        public bool IsDockingSensor2ON { get; set; }

        public bool IsLockSensorON { get; set; }
        public bool IsUnlockSensorON { get; set; }
    }
}
