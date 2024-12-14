using AGVSystemCommonNet6.AGVDispatch;
using GPMVehicleControlSystem.Models.VehicleControl.Vehicles.Params;
using Newtonsoft.Json;
using NLog;
using System.Text;

namespace GPMVehicleControlSystem.Models.VehicleControl.Vehicles
{
    public partial class Vehicle
    {
        private static SemaphoreSlim writeParamsToFileSemaphoreSlim = new SemaphoreSlim(1, 1);
        private clsVehicelParam _Parameters = new clsVehicelParam();
        public clsVehicelParam Parameters
        {
            get => _Parameters;
            set
            {
                _Parameters = value;
                //TryChangeAGVSOptions(value);
                //ModifyAGVSMessageEncoder(value.AGVsMessageEncoding);
                logger.LogInformation($"Parameters updated");
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
            Logger logger = LogManager.GetLogger(typeof(Vehicle).Name);
            try
            {
                var newEncoder = Encoding.GetEncoding(encoding);
                if (AGVSMessageFactory.Encoder != newEncoder)
                {
                    AGVSMessageFactory.Encoder = newEncoder;
                    logger.Info($"AGVS Message Encoder Changed to {AGVSMessageFactory.Encoder.EncodingName}");

                }
            }
            catch (Exception ex)
            {
                logger.Error($"Modify AGVS Message Encoder Fail..({ex.Message})");
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
        public static clsVehicelParam LoadParameters(string filepath = null)
        {
            Logger logger = LogManager.GetLogger(typeof(Vehicle).Name);
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
                logger.Trace("Parameters Load done");
                return Parameters;
            }
            catch (Exception ex)
            {
                logger.Error(ex);
                return null;
            }
        }

        public static async Task<(bool, string)> SaveParameters(clsVehicelParam Parameters)
        {
            try
            {
                await writeParamsToFileSemaphoreSlim.WaitAsync();
                string param_json = JsonConvert.SerializeObject(Parameters, Formatting.Indented);
                File.WriteAllText(ParametersFilePath, param_json);
                return (true, "");
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
            finally
            {
                writeParamsToFileSemaphoreSlim.Release();
            }
        }
    }
}
