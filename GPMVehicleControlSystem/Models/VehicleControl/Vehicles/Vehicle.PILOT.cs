using AGVSystemCommonNet6.AGVDispatch.Messages;
using AGVSystemCommonNet6.AGVDispatch.Model;
using AGVSystemCommonNet6.Alarm.VMS_ALARM;
using AGVSystemCommonNet6.Log;
using GPMVehicleControlSystem.Models.Buzzer;
using GPMVehicleControlSystem.Models.VehicleControl.TaskExecute;
using RosSharp.RosBridgeClient.Actionlib;
using static AGVSystemCommonNet6.AGVDispatch.Model.clsDynamicTrafficState;
using static AGVSystemCommonNet6.clsEnums;
using static GPMVehicleControlSystem.Models.VehicleControl.AGVControl.CarController;
using static GPMVehicleControlSystem.Models.VehicleControl.TaskExecute.LoadTask;
using static GPMVehicleControlSystem.VehicleControl.DIOModule.clsDOModule;

namespace GPMVehicleControlSystem.Models.VehicleControl.Vehicles
{
    public partial class Vehicle
    {
        private string TaskName = "";
        public TASK_RUN_STATUS CurrentTaskRunStatus = TASK_RUN_STATUS.NO_MISSION;
        public enum EQ_HS_METHOD
        {
            E84,
            MODBUS,
            /// <summary>
            /// 模擬
            /// </summary>
            EMULATION
        }

        private TaskBase _ExecutingTask;
        public TaskBase ExecutingTaskModel
        {
            get => _ExecutingTask;
            set
            {
                if (_ExecutingTask != value)
                {
                    _ExecutingTask = value;
                }
            }
        }

        Dictionary<string, List<int>> TaskTrackingTags = new Dictionary<string, List<int>>();

        internal clsTaskDownloadData _RunTaskData = new clsTaskDownloadData()
        {
            IsLocalTask = true,
            IsActionFinishReported = true
        };


        /// <summary>
        /// 
        /// </summary>
        /// <param name="taskDownloadData"></param>
        /// <returns></returns>
        internal TASK_DOWNLOAD_RETURN_CODES AGVSTaskDownloadConfirm(clsTaskDownloadData taskDownloadData)
        {

            TASK_DOWNLOAD_RETURN_CODES returnCode = TASK_DOWNLOAD_RETURN_CODES.OK;
            AGV_Reset_Flag = AGVSResetCmdFlag = false;

            var action_type = taskDownloadData.Action_Type;

            if (Sub_Status == SUB_STATUS.DOWN) //TODO More Status Confirm when recieve AGVS Task
                returnCode = TASK_DOWNLOAD_RETURN_CODES.AGV_STATUS_DOWN;

            if (Batteries.Average(bat => bat.Value.Data.batteryLevel) < 10)
                returnCode = TASK_DOWNLOAD_RETURN_CODES.AGV_BATTERY_LOW_LEVEL;
            if (Parameters.AgvType != AGV_TYPE.INSPECTION_AGV && taskDownloadData.Destination % 2 == 0 && action_type == ACTION_TYPE.None)
                returnCode = TASK_DOWNLOAD_RETURN_CODES.AGV_CANNOT_GO_TO_WORKSTATION_WITH_NORMAL_MOVE_ACTION;

            LOG.INFO($"Check Status When AGVS Taskdownload, Return Code:{returnCode}({(int)returnCode})");
            return returnCode;
        }

