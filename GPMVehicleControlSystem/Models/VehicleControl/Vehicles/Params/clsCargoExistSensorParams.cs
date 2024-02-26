using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace GPMVehicleControlSystem.Models.VehicleControl.Vehicles.Params
{
    public class clsCargoExistSensorParams
    {


        public bool TraySensorMounted { get; set; } = true;
        public bool RackSensorMounted { get; set; } = false;

        [JsonConverter(typeof(StringEnumConverter))]
        public IO_CONEECTION_POINT_TYPE TraySensorPointType { get; set; } = IO_CONEECTION_POINT_TYPE.A;
        [JsonConverter(typeof(StringEnumConverter))]
        public IO_CONEECTION_POINT_TYPE RackSensorPointType { get; set; } = IO_CONEECTION_POINT_TYPE.A;
    }
}
