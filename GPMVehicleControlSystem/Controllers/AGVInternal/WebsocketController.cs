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
            if (HttpContext.WebSockets.IsWebSocketRequest)
            {

                WebsocketAgent.ClientRequest(HttpContext, WebsocketAgent.WEBSOCKET_CLIENT_ACTION.GETVMSStates);
            }
            else
            {
                HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            }
        }
        [HttpGet("/ws/ConnectionState")]
        public async Task ConnectionStateEcho()
        {
            if (HttpContext.WebSockets.IsWebSocketRequest)
            {
                WebsocketAgent.ClientRequest(HttpContext, WebsocketAgent.WEBSOCKET_CLIENT_ACTION.GETConnectionStates);
            }
            else
            {
                HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            }
        }


        [HttpGet("/ws/ModuleInformation")]
        public async Task ModuleInformation()
        {
            if (HttpContext.WebSockets.IsWebSocketRequest)
            {

                WebsocketAgent.ClientRequest(HttpContext, WebsocketAgent.WEBSOCKET_CLIENT_ACTION.GETAGVCModuleInformation);
            }
            else
            {
                HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            }
        }

        [HttpGet("/ws/DIOTableData")]
        public async Task DIOTableData()
        {
            if (HttpContext.WebSockets.IsWebSocketRequest)
            {

                WebsocketAgent.ClientRequest(HttpContext, WebsocketAgent.WEBSOCKET_CLIENT_ACTION.GETDIOTable);
            }
            else
            {
                HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            }
        }


        [HttpGet("/ws/AGVS_MSG_IO")]
        public async Task AGVS_MSG_IO()
        {
            if (HttpContext.WebSockets.IsWebSocketRequest)
            {

                WebsocketAgent.ClientRequest(HttpContext, WebsocketAgent.WEBSOCKET_CLIENT_ACTION.GETAGVSMSGIODATA);
            }
            else
            {
                HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            }
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
                            webSocket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(e))), System.Net.WebSockets.WebSocketMessageType.Text, true, CancellationToken.None);
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
                webSocket?.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(new clsSysMessage
                {
                    Message = "車載系統"
                }))), System.Net.WebSockets.WebSocketMessageType.Text, true, CancellationToken.None);

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
