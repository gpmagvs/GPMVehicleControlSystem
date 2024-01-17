using AGVSystemCommonNet6.Log;
using GPMVehicleControlSystem.Models;
using GPMVehicleControlSystem.Models.VehicleControl.Vehicles;
using GPMVehicleControlSystem.Models.VehicleControl.Vehicles.Params;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;

namespace GPMVehicleControlSystem.Controllers.AGVInternal
{
    [Route("api/[controller]")]
    [ApiController]
    public class SystemController : ControllerBase
    {
        [HttpGet("Settings")]
        public async Task<IActionResult> GetParameters()
        {
            return Ok(StaStored.CurrentVechicle.Parameters);
        }

        [HttpPost("SaveParameters")]
        public async Task<IActionResult> SaveParameters([FromBody] clsVehicelParam param)
        {
            try
            {
                StaStored.CurrentVechicle.Parameters = param;
                Vehicle.SaveParameters(param);
                return Ok(true);
            }
            catch (Exception)
            {
                return Ok(false);
            }
        }

        [HttpPost("CloseSystem")]
        public async Task<IActionResult> CloseSystem()
        {

            if (agv.Sub_Status == AGVSystemCommonNet6.clsEnums.SUB_STATUS.RUN || agv.Sub_Status == AGVSystemCommonNet6.clsEnums.SUB_STATUS.Initializing)
                return Ok(new { confirm = false, message = $"AGV當前狀態({agv.Sub_Status})禁止重新啟動系統!" });
            StaSysControl.SystemClose();
            return Ok(new { confirm = true, message = "" });
        }


        [HttpPost("RestartSystem")]
        public async Task<IActionResult> RestartSystem()
        {
            if (agv.Sub_Status == AGVSystemCommonNet6.clsEnums.SUB_STATUS.RUN|| agv.Sub_Status == AGVSystemCommonNet6.clsEnums.SUB_STATUS.Initializing)
                return Ok(new { confirm = false, message = $"AGV當前狀態({agv.Sub_Status})禁止重新啟動系統!" });
            StaSysControl.SystemRestart();
            return Ok(new { confirm = true, message = "" });
        }

      
        private Vehicle agv => StaStored.CurrentVechicle;
    }
}
