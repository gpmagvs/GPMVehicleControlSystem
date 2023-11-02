using AGVSystemCommonNet6.AGVDispatch.Messages;
using AGVSystemCommonNet6.AGVDispatch.Model;
using AGVSystemCommonNet6.Alarm.VMS_ALARM;
using AGVSystemCommonNet6.Log;
using RosSharp.RosBridgeClient.Actionlib;
using static AGVSystemCommonNet6.clsEnums;

namespace GPMVehicleControlSystem.Models.VehicleControl.Vehicles
{
    public partial class Vehicle
    {
        private REMOTE_MODE _Remote_Mode = REMOTE_MODE.OFFLINE;
        /// <summary>
        /// Online/Offline 模式
        /// </summary>
        public REMOTE_MODE Remote_Mode
        {
            get => _Remote_Mode;
            set
            {
                _Remote_Mode = value;
                if (Parameters.SimulationMode)
                    return;
                if (value == REMOTE_MODE.ONLINE)
                {
                    StatusLighter.ONLINE();
                }
                else
                    StatusLighter.OFFLINE();
            }
        }

        private bool OnlineModeChangingFlag = false;


        internal bool HandleRemoteModeChangeReq(REMOTE_MODE mode, bool IsAGVSRequest = false)
        {
            if (mode != Remote_Mode)
            {
                string request_user_name = IsAGVSRequest ? "AGVS" : "車載用戶";
                LOG.WARN($"{request_user_name} 請求變更Online模式為:{mode}");

                (bool success, RETURN_CODE return_code) result = Online_Mode_Switch(mode).Result;
                if (result.success)
                {

                    LOG.WARN($"{request_user_name} 請求變更Online模式為 {mode}---成功");
                    if (IsAGVSRequest && mode == REMOTE_MODE.OFFLINE && AutoOnlineRaising)
                    {
                        Task.Factory.StartNew(async () =>
                        {
                            await Task.Delay(1000);
                            while (Sub_Status != SUB_STATUS.IDLE)
                                await Task.Delay(1000);
                            LOG.WARN($"[{Sub_Status}] Raise ONLINE Request . Because Action_Finish_Feedback is proccessed before.");
                            bool OnlineSuccess = HandleRemoteModeChangeReq(REMOTE_MODE.ONLINE, false);
                            AutoOnlineRaising = false;
                        });
                    }
                }
                else
                    LOG.ERROR($"{request_user_name} 請求變更Online模式為{mode}---失敗 Return Code = {(int)result.return_code}-{result.return_code})");
                return result.success;
            }
            else
            {
                return true;
            }
        }
        internal async Task<(bool success, RETURN_CODE return_code)> Online_Mode_Switch(REMOTE_MODE mode, bool bypassStatusCheck = false)
        {
            var currentTag = BarcodeReader.CurrentTag;
            if (mode == REMOTE_MODE.ONLINE && Parameters.AgvType != AGV_TYPE.INSPECTION_AGV)
            {
                await Auto_Mode_Siwtch(OPERATOR_MODE.AUTO);
                if (currentTag == 0)//檢查Tag
                    return (false, RETURN_CODE.AGV_Need_Park_Above_Tag);
                if (!bypassStatusCheck && Parameters.ForbidToOnlineTags.Contains(currentTag))
                {
                    AlarmManager.AddWarning(AlarmCodes.Cant_Online_With_Forbid_Tag);
                    //檢查是否停在禁止上線的TAG位置
                    return (false, RETURN_CODE.Current_Tag_Cannot_Online);
                }
            }
            var _oriMode = Remote_Mode;
            (bool success, RETURN_CODE return_code) result = AGVS.TrySendOnlineModeChangeRequest(currentTag, mode).Result;
            if (!result.success)
            {
                if (mode == REMOTE_MODE.OFFLINE && result.return_code == RETURN_CODE.System_Error)
                {
                    Remote_Mode = REMOTE_MODE.OFFLINE;
                    return (true, RETURN_CODE.OK);
                }
                else
                {
                    Remote_Mode = _oriMode;
                    LOG.ERROR($"車輛{mode}失敗 : Return Code : {result.return_code}");
                }
            }
            else
                Remote_Mode = mode;
            return result;
        }


    }
}
