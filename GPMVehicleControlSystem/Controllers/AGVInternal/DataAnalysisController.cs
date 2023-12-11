using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace GPMVehicleControlSystem.Controllers.AGVInternal
{
    [Route("api/[controller]")]
    [ApiController]
    public class DataAnalysisController : ControllerBase
    {
        [HttpGet("QueryAvalibility")]
        public async Task<IActionResult> QueryAvalibility(DateTime from, DateTime to)
        {
            Models.Analysis.AavlibilityAnalyer analyer = new Models.Analysis.AavlibilityAnalyer();
            return Ok(analyer.Query(from, to));
        }
    }
}
