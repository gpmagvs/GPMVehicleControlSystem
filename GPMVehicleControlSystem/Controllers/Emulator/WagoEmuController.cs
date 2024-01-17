using GPMVehicleControlSystem.Models;
using GPMVehicleControlSystem.Models.Emulators;
using GPMVehicleControlSystem.Models.VehicleControl.Vehicles;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using static GPMVehicleControlSystem.VehicleControl.DIOModule.clsDIModule;

namespace GPMVehicleControlSystem.Controllers.Emulator
{
    [Route("api/[controller]")]
    [ApiController]
    public class WagoEmuController : ControllerBase
    {

        private Vehicle Agv => StaStored.CurrentVechicle;


        [HttpGet("Disconnect")]
        public async Task<IActionResult> Disconnect()
        {
            StaEmuManager.wagoEmu.Disconnect();
            return Ok();
        }



        [HttpGet("Connect")]
        public async Task<IActionResult> Connect()
        {
            return Ok(await StaEmuManager.wagoEmu.Connect());
        }

        [HttpGet("SetInput")]
        public async Task<IActionResult> SetInput(int YAddress, bool State)
        {
            VehicleControl.DIOModule.clsIOSignal add = StaStored.CurrentVechicle.WagoDI.VCSInputs.First(ad => ad.Address == $"X{YAddress.ToString("X4")}");
            StaEmuManager.wagoEmu.SetState(add.Input, State);
            return Ok(new { Address = add.Address, State });
        }



        [HttpGet("Emo")]
        public async Task<IActionResult> Emo()
        {
            StaEmuManager.wagoEmu.SetState(DI_ITEM.EMO, false);
            return Ok();
        }

        [HttpGet("EmoButton1_Push")]
        public async Task<IActionResult> EmoButton1Push()
        {
            StaEmuManager.wagoEmu.SetState(DI_ITEM.EMO_Button, true);
            return Ok();
        }

        [HttpGet("EmoButton2_Push")]
        public async Task<IActionResult> EmoButton2_Push()
        {
            StaEmuManager.wagoEmu.SetState(DI_ITEM.EMO_Button_2, true);
            return Ok();
        }


        [HttpGet("EmoButtonsRelease")]
        public async Task<IActionResult> EmoButtonsRelease()
        {
            StaEmuManager.wagoEmu.SetState(DI_ITEM.EMO_Button, false);
            StaEmuManager.wagoEmu.SetState(DI_ITEM.EMO_Button_2, false);
            return Ok();
        }

        [HttpGet("EmoButton1Release")]
        public async Task<IActionResult> EmoButton1Release()
        {
            StaEmuManager.wagoEmu.SetState(DI_ITEM.EMO_Button, false);
            return Ok();
        }

