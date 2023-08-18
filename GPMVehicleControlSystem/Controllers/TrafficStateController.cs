using AGVSystemCommonNet6.AGVDispatch.Model;
using AGVSystemCommonNet6.Log;
using GPMVehicleControlSystem.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace GPMVehicleControlSystem.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TrafficStateController : ControllerBase
    {
        /// <summary>
        /// 接收派車系統上報之多車動態
        /// </summary>
        /// <returns></returns>
        [HttpPost("DynamicTrafficState")]
        public async Task<IActionResult> DynamicTrafficState(clsDynamicTrafficState traffic_state)
        {
            try
            {
                StaStored.CurrentVechicle.DynamicTrafficState = traffic_state;

            }
            catch (Exception ex)
            {
                LOG.ERROR(ex);
            }
            return Ok();
        }
    }
}
