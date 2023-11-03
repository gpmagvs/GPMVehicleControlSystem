using AGVSystemCommonNet6.AGVDispatch.Messages;
using AGVSystemCommonNet6.AGVDispatch;
using AGVSystemCommonNet6.Alarm.VMS_ALARM;
using AGVSystemCommonNet6.Log;
using static AGVSystemCommonNet6.clsEnums;

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
            AGVS.SetLogFolder(Path.Combine(Parameters.LogFolder, "AGVS_Message_Log"));
            AGVS.UseWebAPI = Parameters.VMSParam.Protocol == VMS_PROTOCOL.GPM_VMS;
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
            AGVS.Start();
            AGVS.TrySendOnlineModeChangeRequest(BarcodeReader.CurrentTag, REMOTE_MODE.OFFLINE);
        }


        private void AGVS_OnDisconnected(object? sender, EventArgs e)
        {
            RemoteModeSettingWhenAGVsDisconnect = _Remote_Mode;
            AutoOnlineRaising = _Remote_Mode == REMOTE_MODE.ONLINE;
            Remote_Mode = REMOTE_MODE.OFFLINE;
        }
        private void AGVS_OnConnectionRestored(object? sender, EventArgs e)
        {
            if (RemoteModeSettingWhenAGVsDisconnect == REMOTE_MODE.ONLINE && Sub_Status == SUB_STATUS.IDLE && !IsActionFinishTaskFeedbackExecuting)
            {
                HandleRemoteModeChangeReq(REMOTE_MODE.ONLINE);
                AutoOnlineRaising = false;
            }
        }
    }
}
