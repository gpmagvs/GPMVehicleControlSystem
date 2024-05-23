using AGVSystemCommonNet6.Log;
using GPMVehicleControlSystem.Service;
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
        private WebsocketMiddlewareService _WebsocketMiddlewareService;
        public WebsocketController(WebsocketMiddlewareService websocketBackground)
        {
            _WebsocketMiddlewareService = websocketBackground;
        }

        [HttpGet("/ws")]
        public async Task Get(string user_id)
        {
            if (HttpContext.WebSockets.IsWebSocketRequest)
            {
                try
                {
                    string RemoteIpAddress = HttpContext.Request.HttpContext.Connection.RemoteIpAddress.ToString();
                    Console.WriteLine($"{RemoteIpAddress}-broswering website.");
                    await _WebsocketMiddlewareService.ClientConnect(user_id, await HttpContext.WebSockets.AcceptWebSocketAsync());
                }
                catch (TaskCanceledException ex)
                {
                    LOG.TRACE($"Website client-{user_id} connection closed");
                }
            }
            else
            {
                HttpContext.Response.StatusCode = 500;
            }
        }
    }
}
