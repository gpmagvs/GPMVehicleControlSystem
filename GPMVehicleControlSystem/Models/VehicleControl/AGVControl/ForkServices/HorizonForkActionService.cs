using AGVSystemCommonNet6.GPMRosMessageNet.Messages;
using AGVSystemCommonNet6.GPMRosMessageNet.Services;
using GPMVehicleControlSystem.Models.VehicleControl.Vehicles;
using NLog;
using NLog.Targets;
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
        public override async Task<(bool confirm, string message)> Home(double speed = 1, bool wait_done = true)
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

                (bool confirm, string message) callSerivceResult = await base.CallVerticalCommandService(new AGVSystemCommonNet6.GPMRosMessageNet.Services.VerticalCommandRequest
                {
                    model = modelName,
                    command = "extend",
                });
                if (!waitActionDone)
                    return callSerivceResult;
                else
                {
                    if (IsActionDone)
                        return (true, "");
                    double extendPosition = vehicle.Parameters.ForkAGV.HorizonArmConfigs.ExtendPose;
                    return await WaitMoveActionDone(extendPosition);
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

                (bool confirm, string message) callSerivceResult = await base.CallVerticalCommandService(new AGVSystemCommonNet6.GPMRosMessageNet.Services.VerticalCommandRequest
                {
                    model = modelName,
                    command = "retract",
                });
                if (!waitActionDone)
                    return callSerivceResult;
                else
                {
                    if (IsActionDone)
                        return (true, "");
                    double shortenPosition = vehicle.Parameters.ForkAGV.HorizonArmConfigs.ShortenPose;
                    return await WaitMoveActionDone(shortenPosition);
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

                (bool confirm, string message) callSerivceResult = await base.CallVerticalCommandService(new AGVSystemCommonNet6.GPMRosMessageNet.Services.VerticalCommandRequest
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
        private async Task<(bool, string)> WaitMoveActionDone(double aimPosition, int timeout = 30)
        {
            CancellationTokenSource timeoutTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(timeout));
            while (true)
            {
                if (CurrentForkActionRequesting.command == "stop")
                    return (true, "Stopped");

                if (Math.Abs(driverState.position - aimPosition) <= 1)
                {
                    logger.Info($"牙叉到達目標位置: {aimPosition}");
                    return (true, "");
                }
                try
                {
                    await Task.Delay(100, timeoutTokenSource.Token);
                }
                catch (TaskCanceledException)
                {
                    return (false, "Timeout");
                }
            }
            return (false, "");
        }
    }
}
