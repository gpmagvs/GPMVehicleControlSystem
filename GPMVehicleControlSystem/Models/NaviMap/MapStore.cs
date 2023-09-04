using AGVSystemCommonNet6.Log;
using AGVSystemCommonNet6.MAP;
using GitVersion.Logging;
using GPMVehicleControlSystem.Models.VehicleControl.Vehicles;
using Newtonsoft.Json;

namespace GPMVehicleControlSystem.Models.NaviMap
{
    public static class MapStore
    {
        public static string GetMapUrl
        {
            get
            {
                return StaStored.CurrentVechicle.Parameters.VMSParam.MapUrl;
            }
        }

        public static async Task<Map> GetMapFromServer()
        {
            Map? map = null;
            try
            {
                using (HttpClient client = new HttpClient())
                {
                    var response = await client.GetAsync(GetMapUrl);
                    if (response.StatusCode == System.Net.HttpStatusCode.OK)
                    {
                        var jsonStr = await response.Content.ReadAsStringAsync();
                        map = JsonConvert.DeserializeObject<Map>(jsonStr);
                    }
                }
                return map;
            }
            catch (Exception ex)
            {
                return null;
            }

        }

        internal static Map? GetMapFromFile(string localMapFileFullName)
        {
            Map emptyMap = new Map()
            {
                Note = "empty"
            };
            try
            {
                if (!File.Exists(localMapFileFullName))
                {
                    return emptyMap;
                }
                var json = File.ReadAllText(localMapFileFullName);
                return JsonConvert.DeserializeObject<Map>(json);
            }
            catch (Exception ex)
            {
                LOG.ERROR($"GetMapFromFile Fail...{ex.Message}", ex);
                return emptyMap;
            }
        }
    }
}
