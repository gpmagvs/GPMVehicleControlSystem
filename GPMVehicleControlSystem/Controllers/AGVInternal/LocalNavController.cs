
using Microsoft.AspNetCore.Mvc;
using AGVSystemCommonNet6.MAP;
using GPMVehicleControlSystem.Models;
using AGVSystemCommonNet6.AGVDispatch.Messages;
using GPMVehicleControlSystem.Models.VehicleControl;
using AGVSystemCommonNet6;
using Microsoft.AspNetCore.Razor.TagHelpers;
using GPMVehicleControlSystem.Models.VehicleControl.Vehicles;
using GPMVehicleControlSystem.ViewModels;
using GPMVehicleControlSystem.Models.NaviMap;
using static AGVSystemCommonNet6.MAP.MapPoint;
using NLog;

namespace GPMVehicleControlSystem.Controllers.AGVInternal
{
    [Route("api/[controller]")]
    [ApiController]
    public class LocalNavController : ControllerBase
    {
        private Vehicle agv => StaStored.CurrentVechicle;
        Logger logger = LogManager.GetCurrentClassLogger();

        [HttpGet("TaskContinueTest")]
        public async Task TaskContinueTest()
        {
            agv.ExecutingTaskEntity.TaskCancelByReplan.Cancel();
        }


