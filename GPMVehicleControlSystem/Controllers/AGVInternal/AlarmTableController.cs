using AGVSystemCommonNet6.Vehicle_Control.VCS_ALARM;
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
        public async Task<IActionResult> QueryAlarmsByPage(DateTime from, DateTime to, int page, int page_size = 16, string alarm_type = "All", int code = 0)
        {
            return Ok(DBhelper.Query.QueryAlarm(from, to, page, page_size, alarm_type, code));
        }
        [HttpGet("Total")]
        public async Task<IActionResult> Total(DateTime from, DateTime to, string alarm_type = "All", int code = -1)
        {
            return Ok(DBhelper.AlarmsTotalNum(from, to, alarm_type, code));
        }


        [HttpGet("GetAlarmClassifies")]
        public async Task<IActionResult> GetAlarmClassifies()
        {
            return Ok(DBhelper.Query.QueryAlarmCodeClassifies());
        }

        [HttpDelete("DeleteOldAlarms")]
        public async Task DeleteOldAlarms(DateTime timeEarlyTo)
        {
            AlarmManager.RemoveOldAlarmFromDB(timeEarlyTo);
        }

        [HttpGet("GetAlarmCodesTable")]
        public async Task<List<clsAlarmCode>> GetAlarmCodesTable()
        {
            return AlarmManager.AlarmList.OrderBy(al => al.Code).ToList();
        }
    }
}
