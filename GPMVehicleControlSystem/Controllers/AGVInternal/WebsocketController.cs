using GPMVehicleControlSystem.Models.VCSSystem;
using GPMVehicleControlSystem.Models.WebsocketMiddleware;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using System.Diagnostics;
using System.Text;

namespace GPMVehicleControlSystem.Controllers.AGVInternal
{
    [Route("api/[controller]")]
    [ApiController]
    public class WebsocketController : ControllerBase
    {


        [HttpGet("/ws/AGVCState")]
        public async Task Get()
        {
            await WebsocketAgent.ClientRequest(HttpContext, WebsocketAgent.WEBSOCKET_CLIENT_ACTION.GETVMSStates);
        }
        [HttpGet("/ws/ConnectionState")]
        public async Task ConnectionStateEcho()
        {
            await WebsocketAgent.ClientRequest(HttpContext, WebsocketAgent.WEBSOCKET_CLIENT_ACTION.GETConnectionStates);
        }


        [HttpGet("/ws/ModuleInformation")]
        public async Task ModuleInformation()
        {

            await WebsocketAgent.ClientRequest(HttpContext, WebsocketAgent.WEBSOCKET_CLIENT_ACTION.GETAGVCModuleInformation);

        }

        [HttpGet("/ws/DIOTableData")]
        public async Task DIOTableData()
        {

            await WebsocketAgent.ClientRequest(HttpContext, WebsocketAgent.WEBSOCKET_CLIENT_ACTION.GETDIOTable);

        }

        [HttpGet("/ws/AGVS_MSG_IO")]
        public async Task AGVS_MSG_IO()
        {
            await WebsocketAgent.ClientRequest(HttpContext, WebsocketAgent.WEBSOCKET_CLIENT_ACTION.GETAGVSMSGIODATA);
        }
    }
}
