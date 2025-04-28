using AGVSystemCommonNet6.GPMRosMessageNet.Services;
using NLog;
using NLog.Targets;
using RosSharp.RosBridgeClient;

namespace GPMVehicleControlSystem.Models.VehicleControl.AGVControl.ForkServices
{
    public class HorizonForkActionService : ForkActionServiceBase
    {
        public HorizonForkActionService(RosSocket rosSocket) : base(rosSocket)
        {
            logger = LogManager.GetCurrentClassLogger();
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
                    return await WaitActionDone();
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
                    return await WaitActionDone();
                }
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }
    }
}
