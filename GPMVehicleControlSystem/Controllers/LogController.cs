using GPMVehicleControlSystem.Models.Log;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace GPMVehicleControlSystem.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class LogController : ControllerBase
    {

        [HttpPost("Query")]
        public async Task<IActionResult> QueryLog([FromBody] clsLogQueryOptions option)
        {
            clsLogQueryResults result = await LogService.QueryLog(option);
            return Ok(result);
        }

    }
}
