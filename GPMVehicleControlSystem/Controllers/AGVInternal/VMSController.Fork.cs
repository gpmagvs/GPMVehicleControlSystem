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


        [HttpGet("Fork")]
        public async Task<IActionResult> ForkAction(string action, double pose = 0, double speed = 0)
        {
            if (!forkAgv.IsForkInitialized)
                return Ok(new { confirm = false, message = "禁止操作:Z軸尚未初始化" });
            if (action == "home"｜action == "orig")
            {
                (bool success, string message) result = await forkAgv.ForkLifter.ForkGoHome(speed);
                return Ok(new { confirm = result.success, message = result.message });

            }
            else if (action == "init")
            {
                (bool success, string message) result = await forkAgv.ForkLifter.ForkPositionInit();
                return Ok(result);
            }
            else if (action == "pose")
            {

                (bool success, string message) result = await forkAgv.ForkLifter.ForkPose(pose, speed);
                return Ok(new { confirm = result.success, message = result.message });
            }
            else if (action == "up")
            {
                var pose_to = forkAgv.ForkLifter.Driver.CurrentPosition + 0.1;
                (bool success, string message) result = await forkAgv.ForkLifter.ForkPose(pose_to, speed);
                return Ok(new { confirm = result.success, message = result.message });
            }
            else if (action == "down")
            {
                var pose_to = forkAgv.ForkLifter.Driver.CurrentPosition - 0.1;
                (bool success, string message) result = await forkAgv.ForkLifter.ForkPose(pose_to, speed);
                return Ok(new { confirm = result.success, message = result.message });
            }
            else
                return Ok(new { confirm = false, message = "invalid action type" });
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
