using GPMVehicleControlSystem.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace GPMVehicleControlSystem.Controllers.AGVInternal
{
    [Route("api/[controller]")]
    [ApiController]
    public class IMUController : ControllerBase
    {
        [HttpGet("ResetMAXMINRecord")]
        public async Task<IActionResult> ResetMAXMINRecord()
        {
            StaStored.CurrentVechicle.IMU.ResetMaxAndMinGvalRecord();
            return Ok();
        }
    }
}
