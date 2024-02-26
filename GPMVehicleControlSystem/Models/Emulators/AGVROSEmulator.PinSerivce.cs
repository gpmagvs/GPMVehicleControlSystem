using AGVSystemCommonNet6;
using AGVSystemCommonNet6.GPMRosMessageNet.Services;

namespace GPMVehicleControlSystem.Models.Emulators
{
    /// <summary>
    /// 模擬 /pin_command_action 處理
    /// </summary>
    public partial class AGVROSEmulator
    {

        private bool PinActionCallback(PinCommandRequest request, out PinCommandResponse response)
        {
            EmuLog($"recieve Pin_command_action : {request.ToJson()}");

            Task.Factory.StartNew(async () =>
            {
                await Task.Delay(1000);
                PinActionDone();
            });

            response = new PinCommandResponse()
            {
                confirm = true,
            };
            return true;
        }
        private async void PinActionDone()
        {
            _ = await rosSocket.CallServiceAndWait<PinCommandRequest, PinCommandResponse>("/pin_done_action", new PinCommandRequest
            {
                command = "done"
            });
        }
    }
}
