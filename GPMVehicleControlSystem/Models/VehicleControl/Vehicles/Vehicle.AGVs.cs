using AGVSystemCommonNet6.AGVDispatch.Messages;
using AGVSystemCommonNet6.AGVDispatch;
using AGVSystemCommonNet6.Alarm.VMS_ALARM;
using AGVSystemCommonNet6.Log;
using static AGVSystemCommonNet6.clsEnums;
using AGVSystemCommonNet6.AGVDispatch.Model;
using Newtonsoft.Json;
using GPMVehicleControlSystem.Models.Emulators;
using GPMVehicleControlSystem.Models.NaviMap;

namespace GPMVehicleControlSystem.Models.VehicleControl.Vehicles
{
    public partial class Vehicle
    {
        private void AGVSInit()
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
                AlarmManager.AddAlarm(AlarmCodes.AGVS_PING_FAIL);
            };
            AGVS.OnPingSuccess += (sender, arg) =>
            {
                LOG.TRACE($"AGVS Network restored. ");
                AlarmManager.ClearAlarm(AlarmCodes.AGVS_PING_FAIL);
            };
            DownloadMapFromServer();
            AGVS.Start();
            AGVS.TrySendOnlineModeChangeRequest(BarcodeReader.CurrentTag, REMOTE_MODE.OFFLINE);

        }

        internal async Task DownloadMapFromServer()
        {
            await Task.Run(async () =>
            {
                try
                {
                    NavingMap = await MapStore.GetMapFromServer();
                    if (NavingMap != null)
                    {
                        MapStore.SaveCurrentMap(NavingMap);
                        LOG.INFO($"Map Downloaded. Map Name : {NavingMap.Name}, Version: {NavingMap.Note}");
                        var lastPoint = NavingMap.Points.FirstOrDefault(pt => pt.Value.TagNumber == Parameters.LastVisitedTag).Value;
                        if (lastPoint != null && Parameters.SimulationMode)
                            StaEmuManager.agvRosEmu.SetCoordination(lastPoint.X, lastPoint.Y, 0);
                    }
                    else
                    {
                        if (File.Exists(Parameters.MapParam.LocalMapFileFullName))
                        {
                            LOG.WARN($"Try load map from local : {Parameters.MapParam.LocalMapFileFullName}");
                            NavingMap = MapStore.GetMapFromFile(Parameters.MapParam.LocalMapFileFullName);
                            if (NavingMap.Note != "empty")
                                LOG.WARN($"Local Map data load success: {NavingMap.Name}({NavingMap.Note})");
                        }
                        else
                        {
                            LOG.ERROR($"Cannot download map from server.({MapStore.GetMapUrl}) and not any local map file exist({Parameters.MapParam.LocalMapFileFullName})");
                        }
                    }
                }
                catch (Exception ex)
                {
                    LOG.WARN($"Map Download Fail....{ex.Message}");
                }
            });
        }



        public async Task<List<clsAGVSConnection.clsEQOptions>> GetWorkStationEQInformation()
        {
            List<clsAGVSConnection.clsEQOptions> eqOptions = await AGVS.GetEQsInfos(WorkStations.Stations.Keys.ToArray());
            LOG.INFO($"WorkStation EQ Infos : \r\n{JsonConvert.SerializeObject(eqOptions, Formatting.Indented)}");
            return eqOptions;
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
        public virtual clsRunningStatus HandleWebAPIProtocolGetRunningStatus()
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
                    Cargo_Status = HasAnyCargoOnAGV() ? 1 : 0,
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
                return status;
            }
            catch (Exception ex)
            {
                //LOG.ERROR("GenRunningStateReportData ", ex);
                return new clsRunningStatus();
            }
        }

        private AGVSystemCommonNet6.AGVDispatch.Model.clsAlarmCode[] GetAlarmCodesUserReportToAGVS_WebAPI()
        {
            return AlarmManager.CurrentAlarms.ToList().FindAll(alarm => alarm.Value.EAlarmCode != AlarmCodes.None).Select(alarm => new AGVSystemCommonNet6.AGVDispatch.Model.clsAlarmCode
            {
                Alarm_ID = alarm.Value.Code,
                Alarm_Level = alarm.Value.ELevel == AGVSystemCommonNet6.Alarm.VMS_ALARM.clsAlarmCode.LEVEL.Alarm ? 1 : 0,
                Alarm_Description = alarm.Value.CN,
                Alarm_Description_EN = alarm.Value.Description,
                Alarm_Category = alarm.Value.IsRecoverable ? 0 : 1,
            }).DistinctBy(alarm => alarm.Alarm_ID).ToArray();
        }

        /// <summary>
        /// 生成支援TCPIP通訊的RunningStatus Model
        /// </summary>
        /// <returns></returns>
        protected virtual RunningStatus HandleTcpIPProtocolGetRunningStatus()
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
                    Cargo_Status = HasAnyCargoOnAGV() ? 1 : 0,
                    CargoType = GetCargoType(),
                    AGV_Status = _Main_Status,
                    Electric_Volume = batteryLevels,
                    Last_Visited_Node = lastVisitedMapPoint.IsVirtualPoint ? lastVisitedMapPoint.TagNumber : Navigation.Data.lastVisitedNode.data,
                    Coordination = Corrdination,
                    Odometry = Odometry,
                    AGV_Reset_Flag = AGV_Reset_Flag,
                    Alarm_Code = _RunTaskData.IsLocalTask ? new AGVSystemCommonNet6.AGVDispatch.Messages.clsAlarmCode[0] : alarm_codes,
                    Escape_Flag = ExecutingTaskModel == null ? false : ExecutingTaskModel.RunningTaskData.Escape_Flag,
                    IsCharging = IsCharging
                };
                return status;
            }
            catch (Exception ex)
            {
                //LOG.ERROR("GenRunningStateReportData ", ex);
                return new RunningStatus();
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
