using AGVSystemCommonNet6;
using AGVSystemCommonNet6.AGVDispatch.Messages;
using AGVSystemCommonNet6.GPMRosMessageNet.Messages;
using AGVSystemCommonNet6.GPMRosMessageNet.Services;
using AGVSystemCommonNet6.Log;
using GPMVehicleControlSystem.Models.VehicleControl.Vehicles;
using Newtonsoft.Json;
using RosSharp.RosBridgeClient;
using System.Diagnostics.Metrics;
using static AGVSystemCommonNet6.GPMRosMessageNet.Services.VerticalCommandRequest;

namespace GPMVehicleControlSystem.Models.VehicleControl.AGVControl
{
    public partial class InspectorAGVCarController : CarController
    {
        public class clsMeasureDone
        {
            public string result_cmd { get; set; } = "";
            public DateTime start_time { get; set; } = DateTime.Now;
        }
        public Action<clsMeasureDone> OnInstrumentMeasureDone;
        private COMMANDS action_command;
        private COMMANDS battery_lock_action_command;
        private DateTime previousStartMeasureTime = DateTime.MinValue;
        private ManualResetEvent batteryLockManualResetEvent = new ManualResetEvent(false);
        private string IOListsTopicID = "";
        public string InstrumentMeasureServiceName => "/command_action";
        public string BatteryLockControlServiceName => "/command_actionm";

        public InspectorAGVCarController()
        {
        }
        public InspectorAGVCarController(string IP, int Port) : base(IP, Port)
        {
        }
        public override string alarm_locate_in_name => "InspectorAGVCarController";

        public override void AdertiseROSServices()
        {
            IOListsTopicID = rosSocket.Advertise<IOlistsMsg>("IOlists");
            string _service_name = rosSocket?.AdvertiseService<VerticalCommandRequest, VerticalCommandResponse>("/done_action", InstrumentMeasureDone);
            LOG.TRACE($"Service Advertised: {_service_name}");
            _service_name = rosSocket?.AdvertiseService<Fire_Action_Request, Fire_Action_Response>("/fire_action", FireActionDoneCallback);
            LOG.TRACE($"Service Advertised: {_service_name}");
        }

        internal void IOListMsgPublisher(IOlistsMsg payload)
        {
            rosSocket.Publish(IOListsTopicID, payload);
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

            return (response == null ? false : response.confirm, response == null ? "Call Service Error" : "");
        }
        /// <summary>
        /// 確認儀器狀態
        /// </summary>
        /// <returns></returns>
        public async Task<(bool confirm, string message)> MeasurementInit()
        {
            return await CallCommandAction(InstrumentMeasureServiceName, COMMANDS.init);
        }

        /// <summary>
        /// 開始量測
        /// </summary>
        /// <returns></returns>
        public async Task<(bool confirm, string message)> StartInstrumentMeasure(int tagID)
        {
            previousStartMeasureTime = DateTime.Now;
            return await CallCommandAction(InstrumentMeasureServiceName, COMMANDS.pose, tagID);
        }

