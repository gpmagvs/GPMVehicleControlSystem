using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace GPMVehicleControlSystem.Models.VehicleControl.Vehicles.Params
{
    public class clsCargoExistSensorParams
    {
        public bool TraySensorMounted { get; set; } = true;
        public bool RackSensorMounted { get; set; } = false;
        public int RackSensorNumber { get; set; } = 2;
        public int TraySensorNumber { get; set; } = 4;

        [JsonConverter(typeof(StringEnumConverter))]
        public IO_CONEECTION_POINT_TYPE SensorPointType { get; set; } = IO_CONEECTION_POINT_TYPE.B;
    }
}
