using GPMVehicleControlSystem.Models;
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
    }
}
