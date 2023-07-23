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

        [HttpGet("/ws/Sys_Messages")]
        public async Task Sys_Messages()
        {
            if (HttpContext.WebSockets.IsWebSocketRequest)
            {
                System.Net.WebSockets.WebSocket webSocket = HttpContext.WebSockets.AcceptWebSocketAsync().Result;
                void StaSysMessageManager_OnSystemMessageAdd(object? sender, clsSysMessage e)
                {
                    Task.Factory.StartNew(() =>
                    {
                        try
                        {
                            webSocket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(new clsSysMessage[] { e }))), System.Net.WebSockets.WebSocketMessageType.Text, true, CancellationToken.None);
                        }
                        catch (Exception)
                        {
                            StaSysMessageManager.OnSystemMessageAdd -= StaSysMessageManager_OnSystemMessageAdd;
                        }
                    });
                }

                byte[] buffer = new byte[256];
                var bufferSegment = new ArraySegment<byte>(buffer);
                Stopwatch sw = Stopwatch.StartNew();
                sw.Start();
                StaSysMessageManager.OnSystemMessageAdd += StaSysMessageManager_OnSystemMessageAdd;
                webSocket?.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(
                     StaSysMessageManager.SysMessages.Messages.Distinct()
                    ))), System.Net.WebSockets.WebSocketMessageType.Text, true, CancellationToken.None);

                while (webSocket.State == System.Net.WebSockets.WebSocketState.Open)
                {
                    await Task.Delay(1);
                    try
                    {
                        webSocket.ReceiveAsync(bufferSegment, CancellationToken.None);
                    }
                    catch
                    {
                        break;
                    }
                }
                StaSysMessageManager.OnSystemMessageAdd -= StaSysMessageManager_OnSystemMessageAdd;
            }
            else
            {
                HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            }
        }

    }
}
