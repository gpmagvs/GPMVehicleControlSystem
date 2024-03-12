using AGVSystemCommonNet6.AGVDispatch.Messages;
using AGVSystemCommonNet6.AGVDispatch;
using AGVSystemCommonNet6.Log;
using static AGVSystemCommonNet6.clsEnums;
using AGVSystemCommonNet6.AGVDispatch.Model;
using Newtonsoft.Json;

using GPMVehicleControlSystem.Models.NaviMap;
using AGVSystemCommonNet6.GPMRosMessageNet.Actions;
using RosSharp.RosBridgeClient.Actionlib;
using AGVSystemCommonNet6.MAP;
using System.Diagnostics;
using AGVSystemCommonNet6.Vehicle_Control.VCS_ALARM;

namespace GPMVehicleControlSystem.Models.VehicleControl.Vehicles
{
    public partial class Vehicle
    {
        private async void AGVSInit()
        {
            string vms_ip = Parameters.Connections["AGVS"].IP;
            int vms_port = Parameters.Connections["AGVS"].Port;
            //AGVS
            AGVS = new clsAGVSConnection(vms_ip, vms_port, Parameters.VMSParam.LocalIP);
            AGVS.Setup(Parameters.SID, Parameters.VehicleName);
            AGVS.SetLogFolder(Path.Combine(Parameters.LogFolder, "AGVS_Message_Log"));
            AGVS.UseWebAPI = Parameters.VMSParam.Protocol == VMS_PROTOCOL.GPM_VMS;
            AGVS.OnWebAPIProtocolGetRunningStatus += HandleWebAPIProtocolGetRunningStatus;
            AGVS.OnTcpIPProtocolGetRunningStatus += HandleTcpIPProtocolGetRunningStatus;
            AGVS.OnRemoteModeChanged = HandleRemoteModeChangeReq;
            AGVS.OnTaskDownload += AGVSTaskDownloadConfirm;
            AGVS.OnTaskResetReq = HandleAGVSTaskCancelRequest;
            AGVS.OnTaskDownloadFeekbackDone += ExecuteAGVSTask;
            AGVS.OnConnectionRestored += AGVS_OnConnectionRestored;
            AGVS.OnDisconnected += AGVS_OnDisconnected;
            AGVS.OnPingFail += (sender, arg) =>
            {
                LOG.TRACE($"AGVS Network Ping Fail.... ");
                AlarmManager.AddWarning(AlarmCodes.AGVS_PING_FAIL);
            };
            AGVS.OnPingSuccess += (sender, arg) =>
            {
                LOG.TRACE($"AGVS Network restored. ");
                AlarmManager.ClearAlarm(AlarmCodes.AGVS_PING_FAIL);
            };
            AGVS.Start();
            AGVS.TrySendOnlineModeChangeRequest(BarcodeReader.CurrentTag, REMOTE_MODE.OFFLINE);
            if (AGVS.UseWebAPI)
            {
                var eqinfomations = await GetWorkStationEQInformation();
                if (eqinfomations != null)
                {
                    WorkStations.SyncInfo(eqinfomations);
                    SaveTeachDAtaSettings();
                }
            }

        }
        private void ReloadLocalMap()
        {
            if (File.Exists(Parameters.MapParam.LocalMapFileFullName))
            {
                LOG.WARN($"Try load map from local : {Parameters.MapParam.LocalMapFileFullName}");
                NavingMap = MapStore.GetMapFromFile(Parameters.MapParam.LocalMapFileFullName);
                if (NavingMap.Note != "empty")
                {
                    LOG.WARN($"Local Map data load success: {NavingMap.Name}({NavingMap.Note})");

                    //var lastPoint = NavingMap.Points.FirstOrDefault(pt => pt.Value.TagNumber == Parameters.LastVisitedTag).Value;
                    //if (lastPoint != null && Parameters.SimulationMode)
                    //    StaEmuManager.agvRosEmu.SetCoordination(lastPoint.X, lastPoint.Y, 0);
                }
            }
            else
            {
                LOG.ERROR($"Local map file dosen't exist({Parameters.MapParam.LocalMapFileFullName})");
            }
        }
        internal async Task DownloadMapFromServer()
        {
            await Task.Run(async () =>
            {
                try
                {
                    var _NavingMap = await MapStore.GetMapFromServer();

                    if (_NavingMap != null)
                    {
                        _NavingMap.Segments = MapManager.CreateSegments(_NavingMap);
                        MapStore.SaveCurrentMap(_NavingMap, out string map_file_saved_path);
                        NavingMap = _NavingMap;
                        LOG.INFO($"Map Downloaded. Map Name : {NavingMap.Name}, Version: {NavingMap.Note}");
                    }
                    else
                    {
                        LOG.ERROR($"Cannot download map from server.({MapStore.GetMapUrl})");
                    }
                }
                catch (Exception ex)
                {

                    LOG.Critical($"Map Download Fail....{ex.Message}", ex);
                }
            });
        }



