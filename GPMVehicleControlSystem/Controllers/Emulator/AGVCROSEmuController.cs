using GPMVehicleControlSystem.Models.Emulators;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace GPMVehicleControlSystem.Controllers.Emulator
{
    [Route("api/[controller]")]
    [ApiController]
    public class AGVCROSEmuController : ControllerBase
    {
        [HttpGet("/ws/ros")]
        public async Task Conn()
        {
            if (HttpContext.WebSockets.IsWebSocketRequest)
            {

            }
            else
            {
                HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            }
        }

        [HttpGet("ImpactSimulation")]
        public async Task ImpactSimulation()
        {
            StaEmuManager.agvRosEmu.ImpactingSimulation();
        }


        [HttpGet("PitchErrorSimulation")]
        public async Task PitchErrorSimulation()
        {
            StaEmuManager.agvRosEmu.PitchErrorSimulation();
        }

        [HttpGet("PitchNormalSimulation")]
        public async Task PitchNormalSimulation()
        {
            StaEmuManager.agvRosEmu.PitchNormalSimulation();
        }
    }
}
