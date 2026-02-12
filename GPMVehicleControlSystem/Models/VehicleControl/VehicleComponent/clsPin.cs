using AGVSystemCommonNet6;
using AGVSystemCommonNet6.GPMRosMessageNet.Services;
using RosSharp.RosBridgeClient;

namespace GPMVehicleControlSystem.Models.VehicleControl.VehicleComponent
{
    public class clsPin : CarComponent, IRosSocket
    {
        public enum PIN_STATES
        {
            UNKNOWN,
            LOCKED,
            RELEASED
        }

        public string PinActionServiceName = "/pin_action";
        public string PinActionDonwServiceName = "/pin_done_action";

        public bool IsPinActionDone;
        public virtual bool IsReleased => CurrentPinState == PIN_STATES.RELEASED;
        public PIN_STATES CurrentPinState { get; protected set; } = PIN_STATES.UNKNOWN;

        private RosSocket _rosSocket;

        public virtual bool isRosBase { get; } = true;

        public PinCommandRequest pin_command = new PinCommandRequest()
        {
            model = "FORK",
        };

        public virtual RosSocket rosSocket
        {
            get => _rosSocket;
            set
            {
                _rosSocket = value;
                _rosSocket?.AdvertiseService<PinCommandRequest, PinCommandResponse>(PinActionDonwServiceName, PinDoneActionCallback);
                logger.Trace($"[Pin] {PinActionDonwServiceName} Service advertised (Action request service name={PinActionServiceName})");
            }
        }

        public override COMPOENT_NAME component_name => COMPOENT_NAME.PIN;

        public override string alarm_locate_in_name => "PIN_DRIVER";

        public clsPin()
        {
        }

        protected void SetPinState(PIN_STATES state)
        {
            if (CurrentPinState == state)
                return;

            CurrentPinState = state;
            logger.Trace($"[Pin] Current state changed => {CurrentPinState}");
        }

        /// <summary>
        /// 清除異常初始化回到原點
        /// </summary>
        /// <returns></returns>
        public virtual async Task Init(CancellationToken token = default)
        {
            SetPinState(PIN_STATES.UNKNOWN);
            pin_command.command = "init";
            await _CallPinCommandActionService(pin_command, 30, cancelToken: token);
            SetPinState(PIN_STATES.UNKNOWN);

        }

        public virtual async Task Lock(CancellationToken token = default)
        {
            SetPinState(PIN_STATES.UNKNOWN);
            pin_command.command = "lock";
            bool lockDone = await _CallPinCommandActionService(pin_command, cancelToken: token);
            if (lockDone)
                SetPinState(PIN_STATES.LOCKED);
        }
        public virtual async Task Release(CancellationToken token = default)
        {
            SetPinState(PIN_STATES.UNKNOWN);
            pin_command.command = "release";
            bool releaseDone = await _CallPinCommandActionService(pin_command, cancelToken: token);
            if (releaseDone)
                SetPinState(PIN_STATES.RELEASED);
        }


        private async Task<bool> _CallPinCommandActionService(PinCommandRequest request, int timeout = 10, CancellationToken cancelToken = default)
        {
            logger.Trace($"call service-> pin command={pin_command.ToJson(Newtonsoft.Json.Formatting.None)},wait agvc response...");
            IsPinActionDone = false;
            PinCommandResponse _resonpse = await _rosSocket.CallServiceAndWait<PinCommandRequest, PinCommandResponse>(PinActionServiceName, request);
            if (_resonpse == null)
                throw new Exception("Call Service Fail");

            logger.Trace($"call service-> pin command={pin_command.ToJson(Newtonsoft.Json.Formatting.None)},wait agvc response-> {_resonpse.ToJson(Newtonsoft.Json.Formatting.None)}..");
            if (_resonpse.confirm)
            {
                logger.Trace($"start wait action done...");

                CancellationTokenSource _wait = new CancellationTokenSource(TimeSpan.FromSeconds(timeout));
                while (!IsPinActionDone)
                {
                    try
                    {
                        await Task.Delay(1, cancelToken);
                    }
                    catch (TaskCanceledException)
                    {
                        throw new TaskCanceledException($"Pin-{request.command} action be canceled.");
                    }
                    if (_wait.IsCancellationRequested)
                    {
                        logger.Error($"Pin-{request.command} request timeout");
                        Current_Alarm_Code = AGVSystemCommonNet6.Vehicle_Control.VCS_ALARM.AlarmCodes.Pin_Action_Error;
                        throw new TimeoutException();
                    }
                }
                logger.Info($"Pin-{request.command} action finish done.");
                return true;
            }
            else
            {
                logger.Error($"Pin-{request.command} request is rejected.");
                return false;
            }
        }

        private bool PinDoneActionCallback(PinCommandRequest tin, out PinCommandResponse response)
        {
            IsPinActionDone = true;
            response = new PinCommandResponse()
            {
                confirm = true
            };
            logger.Trace($"current pin command={pin_command.ToJson()}=> agvc action done. response={tin.ToJson()}");
            bool command_reply_done = tin.command == "done";
            if (!command_reply_done)
            {
                logger.Info($"Pin command action not done.. AGVC Reply command =  {tin.command}");
            }
            return true;
        }
    }
}
