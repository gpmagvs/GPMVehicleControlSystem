using AGVSystemCommonNet6;
using AGVSystemCommonNet6.GPMRosMessageNet.Messages;
using AGVSystemCommonNet6.GPMRosMessageNet.Services;
using RosSharp.RosBridgeClient;

namespace GPMVehicleControlSystem.Models.VehicleControl.VehicleComponent
{
    public class clsPin : CarComponent, IRosSocket
    {

        public class BeforePinActionStartEventArgs : EventArgs
        {
            public PIN_STATUS action { get; set; }
            public bool allowed { get; set; } = false;
        }

        public delegate Task<BeforePinActionStartEventArgs> BeforePinActionStartDelegate(BeforePinActionStartEventArgs args);
        public BeforePinActionStartDelegate BeforePinActionStart;

        public enum PIN_STATUS
        {
            LOCK,
            RELEASE,
            INITIALIZING,
            UNKNOW
        }
        public string PinActionServiceName = "/pin_action";
        public string PinActionDonwServiceName = "/pin_done_action";

        public bool IsPinActionDone;

        private RosSocket _rosSocket;

        public virtual bool isRosBase { get; } = true;
        private PinState _pintState = new PinState();
        public PinState pintState
        {
            get => _pintState;
            protected set
            {
                var _lastPose = _pintState.pose;
                var _newPost = value.pose;
                if (_newPost != _lastPose)
                {
                    logger.Info($"Pin state change: {_lastPose} => {_newPost}");
                }

                _pintState = value;

            }
        }

        public virtual PIN_STATUS pinStatus => pintState.pose == "lock" ? PIN_STATUS.LOCK : pintState.pose == "release" ? PIN_STATUS.RELEASE : PIN_STATUS.UNKNOW;
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

        protected async Task<bool> BeforePinActionStartInvokeAsync(PIN_STATUS action)
        {
            if (BeforePinActionStart != null)
            {
                var args = new BeforePinActionStartEventArgs
                {
                    allowed = false,
                    action = action
                };
                args = await BeforePinActionStart(args);
                return args.allowed;
            }
            return true;
        }

        public virtual async Task<bool> Reset(CancellationToken token = default)
        {
            pin_command.command = "reset";
            return await _CallPinCommandActionService(pin_command, 30, cancelToken: token);
        }
        // <summary>
        /// 清除異常初始化回到原點
        /// </summary>
        /// <returns></returns>
        public virtual async Task<bool> Init(CancellationToken token = default)
        {
            if (!await BeforePinActionStartInvokeAsync(PIN_STATUS.INITIALIZING))
                return false;

            pin_command.command = "init";
            return await _CallPinCommandActionService(pin_command, 30, cancelToken: token);
        }

        public virtual async Task Lock(CancellationToken token = default)
        {
            if (!await BeforePinActionStartInvokeAsync(PIN_STATUS.LOCK))
                return;

            pin_command.command = "lock";
            await _CallPinCommandActionService(pin_command, cancelToken: token);
        }
        public virtual async Task Release(CancellationToken token = default)
        {
            if (!await BeforePinActionStartInvokeAsync(PIN_STATUS.RELEASE))
                return;

            pin_command.command = "release";
            await _CallPinCommandActionService(pin_command, cancelToken: token);
        }

        public virtual async Task<bool> Orig(CancellationToken token = default)
        {
            pin_command.command = "orig";
            return await _CallPinCommandActionService(pin_command, 30, cancelToken: token);
        }


        public virtual async Task<bool> Up(CancellationToken token = default)
        {
            pin_command.command = "up";
            return await _CallPinCommandActionService(pin_command, 30, cancelToken: token);
        }

        public virtual async Task<bool> Down(CancellationToken token = default)
        {
            pin_command.command = "down";
            return await _CallPinCommandActionService(pin_command, 30, cancelToken: token);
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

        public override Message StateData
        {
            get
            {
                return base.StateData;
            }
            set
            {
                base.StateData = value;
                if (value is PinsState state)
                {
                    if (state.PinState.Any())
                    {
                        pintState = state.PinState.FirstOrDefault();
                        lastUpdateTime = DateTime.Now;
                    }
                }
            }
        }
    }
}
