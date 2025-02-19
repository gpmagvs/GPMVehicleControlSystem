using AGVSystemCommonNet6.MAP;
using GPMVehicleControlSystem.Models.VehicleControl.Vehicles;
using Newtonsoft.Json;
using NLog;

namespace GPMVehicleControlSystem.Models.NaviMap
{
    public static class MapStore
    {
        static Logger logger = LogManager.GetCurrentClassLogger();
        public static string GetMapUrl
        {
            get
            {
                return StaStored.CurrentVechicle.Parameters.VMSParam.MapUrl;
            }
        }

        public static bool SaveCurrentMap(Map map, out string path)
        {
            path = "";
            try
            {
                var json = JsonConvert.SerializeObject(map, Formatting.Indented);
                var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "param/temp");
                Directory.CreateDirectory(folder);
                path = Path.Combine(folder, $"{map.Name}.json");
                File.WriteAllText(path, json);
                logger.Info($"Save Map  from server to {path} success!");
                return true;
            }
            catch (Exception ex)
            {
                logger.Error($"Save Current Map Fail..{ex.Message}", ex);
                return false;
            }
        }

        public static async Task<Map> GetMapFromServer()
        {
            Map? map = null;
            try
            {
                using (HttpClient client = new HttpClient())
                {
                    client.Timeout = TimeSpan.FromSeconds(3);
                    var response = await client.GetAsync(GetMapUrl);
                    if (response.StatusCode == System.Net.HttpStatusCode.OK)
                    {
                        var jsonStr = await response.Content.ReadAsStringAsync();
                        var objDict = JsonConvert.DeserializeObject<Dictionary<string, object>>(jsonStr);
                        if (objDict.TryGetValue("Map", out var object_))
                        {
                            return JsonConvert.DeserializeObject<Map>(object_.ToString());
                        }
                        else
                        {
                            return JsonConvert.DeserializeObject<Map>(jsonStr);
                        }
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
                var objDict = JsonConvert.DeserializeObject<Dictionary<string, object>>(json);
                if (objDict.TryGetValue("Map", out var object_))
                {
                    return JsonConvert.DeserializeObject<Map>(object_.ToString());

                }
                else
                {
                    return JsonConvert.DeserializeObject<Map>(json);

                }
            }
            catch (Exception ex)
            {
                logger.Error($"GetMapFromFile Fail...{ex.Message}", ex);
                return emptyMap;
            }
        }
    }
}
