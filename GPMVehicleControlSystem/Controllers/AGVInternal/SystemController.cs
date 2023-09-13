using GPMVehicleControlSystem.Models;
using GPMVehicleControlSystem.Models.VehicleControl.Vehicles;
using GPMVehicleControlSystem.Models.VehicleControl.Vehicles.Params;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

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
    }
}
