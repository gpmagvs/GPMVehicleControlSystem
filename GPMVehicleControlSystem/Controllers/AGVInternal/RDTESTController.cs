using AGVSystemCommonNet6.AGVDispatch.Messages;
using GPMVehicleControlSystem.Models;
using GPMVehicleControlSystem.Models.RDTEST;
using GPMVehicleControlSystem.Models.VehicleControl.Vehicles;
using GPMVehicleControlSystem.ViewModels.RDTEST;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using static AGVSystemCommonNet6.clsEnums;

namespace GPMVehicleControlSystem.Controllers.AGVInternal
{
    [Route("api/[controller]")]
    [ApiController]
    public class RDTESTController : ControllerBase
    {
        Vehicle agv;
        public RDTESTController()
        {
            agv = StaStored.CurrentVechicle;
        }
        [HttpPost("MoveTest")]
        public async Task<IActionResult> MoveTestStart(clsMoveTestModel options)
        {
            if (agv.Remote_Mode == REMOTE_MODE.ONLINE | agv.Operation_Mode == OPERATOR_MODE.AUTO | agv.Sub_Status != SUB_STATUS.IDLE)
            {
                return Ok(new { result = false, message = "AGV必須在Offline、手動模式且狀態為IDLE的狀態方可執行測試" });
            }
            StaRDTestManager.StartMoveTest(options);
            return Ok(new { result = true });
        }
        [HttpPost("MoveTest/Stop")]
        public async Task<IActionResult> MoveTestStop()
        {
            StaRDTestManager.StopMoveTest();
            return Ok();
        }
    }
}
