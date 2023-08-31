using AGVSystemCommonNet6.GPMRosMessageNet.Messages;
using AGVSystemCommonNet6.GPMRosMessageNet.Services;
using static AGVSystemCommonNet6.GPMRosMessageNet.Services.VerticalCommandRequest;

namespace GPMVehicleControlSystem.Models.VehicleControl.AGVControl
{
    public partial class InspectorAGVCarController
    {
        public override void AdertiseROSServices()
        {
            //
        }

        /// <summary>
        /// 確認儀器狀態
        /// </summary>
        /// <returns></returns>
        public async Task<(bool confirm, string message)> MeasurementInit()
        {
            return await CallVerticalCommandService(COMMANDS.init);
        }

        /// <summary>
        /// 開始量測
        /// </summary>
        /// <returns></returns>
        public async Task<(bool confirm, string message)> StartMeasure()
        {
            return await CallVerticalCommandService(COMMANDS.pose);
        }

        public override Task<(bool request_success, bool action_done)> TriggerCSTReader()
        {
            throw new NotImplementedException("巡檢AGV不具有CST Reader 功能");
        }

        public override Task<(bool request_success, bool action_done)> AbortCSTReader()
        {
            throw new NotImplementedException("巡檢AGV不具有CST Reader 功能");
        }

        private async Task<(bool confirm, string message)> CallVerticalCommandService(COMMANDS command)
        {
            try
            {
                VerticalCommandResponse? response = await rosSocket?.CallServiceAndWait<VerticalCommandRequest, VerticalCommandResponse>("/command_action",
                        new VerticalCommandRequest
                        {
                            command = command.ToString(),
                            model = "OHA", //?
                            speed = 0,
                            target = 0
                        }
                );
                return (response.confirm, $"{command}_");
            }
            catch (Exception ex)
            {
                return (false, $"call {command}_fail_{ex.Message}");
            }
        }
    }
}
