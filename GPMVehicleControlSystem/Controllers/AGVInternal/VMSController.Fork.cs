using GPMVehicleControlSystem.Models.VehicleControl.AGVControl;
using GPMVehicleControlSystem.Models.VehicleControl.Vehicles;
using Microsoft.AspNetCore.Mvc;
using static SQLite.SQLite3;

namespace GPMVehicleControlSystem.Controllers.AGVInternal
{
    public partial class VMSController
    {
        private ForkAGVController forkAgvControl => (agv.AGVC) as ForkAGVController;

        [HttpGet("Fork/Init")]
        public async Task<IActionResult> VerticalInit()
        {
            var result = await forkAgvControl.ZAxisInit();
            return Ok(new { confirm = result.confirm, message = result.message });
        }


        /// <summary>
        /// 回到定義的Home點
        /// </summary>
        /// <returns></returns>
        [HttpGet("Fork/Home")]
        public async Task<IActionResult> VerticalHome(double speed = 1.0)
        {
            var result = await forkAgvControl.ZAxisGoHome(speed);
            return Ok(new { confirm = result.confirm, message = result.message });
        }

        [HttpGet("Fork/Pose")]
        public async Task<IActionResult> VerticalPose(double pose, double? speed = 1.0)
        {
            (bool success, string message) result = await forkAgvControl.ZAxisGoTo(pose, speed);
            return Ok(new { confirm = result.success, message = result.message });
        }
        [HttpGet("Fork/Stop")]
        public async Task<IActionResult> VerticalStop()
        {
            var result = await forkAgvControl.ZAxisStop();
            return Ok(new { confirm = result.confirm, message = result.message });
        }
        [HttpGet("Fork/Up")]
        public async Task<IActionResult> VerticalUp(double speed = 1.0)
        {
            var result = await forkAgvControl.ZAxisUp(speed);
            return Ok(new { confirm = result.confirm, message = result.message });
        }

        [HttpGet("Fork/Down")]
        public async Task<IActionResult> VerticalDown(double speed = 1.0)
        {
            var result = await forkAgvControl.ZAxisDown(speed);
            return Ok(new { confirm = result.confirm, message = result.message });
        }

        [HttpGet("Fork/Down_Search")]
        public async Task<IActionResult> VerticalDownSearch(double speed = 1.0)
        {
            var result = await forkAgvControl.ZAxisDownSearch(speed);
            return Ok(new { confirm = result.confirm, message = result.message });
        }

        [HttpGet("Fork/Up_Search")]
        public async Task<IActionResult> VerticalUpSearch(double speed = 1.0)
        {
            var result = await forkAgvControl.ZAxisUpSearch(speed);
            return Ok(new { confirm = result.confirm, message = result.message });
        }
    }
}