        [HttpGet("Action")]
        public async Task<IActionResult> Action(ACTION_TYPE action, string? from, string? to = "", string? cst_id = "")
        {

            //重新下載圖資
            //if (agv.AGVS.Connected)
            //    await agv.DownloadMapFromServer();

            if (agv.Remote_Mode == REMOTE_MODE.ONLINE)
            {
                return Ok(new
                {
                    accpet = false,
                    error_message = $"AGV需為 OFFLine 模式才可以執行任務"
                });
            }

            if (agv.GetSub_Status() != AGVSystemCommonNet6.clsEnums.SUB_STATUS.IDLE && agv.GetSub_Status() != AGVSystemCommonNet6.clsEnums.SUB_STATUS.Charging)
            {
                return Ok(new
                {
                    accpet = false,
                    error_message = $"AGV當前狀態無法執行任務({agv.GetSub_Status()})"
                });
            }

            if (agv.Parameters.AgvType != clsEnums.AGV_TYPE.INSPECTION_AGV && agv.Parameters.LocalTaskCheckCargoExist)
            {
                if (agv.BarcodeReader.CurrentTag == 0)
                    return Ok(new
                    {
                        accpet = false,
                        error_message = $"AGV需停在Tag上才可以執行任務。"
                    });


                if (agv.CargoStateStorer.HasAnyCargoOnAGV(agv.Parameters.LDULD_Task_No_Entry) && action == ACTION_TYPE.Unload)
                    return Ok(new
                    {
                        accpet = false,
                        error_message = $"AGV車上有貨物不可進行取貨任務"
                    });

                if (!agv.CargoStateStorer.HasAnyCargoOnAGV(agv.Parameters.LDULD_Task_No_Entry) && action == ACTION_TYPE.Load)
                    return Ok(new
                    {
                        accpet = false,
                        error_message = $"AGV車上無貨物不可進行放貨任務"
                    });
            }


            (bool confirm, string message) hardware_status_check;
            if (!(hardware_status_check = agv.CheckHardwareStatus()).confirm)
            {
                return Ok(new
                {
                    accpet = false,
                    error_message = hardware_status_check.message
                });
            }

            from = from == null ? "" : from;
            to = to == null ? "" : to;
            cst_id = cst_id == null ? "" : cst_id;

            int fromtag = -1;
            int totag = int.Parse(to);
            int currentTag = -1;

            if (agv.Parameters.AgvType == clsEnums.AGV_TYPE.INSPECTION_AGV)
            {
                IOrderedEnumerable<MapPoint> ordered = agv.NavingMap.Points.Values.OrderBy(pt => pt.CalculateDistance(agv.Navigation.Data.robotPose.pose.position.x, agv.Navigation.Data.robotPose.pose.position.y));
                currentTag = ordered.First().TagNumber;
            }
            else
                currentTag = agv.Navigation.LastVisitedTag;

            if (action != ACTION_TYPE.Carry)
                fromtag = currentTag;
            else
                fromtag = int.Parse(from);

            MapPoint? fromStationFound = agv.NavingMap.Points.Values.ToList().FirstOrDefault(st => st.TagNumber == fromtag);
            MapPoint? toStationFound = agv.NavingMap.Points.Values.ToList().FirstOrDefault(st => st.TagNumber == totag);

            if (fromStationFound == null)
            {
                return Ok(new TaskActionResult
                {
                    accpet = false,
                    error_message = $"在圖資中找不到Tag為{fromtag}的站點"
                });
            }
            if (toStationFound == null)
            {
                return Ok(new TaskActionResult
                {
                    accpet = false,
                    error_message = $"在圖資中找不到Tag為{totag}的站點"
                });
            }

            var _OrderInfo = new clsTaskDownloadData.clsOrderInfo
            {
                ActionName = action,
                SourceName = fromStationFound?.Graph.Display,
                DestineName = toStationFound?.Graph.Display,
                SourceTag = fromStationFound.TagNumber,
                DestineTag = toStationFound.TagNumber
            };
            clsTaskDownloadData[]? taskLinkList = CreateActionLinksTaskJobs(agv.NavingMap, action, fromtag, totag);

            bool isPointCoordinationNotDefined = taskLinkList.Any(task => task.ExecutingTrajecory.Any(pt => pt.X > 100 || pt.Y > 100));
            if (isPointCoordinationNotDefined)
            {
                return Ok(new TaskActionResult
                {
                    accpet = false,
                    error_message = "圖資中有尚未踩點的點位，禁止派送任務!"
                });
            }
            //LOG.INFO($"Local Task Dispath, Task Link Count: {taskLinkList.Length},({string.Join("->", taskLinkList.Select(act => act.Action_Type))})");
            if (agv.Operation_Mode != clsEnums.OPERATOR_MODE.AUTO)
                await agv.Auto_Mode_Siwtch(clsEnums.OPERATOR_MODE.AUTO);

            if (taskLinkList.Length >= 1)
            {
                _ = Task.Run(async () =>
                  {
                      foreach (clsTaskDownloadData? _taskDataDto in taskLinkList)
                      {
                          if (agv.GetSub_Status() == clsEnums.SUB_STATUS.DOWN)
                              return;
                          _taskDataDto.OrderInfo = _OrderInfo;
                          logger.Warn($"[Local Task Dispather] Wait Task-{_taskDataDto.Action_Type} Done...");
                          await agv.ExecuteAGVSTask(_taskDataDto);
                          logger.Warn($"[Local Task Dispather] Task -{_taskDataDto.Action_Type} Done.!");
                      }
                  });
                return Ok(new TaskActionResult
                {
                    accpet = true,
                    error_message = "",
                    path = taskLinkList.First().ExecutingTrajecory
                });
            }
            else
            {
                return Ok(new TaskActionResult
                {
                    accpet = true,
                    error_message = "",
                });
            }


        }

