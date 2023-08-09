using GPMVehicleControlSystem.Tools;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using AGVSystemCommonNet6.MAP;
using System.Net.NetworkInformation;
using GPMVehicleControlSystem.Models.NaviMap;
using GPMVehicleControlSystem.Models;

namespace GPMVehicleControlSystem.Controllers.AGVInternal
{
    [Route("api/[controller]")]
    [ApiController]
    public class MapController : ControllerBase
    {

        [HttpGet("GetMapFromServer")]
        public async Task<IActionResult> GetMap()
        {
            try
            {
                return Ok(StaStored.CurrentVechicle.NavingMap);
            }
            catch (Exception ex)
            {
                return Ok(MapManager.LoadMapFromFile(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "param/Map_UMTC_3F_Yellow.json")));
            }
        }
        [HttpGet("UploadCoordination")]
        public async Task<IActionResult> UploadCoordintaion(string AGVName, int tagNumber, double x, double y, double theta)
        {
            string agvsHost = $"{StaStored.CurrentVechicle.AGVS.IP}:5216";
            string url = $"http://{agvsHost}/api/Map/UploadCoordination?AGVName={AGVName}&tagNumber={tagNumber}&x={x}&y={y}&theta={theta}";
            bool response = await AGVSystemCommonNet6.HttpHelper.Http.GetAsync<bool>(url);
            return Ok(response);
        }
    }
}