        public async Task<List<clsAGVSConnection.clsEQOptions>> GetWorkStationEQInformation()
        {
            try
            {
                List<clsAGVSConnection.clsEQOptions> eqOptions = await AGVS.GetEQsInfos(WorkStations.Stations.Keys.ToArray());
                LOG.INFO($"WorkStation EQ Infos : \r\n{JsonConvert.SerializeObject(eqOptions, Formatting.Indented)}");
                return eqOptions;
            }
            catch (Exception ex)
            {
                LOG.ERROR($"WorkStation EQ Infos fetch from AGVs Fail {ex.Message}");
                return null;
            }
        }

        private void AGVS_OnDisconnected(object? sender, EventArgs e)
        {
            AlarmManager.AddWarning(AlarmCodes.AGVs_Disconnected);
            RemoteModeSettingWhenAGVsDisconnect = _Remote_Mode == REMOTE_MODE.ONLINE ? REMOTE_MODE.ONLINE : REMOTE_MODE.OFFLINE;
            AutoOnlineRaising = _Remote_Mode == REMOTE_MODE.ONLINE;
            Remote_Mode = REMOTE_MODE.OFFLINE;
        }
        private void AGVS_OnConnectionRestored(object? sender, EventArgs e)
        {
            AlarmManager.ClearAlarm(AlarmCodes.AGVs_Disconnected);
            Task.Factory.StartNew(async () =>
            {
                await Task.Delay(1000);
                if (RemoteModeSettingWhenAGVsDisconnect == REMOTE_MODE.ONLINE && !IsActionFinishTaskFeedbackExecuting && (Sub_Status == SUB_STATUS.IDLE | Sub_Status == SUB_STATUS.Charging))
                {
                    HandleRemoteModeChangeReq(REMOTE_MODE.ONLINE);
                }
            });
        }

        /// <summary>
        /// 生成支援WebAPI的RunningStatus Model
        /// </summary>
        /// <returns></returns>
        public virtual (bool report_allow, clsRunningStatus running_status) HandleWebAPIProtocolGetRunningStatus()
        {

            clsCoordination Corrdination = new clsCoordination();
            MAIN_STATUS _Main_Status = Main_Status;
            int lastVisitedNode = 0;
            if (Navigation.Data != null)
            {
                Corrdination.X = Math.Round(Navigation.Data.robotPose.pose.position.x, 3);
                Corrdination.Y = Math.Round(Navigation.Data.robotPose.pose.position.y, 3);
                Corrdination.Theta = Math.Round(Navigation.Angle, 3);
                lastVisitedNode = Navigation.Data.lastVisitedNode.data;
            }
            AGVSystemCommonNet6.AGVDispatch.Model.clsAlarmCode[] alarm_codes = GetAlarmCodesUserReportToAGVS_WebAPI();
            try
            {
                double[] batteryLevels = Batteries.ToList().FindAll(bt => bt.Value != null).Select(battery => (double)battery.Value.Data.batteryLevel).ToArray();
                var status = new clsRunningStatus
                {
                    Cargo_Status = CargoStatus == CARGO_STATUS.HAS_CARGO_NORMAL ? 1 : 0,
                    CargoType = GetCargoType(),
                    AGV_Status = _Main_Status,
                    Electric_Volume = batteryLevels,
                    Last_Visited_Node = lastVisitedNode,
                    Coordination = Corrdination,
                    Odometry = Odometry,
                    AGV_Reset_Flag = AGV_Reset_Flag,
                    Alarm_Code = alarm_codes,
                    Escape_Flag = ExecutingTaskModel == null ? false : ExecutingTaskModel.RunningTaskData.Escape_Flag,
                    IsCharging = IsCharging
                };
                return (true, status);
            }
            catch (Exception ex)
            {
                //LOG.ERROR("GenRunningStateReportData ", ex);
                return (false, new clsRunningStatus());
            }
        }

        private AGVSystemCommonNet6.AGVDispatch.Model.clsAlarmCode[] GetAlarmCodesUserReportToAGVS_WebAPI()
        {
            return AlarmManager.CurrentAlarms.ToList().FindAll(alarm => alarm.Value.EAlarmCode != AlarmCodes.None).Select(alarm => new AGVSystemCommonNet6.AGVDispatch.Model.clsAlarmCode
            {
                Alarm_ID = alarm.Value.Code,
                Alarm_Level = alarm.Value.ELevel == AGVSystemCommonNet6.Vehicle_Control.VCS_ALARM.clsAlarmCode.LEVEL.Alarm ? 1 : 0,
                Alarm_Description = alarm.Value.CN,
                Alarm_Description_EN = alarm.Value.Description,
                Alarm_Category = alarm.Value.IsRecoverable ? 0 : 1,
            }).DistinctBy(alarm => alarm.Alarm_ID).ToArray();
        }

