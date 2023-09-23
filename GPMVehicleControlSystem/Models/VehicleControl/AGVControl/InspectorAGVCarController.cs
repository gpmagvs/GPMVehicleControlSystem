using AGVSystemCommonNet6.GPMRosMessageNet.Services;
using AGVSystemCommonNet6.Log;
using System.Diagnostics.Metrics;
using static AGVSystemCommonNet6.GPMRosMessageNet.Services.VerticalCommandRequest;

namespace GPMVehicleControlSystem.Models.VehicleControl.AGVControl
{
    public partial class InspectorAGVCarController : CarController
    {
        public Action<string> OnInstrumentMeasureDone;
        private COMMANDS action_command;
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
        ///  由車載畫面設定機器人目前位置。
        /// </summary>
        /// <param name="tagID"></param>
        /// <param name="map_name"></param>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <param name="theta"></param>
        /// <returns></returns>
        public async Task<(bool confrim, string message)> SetCurrentTagID(ushort tagID, string map_name, double x, double y, double theta)
        {
            SetcurrentTagIDResponse response = await rosSocket.CallServiceAndWait<SetcurrentTagIDRequest, SetcurrentTagIDResponse>("/set_currentTagID",

                new SetcurrentTagIDRequest
                {
                    tagID = tagID,
                    map = map_name,
                    X = x,
                    Y = y,
                    angle = theta
                }
                );

            return (response == null ? false : response.confirm, response == null ?"Call Service Error":"");
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
            if (action_command == COMMANDS.pose && OnInstrumentMeasureDone != null)
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
            action_command = command;
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
