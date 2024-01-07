using GPMVehicleControlSystem.Tools;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using AGVSystemCommonNet6.MAP;
using System.Net.NetworkInformation;
using GPMVehicleControlSystem.Models.NaviMap;
using GPMVehicleControlSystem.Models;
using AGVSystemCommonNet6.HttpTools;

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
                return Ok(MapManager.LoadMapFromFile(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "param/Map_UMTC_3F_Yellow.json"), out string msg, false));
            }
        }
        [HttpGet("UploadCoordination")]
        public async Task<IActionResult> UploadCoordintaion(string AGVName, int tagNumber, double x, double y, double theta)
        {
            string agvsHost = $"{StaStored.CurrentVechicle.AGVS.IP}:5216";
            string url = $"/api/Map/UploadCoordination?AGVName={AGVName}&tagNumber={tagNumber}&x={x}&y={y}&theta={theta}";
            HttpHelper http = new HttpHelper($"http://{agvsHost}");
            bool response = await http.GetAsync<bool>(url);
            return Ok(response);
        }
        [HttpGet("GetNormalStations")]
        public async Task<IActionResult> GetNormalStations()
        {
            var datas = StaStored.CurrentVechicle.NavingMap.Points.Values.Where(pt => pt.StationType == AGVSystemCommonNet6.AGVDispatch.Messages.STATION_TYPE.Normal)
                                                             .Where(pt => !pt.IsVirtualPoint)
                                                             .Select(pt => new { tag = pt.TagNumber, name = pt.Graph.Display }).ToList();
            datas = datas.OrderBy(pt => pt.tag).ToList();
            return Ok(datas);
        }
    }
}