        /// <summary>
        /// 生成支援TCPIP通訊的RunningStatus Model
        /// </summary>
        /// <returns></returns>
        protected virtual (bool report_allow, RunningStatus running_status) HandleTcpIPProtocolGetRunningStatus()
        {
            clsCoordination Corrdination = new clsCoordination();
            MAIN_STATUS _Main_Status = Main_Status;
            Corrdination.X = Math.Round(Navigation.Data.robotPose.pose.position.x, 3);
            Corrdination.Y = Math.Round(Navigation.Data.robotPose.pose.position.y, 3);
            Corrdination.Theta = Math.Round(Navigation.Angle, 3);
            //gen alarm codes 
            AGVSystemCommonNet6.AGVDispatch.Messages.clsAlarmCode[] alarm_codes = GetAlarmCodesUserReportToAGVS();
            try
            {
                double[] batteryLevels = Batteries.ToList().FindAll(bky => bky.Value != null).Select(battery => (double)battery.Value.Data.batteryLevel).ToArray();
                var status = new RunningStatus
                {
                    Cargo_Status = CargoStatus == CARGO_STATUS.HAS_CARGO_NORMAL ? 1 : 0,
                    CargoType = GetCargoType(),
                    AGV_Status = _Main_Status,
                    Electric_Volume = batteryLevels,
                    Last_Visited_Node = lastVisitedMapPoint.IsVirtualPoint ? lastVisitedMapPoint.TagNumber : Navigation.Data.lastVisitedNode.data,
                    Coordination = Corrdination,
                    Odometry = Odometry,
                    AGV_Reset_Flag = AGV_Reset_Flag,
                    Alarm_Code = _RunTaskData.IsLocalTask && !Debugger.IsAttached ? new AGVSystemCommonNet6.AGVDispatch.Messages.clsAlarmCode[0] : alarm_codes,
                    Escape_Flag = ExecutingTaskModel == null ? false : ExecutingTaskModel.RunningTaskData.Escape_Flag,
                    IsCharging = IsCharging
                };
                return (true, status);
            }
            catch (Exception ex)
            {
                //LOG.ERROR("GenRunningStateReportData ", ex);
                return (false, new RunningStatus());
            }
        }
        private SemaphoreSlim taskExecuteSlim = new SemaphoreSlim(1, 1);
        /// <summary>
        /// 處理任務取消請求
        /// </summary>
        /// <param name="mode">取消模式</param>
        /// <param name="normal_state"></param>
        /// <returns></returns>
        internal async Task<bool> HandleAGVSTaskCancelRequest(RESET_MODE mode, bool normal_state = false)
        {
            await taskExecuteSlim.WaitAsync();

            try
            {
                if (AGVSResetCmdFlag)
                    return true;

                AGVSResetCmdFlag = true;
                IsWaitForkNextSegmentTask = false;

                LOG.WARN($"AGVS TASK Cancel Request ({mode}),Current Action Status={AGVC.ActionStatus}, AGV SubStatus = {Sub_Status}");

                if (AGVC.ActionStatus != ActionStatus.ACTIVE && AGVC.ActionStatus != ActionStatus.PENDING && mode == RESET_MODE.CYCLE_STOP)
                {
                    AGVC.OnAGVCActionChanged = null;
                    LOG.WARN($"AGVS TASK Cancel Request ({mode}),But AGV is stopped.(IDLE)");
                    await AGVC.SendGoal(new TaskCommandGoal());//下空任務清空
                    FeedbackTaskStatus(TASK_RUN_STATUS.ACTION_FINISH, delay: 10, IsTaskCancel: true);
                    AGVC._ActionStatus = ActionStatus.NO_GOAL;
                    AGVSResetCmdFlag = false;
                    AGV_Reset_Flag = true;
                    //Sub_Status = SUB_STATUS.IDLE;
                    return true;
                }
                else
                {
                    bool result = await AGVC.ResetTask(mode);
                    if (mode == RESET_MODE.ABORT)
                    {

                        _ = Task.Factory.StartNew(async () =>
                        {
                            if (!normal_state)
                            {
                                AlarmManager.AddAlarm(AlarmCodes.AGVs_Abort_Task);
                                Sub_Status = SUB_STATUS.DOWN;
                            }
                            ExecutingTaskModel.Abort();
                        });
                        AGVSResetCmdFlag = false;
                        AGV_Reset_Flag = true;
                    }
                    return result;
                }

            }
            catch (Exception ex)
            {
                LOG.Critical(ex);
                return false;
                Sub_Status = SUB_STATUS.DOWN;
            }
            finally
            {
                taskExecuteSlim.Release();
            }

        }

        private static AGVSystemCommonNet6.AGVDispatch.Messages.clsAlarmCode[] GetAlarmCodesUserReportToAGVS()
        {
            return AlarmManager.CurrentAlarms.ToList().FindAll(alarm => alarm.Value.EAlarmCode != AlarmCodes.None).Select(alarm => new AGVSystemCommonNet6.AGVDispatch.Messages.clsAlarmCode
            {
                Alarm_ID = alarm.Value.Code,
                Alarm_Level = alarm.Value.IsRecoverable ? 0 : 1,
                Alarm_Description = alarm.Value.CN,
                Alarm_Description_EN = alarm.Value.Description,
                Alarm_Category = alarm.Value.IsRecoverable ? 0 : 1,
            }).DistinctBy(alarm => alarm.Alarm_ID).ToArray();
        }
    }
}
