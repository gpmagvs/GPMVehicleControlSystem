using AGVSystemCommonNet6.Vehicle_Control.Models;
using AGVSystemCommonNet6.Vehicle_Control.VCSDatabase;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace GPMVehicleControlSystem.Controllers.AGVInternal
{
    [Route("api/[controller]")]
    [ApiController]
    public class ParkingAccurcyController : ControllerBase
    {
        [HttpGet("GetAllParkLoc")]
        public async Task<IActionResult> GetAllParkLoc()
        {
            List<string>? locList = DBhelper.Query.QueryAllParkLoc();
            locList.Sort();
            return Ok(locList);
        }

        [HttpPost("Query")]
        public async Task<IActionResult> GetAllParkLoc([FromBody] clsParkingAcqQueryOption option)
        {
            List<clsParkingAccuracy> data_result = DBhelper.Query.QueryParkingAccuracy(option.Tag, option.StartTimeStr, option.EndTimeStr,option.TaskName);
            return Ok(data_result.OrderByDescending(d=>d.Time).ToList());
        }

        public class clsParkingAcqQueryOption
        {
            public int Tag { get; set; } = -1;
            public string StartTimeStr { get; set; }
            public string EndTimeStr { get; set; }
            public string TaskName { get; set; } = "";

        }
    }
}
