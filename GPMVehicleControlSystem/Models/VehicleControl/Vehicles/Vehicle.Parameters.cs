using AGVSystemCommonNet6.AGVDispatch;
using GPMVehicleControlSystem.Models.Exceptions;
using GPMVehicleControlSystem.Models.VehicleControl.Vehicles.Params;
using GPMVehicleControlSystem.Service;
using Microsoft.AspNetCore.SignalR;
using Newtonsoft.Json;
using NLog;
using System.Runtime.Serialization;
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
        public static async Task<clsVehicelParam> LoadParameters(string filepath = null)
        {
            Logger logger = LogManager.GetLogger(typeof(Vehicle).Name);
            clsVehicelParam? Parameters = new clsVehicelParam();
            string param_file = filepath != null ? filepath : ParametersFilePath.ToString();
            try
            {
                if (File.Exists(param_file))
                {
                    string param_json = File.ReadAllText(param_file);
                    Parameters = JsonConvert.DeserializeObject<clsVehicelParam>(param_json);
                    if (Parameters != null)
                        await SaveParameters(Parameters);
                    else
                        throw new VehicleInstanceInitializeFailException($"Load Param Fail! Null refrence( Parameters equal Null when 'JsonConvert.DeserializeObject<clsVehicelParam>(param_json);' code line invoked)");
                }
                else
                    throw new VehicleInstanceInitializeFailException($"Load Param Fail! File not found({param_file})");

                logger.Trace("Parameters Load done");
                return Parameters;
            }
            catch (JsonReaderException ex)
            {
                throw new VehicleInstanceInitializeFailException($"車載系統參數異常!請確認{param_file}內容({ex.Message})");
            }
            catch (VehicleInstanceInitializeFailException ex)
            {
                throw ex;
            }
            catch (Exception ex)
            {
                logger.Error(ex);
                throw new VehicleInstanceInitializeFailException($"讀取系統參數異常!({ex.Message})");
            }
        }

        public static async Task<(bool, string)> SaveParameters(clsVehicelParam Parameters, IHubContext<FrontendHub> hubContext = null)
        {
            try
            {
                await writeParamsToFileSemaphoreSlim.WaitAsync();
                string param_json = JsonConvert.SerializeObject(Parameters, Formatting.Indented);
                File.WriteAllText(ParametersFilePath, param_json);

                if (hubContext != null)
                {
                    try
                    {
                        await hubContext.Clients.All.SendAsync("ParameterChanged", Parameters);
                    }
                    catch (Exception ex)
                    {
                        StaStored.CurrentVechicle.logger.LogError(ex, ex.Message);
                    }
                }

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
