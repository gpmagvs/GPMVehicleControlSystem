using GPMVehicleControlSystem.Models;
using GPMVehicleControlSystem.Models.VehicleControl.AGVControl;
using GPMVehicleControlSystem.Models.VehicleControl.Vehicles;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using RosSharp.RosBridgeClient;

namespace GPMVehicleControlSystem.Controllers.AGVInternal
{
    [Route("api/[controller]")]
    [ApiController]
    public class SickLaserController : ControllerBase
    {
        Vehicle agvInstance => StaStored.CurrentVechicle;
        CarController agvControl => agvInstance != null ? agvInstance.AGVC : null;

        [HttpPost("ResetSickLaser")]
        public async Task<IActionResult> ResetSickLaser()
        {
            try
            {

                (bool confirm, string message) = await agvControl?.ResetSickLaser();
                return Ok(new
                {
                    confirm = confirm,
                    message = message
                });
            }
            catch (Exception ex)
            {
                return Ok(new
                {
                    confirm = false,
                    message = $"{ex.Message}-{ex.StackTrace}"
                });
            }
        }
    }
}
