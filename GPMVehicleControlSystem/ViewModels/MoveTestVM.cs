namespace GPMVehicleControlSystem.ViewModels
{
    public class MoveTestVM
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Theta { get; set; }
        public int DestinPointID { get; set; }
        public int Direction { get; set; }
        public int LaserMode { get; set; }
        public double Speed { get; set; } = 1;
        public double? UltrasonicDistance { get; set; } = 30;
    }
}
