using GPMVehicleControlSystem.ViewModels.BatteryQuery;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace GPMVehicleControlSystem.Controllers.AGVInternal
{
    [Route("api/[controller]")]
    [ApiController]
    public class BatteryController : ControllerBase
    {
        [HttpPost]
        public async Task<IActionResult> Query([FromBody] clsBatQueryOptions options)
        {
            clsBatteryInfoQuery queryer = new clsBatteryInfoQuery(options);
            var results = await queryer.Query();
            results = results.OrderBy(p => p.Key).ToDictionary(p => p.Key, p => p.Value);
            return Ok(results);
        }
    }
}
