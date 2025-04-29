using GPMVehicleControlSystem.Models;
using GPMVehicleControlSystem.Models.VehicleControl.Vehicles;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace GPMVehicleControlSystem.Controllers.AGVInternal
{
    [Route("api/[controller]")]
    [ApiController]
    public class FORKAGVontroller : ControllerBase
    {

        private ForkAGV agv => StaStored.CurrentVechicle as ForkAGV;

        [HttpPost("VerticalInit")]
        public async Task VerticalInit()
        {
            agv.VerticalForkInitProcess();
        }

        [HttpPost("HorizonInit")]
        public async Task HorizonInit()
        {
            agv.HorizonForkInitProcess();
        }

    }
}