        /// <summary>
        /// 電池鎖定Service.
        /// </summary>
        /// <param name="batNo"></param>
        /// <param name="lockAction"></param>
        /// <returns></returns>
        public async Task<(bool confirm, string message)> BatteryLockControlService(int batNo, TsmcMiniAGV.BAT_LOCK_ACTION lockAction)
        {
            batteryLockManualResetEvent.Reset();
            try
            {
                COMMANDS CreateCommand(int batNO, TsmcMiniAGV.BAT_LOCK_ACTION lockAction)
                {
                    if (batNO == 1 && lockAction == TsmcMiniAGV.BAT_LOCK_ACTION.LOCK)
                        return COMMANDS.lock1;
                    if (batNO == 1 && lockAction == TsmcMiniAGV.BAT_LOCK_ACTION.UNLOCK)
                        return COMMANDS.unlock1;
                    if (batNO == 2 && lockAction == TsmcMiniAGV.BAT_LOCK_ACTION.LOCK)
                        return COMMANDS.lock2;
                    if (batNO == 2 && lockAction == TsmcMiniAGV.BAT_LOCK_ACTION.UNLOCK)
                        return COMMANDS.unlock2;
                    throw new NotImplementedException("錯誤的電池鎖定/解鎖請求參數");
                }

                battery_lock_action_command = lockAction == TsmcMiniAGV.BAT_LOCK_ACTION.Stop ? COMMANDS.stop : CreateCommand(batNo, lockAction);

                VerticalCommandResponse? response = await rosSocket.CallServiceAndWait<VerticalCommandRequest, VerticalCommandResponse>(BatteryLockControlServiceName,
                    new VerticalCommandRequest
                    {
                        model = "batterylock",
                        command = battery_lock_action_command.ToString()
                    });

                bool _success = response != null && response.confirm;
                return new(_success, _success ? "" : "Call battery lock service fail");
            }
            catch (Exception ex)
            {
                batteryLockManualResetEvent.Set();
                return new(false, $"Call battery lock service fail-{ex.Message}");
            }
        }


        public override Task<(bool request_success, bool action_done)> TriggerCSTReader()
        {
            throw new NotImplementedException("巡檢AGV不具有CST Reader 功能");
        }

        public override Task<(bool request_success, bool action_done)> AbortCSTReader()
        {
            throw new NotImplementedException("巡檢AGV不具有CST Reader 功能");
        }

        internal override async Task<SendActionCheckResult> ExecuteTaskDownloaded(clsTaskDownloadData taskDownloadData, double action_timeout = 5)
        {
            RunningTaskData = taskDownloadData;
            AGVSystemCommonNet6.GPMRosMessageNet.Actions.TaskCommandGoal rosgoal = RunningTaskData.RosTaskCommandGoal;

            rosgoal.pathInfo = JsonConvert.DeserializeObject<AMCPathInfo[]>(rosgoal.pathInfo.ToJson());
            
            return await SendGoal(rosgoal, action_timeout);
        }
        #region Private Methods

        /// <summary>
        /// 儀器量測結束的回調函數
        /// </summary>
        /// <param name="tin"></param>
        /// <param name="response"></param>
        /// <returns></returns>
        private bool InstrumentMeasureDone(VerticalCommandRequest request, out VerticalCommandResponse response)
        {

            string model = request.model;
            response = new VerticalCommandResponse()
            {
                confirm = true,
            };
            if (model == "batterylock")
            {
                LOG.INFO($"電池{battery_lock_action_command} 動作完成, request.command= {request.command}");
                batteryLockManualResetEvent.Set();
            }
            else
            {

                LOG.INFO($"儀器量測結束, request.command= {request.command}");
                if (action_command == COMMANDS.pose && OnInstrumentMeasureDone != null)
                    OnInstrumentMeasureDone(new clsMeasureDone()
                    {
                        result_cmd = request.command,
                        start_time = previousStartMeasureTime
                    });
            }
            return true;
        }
        private bool FireActionDoneCallback(Fire_Action_Request request, out Fire_Action_Response response)
        {
            LOG.INFO($"車控端已完成避災動作!", color: ConsoleColor.Red);
            response = new Fire_Action_Response(true);
            return true;
        }
        private async Task<(bool confirm, string message)> CallCommandAction(string service_name, COMMANDS command)
        {
            try
            {
                VerticalCommandResponse? response = await rosSocket.CallServiceAndWait<VerticalCommandRequest, VerticalCommandResponse>(service_name,
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

        private async Task<(bool confirm, string message)> CallCommandAction(string service_name, COMMANDS command, int tagID)
        {
            action_command = command;
            try
            {
                VerticalCommandResponse? response = await rosSocket.CallServiceAndWait<VerticalCommandRequest, VerticalCommandResponse>(service_name,
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

        public override Task<(bool request_success, bool action_done)> TriggerCSTReader(CST_TYPE cst_type)
        {
            throw new NotImplementedException();
        }

        #endregion

    }
}
