namespace GPMVehicleControlSystem.Models.VehicleControl.Vehicles
{
    public partial class TsmcMiniAGV
    {
        public class clsMeasureResult
        {
            public string result { get; set; } = "";
            public string location { get; set; } = "";
            public int illuminance { get; set; }
            public int decibel { get; set; }
            public double temperature { get; set; }
            public double humudity { get; set; }
            public int IPA { get; set; }
            public double TVOC { get; set; }
            public string time { get; set; } = "";
            public int partical_03um { get; set; }
            public int partical_05um { get; set; }
            public int partical_10um { get; set; }
            public int partical_30um { get; set; }
            public int partical_50um { get; set; }
            public int partical_100um { get; set; }
        }
    }
}
