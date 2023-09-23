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

        public TaskBase ExecutingTask;

        Dictionary<string, List<int>> TaskTrackingTags = new Dictionary<string, List<int>>();

        /// <summary>
        /// 執行派車系統任務
        /// 19:10:07 端點未掃描到QR Code
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="taskDownloadData"></param>
        internal void ExecuteAGVSTask(object? sender, clsTaskDownloadData taskDownloadData)
        {

            Sub_Status = SUB_STATUS.RUN;
            Laser.AllLaserActive();
            WriteTaskNameToFile(taskDownloadData.Task_Name);
            LOG.INFO($"Task Download: Task Name = {taskDownloadData.Task_Name} , Task Simple = {taskDownloadData.Task_Simplex}", false);
            LOG.WARN($"{taskDownloadData.Task_Simplex},Trajectory: {string.Join("->", taskDownloadData.ExecutingTrajecory.Select(pt => pt.Point_ID))}");
            ACTION_TYPE action = taskDownloadData.Action_Type;

            if (!TaskTrackingTags.TryAdd(taskDownloadData.Task_Simplex, taskDownloadData.TagsOfTrajectory))
            {
                if (taskDownloadData.TagsOfTrajectory.Count != 1)
                    TaskTrackingTags[taskDownloadData.Task_Simplex] = taskDownloadData.TagsOfTrajectory;
            }

            Task.Run(async () =>
            {
                if (IsReplanTask(taskDownloadData))
                {
                    LOG.INFO($"在 TAG {BarcodeReader.CurrentTag} (LastVisitedTag={Navigation.LastVisitedTag},Coordination:{Navigation.Data.robotPose.pose.position.x},{Navigation.Data.robotPose.pose.position.y}) 收到新的路徑擴充任務");
                    if (!BuzzerPlayer.IsMovingPlaying)
                    {
                        await BuzzerPlayer.Stop();
                        await Task.Delay(100);
                        BuzzerPlayer.Move();
                    }
                    StatusLighter.RUN();
                    await ExecutingTask.AGVSPathExpand(taskDownloadData);
                    FeedbackTaskStatus(TASK_RUN_STATUS.NAVIGATING);
                }
                else
                {
                    clsTaskDownloadData _taskDownloadData;
                    _taskDownloadData = taskDownloadData;

                    if (action == ACTION_TYPE.None)
                        ExecutingTask = new NormalMoveTask(this, _taskDownloadData);
                    else
                    {
                        if (action == ACTION_TYPE.Charge)
                            ExecutingTask = new ChargeTask(this, _taskDownloadData);
                        else if (action == ACTION_TYPE.Discharge)
                            ExecutingTask = new DischargeTask(this, _taskDownloadData);
                        else if (action == ACTION_TYPE.Load)
                            ExecutingTask = new LoadTask(this, _taskDownloadData);
                        else if (action == ACTION_TYPE.Unload)
                            ExecutingTask = new UnloadTask(this, _taskDownloadData);
                        else if (action == ACTION_TYPE.Park)
                            ExecutingTask = new ParkTask(this, _taskDownloadData);
                        else if (action == ACTION_TYPE.Unpark)
                            ExecutingTask = new UnParkTask(this, _taskDownloadData);
                        else if (action == ACTION_TYPE.Measure)
                            ExecutingTask = new MeasureTask(this, _taskDownloadData);
                        else if (action == ACTION_TYPE.ExchangeBattery)
                            ExecutingTask = new ExchangeBatteryTask(this, _taskDownloadData);
                        else
                        {
                            throw new NotImplementedException();
                        }
                    }
                    previousTagPoint = ExecutingTask?.RunningTaskData.ExecutingTrajecory[0];
                    ExecutingTask.ForkLifter = ForkLifter;
                    await Task.Delay(1000);
                    //if ((action == ACTION_TYPE.Load | action == ACTION_TYPE.Unload) && Parameters.LDULD_Task_No_Entry)
                    //{
                    //    LOG.WARN($"Load/Unload Task With NO ENTER EQ MODE(Valid By Parameter setting)");
                    //    Sub_Status = SUB_STATUS.RUN;
                    //    await FeedbackTaskStatus(TASK_RUN_STATUS.ACTION_START);
                    //    await Task.Delay(1000);

                    //    if (Parameters.CST_READER_TRIGGER && action == ACTION_TYPE.Unload)
                    //    {
                    //        (bool confirm, AlarmCodes alarmCode) cstReadResult = await (ExecutingTask as UnloadTask).CSTBarcodeReadAfterAction();
                    //        if (!cstReadResult.confirm)
                    //        {
                    //            Sub_Status = SUB_STATUS.DOWN;
                    //            AlarmManager.AddAlarm(cstReadResult.alarmCode, false);
                    //        }
                    //        else
                    //            Sub_Status = SUB_STATUS.IDLE;

                    //        await FeedbackTaskStatus(TASK_RUN_STATUS.ACTION_FINISH);
                    //    }
                    //    else
                    //    {
                    //        Sub_Status = SUB_STATUS.IDLE;
                    //        await FeedbackTaskStatus(TASK_RUN_STATUS.ACTION_FINISH);
                    //        return;
                    //    }
                    //}
                    var result = await ExecutingTask.Execute();
                    if (result != AlarmCodes.None)
                    {
                        Sub_Status = SUB_STATUS.DOWN;
                        LOG.Critical($"{action} 任務失敗:Alarm:{result}");
                        FeedbackTaskStatus(TASK_RUN_STATUS.ACTION_FINISH);
                        AlarmManager.AddAlarm(result, false);
                        AGVC.OnAGVCActionChanged = null;
                    }
                }
            });
        }

        private bool IsReplanTask(clsTaskDownloadData taskDownloadData)
        {
            if (ExecutingTask == null)
                return false;

            var newAction = taskDownloadData.Action_Type;
            var newTaskName = taskDownloadData.Task_Name;
            var previousAction = ExecutingTask.RunningTaskData.Action_Type;
            var previousTaskName = ExecutingTask.RunningTaskData.Task_Name;

            if (newTaskName != ExecutingTask.RunningTaskData.Task_Name)
                return false;
            if (newAction != ACTION_TYPE.None)
                return false;
            if (taskDownloadData.Trajectory.First().Point_ID != ExecutingTask.RunningTaskData.Trajectory.First().Point_ID)
                return false;

            return newAction == previousAction && taskDownloadData.Task_Name == previousTaskName;
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

        private void OnTagLeaveHandler(object? sender, int leaveTag)
        {
            if (Operation_Mode == OPERATOR_MODE.MANUAL)
                return;
            Task.Factory.StartNew(() =>
            {
                LOG.INFO($"脫離 Tag {previousTagPoint.Point_ID}", false);
            });

        }

        private clsMapPoint? previousTagPoint;
        private void HandleLastVisitedTagChanged(object? sender, int newVisitedNodeTag)
        {
            Task.Factory.StartNew(async () =>
            {
                if (Operation_Mode == OPERATOR_MODE.MANUAL)
                    return;
                if (ExecutingTask == null)
                    return;

                previousTagPoint = ExecutingTask.RunningTaskData.ExecutingTrajecory.FirstOrDefault(pt => pt.Point_ID == newVisitedNodeTag);
                if (previousTagPoint == null)
                {
                    LOG.Critical($"AGV抵達 {newVisitedNodeTag} 但在任務軌跡上找不到該站點。");//脫離路徑
                    AGVC.AbortTask();
                    AlarmManager.AddAlarm(AlarmCodes.Motion_control_Out_Of_Line_While_Moving, false);
                    Sub_Status = SUB_STATUS.DOWN;
                    return;
                }

                if (ExecutingTask.action == ACTION_TYPE.None)
                {
                    var laser_mode = ExecutingTask.RunningTaskData.ExecutingTrajecory.FirstOrDefault(pt => pt.Point_ID == newVisitedNodeTag).Laser;
                    await Laser.ModeSwitch(laser_mode, true);
                }

                if (ExecutingTask.RunningTaskData.TagsOfTrajectory.Last() != Navigation.LastVisitedTag)
                {
                    FeedbackTaskStatus(TASK_RUN_STATUS.NAVIGATING);
                }
            });
        }

        clsMapPoint NextTagPoint;
        private TRAFFIC_ACTION _TrafficState = TRAFFIC_ACTION.PASS;
        internal TRAFFIC_ACTION TrafficState
        {
            get => _TrafficState;
            set
            {
                if (_TrafficState != value)
                {
                    _TrafficState = value;

                    if (_TrafficState == TRAFFIC_ACTION.PASS)
                    {
                        if (IsAllLaserNoTrigger())
                        {
                            AGVC.CarSpeedControl(ROBOT_CONTROL_CMD.SPEED_Reconvery);
                            DirectionLighter.Forward();
                        }
                        else
                        {
                            _TrafficState = TRAFFIC_ACTION.WAIT;
                        }
                        LOG.INFO($"交管訊號以解除 {NextTagPoint?.Point_ID} Release!");
                    }
                    else
                    {
                        LOG.WARN($"交管訊號觸發 等待{NextTagPoint.Point_ID} Release...");

                        Task.Factory.StartNew(async () =>
                        {
                            await AGVC.CarSpeedControl(ROBOT_CONTROL_CMD.DECELERATE);
                            await Task.Delay(50);
                            await AGVC.CarSpeedControl(ROBOT_CONTROL_CMD.STOP);
                            DirectionLighter.WaitPassLights();
                        });
                    }
                }
            }
        }
        private async Task TrafficMonitor()
        {
            await Task.Delay(3000);
            _ = Task.Run(() =>
            {
                LOG.INFO($"Traffic Monitor Start!");
                while (true)
                {
                    Thread.Sleep(1);
                    if (!Parameters.ActiveTrafficControl)
                        continue;
                    try
                    {
                        if (ExecutingTask == null)
                        {
                            TrafficState = TRAFFIC_ACTION.PASS;
                            continue;
                        }
                        if (Remote_Mode == REMOTE_MODE.OFFLINE)
                        {
                            TrafficState = TRAFFIC_ACTION.PASS;
                            continue;
                        }

                        clsMapPoint? TagPoint = ExecutingTask.RunningTaskData.ExecutingTrajecory.FirstOrDefault(pt => pt.Point_ID == Navigation.LastVisitedTag);
                        var nextTagIndex = ExecutingTask.RunningTaskData.ExecutingTrajecory.ToList().IndexOf(TagPoint) + 1;
                        if (nextTagIndex >= ExecutingTask.RunningTaskData.ExecutingTrajecory.Length)
                        {
                            NextTagPoint = null;
                            TrafficState = TRAFFIC_ACTION.PASS;
                            continue;
                        }
                        NextTagPoint = ExecutingTask.RunningTaskData.ExecutingTrajecory[nextTagIndex];
                        TrafficState = DynamicTrafficState.GetTrafficStatusByTag(Parameters.VehicleName, NextTagPoint.Point_ID);
                    }
                    catch (Exception ex)
                    {
                        LOG.Critical("[TrafficMonitor_Error]", ex);
                    }

                }
            });
        }
        private async Task TrafficStop()
        {

            if (Parameters.VMSParam.Protocol != VMS_PROTOCOL.GPM_VMS)
                return;

            clsMapPoint? TagPoint = ExecutingTask.RunningTaskData.ExecutingTrajecory.FirstOrDefault(pt => pt.Point_ID == Navigation.LastVisitedTag);
            var nextTagIndex = ExecutingTask.RunningTaskData.ExecutingTrajecory.ToList().IndexOf(TagPoint) + 1;
            if (ExecutingTask.RunningTaskData.ExecutingTrajecory.Length > nextTagIndex)
            {
                _ = Task.Factory.StartNew(async () =>
                 {
                     var NextTagPoint = ExecutingTask.RunningTaskData.ExecutingTrajecory[nextTagIndex];
                     //取得下一個位置動態
                     bool stopedFlag = false;
                     while ((TrafficState = DynamicTrafficState.GetTrafficStatusByTag(Parameters.VehicleName, NextTagPoint.Point_ID)) != TRAFFIC_ACTION.PASS)
                     {
                         if (!stopedFlag)
                         {
                             LOG.WARN($"交管訊號觸發 等待{NextTagPoint.Point_ID} Release...");
                             await AGVC.CarSpeedControl(ROBOT_CONTROL_CMD.DECELERATE);
                             await Task.Delay(50);
                             await AGVC.CarSpeedControl(ROBOT_CONTROL_CMD.STOP);
                             stopedFlag = true;
                             DirectionLighter.WaitPassLights();
                         }
                         await Task.Delay(1000);
                     }
                     DirectionLighter.CloseAll();
                     DirectionLighter.Forward();
                     LOG.WARN($"交管訊號以解除 {NextTagPoint.Point_ID} Release...");
                     await AGVC.CarSpeedControl(ROBOT_CONTROL_CMD.SPEED_Reconvery);

                 });
            }
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
                AlarmManager.AddWarning( AlarmCodes.Measure_Result_Data_Report_Fail);
            }
        }
        internal async Task FeedbackTaskStatus(TASK_RUN_STATUS status)
        {
            try
            {

                CurrentTaskRunStatus = status;
                if (Remote_Mode == REMOTE_MODE.ONLINE)
                {
                    double X = Math.Round(Navigation.Data.robotPose.pose.position.x, 3);
                    double Y = Math.Round(Navigation.Data.robotPose.pose.position.y, 3);
                    double Theta = Math.Round(Navigation.Angle, 3);
                    clsCoordination coordination = new clsCoordination(X, Y, Theta);
                    if (ExecutingTask != null)
                        await AGVS.TryTaskFeedBackAsync(ExecutingTask.RunningTaskData, GetCurrentTagIndexOfTrajectory(), status, Navigation.LastVisitedTag, coordination);
                }
                if (status == TASK_RUN_STATUS.ACTION_FINISH)
                {
                    AGVC._ActionStatus = ActionStatus.NO_GOAL;
                    CurrentTaskRunStatus = TASK_RUN_STATUS.WAIT;
                    if (ExecutingTask != null)
                    {
                        ExecutingTask.Abort();
                        ExecutingTask.Dispose();
                        ExecutingTask = null;
                    }
                }
            }
            catch (Exception ex)
            {
                LOG.Critical(ex);
            }
        }

        internal int GetCurrentTagIndexOfTrajectory()
        {
            try
            {
                clsMapPoint? currentPt = ExecutingTask.RunningTaskData.ExecutingTrajecory.FirstOrDefault(pt => pt.Point_ID == Navigation.LastVisitedTag);
                if (currentPt == null)
                {
                    LOG.ERROR("計算目前點位在移動路徑中的INDEX過程發生錯誤 !");
                    return -1;
                }
                else
                {
                    return ExecutingTask.RunningTaskData.ExecutingTrajecory.ToList().IndexOf(currentPt);
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
