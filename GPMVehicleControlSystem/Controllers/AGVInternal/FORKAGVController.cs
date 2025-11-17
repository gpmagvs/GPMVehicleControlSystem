using AGVSystemCommonNet6.Vehicle_Control.VCS_ALARM;
using GPMVehicleControlSystem.Models;
using GPMVehicleControlSystem.Models.VehicleControl.VehicleComponent.Forks;
using GPMVehicleControlSystem.Models.VehicleControl.Vehicles;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using static SQLite.SQLite3;

namespace GPMVehicleControlSystem.Controllers.AGVInternal
{
    [Route("api/[controller]")]
    [ApiController]
    public class FORKAGVController : ControllerBase
    {

        private ForkAGV agv => StaStored.CurrentVechicle as ForkAGV;

        [HttpPost("VerticalInit")]
        public async Task VerticalInit()
        {

            agv.VerticalForkInitProcess(new CancellationToken());
        }

        [HttpPost("HorizonInit")]
        public async Task HorizonInit()
        {
            agv.HorizonForkInitProcess(new CancellationToken());
        }

        [HttpPost("ForkVerticalInitActionResume")]
        public async Task ForkVerticalInitActionResume(bool resume)
        {
            agv.AcceptResumeForkInitWhenActionDriverStateUnknown(resume);
        }

        [HttpGet("FindHome")]
        public async Task<IActionResult> FindHome(string name)
        {
            try
            {
                ForkAGV forkAGV = (ForkAGV)agv;
                (bool done, AlarmCodes alarm_code) result = (false, AlarmCodes.None);
                if (name == "Vertical")
                    result = await forkAGV.ForkLifter.VerticalForkInitialize();

                if (name == "Horizon")
                {
                    clsForkLifterWithDriverBaseExtener forkLifterWithDriverBaseExtener = (clsForkLifterWithDriverBaseExtener)forkAGV.ForkLifter;
                    result = await forkLifterWithDriverBaseExtener.HorizonForkInitialize(bypassSubStatusCheck: true);
                }

                return Ok(new
                {
                    success = result.done,
                    alarm = result.alarm_code.ToString()
                });
            }
            catch (Exception ex)
            {
                return Ok(new
                {
                    success = false,
                    alarm = ex.Message
                });
            }
        }
    }
}
