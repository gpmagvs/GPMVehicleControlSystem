using AGVSystemCommonNet6.Tools;
using AGVSystemCommonNet6.Tools.Database;
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
            List<string>? locList = DBhelper.QueryAllParkLoc();
            return Ok(locList);
        }

        [HttpPost("Query")]
        public async Task<IActionResult> GetAllParkLoc([FromBody] clsParkingAcqQueryOption option)
        {
            List<clsParkingAccuracy> data_result = DBhelper.QueryParkingAccuracy(option.Tag, option.StartTimeStr, option.EndTimeStr);
            return Ok(data_result);
        }

        public class clsParkingAcqQueryOption
        {
            public int Tag { get; set; }
            public string StartTimeStr { get; set; }
            public string EndTimeStr { get; set; }

        }
    }
}