        [HttpGet("EmoButton2Release")]
        public async Task<IActionResult> EmoButton2Release()
        {
            StaEmuManager.wagoEmu.SetState(DI_ITEM.EMO_Button_2, false);
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

        [HttpGet("SetSideLaserNormal")]
        public async Task SetSideLaserNormal()
        {
            StaEmuManager.wagoEmu.SetState(DI_ITEM.RightProtection_Area_Sensor_3, true);
            StaEmuManager.wagoEmu.SetState(DI_ITEM.LeftProtection_Area_Sensor_3, true);
        }
        [HttpGet("SetSideLaserAbnormal")]
        public async Task SetSideLaserAbnormal()
        {
            StaEmuManager.wagoEmu.SetState(DI_ITEM.RightProtection_Area_Sensor_3, false);
            StaEmuManager.wagoEmu.SetState(DI_ITEM.LeftProtection_Area_Sensor_3, false);
        }

        [HttpGet("SetFrontLaserNormal")]
        public async Task SetFrontLaserNormal()
        {
            StaEmuManager.wagoEmu.SetState(DI_ITEM.FrontProtection_Area_Sensor_1, true);
            StaEmuManager.wagoEmu.SetState(DI_ITEM.FrontProtection_Area_Sensor_2, true);
            StaEmuManager.wagoEmu.SetState(DI_ITEM.FrontProtection_Area_Sensor_3, true);
            StaEmuManager.wagoEmu.SetState(DI_ITEM.FrontProtection_Area_Sensor_4, true);
        }

        [HttpGet("SeBackLaserNormal")]
        public async Task SeBackLaserNormal()
        {
            StaEmuManager.wagoEmu.SetState(DI_ITEM.BackProtection_Area_Sensor_1, true);
            StaEmuManager.wagoEmu.SetState(DI_ITEM.BackProtection_Area_Sensor_2, true);
            StaEmuManager.wagoEmu.SetState(DI_ITEM.BackProtection_Area_Sensor_3, true);
            StaEmuManager.wagoEmu.SetState(DI_ITEM.BackProtection_Area_Sensor_4, true);
        }

        [HttpGet("SetFrontLaserWarning")]
        public async Task SetFrontLaserWarning()
        {
            StaEmuManager.wagoEmu.SetState(DI_ITEM.FrontProtection_Area_Sensor_1, false);
            StaEmuManager.wagoEmu.SetState(DI_ITEM.FrontProtection_Area_Sensor_2, true);
            StaEmuManager.wagoEmu.SetState(DI_ITEM.FrontProtection_Area_Sensor_3, true);
            StaEmuManager.wagoEmu.SetState(DI_ITEM.FrontProtection_Area_Sensor_4, true);
        }
        [HttpGet("SetFrontLaserAlarm")]
        public async Task SetFrontLaserAlarm()
        {
            StaEmuManager.wagoEmu.SetState(DI_ITEM.FrontProtection_Area_Sensor_1, false);
            StaEmuManager.wagoEmu.SetState(DI_ITEM.FrontProtection_Area_Sensor_2, false);
            StaEmuManager.wagoEmu.SetState(DI_ITEM.FrontProtection_Area_Sensor_3, true);
            StaEmuManager.wagoEmu.SetState(DI_ITEM.FrontProtection_Area_Sensor_4, true);
        }


        [HttpGet("SetBackLaserWarning")]
        public async Task SetBackLaserWarning()
        {
            StaEmuManager.wagoEmu.SetState(DI_ITEM.BackProtection_Area_Sensor_1, false);
            StaEmuManager.wagoEmu.SetState(DI_ITEM.BackProtection_Area_Sensor_2, true);
            StaEmuManager.wagoEmu.SetState(DI_ITEM.BackProtection_Area_Sensor_3, true);
            StaEmuManager.wagoEmu.SetState(DI_ITEM.BackProtection_Area_Sensor_4, true);
        }
        [HttpGet("SetBackLaserAlarm")]
        public async Task SetBackLaserAlarm()
        {
            StaEmuManager.wagoEmu.SetState(DI_ITEM.BackProtection_Area_Sensor_1, false);
            StaEmuManager.wagoEmu.SetState(DI_ITEM.BackProtection_Area_Sensor_2, false);
            StaEmuManager.wagoEmu.SetState(DI_ITEM.BackProtection_Area_Sensor_3, true);
            StaEmuManager.wagoEmu.SetState(DI_ITEM.BackProtection_Area_Sensor_4, true);
        }

        [HttpGet("Horizon_Motor_Alarm")]
        public async Task Horizon_Motor_Alarm()
        {
            StaEmuManager.wagoEmu.SetState(DI_ITEM.Horizon_Motor_Busy_1, false);
            StaEmuManager.wagoEmu.SetState(DI_ITEM.Horizon_Motor_Busy_2, false);
            StaEmuManager.wagoEmu.SetState(DI_ITEM.Horizon_Motor_Alarm_1, true);
            StaEmuManager.wagoEmu.SetState(DI_ITEM.Horizon_Motor_Alarm_2, true);
        }

        [HttpGet("Submarine_Frontend_Obs_Control")]
        public async Task Submarine_Frontend_Obs_Control()
        {
            var di_ = DI_ITEM.FrontProtection_Obstacle_Sensor;
            bool state = Agv.WagoDI.GetState(di_);
            StaEmuManager.wagoEmu.SetState(di_, !state);
        }

        [HttpGet("EQ_SIGNALS_OFF")]
        public async Task EQ_SIGNALS_OFF()
        {
            StaEmuManager.wagoEmu.SetState(DI_ITEM.EQ_BUSY, false);
            StaEmuManager.wagoEmu.SetState(DI_ITEM.EQ_READY, false);
            StaEmuManager.wagoEmu.SetState(DI_ITEM.EQ_L_REQ, false);
            StaEmuManager.wagoEmu.SetState(DI_ITEM.EQ_U_REQ, false);
        }

        [HttpGet("EQ_GO_OFF")]
        public async Task EQ_GO_OFF()
        {
            StaEmuManager.wagoEmu.SetState(DI_ITEM.EQ_GO, false);
            StaEmuManager.wagoEmu.SetState(DI_ITEM.EQ_BUSY, false);
            StaEmuManager.wagoEmu.SetState(DI_ITEM.EQ_READY, false);
            StaEmuManager.wagoEmu.SetState(DI_ITEM.EQ_L_REQ, false);
            StaEmuManager.wagoEmu.SetState(DI_ITEM.EQ_U_REQ, false);
        }
        [HttpGet("EQ_GO_Flick")]
        public async Task EQ_GO_Flick()
        {
            var EQ_GO_oriState = StaStored.CurrentVechicle.EQHsSignalStates[Vehicle.EQ_HSSIGNAL.EQ_GO].State;

            if (!EQ_GO_oriState)
                return;

            var EQ_BUSY_oriState = StaStored.CurrentVechicle.EQHsSignalStates[Vehicle.EQ_HSSIGNAL.EQ_BUSY].State;
            var EQ_READY_oriState = StaStored.CurrentVechicle.EQHsSignalStates[Vehicle.EQ_HSSIGNAL.EQ_READY].State;
            var EQ_L_REQ_oriState = StaStored.CurrentVechicle.EQHsSignalStates[Vehicle.EQ_HSSIGNAL.EQ_L_REQ].State;
            var EQ_U_REQ_oriState = StaStored.CurrentVechicle.EQHsSignalStates[Vehicle.EQ_HSSIGNAL.EQ_U_REQ].State;


            StaEmuManager.wagoEmu.SetState(DI_ITEM.EQ_GO, false);
            if (EQ_BUSY_oriState)
                StaEmuManager.wagoEmu.SetState(DI_ITEM.EQ_BUSY, !EQ_BUSY_oriState);
            if (EQ_READY_oriState)
                StaEmuManager.wagoEmu.SetState(DI_ITEM.EQ_READY, !EQ_READY_oriState);
            if (EQ_L_REQ_oriState)
                StaEmuManager.wagoEmu.SetState(DI_ITEM.EQ_L_REQ, !EQ_L_REQ_oriState);
            if (EQ_U_REQ_oriState)
                StaEmuManager.wagoEmu.SetState(DI_ITEM.EQ_U_REQ, !EQ_U_REQ_oriState);
            await Task.Delay(100);

            StaEmuManager.wagoEmu.SetState(DI_ITEM.EQ_GO, EQ_GO_oriState);
            StaEmuManager.wagoEmu.SetState(DI_ITEM.EQ_BUSY, EQ_BUSY_oriState);
            StaEmuManager.wagoEmu.SetState(DI_ITEM.EQ_READY, EQ_READY_oriState);
            StaEmuManager.wagoEmu.SetState(DI_ITEM.EQ_L_REQ, EQ_L_REQ_oriState);
            StaEmuManager.wagoEmu.SetState(DI_ITEM.EQ_U_REQ, EQ_U_REQ_oriState);
        }
    }
}