        [HttpPost("MoveTo")]
        public async Task<IActionResult> MoveTest(MoveTestVM testVM)
        {
            if (agv.GetSub_Status() != clsEnums.SUB_STATUS.IDLE)
            {
                return Ok(new { confirm = false, message = "AGV狀態異常，請先進行初始化" });
            }

            var currentX = agv.Navigation.Data.robotPose.pose.position.x;
            var currentY = agv.Navigation.Data.robotPose.pose.position.y;

            clsTaskDownloadData data = new clsTaskDownloadData()
            {
                Action_Type = ACTION_TYPE.None,
                Task_Name = $"Test_{DateTime.Now.ToString("yyyyMMdd_HHmmssfff")}",
                Trajectory = new clsMapPoint[]
                 {
                     new clsMapPoint
                     {
                          Point_ID = agv.Navigation.LastVisitedTag,
                           X = currentX,
                          Y = currentY,
                          Theta = agv.Navigation.Angle,
                          index = 0,
                          Speed=1,
                          Laser=0,
                          Control_Mode = new clsControlMode
                          {
                               Spin=testVM.Direction,
                               Dodge =testVM.LaserMode
                          }
                     },
                     new clsMapPoint
                     {
                          Point_ID =testVM.DestinPointID,
                           X =testVM.X,
                          Y =testVM.Y,
                          Theta = testVM.Theta,
                          index = 1,
                          Speed=testVM.Speed,
                          Laser =testVM.LaserMode,
                          Control_Mode = new clsControlMode
                          {
                               Spin=testVM.Direction,
                               Dodge =testVM.LaserMode
                          }
                     }
                 },
                Destination = testVM.DestinPointID
            };
            var confirmed = await agv.AGVC.ExecuteTaskDownloaded(data);
            return Ok(new { confirm = confirmed.Accept, message = confirmed.ResultCode.ToString() });
        }
        [HttpGet("MoveTo")]
        public async Task<IActionResult> MoveTo(double x, double y, double theta, int point_id)
        {
            var currentX = agv.Navigation.Data.robotPose.pose.position.x;
            var currentY = agv.Navigation.Data.robotPose.pose.position.y;

            clsTaskDownloadData data = new clsTaskDownloadData()
            {
                Action_Type = ACTION_TYPE.None,
                Task_Name = $"Test_{DateTime.Now.ToString("yyyyMMdd_HHmmssfff")}",
                Trajectory = new clsMapPoint[]
                 {
                     new clsMapPoint
                     {
                          Point_ID = 1,
                           X = currentX,
                          Y = currentY,
                          index = 0,
                          Speed=1,
                          Laser=0,
                          Control_Mode = new clsControlMode
                          {
                               Spin=0
                          }
                     },
                     new clsMapPoint
                     {
                          Point_ID =point_id,
                           X = x,
                          Y =y,
                          Theta = theta,
                          index = 1,
                          Speed=1,
                          Laser =0,
                          Control_Mode = new clsControlMode
                          {
                               Spin=0
                          }
                     }
                 },
                Destination = point_id
            };
            agv.AGVC.ExecuteTaskDownloaded(data);
            return Ok();
        }

        private clsTaskDownloadData CreateMoveActionTaskJob(Map mapData, ACTION_TYPE actionType, string TaskName, int fromTag, int toTag, int Task_Sequence)
        {
            var pathFinder = new PathFinder();
            var pathPlanDto = pathFinder.FindShortestPathByTagNumber(mapData, fromTag, toTag);
            clsTaskDownloadData actionData = new clsTaskDownloadData()
            {
                Action_Type = actionType,
                Destination = toTag,
                Task_Name = TaskName,
                Station_Type = STATION_TYPE.Normal,
                Task_Simplex = $"{TaskName}-{Task_Sequence}",
                Task_Sequence = Task_Sequence,
                Trajectory = PathFinder.GetTrajectory(mapData.Name, pathPlanDto.stations),

            };
            return actionData;
        }

