using GPMVehicleControlSystem.Models.Emulators;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using RosSharp.RosBridgeClient.MessageTypes.Sensor;

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

        [HttpGet("SetBatteryLevel")]
        public async Task SetBatteryLevel(int level, int batIndex = 0)
        {
            StaEmuManager.agvRosEmu.SetBatteryLevel((byte)level, batIndex);
        }


        [HttpPost("IMU_Data")]
        public async Task IMU_Data(Imu imu)
        {
            StaEmuManager.agvRosEmu.SetImuData(imu);
        }

        [HttpGet("SetAGVTag")]
        public async Task SetAGVTag(int tag)
        {
            StaEmuManager.agvRosEmu.SetLastVisitedTag(tag);
        }
    }
}
