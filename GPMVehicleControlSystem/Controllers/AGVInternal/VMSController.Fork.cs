using GPMVehicleControlSystem.Models.VehicleControl.AGVControl;
using GPMVehicleControlSystem.Models.VehicleControl.Vehicles;
using Microsoft.AspNetCore.Mvc;
using static SQLite.SQLite3;

namespace GPMVehicleControlSystem.Controllers.AGVInternal
{
    public partial class VMSController
    {
        private ForkAGV forkAgv => agv as ForkAGV;

        [HttpGet("Fork/Init")]
        public async Task<IActionResult> VerticalInit()
        {
            var result = await forkAgv.ForkLifter.ForkPositionInit();
            return Ok(new { confirm = result.confirm, message = result.message });
        }


        /// <summary>
        /// 回到定義的Home點
        /// </summary>
        /// <returns></returns>
        [HttpGet("Fork/Home")]
        public async Task<IActionResult> VerticalHome(double speed = 1.0)
        {
            var result = await forkAgv.ForkLifter.ForkGoHome();
            return Ok(new { confirm = result.confirm, message = result.message });
        }

        [HttpGet("Fork/Pose")]
        public async Task<IActionResult> VerticalPose(double pose, double? speed = 1.0)
        {
            (bool success, string message) result = await forkAgv.ForkLifter.ForkPose(pose, (double)speed);
            return Ok(new { confirm = result.success, message = result.message });
        }
        [HttpGet("Fork/Stop")]
        public async Task<IActionResult> VerticalStop()
        {
            var result = await forkAgv.ForkLifter.ForkStopAsync();
            return Ok(new { confirm = result.confirm, message = result.message });
        }
        [HttpGet("Fork/Up")]
        public async Task<IActionResult> VerticalUp(double speed = 1.0)
        {
            var result = await forkAgv.ForkLifter.ForkUpAsync(speed);
            return Ok(new { confirm = result.confirm, message = result.message });
        }

        [HttpGet("Fork/Down")]
        public async Task<IActionResult> VerticalDown(double speed = 1.0)
        {
            var result = await forkAgv.ForkLifter.ForkDownAsync(speed);
            return Ok(new { confirm = result.confirm, message = result.message });
        }

        [HttpGet("Fork/Down_Search")]
        public async Task<IActionResult> VerticalDownSearch(double speed = 1.0)
        {
            var result = await forkAgv.ForkLifter.ForkDownSearchAsync(speed);
            return Ok(new { confirm = result.confirm, message = result.message });
        }

        [HttpGet("Fork/Up_Search")]
        public async Task<IActionResult> VerticalUpSearch(double speed = 1.0)
        {
            var result = await forkAgv.ForkLifter.ForkUpSearchAsync(speed);
            return Ok(new { confirm = result.confirm, message = result.message });
        }
    }
}