        private clsTaskDownloadData[] CreateActionLinksTaskJobs(Map mapData, ACTION_TYPE actionType, int fromTag, int toTag)
        {
            string Task_Name = $"UI_{DateTime.Now.ToString("yyyyMMddHHmmssff")}";
            int seq = 1;
            PathFinder pathFinder = new PathFinder();
            int normal_move_start_tag;
            int normal_move_final_tag;
            List<clsTaskDownloadData> taskList = new List<clsTaskDownloadData>();

            MapPoint? currentStation = mapData.Points.First(i => i.Value.TagNumber == fromTag).Value;
            MapPoint? destineStation = mapData.Points.First(i => i.Value.TagNumber == toTag).Value;
            MapPoint secondaryLocStation = mapData.Points[destineStation.Target.First().Key];

            bool isInChargeOrEqPortStation = currentStation.StationType != STATION_TYPE.Normal;
            MapPoint secondaryLocStation_of_chargeStateion = mapData.Points[currentStation.Target.First().Key];
            if (isInChargeOrEqPortStation)
            {
                //Discharge
                clsTaskDownloadData homing_move_task = new clsTaskDownloadData
                {
                    IsLocalTask = true,
                    Task_Name = Task_Name,
                    Task_Simplex = $"{Task_Name}-{seq}",
                    Task_Sequence = seq,
                    Action_Type = ACTION_TYPE.Discharge,
                    Destination = secondaryLocStation_of_chargeStateion.TagNumber,
                    Station_Type = secondaryLocStation_of_chargeStateion.StationType,
                    Homing_Trajectory = PathFinder.GetTrajectory(mapData.Name, new List<MapPoint> { currentStation, secondaryLocStation_of_chargeStateion }),
                };
                taskList.Add(homing_move_task);
                seq += 1;
            }


            normal_move_start_tag = isInChargeOrEqPortStation ? secondaryLocStation_of_chargeStateion.TagNumber : fromTag;

            if (actionType == ACTION_TYPE.None)
                normal_move_final_tag = toTag;
            else
            {
                normal_move_final_tag = secondaryLocStation.TagNumber;
            }

            //add normal 
            PathFinder.clsPathInfo? planPath = pathFinder.FindShortestPath(mapData, isInChargeOrEqPortStation ? secondaryLocStation_of_chargeStateion : currentStation, actionType == ACTION_TYPE.None ? destineStation : secondaryLocStation);
            clsTaskDownloadData normal_move_task = new clsTaskDownloadData
            {
                IsLocalTask = true,
                Task_Name = Task_Name,
                Task_Simplex = $"{Task_Name}-{seq}",
                Task_Sequence = seq,
                Action_Type = ACTION_TYPE.None,
                Destination = normal_move_final_tag,
                Station_Type = STATION_TYPE.Normal,
                Trajectory = PathFinder.GetTrajectory(mapData.Name, planPath.stations),
            };

            if (actionType != ACTION_TYPE.None)
            {
                normal_move_task.Trajectory.Last().Theta = destineStation.Direction; //移動的終點要與機台同向
            }
            if (normal_move_task.Destination != agv.Navigation.LastVisitedTag || CalculateThetaError(normal_move_task.Trajectory.Last().Theta) > 5)
                taskList.Add(normal_move_task);
            seq += 1;

            if (actionType != ACTION_TYPE.None)
            {
                clsTaskDownloadData homing_move_task = new clsTaskDownloadData
                {
                    IsLocalTask = true,
                    Task_Name = Task_Name,
                    Task_Simplex = $"{Task_Name}-{seq}",
                    Task_Sequence = seq,
                    Action_Type = actionType,
                    Destination = destineStation.TagNumber,
                    Station_Type = destineStation.StationType,
                    Homing_Trajectory = PathFinder.GetTrajectory(mapData.Name, new List<MapPoint> { secondaryLocStation, destineStation }),
                };
                taskList.Add(homing_move_task);
            }

            return taskList.ToArray();
        }
        private double CalculateThetaError(double _destinTheta)
        {
            var _agvTheta = agv.Navigation.Angle;
            var theta_error = Math.Abs(_agvTheta - _destinTheta);
            theta_error = theta_error > 180 ? 360 - theta_error : theta_error;
            return Math.Abs(theta_error);
        }

        public class TaskActionResult
        {
            public string agv_name { get; set; } = StaStored.CurrentVechicle.Parameters.VehicleName;
            public bool accpet { get; set; }
            public string error_message { get; set; } = "";
            public clsMapPoint[] path { get; set; } = new clsMapPoint[0];

        }

    }
}
