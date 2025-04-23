using GPMVehicleControlSystem.Models;
using GPMVehicleControlSystem.Models.VehicleControl;
using GPMVehicleControlSystem.Models.VehicleControl.Vehicles;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Swagger;

namespace GPMVehicleControlSystem.Controllers.AGVInternal
{
    [Route("api/[controller]")]
    [ApiController]
    //[ApiExplorerSettings(IgnoreApi = true)]
    public class AGVLightController : ControllerBase
    {
        private Vehicle agv => StaStored.CurrentVechicle;

        [HttpGet("Front")]
        public async Task<IActionResult> Front(bool open)
        {
            agv.DirectionLighter.Forward(open);
            return Ok();
        }
        [HttpGet("Back")]
        public async Task<IActionResult> Back(bool open)
        {
            agv.DirectionLighter.Backward(open);
            return Ok();
        }
        [HttpGet("RightSide")]
        public async Task<IActionResult> RightSide(bool open)
        {
            agv.DirectionLighter.TurnRight(open);
            return Ok();
        }
        [HttpGet("LeftSide")]
        public async Task<IActionResult> LeftSide(bool open)
        {
            agv.DirectionLighter.TurnLeft(open);
            return Ok();
        }

        [HttpGet("Direction_Light_All_Close")]
        public async Task<IActionResult> Direction_Light_All_Close()
        {
            agv.DirectionLighter.CloseAll();
            return Ok();
        }

        [HttpGet("Direction_Light_All_Open")]
        public async Task<IActionResult> Direction_Light_All_Open()
        {
            agv.DirectionLighter.OpenAll();
            return Ok();
        }

        [HttpGet("State_Run")]
        public async Task<IActionResult> State_Run()
        {
            agv.StatusLighter.RUN();
            return Ok();
        }
        [HttpGet("State_Idle")]
        public async Task<IActionResult> State_Idle()
        {
            agv.StatusLighter.IDLE();
            return Ok();
        }
        [HttpGet("State_Down")]
        public async Task<IActionResult> State_Down()
        {
            agv.StatusLighter.DOWN();
            return Ok();
        }


        [HttpGet("WaitPassLights")]
        public async Task<IActionResult> WaitPassLights()
        {
            agv.DirectionLighter.WaitPassLights();
            return Ok();
        }

        [HttpGet("TrafficControllingLightsFlash")]
        public async Task<IActionResult> TrafficControllingLightsFlash(int period=500)
        {
            agv.DirectionLighter.TrafficControllingLightsFlash(period);
            return Ok();
        }

        [HttpGet("DirectLightFlashTest")]
        public async Task<IActionResult> DirectLightFlashTest()
        {
            agv.DirectionLighter.Flash(new VehicleControl.DIOModule.clsDOModule.DO_ITEM[] {
                 VehicleControl.DIOModule.clsDOModule.DO_ITEM.AGV_DiractionLight_Left,
                 VehicleControl.DIOModule.clsDOModule.DO_ITEM.AGV_DiractionLight_Right
            }, 1000);
            return Ok();
        }
    }
}
