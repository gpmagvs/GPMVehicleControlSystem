using AGVSystemCommonNet6.AGVDispatch;
using AGVSystemCommonNet6.Log;
using GPMVehicleControlSystem.Models.Emulators;
using GPMVehicleControlSystem.Models.VehicleControl.Vehicles.Params;
using Newtonsoft.Json;
using System.Text;

namespace GPMVehicleControlSystem.Models.VehicleControl.Vehicles
{
    public partial class Vehicle
    {
        private clsVehicelParam _Parameters = new clsVehicelParam();
        public clsVehicelParam Parameters
        {
            get => _Parameters;
            set
            {
                _Parameters = value;
                TryChangeAGVSOptions(value);
                ModifyAGVSMessageEncoder(value.AGVsMessageEncoding);
                LOG.INFO($"Parameters updated");
            }
        }

        private void TryChangeAGVSOptions(clsVehicelParam value)
        {
            if (AGVS == null)
                return;
            AGVS.UseWebAPI = value.VMSParam.Protocol == VMS_PROTOCOL.GPM_VMS;
            AGVS.LocalIP = value.VMSParam.LocalIP;
            AGVS.IP = value.Connections[clsConnectionParam.CONNECTION_ITEM.AGVS].IP;
            AGVS.VMSPort = value.Connections[clsConnectionParam.CONNECTION_ITEM.AGVS].Port;
        }

        private static void ModifyAGVSMessageEncoder(string encoding)
        {
            try
            {
                var newEncoder = Encoding.GetEncoding(encoding);
                if (AGVSMessageFactory.Encoder != newEncoder)
                {
                    AGVSMessageFactory.Encoder = newEncoder;
                    LOG.INFO($"AGVS Message Encoder Changed to {AGVSMessageFactory.Encoder.EncodingName}");

                }
            }
            catch (Exception ex)
            {
                LOG.ERROR($"Modify AGVS Message Encoder Fail..({ex.Message})");
            }
        }

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
        public static Action<clsVehicelParam> OnParamEdited;
        public static clsVehicelParam LoadParameters(string filepath = null, bool watch_file_change = false)
        {
            try
            {
                clsVehicelParam? Parameters = new clsVehicelParam();
                string param_file = filepath != null ? filepath : ParametersFilePath.ToString();
                if (File.Exists(param_file))
                {
                    string param_json = File.ReadAllText(param_file);
                    Parameters = JsonConvert.DeserializeObject<clsVehicelParam>(param_json);
                }
                SaveParameters(Parameters);
                if (watch_file_change)
                {
                    InitWatchConfigFileChange(param_file);
                }
                return Parameters;
            }
            catch (Exception ex)
            {
                LOG.ERROR(ex);
                return null;
            }
        }

        static FileSystemWatcher configFileChangedWatcher;
        private static async void InitWatchConfigFileChange(string filePath)
        {
            configFileChangedWatcher = new FileSystemWatcher(Path.GetDirectoryName(filePath), Path.GetFileName(filePath));
            configFileChangedWatcher.Changed += ConfigFileChangedWatcher_Changed;
        }

        private static async void ConfigFileChangedWatcher_Changed(object sender, FileSystemEventArgs e)
        {
            configFileChangedWatcher.EnableRaisingEvents = false;
            await Task.Delay(1000);
            //copy 
            var file_copy = Path.Combine(Path.GetTempPath(), $"parameters_temp_{DateTime.Now.Ticks}.json");
            File.Copy(ParametersFilePath, file_copy, true);
            var updated_param = LoadParameters(file_copy, false);
            if (updated_param != null & OnParamEdited != null)
            {
                OnParamEdited(updated_param);
            }
            else
            {

            }
            configFileChangedWatcher.EnableRaisingEvents = true;
        }

        public static void SaveParameters(clsVehicelParam Parameters)
        {
            string param_json = JsonConvert.SerializeObject(Parameters, Formatting.Indented);
            File.WriteAllText(ParametersFilePath, param_json);
        }

        internal static void StartConfigChangedWatcher()
        {
            configFileChangedWatcher.EnableRaisingEvents = true;
        }
    }
}
