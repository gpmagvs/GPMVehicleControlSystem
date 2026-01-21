using AGVSystemCommonNet6;
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
                return Path.GetFullPath(Path.Combine(folder, ParamFileName));
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
                    Parameters.Descrption = new clsVehicelParam().Descrption; //確保 Descrption 欄位不會因為版本不同而遺失
                    if (Parameters != null)
                        await SaveParameters(Parameters);
                    else
                        throw new VehicleInstanceInitializeFailException($"Load Param Fail! Null refrence( Parameters equal Null when 'JsonConvert.DeserializeObject<clsVehicelParam>(param_json);' code line invoked)");
                }
                else
                    throw new VehicleInstanceInitializeFailException($"Load Param Fail! File not found({param_file})");

                logger.Trace("Parameters Load done");

                //參數檢查
                var agvsConnection = Parameters.Connections[clsConnectionParam.CONNECTION_ITEM.AGVS];
                if (Parameters.VMSParam.Protocol == VMS_PROTOCOL.KGS && !Parameters.VMSParam.MapUrl.ToLower().Contains(":6600/map/get"))
                {
                    logger.Warn($"檢查到圖資 API Url 不正確:對應 KGS 派車系統但 Url 未包含 '6600/map/get'");
                    Parameters.VMSParam.MapUrl = $"http://{agvsConnection.IP}:6600/Map/Get";
                    logger.Info($"圖資 API Url 修正結果:{Parameters.VMSParam.MapUrl}");
                }
                if (Parameters.VMSParam.Protocol == VMS_PROTOCOL.GPM_VMS && !Parameters.VMSParam.MapUrl.ToLower().Contains(":5216/api/map"))
                {
                    logger.Warn($"檢查到圖資 API Url 不正確:對應 GPM 派車系統但 Url 未包含 ':5216/api/Map'");
                    Parameters.VMSParam.MapUrl = $"http://{agvsConnection.IP}:5216/api/Map";
                    logger.Info($"圖資 API Url 修正結果:{Parameters.VMSParam.MapUrl}");
                }

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

                var _originalInputContactTypeDefinesJson = StaStored.CurrentVechicle.Parameters.InputContactTypeDefines.ToJsonWithNewtonsoft(Formatting.None);
                var _newInputContactTypeDefinesJson = Parameters.InputContactTypeDefines.ToJsonWithNewtonsoft(Formatting.None);

                if (_originalInputContactTypeDefinesJson != _newInputContactTypeDefinesJson)
                {
                    StaStored.CurrentVechicle.WagoDI.RegistSignalEvents(out string _msg);
                    StaStored.CurrentVechicle.LogDebugMessage($"INPUT CONTACT TPE 有變更 -> 重新註冊 INPUT 事件", true);
                }

                string param_json = JsonConvert.SerializeObject(Parameters, Formatting.Indented);
                File.WriteAllText(ParametersFilePath, param_json);
                StaStored.CurrentVechicle.Parameters = Parameters;

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
