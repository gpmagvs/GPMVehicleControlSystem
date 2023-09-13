using GPMVehicleControlSystem.Models.VehicleControl.Vehicles.Params;
using Newtonsoft.Json;

namespace GPMVehicleControlSystem.Models.VehicleControl.Vehicles
{
    public partial class Vehicle
    {
        public clsVehicelParam Parameters { get; internal set; } = new clsVehicelParam();
        public const string ParamFileName = "VCS_Params.json";
        public static string ParametersFilePath
        {
            get
            {
                var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "param");
                Directory.CreateDirectory(folder);
                return Path.Combine(folder, ParamFileName);
            }
        }
        public static clsVehicelParam LoadParameters()
        {
            clsVehicelParam? Parameters = new clsVehicelParam();
            string param_file = ParametersFilePath.ToString();
            if (File.Exists(param_file))
            {
                string param_json = File.ReadAllText(param_file);
                Parameters = JsonConvert.DeserializeObject<clsVehicelParam>(param_json);
            }
            SaveParameters(Parameters);
            return Parameters;
        }
        public static void SaveParameters(clsVehicelParam Parameters)
        {
            string param_json = JsonConvert.SerializeObject(Parameters, Formatting.Indented);
            File.WriteAllText(ParametersFilePath, param_json);
        }
    }
}
