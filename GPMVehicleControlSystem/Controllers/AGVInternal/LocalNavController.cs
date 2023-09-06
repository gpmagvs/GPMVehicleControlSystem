
using Microsoft.AspNetCore.Mvc;
using AGVSystemCommonNet6.MAP;
using GPMVehicleControlSystem.Models;
using AGVSystemCommonNet6.AGVDispatch.Messages;
using GPMVehicleControlSystem.Models.VehicleControl;
using AGVSystemCommonNet6.Log;
using AGVSystemCommonNet6;
using Microsoft.AspNetCore.Razor.TagHelpers;
using GPMVehicleControlSystem.Models.VehicleControl.Vehicles;
using GPMVehicleControlSystem.ViewModels;

namespace GPMVehicleControlSystem.Controllers.AGVInternal
{
    [Route("api/[controller]")]
    [ApiController]
    public class LocalNavController : ControllerBase
    {
        private Vehicle agv => StaStored.CurrentVechicle;

        [HttpGet("Action")]
        public async Task<IActionResult> Action(ACTION_TYPE action, string? from, string? to = "", string? cst_id = "")
        {


            if (agv.Remote_Mode == REMOTE_MODE.ONLINE)
            {
                return Ok(new
                {
                    accpet = false,
                    error_message = $"AGV於 OFFLine 模式方可執行任務"
                });
            }

            if (agv.Sub_Status != AGVSystemCommonNet6.clsEnums.SUB_STATUS.IDLE && agv.Sub_Status != AGVSystemCommonNet6.clsEnums.SUB_STATUS.Charging)
            {
                return Ok(new
                {
                    accpet = false,
                    error_message = $"AGV當前狀態無法執行任務({agv.Sub_Status})"
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

            var fromStationFound = agv.NavingMap.Points.Values.ToList().FirstOrDefault(st => st.TagNumber == fromtag);
            var toStationFound = agv.NavingMap.Points.Values.ToList().FirstOrDefault(st => st.TagNumber == totag);

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

            clsTaskDownloadData[]? taskLinkList = CreateActionLinksTaskJobs(agv.NavingMap, action, fromtag, totag);
            LOG.INFO($"Local Task Dispath, Task Link Count: {taskLinkList.Length},({string.Join("->", taskLinkList.Select(act => act.Action_Type))})");
            if (taskLinkList.Length >= 1)
            {
                _ = Task.Run(async () =>
                  {
                      foreach (clsTaskDownloadData? _taskDataDto in taskLinkList)
                      {
                          if (agv.Sub_Status == clsEnums.SUB_STATUS.DOWN)
                              return;
                          agv.ExecuteAGVSTask(this, _taskDataDto);
                          await Task.Delay(200);
                          LOG.WARN($"[Local Task Dispather] Wait AGVC Active");
                          while (agv.AGVC.ActionStatus != RosSharp.RosBridgeClient.Actionlib.ActionStatus.ACTIVE)
                          {
                              if (agv.Sub_Status == clsEnums.SUB_STATUS.DOWN)
                                  return;
                              await Task.Delay(1);
                          }

                          LOG.WARN($"[Local Task Dispather]  AGVC Active");
                          await Task.Delay(10);
                          LOG.WARN($"[Local Task Dispather] Wait AGVC Succeeded");

                          while (agv.ExecutingTask != null)
                          {
                              if (agv.Sub_Status == clsEnums.SUB_STATUS.DOWN)
                                  return;
                              await Task.Delay(200);
                          }
                          LOG.WARN($"[Local Task Dispather]  AGVC Succeeded");
                          LOG.INFO("Local WebUI Task Allocator : Next Task Will Start..");
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
                    accpet = false,
                    error_message = "Oppppps!",
                });
            }


        }

        [HttpPost("MoveTo")]
        public async Task<IActionResult> MoveTest(MoveTestVM testVM)
        {
            if (agv.Sub_Status != clsEnums.SUB_STATUS.IDLE)
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
                               Spin=testVM.Direction
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
                               Spin=testVM.Direction
                          }
                     }
                 },
                Destination = testVM.DestinPointID
            };
            var confirmed = await agv.AGVC.ExecuteTaskDownloaded(data);
            return Ok(new { confirm = confirmed.confirm, message = confirmed.message });
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
            var pathPlanDto = pathFinder.FindShortestPathByTagNumber(mapData.Points, fromTag, toTag);
            clsTaskDownloadData actionData = new clsTaskDownloadData()
            {
                Action_Type = actionType,
                Destination = toTag,
                Task_Name = TaskName,
                Station_Type = STATION_TYPE.Normal,
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
                    Task_Name = Task_Name,
                    Task_Sequence = seq,
                    Action_Type = ACTION_TYPE.Discharge,
                    Destination = secondaryLocStation_of_chargeStateion.TagNumber,
                    Station_Type = secondaryLocStation_of_chargeStateion.StationType,
                    Homing_Trajectory = PathFinder.GetTrajectory(mapData.Name, new List<MapPoint> { currentStation, secondaryLocStation_of_chargeStateion })
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
            PathFinder.clsPathInfo? planPath = pathFinder.FindShortestPath(mapData.Points, isInChargeOrEqPortStation ? secondaryLocStation_of_chargeStateion : currentStation, actionType == ACTION_TYPE.None ? destineStation : secondaryLocStation);
            clsTaskDownloadData normal_move_task = new clsTaskDownloadData
            {
                Task_Name = Task_Name,
                Task_Sequence = seq,
                Action_Type = ACTION_TYPE.None,
                Destination = normal_move_final_tag,
                Station_Type = STATION_TYPE.Normal,
                Trajectory = PathFinder.GetTrajectory(mapData.Name, planPath.stations)
            };
            taskList.Add(normal_move_task);
            seq += 1;

            if (actionType != ACTION_TYPE.None)
            {
                clsTaskDownloadData homing_move_task = new clsTaskDownloadData
                {
                    Task_Name = Task_Name,
                    Task_Sequence = seq,
                    Action_Type = actionType,
                    Destination = destineStation.TagNumber,
                    Station_Type = destineStation.StationType,
                    Homing_Trajectory = PathFinder.GetTrajectory(mapData.Name, new List<MapPoint> { secondaryLocStation, destineStation })
                };
                taskList.Add(homing_move_task);
            }


            return taskList.ToArray();
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
