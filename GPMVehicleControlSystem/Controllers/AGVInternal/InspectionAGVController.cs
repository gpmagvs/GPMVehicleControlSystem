using GPMVehicleControlSystem.Models;
using GPMVehicleControlSystem.Models.VehicleControl.AGVControl;
using GPMVehicleControlSystem.Models.VehicleControl.Vehicles;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace GPMVehicleControlSystem.Controllers.AGVInternal
{
    [Route("api/[controller]")]
    [ApiController]
    public class InspectionAGVController : ControllerBase
    {
        private TsmcMiniAGV inspectAGV => StaStored.CurrentVechicle as TsmcMiniAGV;
        [HttpGet("StartMeasure")]
        public async Task<IActionResult> StartMeasure(int tagID)
        {
            (bool confirm, string message) response = await (inspectAGV.AGVC as InspectorAGVCarController).StartInstrumentMeasure(tagID);
            return Ok(new { confirm = response.confirm, message = response.message });
        }
    }
}
