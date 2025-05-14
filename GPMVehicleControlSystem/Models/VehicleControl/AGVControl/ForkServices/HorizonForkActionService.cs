using AGVSystemCommonNet6.GPMRosMessageNet.Messages;
using AGVSystemCommonNet6.GPMRosMessageNet.Services;
using GPMVehicleControlSystem.Models.VehicleControl.Vehicles;
using NLog;
using RosSharp.RosBridgeClient;
namespace GPMVehicleControlSystem.Models.VehicleControl.AGVControl.ForkServices
{
    public class HorizonForkActionService : ForkActionServiceBase
    {
        public HorizonForkActionService(Vehicle vehicle, RosSocket rosSocket) : base(vehicle, rosSocket)
        {
            logger = LogManager.GetCurrentClassLogger();
            rosSocket.Subscribe<DriverState>("/fork_extend_state", DriverStateTopicCallback);
        }

        private void DriverStateTopicCallback(DriverState t)
        {
            driverState = t;
        }

        protected override string modelName { get; set; } = "Extend";
        protected override string CommandActionServiceName { get; set; } = "/ForkExtend_action";
        protected override string DoneActionServiceName { get; set; } = "/ForkExtend_done_action";
        protected override bool IsStartRunRequesting(VerticalCommandRequest request)
        {
            string[] startRunCmdList = { "extend", "retract" };
            if (startRunCmdList.Contains(request.command))
                return true;
            return base.IsStartRunRequesting(request);
        }

        public override async Task<(bool confirm, string message)> Stop()
        {
            logger.Info("Stop Fork Arm");
            var commandResponseResult = await base.Stop();
            return commandResponseResult;
        }

        public override async Task<(bool confirm, string message)> Home(double speed = 1, bool wait_done = true, bool startActionInvoke = true)
        {
            return await Retract(true);
        }
        /// <summary>
        /// 牙叉伸出
        /// </summary>
        /// <param name="waitActionDone"></param>
        /// <returns></returns>
        public async Task<(bool success, string message)> Extend(bool waitActionDone = true)
        {
            WaitActionDoneFlag = waitActionDone;
            try
            {
                IsActionDone = false;
                VerticalCommandRequest request = new VerticalCommandRequest
                {
                    model = modelName,
                    command = "extend",
                };
                if (!waitActionDone)
                    return await CallVerticalCommandService(request);
                else
                {
                    return await CallServiceAndWaitActionDone(request);
                }
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        /// <summary>
        /// 牙叉縮回
        /// </summary>
        /// <param name="waitActionDone"></param>
        /// <returns></returns>
        public async Task<(bool success, string message)> Retract(bool waitActionDone = true)
        {


            WaitActionDoneFlag = waitActionDone;
            try
            {
                IsActionDone = false;
                VerticalCommandRequest request = new VerticalCommandRequest
                {
                    model = modelName,
                    command = "retract",
                };
                if (!waitActionDone)
                    return await CallVerticalCommandService(request);
                else
                {
                    return await CallServiceAndWaitActionDone(request);
                }
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }
        public async Task<(bool success, string message)> Reset()
        {
            try
            {
                IsActionDone = false;

                (bool confirm, string message) callSerivceResult = await base.CallVerticalCommandService(new VerticalCommandRequest
                {
                    model = modelName,
                    command = "reset",
                });
                return callSerivceResult;
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        protected override async Task<(bool success, string message)> WaitActionDone(CancellationToken token, int timeout = 300)
        {
            double aim = CurrentForkActionRequesting.command == "extend" ? vehicle.Parameters.ForkAGV.HorizonArmConfigs.ExtendPose : vehicle.Parameters.ForkAGV.HorizonArmConfigs.ShortenPose;
            wait_action_down_cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeout));
            while (Math.Abs(aim - driverState.position) >= 2)
            {
                if (wait_action_down_cts.IsCancellationRequested)
                    return (false, "WaitActionDone Timeout");
                await Task.Delay(1);
            }
            return (true, "");
        }
    }
}
