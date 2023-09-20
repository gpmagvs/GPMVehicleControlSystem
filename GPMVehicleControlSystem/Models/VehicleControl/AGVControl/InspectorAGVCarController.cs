using AGVSystemCommonNet6.GPMRosMessageNet.Services;
using AGVSystemCommonNet6.Log;
using System.Diagnostics.Metrics;
using static AGVSystemCommonNet6.GPMRosMessageNet.Services.VerticalCommandRequest;

namespace GPMVehicleControlSystem.Models.VehicleControl.AGVControl
{
    public partial class InspectorAGVCarController : CarController
    {
        public Action<string> OnInstrumentMeasureDone;
        public InspectorAGVCarController()
        {
        }
        public InspectorAGVCarController(string IP, int Port) : base(IP, Port)
        {
        }
        public override string alarm_locate_in_name => "InspectorAGVCarController";

        public override void AdertiseROSServices()
        {
            rosSocket?.AdvertiseService<VerticalCommandRequest, VerticalCommandResponse>("/done_action", InstrumentMeasureDone);
        }


        /// <summary>
        /// 確認儀器狀態
        /// </summary>
        /// <returns></returns>
        public async Task<(bool confirm, string message)> MeasurementInit()
        {
            return await CallCommandAction(COMMANDS.init);
        }

        /// <summary>
        /// 開始量測
        /// </summary>
        /// <returns></returns>
        public async Task<(bool confirm, string message)> StartInstrumentMeasure(int tagID)
        {
            return await CallCommandAction(COMMANDS.pose, tagID);
        }

        /// <summary>
        /// 儀器量測結束的回調函數
        /// </summary>
        /// <param name="tin"></param>
        /// <param name="response"></param>
        /// <returns></returns>
        private bool InstrumentMeasureDone(VerticalCommandRequest request, out VerticalCommandResponse response)
        {
            LOG.INFO($"儀器量測結束, request.command= {request.command}");
            response = new VerticalCommandResponse()
            {
                confirm = true,
            };
            if (OnInstrumentMeasureDone != null)
                OnInstrumentMeasureDone(request.command);
            return true;
        }

        public override Task<(bool request_success, bool action_done)> TriggerCSTReader()
        {
            throw new NotImplementedException("巡檢AGV不具有CST Reader 功能");
        }

        public override Task<(bool request_success, bool action_done)> AbortCSTReader()
        {
            throw new NotImplementedException("巡檢AGV不具有CST Reader 功能");
        }

        private async Task<(bool confirm, string message)> CallCommandAction(COMMANDS command)
        {
            try
            {
                VerticalCommandResponse? response = await rosSocket.CallServiceAndWait<VerticalCommandRequest, VerticalCommandResponse>("/command_action",
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


        private async Task<(bool confirm, string message)> CallCommandAction(COMMANDS command, int tagID)
        {
            try
            {
                VerticalCommandResponse? response = await rosSocket.CallServiceAndWait<VerticalCommandRequest, VerticalCommandResponse>("/command_action",
                        new VerticalCommandRequest
                        {
                            command = $"{command.ToString()},{DateTime.Now.ToString("yyyyMMdd")},{DateTime.Now.ToString("HHmmss")}",
                            model = "OHA", //TODO Confirm
                            target = tagID
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
