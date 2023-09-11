using AGVSystemCommonNet6.AGVDispatch.Messages;
using AGVSystemCommonNet6.Log;
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
        internal async Task<(bool success, RETURN_CODE return_code)> Online_Mode_Switch(REMOTE_MODE mode)
        {
            if (mode == REMOTE_MODE.ONLINE)
                await Auto_Mode_Siwtch(OPERATOR_MODE.AUTO);
            if (Parameters.ForbidToOnlineTags.Contains(BarcodeReader.CurrentTag))
            {
                return (false, RETURN_CODE.Current_Tag_Cannot_Online);
            }
            var _oriMode = Remote_Mode;
            Remote_Mode = REMOTE_MODE.SWITCHING;
            (bool success, RETURN_CODE return_code) result = AGVS.TrySendOnlineModeChangeRequest(Navigation.LastVisitedTag, mode).Result;
            if (!result.success)
            {
                Remote_Mode = _oriMode;
                LOG.ERROR($"車輛{mode}失敗 : Return Code : {result.return_code}");
            }
            else
                Remote_Mode = mode;
            return result;
        }
    }
}
