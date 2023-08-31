using AGVSystemCommonNet6;
using AGVSystemCommonNet6.Log;
using GPMVehicleControlSystem.Models.VehicleControl.AGVControl;
using GPMVehicleControlSystem.Models.VehicleControl.Vehicles;
using GPMVehicleControlSystem.Models.WorkStation;
using GPMVehicleControlSystem.ViewModels.WorkStation;
using Microsoft.AspNetCore.Mvc;
using static SQLite.SQLite3;

namespace GPMVehicleControlSystem.Controllers.AGVInternal
{
    public partial class VMSController
    {
        private ForkAGV forkAgv => agv as ForkAGV;

        private object GetMappData()
        {
            Dictionary<int, clsWorkStationData> settings = forkAgv.ForkLifter.StationDatas;
            var mapped_data = settings.OrderBy(dat => dat.Key).Select(dat => new
            {
                Tag = dat.Key,
                Layers = new List<object> {
                    new {
                        Key=0,
                        Value= new
                        {
                            Name= dat.Value.Name,
                            Up_Pose= dat.Value.LayerDatas[0].Up_Pose,
                            Down_Pose= dat.Value.LayerDatas[0].Down_Pose
                        }
                    },new {
                        Key=1,
                        Value= new
                        {
                            Name= dat.Value.Name,
                            Up_Pose= dat.Value.LayerDatas[1].Up_Pose,
                            Down_Pose= dat.Value.LayerDatas[1].Down_Pose
                        }
                    },
                    new{
                        Key=2,
                        Value= new
                        {
                            Name= dat.Value.Name,
                            Up_Pose= dat.Value.LayerDatas[2].Up_Pose,
                            Down_Pose= dat.Value.LayerDatas[2].Down_Pose
                        }
                    }
                }

            }).ToArray();
            return mapped_data;
        }
        [HttpGet("Fork/TeachDatas")]
        public async Task<IActionResult> GetTeachDatas()
        {

            return Ok(GetMappData());
        }
        [HttpPost("Fork/SaveTeachDatas")]
        public async Task<IActionResult> SaveTeachDatas(Dictionary<int, Dictionary<int, clsStationLayerData>> data)
        {
            var datasetting = data.ToDictionary(d => d.Key, d => new clsWorkStationData()
            {
                LayerDatas = d.Value
            });
            foreach (var item in datasetting)
            {
                var ff = forkAgv.WorkStations.Stations.FirstOrDefault(kp => kp.Key == item.Key);
                if (ff.Value != null)
                {
                    ff.Value.LayerDatas = item.Value.LayerDatas;
                }
            }
            bool confirm = forkAgv.SaveTeachDAtaSettings();
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
            if (forkAgv.ForkLifter.IsInitialing)
                return Ok(new { confirm = false, message = "禁止操作:Z軸正在進行初始化" });
            if (!forkAgv.IsForkInitialized)
                return Ok(new { confirm = false, message = "禁止操作:Z軸尚未初始化" });

            bool isForkWorking = (forkAgv.AGVC as ForkAGVController).WaitActionDoneFlag;
            string current_cmd = (forkAgv.AGVC as ForkAGVController).CurrentForkAction;

            if ((forkAgv.AGVC as ForkAGVController).WaitActionDoneFlag)
                return Ok(new { confirm = false, message = $"禁止操作:Z軸正在執行動作({current_cmd})" });

            if (action == "home" | action == "orig")
            {
                var result = await forkAgv.ForkLifter.ForkGoHome(speed);
                return Ok(new { confirm = result.confirm, message = result.alarm_code.ToString() });
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
            else if (action == "stop")
            {
                (bool success, string message) result = await forkAgv.ForkLifter.ForkStopAsync();
                return Ok(new { confirm = result.success, message = result.message });
            }
            else if (action == "increase")
            {
                var pose_to = forkAgv.ForkLifter.Driver.CurrentPosition + pose;
                LOG.WARN($"USER adjust fork position from web ui:pose to = {pose_to}");
                (bool success, string message) result = await forkAgv.ForkLifter.ForkPose(pose_to, speed);
                return Ok(new { confirm = result.success, message = result.message });
            }
            else
                return Ok(new { confirm = false, message = "invalid action type" });
        }

        [HttpGet("Fork/Arm/Extend")]
        public async Task<IActionResult> ForkArmExtend()
        {
            if (forkAgv.lastVisitedMapPoint.IsChargeAble())
            {
                return Ok(new { confirm = false, message = "AGV 位於充電站內禁止牙叉伸出" });
            }

            var result = await forkAgv.ForkLifter.ForkExtendOutAsync();
            return Ok(new { confirm = result.confirm, message = result.message });
        }
        [HttpGet("Fork/Arm/Shorten")]
        public async Task<IActionResult> ForkArmShorten()
        {
            var result = await forkAgv.ForkLifter.ForkShortenInAsync();
            return Ok(new { confirm = result.confirm, message = result.message });

        }
        [HttpGet("Fork/Arm/Stop")]
        public async Task<IActionResult> ForkArmStop()
        {
            forkAgv.ForkLifter.ForkARMStop();
            return Ok();
        }
    }
}