        /// <summary>
        /// 執行派車系統任務
        /// 19:10:07 端點未掃描到QR Code
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="taskDownloadData"></param>
        internal async void ExecuteAGVSTask(object? sender, clsTaskDownloadData taskDownloadData)
        {

            if (IsActionFinishTaskFeedbackExecuting)
            {
                LOG.WARN($"Recieve AGVs Task But [ACTION_FINISH] Feedback TaskStatus Process is Running...");
            }
            CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            while (IsActionFinishTaskFeedbackExecuting)
            {
                if (cts.IsCancellationRequested)
                {
                    taskfeedbackCanceTokenSoruce?.Cancel();
                    IsActionFinishTaskFeedbackExecuting = false;
                    break;
                }
                await Task.Delay(1);
            }
            LOG.WARN($"Recieve AGVs Task and Prepare to Excute!- NO [ACTION_FINISH] Feedback TaskStatus Process is Running!");

            AGVC.OnAGVCActionChanged = null;
            if (ExecutingTaskModel != null)
                ExecutingTaskModel.Dispose();

            AlarmManager.ClearAlarm();
            Sub_Status = SUB_STATUS.RUN;
            await Laser.AllLaserActive();
            WriteTaskNameToFile(taskDownloadData.Task_Name);
            LOG.INFO($"Task Download: Task Name = {taskDownloadData.Task_Name} , Task Simple = {taskDownloadData.Task_Simplex}", false);
            LOG.WARN($"{taskDownloadData.Task_Simplex},Trajectory: {string.Join("->", taskDownloadData.ExecutingTrajecory.Select(pt => pt.Point_ID))}");
            ACTION_TYPE action = taskDownloadData.Action_Type;
            IsWaitForkNextSegmentTask = false;
            _RunTaskData = new clsTaskDownloadData
            {
                Action_Type = taskDownloadData.Action_Type,
                Task_Name = taskDownloadData.Task_Name,
                Task_Sequence = taskDownloadData.Task_Sequence,
                Trajectory = taskDownloadData.Trajectory,
                Homing_Trajectory = taskDownloadData.Homing_Trajectory,
                Destination = taskDownloadData.Destination,
                IsLocalTask = taskDownloadData.IsLocalTask,
                IsActionFinishReported = false,
            };

            LOG.TRACE($"IsLocal Task ? => {_RunTaskData.IsLocalTask}");
            await Task.Run(async () =>
             {
                 if (action == ACTION_TYPE.None)
                 {
                     ExecutingTaskModel = new NormalMoveTask(this, taskDownloadData);
                 }
                 else
                 {
                     if (taskDownloadData.CST.Length == 0 && Remote_Mode == REMOTE_MODE.OFFLINE)
                         taskDownloadData.CST = new clsCST[1] { new clsCST { CST_ID = $"TAEMU{DateTime.Now.ToString("mmssfff")}" } };
                     if (action == ACTION_TYPE.Charge)
                         ExecutingTaskModel = new ChargeTask(this, taskDownloadData);
                     else if (action == ACTION_TYPE.Discharge)
                         ExecutingTaskModel = new DischargeTask(this, taskDownloadData);
                     else if (action == ACTION_TYPE.Load)
                         ExecutingTaskModel = new LoadTask(this, taskDownloadData);
                     else if (action == ACTION_TYPE.Unload)
                         ExecutingTaskModel = new UnloadTask(this, taskDownloadData);
                     else if (action == ACTION_TYPE.Park)
                         ExecutingTaskModel = new ParkTask(this, taskDownloadData);
                     else if (action == ACTION_TYPE.Unpark)
                         ExecutingTaskModel = new UnParkTask(this, taskDownloadData);
                     else if (action == ACTION_TYPE.Measure)
                         ExecutingTaskModel = new MeasureTask(this, taskDownloadData);
                     else if (action == ACTION_TYPE.ExchangeBattery)
                         ExecutingTaskModel = new ExchangeBatteryTask(this, taskDownloadData);
                     else
                     {
                         throw new NotImplementedException();
                     }
                 }
                 previousTagPoint = ExecutingTaskModel?.RunningTaskData.ExecutingTrajecory[0];
                 ExecutingTaskModel.ForkLifter = ForkLifter;
                 IsLaserRecoveryHandled = false;
                 var result = await ExecutingTaskModel.Execute();
                 if (result != AlarmCodes.None)
                 {
                     Sub_Status = SUB_STATUS.DOWN;
                     LOG.Critical($"{action} 任務失敗:Alarm:{result}");
                     AlarmManager.AddAlarm(result, false);
                     AGVC.OnAGVCActionChanged = null;
                 }
                 //}
             });
        }

        private void WriteTaskNameToFile(string task_Name)
        {
            TaskName = task_Name;
            File.WriteAllText("task_name.txt", task_Name);
        }

        private void ReadTaskNameFromFile()
        {
            if (File.Exists("task_name.txt"))
                TaskName = File.ReadAllText("task_name.txt");
        }


        private clsMapPoint? previousTagPoint;
        private void HandleLastVisitedTagChanged(object? sender, int newVisitedNodeTag)
        {
            Task.Factory.StartNew(async () =>
            {
                if (Operation_Mode == OPERATOR_MODE.MANUAL)
                    return;
                if (ExecutingTaskModel == null)
                    return;

                previousTagPoint = ExecutingTaskModel.RunningTaskData.ExecutingTrajecory.FirstOrDefault(pt => pt.Point_ID == newVisitedNodeTag);
                if (previousTagPoint == null)
                {
                    LOG.Critical($"AGV抵達 {newVisitedNodeTag} 但在任務軌跡上找不到該站點。");//脫離路徑
                    AGVC.AbortTask();
                    AlarmManager.AddAlarm(AlarmCodes.Motion_control_Out_Of_Line_While_Moving, false);
                    Sub_Status = SUB_STATUS.DOWN;
                    return;
                }

                if (ExecutingTaskModel.action == ACTION_TYPE.None)
                {
                    var laser_mode = ExecutingTaskModel.RunningTaskData.ExecutingTrajecory.FirstOrDefault(pt => pt.Point_ID == newVisitedNodeTag).Laser;
                    await Laser.ModeSwitch(laser_mode, true);
                }

                if (ExecutingTaskModel.RunningTaskData.TagsOfTrajectory.Last() != Navigation.LastVisitedTag)
                {
                    FeedbackTaskStatus(TASK_RUN_STATUS.NAVIGATING);
                }
            });
        }

