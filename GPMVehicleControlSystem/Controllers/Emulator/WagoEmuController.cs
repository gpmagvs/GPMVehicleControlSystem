using GPMVehicleControlSystem.Models.Emulators;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using static GPMVehicleControlSystem.VehicleControl.DIOModule.clsDIModule;

namespace GPMVehicleControlSystem.Controllers.Emulator
{
    [Route("api/[controller]")]
    [ApiController]
    public class WagoEmuController : ControllerBase
    {
        [HttpGet("SetInput")]
        public async Task<IActionResult> SetInput(DI_ITEM Input, bool State)
        {
            StaEmuManager.wagoEmu.SetState(Input, State);
            return Ok(new { Address = Input.ToString(), State });
        }



        [HttpGet("Emo")]
        public async Task<IActionResult> Emo()
        {
            StaEmuManager.wagoEmu.SetState(DI_ITEM.EMO, false);
            return Ok();
        }



        [HttpGet("Horizon_Moto_Switch")]
        public async Task<IActionResult> Horizon_Motor_Switch(bool state)
        {
            StaEmuManager.wagoEmu.SetState(DI_ITEM.Horizon_Motor_Switch, state);
            return Ok();
        }


        [HttpGet("ResetButton")]
        public async Task<IActionResult> ResetButton()
        {
            StaEmuManager.wagoEmu.SetState(DI_ITEM.Panel_Reset_PB, true);
            await Task.Delay(500);
            StaEmuManager.wagoEmu.SetState(DI_ITEM.Panel_Reset_PB, false);
            StaEmuManager.wagoEmu.SetState(DI_ITEM.EMO, true);
            return Ok();
        }

        [HttpGet("CST_SENSOR_ON")]
        public async Task CST_SENSOR_ON()
        {

            StaEmuManager.wagoEmu.SetState(DI_ITEM.Cst_Sensor_1, true);
            StaEmuManager.wagoEmu.SetState(DI_ITEM.Cst_Sensor_2, true);
        }


        [HttpGet("CST_SENSOR_OFF")]
        public async Task CST_SENSOR_OFF()
        {
            StaEmuManager.wagoEmu.SetState(DI_ITEM.Cst_Sensor_1, false);
            StaEmuManager.wagoEmu.SetState(DI_ITEM.Cst_Sensor_2, false);
        }


        [HttpGet("ForkReachDownlimit")]
        public async Task ForkReachDownlimit()
        {
            StaEmuManager.wagoEmu.SetState(DI_ITEM.Vertical_Home_Pos, false);
            StaEmuManager.wagoEmu.SetState(DI_ITEM.Vertical_Up_Hardware_limit, false);
            StaEmuManager.wagoEmu.SetState(DI_ITEM.Vertical_Down_Pose, false);
            StaEmuManager.wagoEmu.SetState(DI_ITEM.Vertical_Down_Hardware_limit, true);
        }


        [HttpGet("ForkReachDownPose")]
        public async Task ForkReachDownPose()
        {
            StaEmuManager.wagoEmu.SetState(DI_ITEM.Vertical_Home_Pos, false);
            StaEmuManager.wagoEmu.SetState(DI_ITEM.Vertical_Up_Hardware_limit, false);
            StaEmuManager.wagoEmu.SetState(DI_ITEM.Vertical_Down_Pose, true);
            StaEmuManager.wagoEmu.SetState(DI_ITEM.Vertical_Down_Hardware_limit, false);
        }

        [HttpGet("ForkReachHome")]
        public async Task ForkReachHome()
        {
            StaEmuManager.wagoEmu.SetState(DI_ITEM.Vertical_Home_Pos, true);
            StaEmuManager.wagoEmu.SetState(DI_ITEM.Vertical_Up_Hardware_limit, false);
            StaEmuManager.wagoEmu.SetState(DI_ITEM.Vertical_Down_Pose, false);
            StaEmuManager.wagoEmu.SetState(DI_ITEM.Vertical_Down_Hardware_limit, false);
        }

        [HttpGet("ForkAtUnknownLocation")]
        public async Task ForkAtUnknownLocation()
        {
            StaEmuManager.wagoEmu.SetState(DI_ITEM.Vertical_Home_Pos, false);
            StaEmuManager.wagoEmu.SetState(DI_ITEM.Vertical_Up_Hardware_limit, false);
            StaEmuManager.wagoEmu.SetState(DI_ITEM.Vertical_Down_Pose, false);
            StaEmuManager.wagoEmu.SetState(DI_ITEM.Vertical_Down_Hardware_limit, false);
        }


    }
}
