using GPMVehicleControlSystem.Models.VehicleControl.AGVControl;
using GPMVehicleControlSystem.Models.VehicleControl.AGVControl.ForkServices;
using GPMVehicleControlSystem.Models.VehicleControl.Vehicles;
using GPMVehicleControlSystem.Models.WorkStation;
using GPMVehicleControlSystem.VehicleControl.DIOModule;
using GPMVehicleControlSystem.ViewModels.WorkStation;
using Microsoft.AspNetCore.Mvc;
using static GPMVehicleControlSystem.Models.VehicleControl.VehicleComponent.Forks.clsForkLifter;
using static GPMVehicleControlSystem.VehicleControl.DIOModule.clsDIModule;
using static GPMVehicleControlSystem.VehicleControl.DIOModule.clsDOModule;

namespace GPMVehicleControlSystem.Controllers.AGVInternal
{
    public partial class VMSController
    {
        private ForkAGV forkAgv => agv as ForkAGV;
        private double MoveToPoseSpeedOfManualMode => forkAgv.Parameters.ForkAGV.ManualModeOperationSpeed.MoveToPoseSpeed;

        private object GetMappData()
        {
            forkAgv.LoadWorkStationConfigs();
            Dictionary<int, clsWorkStationData> settings = forkAgv.ForkLifter.StationDatas;
            var mapped_data = settings.OrderBy(dat => dat.Key).Select(dat => new
            {
                Tag = dat.Key,
                Name = dat.Value.Name,
                NeedHandshake = dat.Value.HandShakeModeHandShakeMode == WORKSTATION_HS_METHOD.HS,
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
            foreach (KeyValuePair<int, clsWorkStationData> item in datasetting)
            {
                KeyValuePair<int, clsWorkStationData> ff = forkAgv.WorkStations.Stations.FirstOrDefault(kp => kp.Key == item.Key);
                if (ff.Value != null)
                {
                    ff.Value.LayerDatas = item.Value.LayerDatas;
                }
                else
                {
                    forkAgv.WorkStations.Stations.Add(item.Key, new clsWorkStationData
                    {
                        LayerDatas = item.Value.LayerDatas
                    });
                }
            }
            //remove not exist.
            var tagsToStore = data.Keys;
            forkAgv.WorkStations.Stations = forkAgv.WorkStations.Stations.Where(st => tagsToStore.Contains(st.Key))
                                                                         .ToDictionary(st => st.Key, st => st.Value);

            bool confirm = forkAgv.SaveTeachDAtaSettings();
            return Ok(new { confirm, data = GetMappData() });
        }

        [HttpGet("Workstation/HandshakeSetting")]
        public async Task<IActionResult> HandshakeSetting(int eq_tag, bool need_handshake)
        {
            if (agv.WorkStations.Stations.TryGetValue(eq_tag, out var options))
            {
                options.HandShakeModeHandShakeMode = need_handshake ? WORKSTATION_HS_METHOD.HS : WORKSTATION_HS_METHOD.NO_HS;
                logger.LogTrace($"WorkStation-{eq_tag} Handshake Mode changed to {options.HandShakeModeHandShakeMode}");
            }
            bool confirm = agv.SaveTeachDAtaSettings();
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

        [HttpGet("Fork/Horizon")]
        public async Task<IActionResult> HorizonForkAction(string action, double pose = 0, double speed = 1, bool stopByObstacle = true)
        {
            (bool confirm, string message) result = (false, "");
            forkAgv.logger.LogTrace($"[VMSController.Fork] Horizon Fork Action: {action} pose:{pose} speed:{speed}");
            HorizonForkActionService service = (HorizonForkActionService)(forkAgv.AGVC as ForkAGVController).HorizonActionService;

            if (action == "home" || action == "orig")
                result = await service.Home();

            if (action == "stop")
                result = await service.Stop();

            if (action == "init")
                result = await service.Init();

            if (action == "up_limit")
                result = await service.Extend();

            if (action == "down_limit")
                result = await service.Retract();

            if (action == "pose")
                result = await service.Pose(target: pose, speed: speed);

            return Ok(new
            {
                confirm = result.confirm,
                message = result.message
            });
        }

        [HttpGet("Fork")]
        public async Task<IActionResult> ForkAction(string action, double pose = 0, double speed = 1, bool stopByObstacle = true)
        {
            try
            {
                forkAgv.ForkLifter.IsManualOperation = true;
                if (speed == 0)
                    speed = 1;

                bool _isStop = action == "stop";
                bool _isMoveToPoseOperation = action != "home" && action != "orig";
                bool _isSearchOperation = action == "up_search" || action == "down_search";
                clsIOSignal underPressedSensorBypassSignal = forkAgv.WagoDO.VCSOutputs.First(pt => pt.Output == DO_ITEM.Fork_Under_Pressing_SensorBypass);


                if (_isMoveToPoseOperation)
                    speed = speed > MoveToPoseSpeedOfManualMode ? MoveToPoseSpeedOfManualMode : speed;

                bool _isVerticalMotorStopped = forkAgv.WagoDO.GetState(DO_ITEM.Vertical_Motor_Stop);

                bool _isForkUnderPressSensorBypassed = forkAgv.WagoDO.GetState(DO_ITEM.Fork_Under_Pressing_SensorBypass);
                bool _isVerticalPreessSensorTrigered = !forkAgv.WagoDI.GetState(DI_ITEM.Fork_Under_Pressing_Sensor);

                if (_isVerticalMotorStopped)
                    return Ok(new { confirm = false, message = "垂直馬達 [STOP] 訊號ON，Z軸無法動作。" });

                if (_isSearchOperation)
                {
                    if (action == "down_search")
                    {
                        if (_isForkUnderPressSensorBypassed)
                            return Ok(new
                            {
                                confirm = false,
                                message = $"[{underPressedSensorBypassSignal.Address}] {underPressedSensorBypassSignal.Name} 開啟中禁止操作向下搜尋動作"
                            });

                        if (_isVerticalPreessSensorTrigered)
                            return Ok(new
                            {
                                confirm = false,
                                message = $"牙叉防壓Sensor觸發中:禁止操作向下搜尋動作"
                            });
                    }

                    if (_isVerticalPreessSensorTrigered && !_isForkUnderPressSensorBypassed)
                        return Ok(new
                        {
                            confirm = false,
                            message = $"(須將 [{underPressedSensorBypassSignal.Address}] {underPressedSensorBypassSignal.Name} 開啟)"
                        });
                }

                if (!_isSearchOperation && !_isStop && _isVerticalPreessSensorTrigered && !_isForkUnderPressSensorBypassed)
                {
                    return Ok(new
                    {
                        confirm = false,
                        message = $"牙叉防壓Sensor觸發中，Z軸無法動作。" +
                                  $"(須將 [{underPressedSensorBypassSignal.Address}] {underPressedSensorBypassSignal.Name} 開啟)"
                    });
                }

                if (!_isSearchOperation && !_isStop && !forkAgv.IsVerticalForkInitialized)
                    return Ok(new { confirm = false, message = "禁止操作:Z軸尚未初始化" });

                if (forkAgv.ForkLifter.IsInitialing)
                    return Ok(new { confirm = false, message = "禁止操作:Z軸正在進行初始化" });

                string current_cmd = (forkAgv.AGVC as ForkAGVController).verticalActionService.CurrentForkActionRequesting.command;

                if (forkAgv.IsForkWorking && !_isStop)
                    return Ok(new { confirm = false, message = $"禁止操作:Z軸正在執行動作({current_cmd})" });

                if (action == "home" || action == "orig")
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
                    if (pose < forkAgv.Parameters.ForkAGV.DownlimitPose)
                        pose = forkAgv.Parameters.ForkAGV.DownlimitPose;
                    else if (pose > forkAgv.Parameters.ForkAGV.UplimitPose)
                        pose = forkAgv.Parameters.ForkAGV.UplimitPose;
                    (bool success, string message) result = await forkAgv.ForkLifter.ForkPose(pose, speed, invokeActionStart: stopByObstacle);

                    return Ok(new { confirm = result.success, message = result.message });
                }
                else if (action == "up")
                {
                    var pose_to = forkAgv.ForkLifter.CurrentHeightPosition + 0.1;
                    (bool success, string message) result = await forkAgv.ForkLifter.ForkPose(pose_to, speed, invokeActionStart: false);

                    return Ok(new { confirm = result.success, message = result.message });
                }
                else if (action == "down")
                {
                    var pose_to = forkAgv.ForkLifter.CurrentHeightPosition - 0.1;
                    (bool success, string message) result = await forkAgv.ForkLifter.ForkPose(pose_to, speed, invokeActionStart: false);

                    return Ok(new { confirm = result.success, message = result.message });
                }
                else if (action == "up_limit")
                {
                    var pose_to = forkAgv.Parameters.ForkAGV.UplimitPose;
                    (bool success, string message) result = await forkAgv.ForkLifter.ForkPose(pose_to, speed, invokeActionStart: stopByObstacle);
                    return Ok(new { confirm = true });
                }
                else if (action == "down_limit")
                {
                    var pose_to = forkAgv.Parameters.ForkAGV.DownlimitPose;
                    (bool success, string message) result = await forkAgv.ForkLifter.ForkPose(pose_to, speed, invokeActionStart: stopByObstacle);

                    return Ok(new { confirm = true });
                }
                else if (action == "stop")
                {
                    (bool success, string message) result = await forkAgv.ForkLifter.ForkStopAsync();
                    return Ok(new { confirm = result.success, message = result.message });
                }
                else if (action == "resume")
                {
                    (bool confirm, string message) result = await forkAgv.ForkLifter.ForkResumeAction();
                    return Ok(new { confirm = result.confirm, message = result.message });
                }
                else if (action == "increase")
                {
                    var pose_to = forkAgv.ForkLifter.CurrentHeightPosition + pose;
                    logger.LogWarning($"USER adjust fork position from web ui:pose to = {pose_to}");
                    (bool success, string message) result = await forkAgv.ForkLifter.ForkPose(pose_to, speed, invokeActionStart: stopByObstacle);

                    return Ok(new { confirm = result.success, message = result.message });
                }
                else if (action == "up_search")
                {
                    (bool success, string message) result = await forkAgv.ForkLifter.ForkUpSearchAsync(speed);
                    return Ok(new { confirm = result.success, message = result.message });
                }
                else if (action == "down_search")
                {
                    (bool success, string message) result = await forkAgv.ForkLifter.ForkDownSearchAsync(speed);
                    return Ok(new { confirm = result.success, message = result.message });
                }
                else
                    return Ok(new { confirm = false, message = "invalid action type" });
            }
            catch (Exception ex)
            {
                return Ok(new { confirm = false, message = ex.Message });

            }
            finally
            {
                // 延遲一下再關閉
                _ = Task.Delay(1000).ContinueWith(t => forkAgv.ForkLifter.IsManualOperation = false);
            }

        }

        [HttpGet("Fork/Arm/Extend")]
        public async Task<IActionResult> ForkArmExtend()
        {
            if (agv.AGVC.ActionStatus == RosSharp.RosBridgeClient.Actionlib.ActionStatus.ACTIVE)
                return Ok(new { confirm = false, message = "禁止在AGV移動過程中伸出牙叉" });
            if (agv.BarcodeReader.CurrentTag != 0)
            {
                var currentTag = agv.NavingMap.Points.Values.FirstOrDefault(pt => pt.TagNumber == agv.BarcodeReader.CurrentTag);
                if (currentTag != null)
                {
                    if (currentTag.IsCharge)
                    {
                        return Ok(new { confirm = false, message = "AGV 位於充電站內禁止牙叉伸出" });
                    }
                }
            }
            else if (!agv.WagoDI.GetState(VehicleControl.DIOModule.clsDIModule.DI_ITEM.Fork_Frontend_Abstacle_Sensor))
            {
                return Ok(new { confirm = false, message = "前端障礙物檢出!" });
            }
            forkAgv.ForkLifter.ForkExtendOutAsync();
            return Ok(new { confirm = true, message = "" });
        }
        [HttpGet("Fork/Arm/Shorten")]
        public async Task<IActionResult> ForkArmShorten()
        {
            forkAgv.ForkLifter.ForkShortenInAsync();
            return Ok(new { confirm = true, message = "" });
        }
        [HttpGet("Fork/Arm/Stop")]
        public async Task<IActionResult> ForkArmStop()
        {
            forkAgv.ForkLifter.ForkARMStop();
            return Ok();
        }
        [HttpGet("Fork/Command_Action")]
        public async Task<IActionResult> Command_Action(string command, double target, double speed)
        {
            (forkAgv.AGVC as ForkAGVController).verticalActionService.CallVerticalCommandService(new AGVSystemCommonNet6.GPMRosMessageNet.Services.VerticalCommandRequest
            {
                command = command,
                model = "FORK",
                target = target,
                speed = speed
            });
            return Ok();
        }

        [HttpGet("Fork/Pin/Init")]
        public async Task<IActionResult> PinInit()
        {
            forkAgv.PinHardware?.Init();
            return Ok();
        }

        [HttpGet("Fork/Pin/Lock")]
        public async Task<IActionResult> PinLock()
        {
            if (forkAgv.PinHardware == null)
                return Ok(new
                {
                    confirm = false,
                    message = "AGV沒有安裝浮動牙叉"
                });
            await forkAgv.PinHardware?.Lock();
            return Ok(new
            {
                confirm = true
            });
        }
        [HttpGet("Fork/Pin/Release")]
        public async Task<IActionResult> PinRelease()
        {
            if (forkAgv.PinHardware == null)
                return Ok(new
                {
                    confirm = false,
                    message = "AGV沒有安裝浮動牙叉"
                });
            await forkAgv.PinHardware?.Release();
            return Ok(new
            {
                confirm = true
            });
        }
    }
}