        internal async Task ReportMeasureResult(clsMeasureResult measure_result)
        {
            try
            {
                await Task.Delay(100);
                await AGVS.ReportMeasureData(measure_result);
            }
            catch (Exception ex)
            {
                LOG.ERROR(ex.Message, ex);
                AlarmManager.AddWarning(AlarmCodes.Measure_Result_Data_Report_Fail);
            }
        }
        private bool IsActionFinishTaskFeedbackExecuting = false;
        private CancellationTokenSource taskfeedbackCanceTokenSoruce = new CancellationTokenSource();
        /// <summary>
        /// 上報任務狀態
        /// </summary>
        /// <param name="status"></param>
        /// <param name="delay">延遲毫秒數</param>
        /// <returns></returns>
        internal async Task FeedbackTaskStatus(TASK_RUN_STATUS status, int delay = 1000, AlarmCodes alarm_tracking = AlarmCodes.None, bool IsTaskCancel = false)
        {
            try
            {
                IsActionFinishTaskFeedbackExecuting = status == TASK_RUN_STATUS.ACTION_FINISH;
                if (status == TASK_RUN_STATUS.ACTION_FINISH && !IsTaskCancel)
                {

                    IsWaitForkNextSegmentTask = !AGVSResetCmdFlag && ExecutingTaskModel == null ? false : ExecutingTaskModel.isSegmentTask;

                    if (_RunTaskData.IsActionFinishReported && !AGVSResetCmdFlag)
                    {
                        IsActionFinishTaskFeedbackExecuting = false;
                        return;
                    }
                    else
                        _RunTaskData.IsActionFinishReported = true;
                }
                taskfeedbackCanceTokenSoruce = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                await Task.Delay(alarm_tracking == AlarmCodes.None && status == TASK_RUN_STATUS.ACTION_FINISH ? delay : 10);
                CurrentTaskRunStatus = status;
                if (!_RunTaskData.IsLocalTask)
                {
                    double X = Math.Round(Navigation.Data.robotPose.pose.position.x, 3);
                    double Y = Math.Round(Navigation.Data.robotPose.pose.position.y, 3);
                    double Theta = Math.Round(Navigation.Angle, 3);
                    clsCoordination coordination = new clsCoordination(X, Y, Theta);
                    if (alarm_tracking != AlarmCodes.None)
                    {
                        await WaitAlarmCodeReported(alarm_tracking);
                    }
                    await AGVS.TryTaskFeedBackAsync(_RunTaskData, GetCurrentTagIndexOfTrajectory(), status, Navigation.LastVisitedTag, coordination, IsTaskCancel, taskfeedbackCanceTokenSoruce);
                }
                if (status == TASK_RUN_STATUS.ACTION_FINISH)
                {
                    CurrentTaskRunStatus = TASK_RUN_STATUS.WAIT;
                    if (ExecutingTaskModel != null)
                    {
                        ExecutingTaskModel?.Abort();
                        ExecutingTaskModel?.Dispose();
                        ExecutingTaskModel = null;
                    }
                }
                IsActionFinishTaskFeedbackExecuting = false;
            }
            catch (Exception ex)
            {
                LOG.Critical(ex);
            }
            IsActionFinishTaskFeedbackExecuting = false;
        }

        private async Task WaitAlarmCodeReported(AlarmCodes alarm_tracking)
        {
            LOG.WARN($"Before TaskFeedback, AlarmCodes({alarm_tracking}) reported tracking ");
            bool alarm_reported()
            {
                if (AGVS.UseWebAPI)
                    return AGVS.previousRunningStatusReport_via_WEBAPI.Alarm_Code.Any(al => al.Alarm_ID == (int)alarm_tracking);
                else
                    return AGVS.previousRunningStatusReport_via_TCPIP.Alarm_Code.Any(al => al.Alarm_ID == (int)alarm_tracking);
            }
            CancellationTokenSource cancel_wait = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            while (!alarm_reported())
            {
                await Task.Delay(1);
                if (cancel_wait.IsCancellationRequested)
                {
                    LOG.TRACE($"AlarmCodes({alarm_tracking}) not_reported ,, timeout(10 sec) ");
                    return;
                }
            }
            LOG.WARN($"AlarmCodes({alarm_tracking}) reported ! ");

        }

        internal int GetCurrentTagIndexOfTrajectory()
        {
            try
            {
                clsMapPoint? currentPt = _RunTaskData.ExecutingTrajecory.FirstOrDefault(pt => pt.Point_ID == Navigation.LastVisitedTag);
                if (currentPt == null)
                {
                    LOG.ERROR("計算目前點位在移動路徑中的INDEX過程發生錯誤 !");
                    return -1;
                }
                else
                {
                    return _RunTaskData.ExecutingTrajecory.ToList().IndexOf(currentPt);
                }
            }
            catch (Exception ex)
            {
                LOG.ERROR("GetCurrentTagIndexOfTrajectory exception occur !", ex);
                throw new NullReferenceException();
            }

        }

    }
}
