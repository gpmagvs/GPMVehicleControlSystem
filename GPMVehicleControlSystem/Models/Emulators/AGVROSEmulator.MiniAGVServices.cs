using AGVSystemCommonNet6;
using AGVSystemCommonNet6.GPMRosMessageNet.Services;

namespace GPMVehicleControlSystem.Models.Emulators
{
    public partial class AGVROSEmulator
    {
        /// <summary>
        /// 處理電池鎖定/解鎖 Service 請求
        /// </summary>
        /// <param name="request"></param>
        /// <param name="response"></param>
        /// <returns></returns>
        private bool BatteryLockActionRequestHandler(VerticalCommandRequest request, out VerticalCommandResponse response)
        {
            EmuLog($"[Battery lock service] Recieved battery lock request:{request.ToJson()}");
            response = new VerticalCommandResponse()
            {
                confirm = true
            };

            EmuLog($"[Battery lock service] response:{response.ToJson()}");
            return true;
        }
    }
}
