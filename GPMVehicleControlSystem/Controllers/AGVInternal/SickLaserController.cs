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

        [HttpPost("ScribeSickSaftyScannerOutputPathsTopic")]
        public async Task<string> SubScribeSickSaftyScannerOutputPathsTopic()
        {
            return agvInstance.Laser.SubscribeSickSaftyScannerOuputPathsTopic();
        }
        [HttpPost("SubscribeDiagnosticsTopic")]
        public async Task<string> SubscribeDiagnosticsTopic()
        {
            return agvInstance.Laser.SubscribeDiagnosticsTopic();
        }
        [HttpPost("UnsubScribeSickSaftyScannerOutputPathsTopic")]
        public async Task UnsubScribeSickSaftyScannerOutputPathsTopic()
        {
            agvInstance.Laser.UnSubscribeSickSaftySacnnerOutputPathsTopic();
        }
        [HttpPost("UnSubscribeDiagnosticsTopic")]
        public async Task UnSubscribeDiagnosticsTopic()
        {
            agvInstance.Laser.UnSubscribeDiagnosticsTopic();
        }
    }
}
