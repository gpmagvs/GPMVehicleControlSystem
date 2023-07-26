using GPMVehicleControlSystem.Models.ForkTeach;
using GPMVehicleControlSystem.Models.VehicleControl.AGVControl;
using GPMVehicleControlSystem.Models.VehicleControl.Vehicles;
using GPMVehicleControlSystem.ViewModels.ForkTeach;
using Microsoft.AspNetCore.Mvc;
using static SQLite.SQLite3;

namespace GPMVehicleControlSystem.Controllers.AGVInternal
{
    public partial class VMSController
    {
        private ForkAGV forkAgv => agv as ForkAGV;

        private object GetMappData()
        {
            var mapped_data = forkAgv.ForkLifter.ForkTeachData.Teaches.OrderBy(dat => dat.Key).Select(dat => new
            {
                Tag = dat.Key,
                Layers = dat.Value.Select(layDat => layDat).ToList()
            }).ToArray();
            return mapped_data;
        }
        [HttpGet("Fork/TeachDatas")]
        public async Task<IActionResult> GetTeachDatas()
        {

            return Ok(GetMappData());
        }
        [HttpPost("Fork/SaveTeachDatas")]
        public async Task<IActionResult> SaveTeachDatas(Dictionary<int, Dictionary<int, clsTeachData>> data)
        {
            forkAgv.ForkLifter.ForkTeachData.Teaches = data;
            bool confirm = forkAgv.ForkLifter.SaveTeachDAtaSettings();
            return Ok(new { confirm, data = GetMappData() });
        }
        [HttpPost("Fork/SaveUnitTeachData")]
        public async Task<IActionResult> SaveUnitTeachData(clsSaveUnitTeachDataVM unit_teach_data)
        {
            bool confirm = forkAgv.ForkLifter.SaveUnitTeachData(unit_teach_data);
            return Ok(confirm);
        }


        [HttpGet("Fork/RemoveTagTeachData")]
        public async Task<IActionResult> RemoveTagTeachData(int tag)
        {
            return Ok(forkAgv.ForkLifter.RemoveTagTeachData(tag));
        }

        [HttpGet("Fork/RemoveUnitTeachData")]
        public async Task<IActionResult> RemoveUnitTeachData(int tag, int layer)
        {
            bool confirm = forkAgv.ForkLifter.RemoveUnitTeachData(tag, layer);
            return Ok(confirm);
        }



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
            if (!forkAgv.IsForkInitialized)
                return Ok(new { confirm = false, message = "禁止操作:Z軸尚未初始化" });
            var result = await forkAgv.ForkLifter.ForkGoHome();
            return Ok(new { confirm = result.confirm, message = result.message });
        }

        [HttpGet("Fork/Pose")]
        public async Task<IActionResult> VerticalPose(double pose, double? speed = 1.0)
        {
            if (!forkAgv.IsForkInitialized)
                return Ok(new { confirm = false, message = "禁止操作:Z軸尚未初始化" });
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
            if (!forkAgv.IsForkInitialized)
                return Ok(new { confirm = false, message = "禁止操作:Z軸尚未初始化" });
            var pose_to = forkAgv.ForkLifter.Driver.CurrentPosition + 0.1;
            var result = await forkAgv.ForkLifter.ForkPose(pose_to, speed);
            return Ok(new { confirm = result.confirm, message = result.message });
        }

        [HttpGet("Fork/Down")]
        public async Task<IActionResult> VerticalDown(double speed = 1.0)
        {
            if (!forkAgv.IsForkInitialized)
                return Ok(new { confirm = false, message = "禁止操作:Z軸尚未初始化" });
            var pose_to = forkAgv.ForkLifter.Driver.CurrentPosition - 0.1;
            var result = await forkAgv.ForkLifter.ForkPose(pose_to, speed);
            return Ok(new { confirm = result.confirm, message = result.message });
        }

        [HttpGet("Fork/Down_Search")]
        public async Task<IActionResult> VerticalDownSearch(double speed = 1.0)
        {
            if (!forkAgv.IsForkInitialized)
                return Ok(new { confirm = false, message = "禁止操作:Z軸尚未初始化" });
            var result = await forkAgv.ForkLifter.ForkDownSearchAsync(speed);
            return Ok(new { confirm = result.confirm, message = result.message });
        }

        [HttpGet("Fork/Up_Search")]
        public async Task<IActionResult> VerticalUpSearch(double speed = 1.0)
        {
            if (!forkAgv.IsForkInitialized)
                return Ok(new { confirm = false, message = "禁止操作:Z軸尚未初始化" });
            var result = await forkAgv.ForkLifter.ForkUpSearchAsync(speed);
            return Ok(new { confirm = result.confirm, message = result.message });
        }
        [HttpGet("Fork/Arm/Extend")]
        public async Task<IActionResult> ForkArmExtend()
        {
            var result = await forkAgv.ForkLifter.ForkExtendOutAsync();
            return Ok(new { confirm = result });
        }
        [HttpGet("Fork/Arm/Shorten")]
        public async Task<IActionResult> ForkArmShorten()
        {
            var result = await forkAgv.ForkLifter.ForkShortenInAsync();
            return Ok(new { confirm = result });
        }
        [HttpGet("Fork/Arm/Stop")]
        public async Task<IActionResult> ForkArmStop()
        {
            await forkAgv.ForkLifter.ForkARMStop();
            return Ok();
        }
    }
}
