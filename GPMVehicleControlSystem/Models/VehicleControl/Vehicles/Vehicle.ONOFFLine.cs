using AGVSystemCommonNet6.AGVDispatch.Messages;
using AGVSystemCommonNet6.AGVDispatch.Model;
using AGVSystemCommonNet6.Vehicle_Control.VCS_ALARM;
using RosSharp.RosBridgeClient.Actionlib;
using static AGVSystemCommonNet6.clsEnums;
using static AGVSystemCommonNet6.MAP.MapPoint;
using static GPMVehicleControlSystem.VehicleControl.DIOModule.clsDIModule;

namespace GPMVehicleControlSystem.Models.VehicleControl.Vehicles
{
    public partial class Vehicle
    {
        internal REMOTE_MODE RemoteModeSettingWhenAGVsDisconnect = REMOTE_MODE.OFFLINE;
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
                if (value == REMOTE_MODE.ONLINE)
                {
                    StatusLighter.ONLINE();
                }
                else
                {
                    StatusLighter.OFFLINE();
                    AGVC.EmergencyStop(true);
                }
            }
        }



        private bool OnlineModeChangingFlag = false;



        internal bool HandleRemoteModeChangeReq(REMOTE_MODE mode, bool IsAGVSRequest = false)
        {
            if (mode != Remote_Mode)
            {
                if (RemoteModeRequestingflag)
                    return false;
                RemoteModeRequestingflag = true;
                string request_user_name = IsAGVSRequest ? "AGVS" : "車載用戶";
                logger.LogWarning($"{request_user_name} 請求變更Online模式為:{mode}");

                (bool success, RETURN_CODE return_code) result = Online_Mode_Switch(mode).Result;
                RemoteModeRequestingflag = false;
                if (result.success)
                {
                    RemoteModeSettingWhenAGVsDisconnect = REMOTE_MODE.OFFLINE;
                    logger.LogWarning($"{request_user_name} 請求變更Online模式為 {mode}---成功");
                    if (IsAGVSRequest && mode == REMOTE_MODE.OFFLINE && !IsActionFinishTaskFeedbackExecuting && RemoteModeSettingWhenAGVsDisconnect == REMOTE_MODE.ONLINE)
                    {
                        Task.Factory.StartNew(async () =>
                        {
                            await Task.Delay(1000);
                            while (GetSub_Status() != SUB_STATUS.IDLE)
                                await Task.Delay(1000);
                            logger.LogWarning($"[{GetSub_Status()}] Raise ONLINE Request . Because Remote Mode Before AGVs Disconnected is {RemoteModeSettingWhenAGVsDisconnect}");
                            bool OnlineSuccess = HandleRemoteModeChangeReq(REMOTE_MODE.ONLINE, false);
                            AutoOnlineRaising = false;
                        });
                    }
                }
                else
                    logger.LogError($"{request_user_name} 請求變更Online模式為{mode}---失敗 Return Code = {(int)result.return_code}-{result.return_code})");
                return result.success;
            }
            else
            {
                return true;
            }
        }
        private bool RemoteModeRequestingflag = false;
        internal async Task<(bool success, RETURN_CODE return_code)> Online_Mode_Switch(REMOTE_MODE mode, bool bypassStatusCheck = false)
        {
            int currentTag = BarcodeReader.CurrentTag;
            logger.LogTrace($"Online_Mode_Switch, current tag = {currentTag}");
            if (mode == REMOTE_MODE.ONLINE && Parameters.AgvType != AGV_TYPE.INSPECTION_AGV)
            {
                if (!IsInitialized)
                {
                    return (false, RETURN_CODE.AGV_Not_Initialized);
                }
                if (currentTag == 0)//檢查Tag
                    return (false, RETURN_CODE.AGV_Need_Park_Above_Tag);
                if (lastVisitedMapPoint.StationType != STATION_TYPE.Normal && !lastVisitedMapPoint.IsCharge)
                {
                    AlarmManager.AddWarning(AlarmCodes.Cant_Online_In_Equipment);
                    return (false, RETURN_CODE.Current_Tag_Cannot_Online_In_Equipment);
                }
                if (!bypassStatusCheck && Parameters.ForbidToOnlineTags.Contains(currentTag))
                {
                    AlarmManager.AddWarning(AlarmCodes.Cant_Online_With_Forbid_Tag);
                    //檢查是否停在禁止上線的TAG位置
                    return (false, RETURN_CODE.Current_Tag_Cannot_Online);
                }
                if (Parameters.AgvType != AGV_TYPE.INSPECTION_AGV && !bypassStatusCheck && lastVisitedMapPoint.IsVirtualPoint)
                {
                    AlarmManager.AddWarning(AlarmCodes.Cant_Online_At_Virtual_Point);
                    //檢查是否停在禁止上線的TAG位置
                    return (false, RETURN_CODE.Current_Tag_Cannot_Online_At_Virtual_Point);
                }
                if (!bypassStatusCheck && IsNoCargoButIDExist)
                {
                    return (false, RETURN_CODE.AGV_HasIDBut_No_Cargo);
                }
            }
            await Auto_Mode_Siwtch(OPERATOR_MODE.AUTO);
            var _oriMode = Remote_Mode;
            (bool success, RETURN_CODE return_code) result = await AGVS.TrySendOnlineModeChangeRequest(currentTag, mode);

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
                    logger.LogError($"車輛{mode}失敗 : Return Code : {result.return_code}");
                }
            }
            else
            {
                Remote_Mode = mode;
                if (mode == REMOTE_MODE.ONLINE)
                {
                    DownloadMapFromServer();
                }
            }


            return result;
        }


    }
}
