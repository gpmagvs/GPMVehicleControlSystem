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
        [HttpGet("/ws")]
        public async Task Get(string user_id)
        {
            await WebsocketAgent.Middleware.HandleWebsocketClientConnectIn(HttpContext, user_id);
        }
    }
}
