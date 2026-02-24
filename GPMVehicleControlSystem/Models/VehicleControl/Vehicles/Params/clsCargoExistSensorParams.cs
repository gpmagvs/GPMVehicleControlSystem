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
        public IO_CONTACT_TYPE SensorPointType { get; set; } = IO_CONTACT_TYPE.B;

        /// <summary>
        /// 模擬在席
        /// </summary>
        public bool ExistSensorSimulation { get; set; } = false;

        public bool GenerateCarrierIdWhenSensorTriggered { get; set; } = false;
    }
}
