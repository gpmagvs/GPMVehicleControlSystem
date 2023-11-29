using AGVSystemCommonNet6.Vehicle_Control.VCSDatabase;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace GPMVehicleControlSystem.Controllers.AGVInternal
{
    [Route("api/[controller]")]
    [ApiController]
    [ApiExplorerSettings(IgnoreApi = true)]
    public class AlarmTableController : ControllerBase
    {
        [HttpGet("Clear")]
        public async Task<IActionResult> Clear()
        {
            return Ok(DBhelper.ClearAllAlarm());
        }

        [HttpGet("Query")]
        public async Task<IActionResult> QueryAlarmsByPage(int page, int page_size = 16,string alarm_type = "All")
        {
            return Ok(DBhelper.QueryAlarm(page, page_size, alarm_type));
        }
        [HttpGet("Total")]
        public async Task<IActionResult> Total(string alarm_type = "All")
        {
            return Ok(DBhelper.AlarmsTotalNum(alarm_type));
        }
    }
}
