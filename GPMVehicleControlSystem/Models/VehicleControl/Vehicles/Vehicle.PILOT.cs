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

        protected clsTaskDownloadData _RunTaskData = new clsTaskDownloadData()
        {
            IsLocalTask = true,
            IsActionFinishReported = true
        };
        /// <summary>
        /// 執行派車系統任務
        /// 19:10:07 端點未掃描到QR Code
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="taskDownloadData"></param>
        internal async void ExecuteAGVSTask(object? sender, clsTaskDownloadData taskDownloadData)
        {
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

            await Task.Run(async () =>
             {
                 clsTaskDownloadData _taskDownloadData;
                 _taskDownloadData = taskDownloadData;
                 _RunTaskData = new clsTaskDownloadData
                 {
                     IsLocalTask = taskDownloadData.IsLocalTask,
                     IsActionFinishReported = false,
                     Task_Name = taskDownloadData.Task_Name,
                     Task_Sequence = taskDownloadData.Task_Sequence,
                     Trajectory = taskDownloadData.Trajectory,
                     Homing_Trajectory = taskDownloadData.Homing_Trajectory,
                 };
                 if (action == ACTION_TYPE.None)
                 {
                     ExecutingTaskModel = new NormalMoveTask(this, _taskDownloadData);
                     if (Parameters.SimulationMode)
                         WagoDO.SetState(DO_ITEM.EMU_EQ_GO, false);//模擬離開二次定位點EQ GO訊號會消失
                 }
                 else
                 {
                     if (_taskDownloadData.CST.Length == 0 && Remote_Mode == REMOTE_MODE.OFFLINE)
                         _taskDownloadData.CST = new clsCST[1] { new clsCST { CST_ID = $"TAEMU{DateTime.Now.ToString("mmssfff")}" } };
                     if (action == ACTION_TYPE.Charge)
                         ExecutingTaskModel = new ChargeTask(this, _taskDownloadData);
                     else if (action == ACTION_TYPE.Discharge)
                         ExecutingTaskModel = new DischargeTask(this, _taskDownloadData);
                     else if (action == ACTION_TYPE.Load)
                         ExecutingTaskModel = new LoadTask(this, _taskDownloadData);
                     else if (action == ACTION_TYPE.Unload)
                         ExecutingTaskModel = new UnloadTask(this, _taskDownloadData);
                     else if (action == ACTION_TYPE.Park)
                         ExecutingTaskModel = new ParkTask(this, _taskDownloadData);
                     else if (action == ACTION_TYPE.Unpark)
                         ExecutingTaskModel = new UnParkTask(this, _taskDownloadData);
                     else if (action == ACTION_TYPE.Measure)
                         ExecutingTaskModel = new MeasureTask(this, _taskDownloadData);
                     else if (action == ACTION_TYPE.ExchangeBattery)
                         ExecutingTaskModel = new ExchangeBatteryTask(this, _taskDownloadData);
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
                     FeedbackTaskStatus(TASK_RUN_STATUS.ACTION_FINISH, alarm_tracking: result);
                     AGVC.OnAGVCActionChanged = null;
                 }
                 //}
             });
        }

        private void ExecutingActionTask_OnSegmentTaskExecuting2Sec(object? sender, clsTaskDownloadData e)
        {
            LOG.Critical($"分段任務模擬-發送整段任務");
            ExecuteAGVSTask(sender, e);
        }

        private void AutoClearOldCstReadFailAlarms()
        {
            if (AlarmManager.CurrentAlarms.Count == 0)
                return;
            var alarm_codes = new AlarmCodes[] { AlarmCodes.Cst_ID_Not_Match, AlarmCodes.Read_Cst_ID_Fail, AlarmCodes.Read_Cst_ID_Fail_Service_Done_But_Topic_No_CSTID };
            foreach (var alarm_code in alarm_codes)
            {
                var _alarm_key_pair = AlarmManager.CurrentAlarms.FirstOrDefault(a => a.Value.EAlarmCode == alarm_code);
                if (_alarm_key_pair.Value != null)
                    AlarmManager.ClearAlarm(alarm_code);
            }
        }

        private bool IsReplanTask(clsTaskDownloadData taskDownloadData)
        {
            if (_RunTaskData == null)
                return false;
            if (_RunTaskData.ExecutingTrajecory.Length == 0)
                return false;

            var newAction = taskDownloadData.Action_Type;
            var newTaskName = taskDownloadData.Task_Name;
            var previousAction = _RunTaskData.Action_Type;
            var previousTaskName = _RunTaskData.Task_Name;

            if (newTaskName != previousTaskName)
                return false;
            if (newAction != ACTION_TYPE.None)
                return false;

            if (taskDownloadData.Trajectory.First().Point_ID != _RunTaskData.Trajectory.First().Point_ID)
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

        clsMapPoint NextTagPoint;
        //private TRAFFIC_ACTION _TrafficState = TRAFFIC_ACTION.PASS;
        //internal TRAFFIC_ACTION TrafficState
        //{
        //    get => _TrafficState;
        //    set
        //    {
        //        if (_TrafficState != value)
        //        {
        //            _TrafficState = value;

        //            if (_TrafficState == TRAFFIC_ACTION.PASS)
        //            {
        //                if (IsAllLaserNoTrigger())
        //                {
        //                    AGVC.CarSpeedControl(ROBOT_CONTROL_CMD.SPEED_Reconvery);
        //                    DirectionLighter.Forward();
        //                }
        //                else
        //                {
        //                    _TrafficState = TRAFFIC_ACTION.WAIT;
        //                }
        //                LOG.INFO($"交管訊號以解除 {NextTagPoint?.Point_ID} Release!");
        //            }
        //            else
        //            {
        //                LOG.WARN($"交管訊號觸發 等待{NextTagPoint.Point_ID} Release...");

        //                Task.Factory.StartNew(async () =>
        //                {
        //                    await AGVC.CarSpeedControl(ROBOT_CONTROL_CMD.DECELERATE);
        //                    await Task.Delay(50);
        //                    await AGVC.CarSpeedControl(ROBOT_CONTROL_CMD.STOP);
        //                    DirectionLighter.WaitPassLights();
        //                });
        //            }
        //        }
        //    }
        //}
        //private async Task TrafficMonitor()
        //{
        //    await Task.Delay(3000);
        //    _ = Task.Run(() =>
        //    {
        //        LOG.INFO($"Traffic Monitor Start!");
        //        while (true)
        //        {
        //            Thread.Sleep(1);
        //            if (!Parameters.ActiveTrafficControl)
        //                continue;
        //            try
        //            {
        //                if (ExecutingTask == null)
        //                {
        //                    TrafficState = TRAFFIC_ACTION.PASS;
        //                    continue;
        //                }
        //                if (Remote_Mode == REMOTE_MODE.OFFLINE)
        //                {
        //                    TrafficState = TRAFFIC_ACTION.PASS;
        //                    continue;
        //                }

        //                clsMapPoint? TagPoint = ExecutingTask.RunningTaskData.ExecutingTrajecory.FirstOrDefault(pt => pt.Point_ID == Navigation.LastVisitedTag);
        //                var nextTagIndex = ExecutingTask.RunningTaskData.ExecutingTrajecory.ToList().IndexOf(TagPoint) + 1;
        //                if (nextTagIndex >= ExecutingTask.RunningTaskData.ExecutingTrajecory.Length)
        //                {
        //                    NextTagPoint = null;
        //                    TrafficState = TRAFFIC_ACTION.PASS;
        //                    continue;
        //                }
        //                NextTagPoint = ExecutingTask.RunningTaskData.ExecutingTrajecory[nextTagIndex];
        //                TrafficState = DynamicTrafficState.GetTrafficStatusByTag(Parameters.VehicleName, NextTagPoint.Point_ID);
        //            }
        //            catch (Exception ex)
        //            {
        //                LOG.Critical("[TrafficMonitor_Error]", ex);
        //            }

        //        }
        //    });
        //}
        //private async Task TrafficStop()
        //{

        //    if (Parameters.VMSParam.Protocol != VMS_PROTOCOL.GPM_VMS)
        //        return;

        //    clsMapPoint? TagPoint = ExecutingTask.RunningTaskData.ExecutingTrajecory.FirstOrDefault(pt => pt.Point_ID == Navigation.LastVisitedTag);
        //    var nextTagIndex = ExecutingTask.RunningTaskData.ExecutingTrajecory.ToList().IndexOf(TagPoint) + 1;
        //    if (ExecutingTask.RunningTaskData.ExecutingTrajecory.Length > nextTagIndex)
        //    {
        //        _ = Task.Factory.StartNew(async () =>
        //         {
        //             var NextTagPoint = ExecutingTask.RunningTaskData.ExecutingTrajecory[nextTagIndex];
        //             //取得下一個位置動態
        //             bool stopedFlag = false;
        //             while ((TrafficState = DynamicTrafficState.GetTrafficStatusByTag(Parameters.VehicleName, NextTagPoint.Point_ID)) != TRAFFIC_ACTION.PASS)
        //             {
        //                 if (!stopedFlag)
        //                 {
        //                     LOG.WARN($"交管訊號觸發 等待{NextTagPoint.Point_ID} Release...");
        //                     await AGVC.CarSpeedControl(ROBOT_CONTROL_CMD.DECELERATE);
        //                     await Task.Delay(50);
        //                     await AGVC.CarSpeedControl(ROBOT_CONTROL_CMD.STOP);
        //                     stopedFlag = true;
        //                     DirectionLighter.WaitPassLights();
        //                 }
        //                 await Task.Delay(1000);
        //             }
        //             DirectionLighter.CloseAll();
        //             DirectionLighter.Forward();
        //             LOG.WARN($"交管訊號以解除 {NextTagPoint.Point_ID} Release...");
        //             await AGVC.CarSpeedControl(ROBOT_CONTROL_CMD.SPEED_Reconvery);

        //         });
        //    }
        //}
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

        /// <summary>
        /// 上報任務狀態
        /// </summary>
        /// <param name="status"></param>
        /// <param name="delay">延遲毫秒數</param>
        /// <returns></returns>
        internal async Task FeedbackTaskStatus(TASK_RUN_STATUS status, int delay = 1000, AlarmCodes alarm_tracking = AlarmCodes.None)
        {
            try
            {
                if (status == TASK_RUN_STATUS.ACTION_FINISH)
                {
                    if (_RunTaskData.IsActionFinishReported && !AGVSResetCmdFlag)
                        return;
                    else
                        _RunTaskData.IsActionFinishReported = true;
                }
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
                    await AGVS.TryTaskFeedBackAsync(_RunTaskData, GetCurrentTagIndexOfTrajectory(), status, Navigation.LastVisitedTag, coordination);
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
            }
            catch (Exception ex)
            {
                LOG.Critical(ex);
            }
        }

        private async Task WaitAlarmCodeReported(AlarmCodes alarm_tracking)
        {
            LOG.TRACE($"Before TaskFeedback, AlarmCodes({alarm_tracking}) reported tracking ");

            bool alarm_reported()
            {
                if (AGVS.UseWebAPI)
                    return AGVS.previousRunningStatusReport_via_WEBAPI.Alarm_Code.Any(al => al.Alarm_ID == (int)alarm_tracking);
                else
                    return AGVS.previousRunningStatusReport_via_TCPIP.Alarm_Code.Any(al => al.Alarm_ID == (int)alarm_tracking);
            }
            CancellationTokenSource cancel_wait = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            while (!alarm_reported())
            {
                await Task.Delay(1);
                if (cancel_wait.IsCancellationRequested)
                {
                    LOG.TRACE($"AlarmCodes({alarm_tracking}) not_reported ,, timeout(2sec) ");
                    return;
                }
            }
            LOG.TRACE($"AlarmCodes({alarm_tracking}) reported ! ");

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
