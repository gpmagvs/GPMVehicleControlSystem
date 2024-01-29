using GPMVehicleControlSystem.Models.Emulators;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using static GPMVehicleControlSystem.VehicleControl.DIOModule.clsDIModule;

namespace GPMVehicleControlSystem.Controllers.Emulator
{
    public partial class WagoEmuController : ControllerBase
    {
        public enum BAT_LOCATION
        {
            INSTALLED,
            INSTALLING,
            REMOVED
        }

        [HttpGet("Battery1Location")]
        public async Task<IActionResult> Battery1Location(BAT_LOCATION location)
        {
            bool exist1_state = false;
            bool exist2_state = false;
            switch (location)
            {
                case BAT_LOCATION.INSTALLED:
                    exist1_state = false;
                    exist2_state = !exist1_state;
                    break;
                case BAT_LOCATION.INSTALLING:
                    exist1_state = exist2_state = true;
                    break;
                case BAT_LOCATION.REMOVED:

                    exist1_state = true;
                    exist2_state = !exist1_state;
                    break;
                default:
                    break;
            }
            StaEmuManager.wagoEmu.SetState(DI_ITEM.Battery_1_Exist_1, exist1_state);
            StaEmuManager.wagoEmu.SetState(DI_ITEM.Battery_1_Exist_2, exist2_state);
            return Ok();
        }

        [HttpGet("Battery2Location")]
        public async Task<IActionResult> Battery2Location(BAT_LOCATION location)
        {
            bool exist1_state = false;
            bool exist2_state = false;
            switch (location)
            {
                case BAT_LOCATION.INSTALLED:
                    exist1_state = false;
                    exist2_state = !exist1_state;
                    break;
                case BAT_LOCATION.INSTALLING:
                    exist1_state = exist2_state = true;
                    break;
                case BAT_LOCATION.REMOVED:

                    exist1_state = true;
                    exist2_state = !exist1_state;
                    break;
                default:
                    break;
            }
            StaEmuManager.wagoEmu.SetState(DI_ITEM.Battery_2_Exist_1, exist1_state);
            StaEmuManager.wagoEmu.SetState(DI_ITEM.Battery_2_Exist_2, exist2_state);
            return Ok();
        }
    }
}
