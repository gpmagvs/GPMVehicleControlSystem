using AGVSystemCommonNet6.AGVDispatch.Messages;
using GPMVehicleControlSystem.Models;
using GPMVehicleControlSystem.Models.RDTEST;
using GPMVehicleControlSystem.Models.VehicleControl.Vehicles;
using GPMVehicleControlSystem.Models.WebsocketMiddleware;
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
        [HttpGet("/ws/RDTestData")]
        public async Task ConnectionStateEcho()
        {
            await WebsocketAgent.ClientRequest(HttpContext, WebsocketAgent.WEBSOCKET_CLIENT_ACTION.GETRDTestData);
        }

        [HttpPost("IOWriteTest")]
        public async Task IOWriteTest()
        {
            _ = Task.Run(async () =>
            {
                await agv.WagoDO.SetState(VehicleControl.DIOModule.clsDOModule.DO_ITEM.AGV_DiractionLight_Back, false);
                await agv.WagoDO.SetState(VehicleControl.DIOModule.clsDOModule.DO_ITEM.AGV_DiractionLight_Front, true);
                await agv.WagoDO.SetState(VehicleControl.DIOModule.clsDOModule.DO_ITEM.AGV_DiractionLight_Back, false);
                Console.WriteLine($"DO_Set1_Write_Done{DateTime.Now}");
            });
            _ = Task.Run(async () =>
            {
                await agv.WagoDO.SetState(VehicleControl.DIOModule.clsDOModule.DO_ITEM.AGV_DiractionLight_Front, false);
                await agv.WagoDO.SetState(VehicleControl.DIOModule.clsDOModule.DO_ITEM.AGV_DiractionLight_Back, true);
                await agv.WagoDO.SetState(VehicleControl.DIOModule.clsDOModule.DO_ITEM.AGV_DiractionLight_Front, false);
                Console.WriteLine($"DO_Set2_Write_Done{DateTime.Now}");
            });
        }
    }
}
