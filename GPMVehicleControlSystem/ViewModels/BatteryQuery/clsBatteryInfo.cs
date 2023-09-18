namespace GPMVehicleControlSystem.ViewModels.BatteryQuery
{
    public class clsBatteryInfo
    {
        public double Level { get; set; }
        public double Voltage { get; set; }
        public double ChargeCurrent { get; set; }
        public double DischargeCurrent { get; set; }
        public double Temperature { get; set; }
        public DateTime Time { get; set; }
    }
}
