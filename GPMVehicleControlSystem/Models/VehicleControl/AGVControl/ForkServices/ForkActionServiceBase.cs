using AGVSystemCommonNet6;
using AGVSystemCommonNet6.GPMRosMessageNet.Messages;
using AGVSystemCommonNet6.GPMRosMessageNet.Services;
using GPMVehicleControlSystem.Models.VehicleControl.Vehicles;
using NLog;
using RosSharp.RosBridgeClient;

namespace GPMVehicleControlSystem.Models.VehicleControl.AGVControl.ForkServices
{
    public abstract class ForkActionServiceBase
    {
        public class BeforActionStartErgs : EventArgs
        {
            public VerticalCommandRequest currentCommandReg { get; set; } = new VerticalCommandRequest();
            public bool isNeedWaitDriverStop { get; set; } = false;
        }

        protected readonly Vehicle vehicle;
        protected readonly RosSocket rosSocket;
        protected virtual string DoneActionServiceName { get; set; } = "/done_action";
        protected virtual string CommandActionServiceName { get; set; } = "/command_action";

        protected virtual string modelName { get; set; } = "FORK";

        public bool _IsActionDone = false;
        public bool WaitActionDoneFlag { get; protected set; } = false;
        public bool IsStoppedByObstacleDetected { get; protected set; } = false;
        public VerticalCommandRequest BeforeStopActionRequesting { get; set; } = new VerticalCommandRequest();
        public VerticalCommandRequest _CurrentForkActionRequesting = new VerticalCommandRequest();
        public VerticalCommandRequest CurrentForkActionRequesting
        {
            get => _CurrentForkActionRequesting;
            set
            {
                _CurrentForkActionRequesting = value;
                logger.Debug($"current action requesting is {_CurrentForkActionRequesting.ToJson(Newtonsoft.Json.Formatting.None)}");
            }
        }

        public CancellationTokenSource wait_action_down_cts = new CancellationTokenSource();

        public event EventHandler<BeforActionStartErgs> BeforeActionStart;
        public event EventHandler<VerticalCommandRequest> OnActionStart;
        public event EventHandler OnActionDone;

        public delegate double OnForkStartGoHomeDelage();
        public OnForkStartGoHomeDelage OnForkStartGoHome;

        public double HSafeSetting { get; set; } = 0;

        protected double PoseTarget = 0;
        protected double Speed { get; private set; } = 0;

        public virtual double CurrentDriverSpeed { get; } = 0;
        public virtual double CurrentPosition { get; } = 0;

        public DriverState driverState { get; set; } = new DriverState();

        protected Logger logger;
        public bool IsActionDone
        {
            get => _IsActionDone;
            set
            {
                _IsActionDone = value;
                if (_IsActionDone)
                {
                    WaitActionDoneFlag = false;

                    if (IsStartRunRequesting(CurrentForkActionRequesting))
                        OnActionDone?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        public ForkActionServiceBase(Vehicle vehicle, RosSocket rosSocket)
        {
            this.vehicle = vehicle;
            this.rosSocket = rosSocket;
        }


        public virtual void AdertiseRequiredService()
        {
            rosSocket?.AdvertiseService<VerticalCommandRequest, VerticalCommandResponse>(DoneActionServiceName, VerticalDoneActionCallback);
            logger.Trace($"Service:{DoneActionServiceName} advertised");

        }

        public async Task<(bool confirm, string message)> Init()
        {
            WaitActionDoneFlag = false;
            VerticalCommandRequest request = new VerticalCommandRequest
            {
                model = modelName,
                command = "init",
                target = 0,
                speed = 0
            };
            return await CallVerticalCommandService(request);
        }

        public virtual async Task<(bool confirm, string message)> StopWithoutCallService()
        {
            try
            {
                WaitActionDoneFlag = false;
                return (true, "");
            }
            finally
            {
                try
                {
                    wait_action_down_cts?.Cancel();
                    wait_action_down_cts?.Dispose();
                }
                catch (Exception ex)
                {
                }
            }
        }

        public virtual async Task<(bool confirm, string message)> Stop()
        {
            try
            {
                WaitActionDoneFlag = false;
                VerticalCommandRequest request = new VerticalCommandRequest
                {
                    model = modelName,
                    command = "stop",
                    speed = 0,
                    target = 0
                };
                (bool confirm, string message) callSerivceResult = await CallVerticalCommandService(request);
                return callSerivceResult;
            }
            finally
            {
                try
                {
                    wait_action_down_cts?.Cancel();
                    wait_action_down_cts?.Dispose();
                }
                catch (Exception ex)
                {
                }
            }
            //return await WaitStopActionDone();
        }
        public virtual async Task<(bool confirm, string message)> Home(double speed = 1.0, bool wait_done = true, bool startActionInvoke = true)
        {
            WaitActionDoneFlag = wait_done;
            HSafeSetting = OnForkStartGoHome == null ? 0 : OnForkStartGoHome();
            logger.Info($"Fork ready Go Home Position,HSafe={HSafeSetting}");

            VerticalCommandRequest request = new VerticalCommandRequest
            {
                model = modelName,
                command = "orig",
                speed = speed,
            };

            if (!wait_done)
            {
                (bool confirm, string message) callSerivceResult = await CallVerticalCommandService(request, startActionInvoke);
                return callSerivceResult;
            }
            else
            {
                return await CallServiceAndWaitActionDone(request, startActionInvoke: startActionInvoke);
            }
        }

        public async Task<(bool success, string message)> Pose(double target, double speed = 1.0, bool wait_done = true, bool startActionInvoke = true)
        {
            PoseTarget = target;
            Speed = speed;
            WaitActionDoneFlag = wait_done;
            VerticalCommandRequest request = new VerticalCommandRequest
            {
                model = modelName,
                command = "pose",
                speed = speed,
                target = target
            };
            if (!wait_done)
            {
                (bool confirm, string message) callSerivceResult = await CallVerticalCommandService(request, startActionInvoke);
                return callSerivceResult;
            }
            else
            {
                return await CallServiceAndWaitActionDone(request, startActionInvoke: startActionInvoke);
            }
        }
        public async Task<(bool confirm, string message)> Down(double speed = 1.0, bool startActionInvoke = true)
        {
            VerticalCommandRequest request = new VerticalCommandRequest
            {
                model = modelName,
                command = "down",
                speed = speed,
            };
            return await CallVerticalCommandService(request, startActionInvoke);
        }
        public async Task<(bool confirm, string message)> Up(double speed = 1.0, bool startActionInvoke = true)
        {
            VerticalCommandRequest request = new VerticalCommandRequest
            {
                model = modelName,
                command = "up",
                speed = speed,
            };
            return await CallVerticalCommandService(request, startActionInvoke);
        }
        public async Task<(bool confirm, string message)> UpSearch(double speed = 1.0, bool startActionInvoke = true)
        {
            VerticalCommandRequest request = new VerticalCommandRequest
            {
                model = modelName,
                command = "up_search",
                speed = speed,
            };
            return await CallVerticalCommandService(request, startActionInvoke);
        }
        public async Task<(bool confirm, string message)> DownSearch(double speed = 1.0, bool startActionInvoke = true)
        {
            VerticalCommandRequest request = new VerticalCommandRequest
            {
                model = modelName,
                command = "down_search",
                speed = speed,
            };
            return await CallVerticalCommandService(request, startActionInvoke);
        }

        public async Task<(bool confirm, string message)> ZAxisResume(VerticalCommandRequest lastVerticalForkActionCmd)
        {
            await Task.Delay(800);
            logger.Warn($"Fork {lastVerticalForkActionCmd.command} resume to action");
            return await CallVerticalCommandService(lastVerticalForkActionCmd);
        }
        public async Task<(bool confirm, string message)> ZAxisResume()
        {
            await Task.Delay(800);
            if (BeforeStopActionRequesting.command == "")
                return (true, "No Request excuting before fork stopped");
            VerticalCommandRequest request = BeforeStopActionRequesting.Clone();
            logger.Warn($"Fork {request.command} resume to action");
            return await CallVerticalCommandService(request);
        }
        public virtual bool IsStartRunRequesting(VerticalCommandRequest request)
        {
            return request.command == "pose" || request.command == "orig" || request.command == "up" || request.command == "up_search"
                                                || request.command == "down" || request.command == "down_search";
        }

        internal async Task<(bool confirm, string message)> CallVerticalCommandService(VerticalCommandRequest request, bool startActionInvoke = true)
        {
            try
            {
                if (request.command != "stop")
                {
                    BeforeStopActionRequesting = request.Clone();
                    //開始動作前
                    if (IsStartRunRequesting(request) && IsStartRunRequesting(CurrentForkActionRequesting) && BeforeActionStart != null)
                    {
                        BeforActionStartErgs args = new BeforActionStartErgs() { currentCommandReg = BeforeStopActionRequesting, isNeedWaitDriverStop = false };
                        BeforeActionStart?.Invoke(this, args);

                        if (args.isNeedWaitDriverStop)
                        {
                            logger.Info($"開始等待 Driver Speed 為 0");
                            CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                            while (driverState.speed != 0)
                            {
                                logger.Info($"Driver Speed : {driverState.speed}");
                                try
                                {
                                    await Task.Delay(200, cts.Token);
                                }
                                catch (TaskCanceledException ex)
                                {
                                    throw new Exception("連續牙叉動作需等待牙叉停止但已逾時");
                                }
                            }
                            logger.Info("連續牙叉動作等待牙叉停止 Success");
                        }
                    }
                }
                else
                    BeforeStopActionRequesting = new VerticalCommandRequest();



                logger.Info($"Try rosservice call {CommandActionServiceName} : {request.ToJson(Newtonsoft.Json.Formatting.None)} ,Current Position : {driverState.position}");
                VerticalCommandResponse? response = await rosSocket?.CallServiceAndWait<VerticalCommandRequest, VerticalCommandResponse>(CommandActionServiceName, request, 1000);
                if (response == null)
                    throw new TimeoutException();


                logger.Info($"Call {CommandActionServiceName} response: {response.ToJson(Newtonsoft.Json.Formatting.None)}");
                if (response.confirm)
                {
                    if (startActionInvoke && IsStartRunRequesting(request) || (CurrentForkActionRequesting.command == "stop" && request.command == "resume"))
                    {
                        OnActionStart?.Invoke(this, request);
                    }
                    if (request.command == "stop")
                    {
                        OnActionDone?.Invoke(this, EventArgs.Empty);
                    }
                    CurrentForkActionRequesting = request;
                }
                return (response.confirm, "");

            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        internal async Task<(bool confirm, string message)> CallServiceAndWaitActionDone(VerticalCommandRequest request, int timeout = 80, bool startActionInvoke = true)
        {
            wait_action_down_cts = new CancellationTokenSource();
            var requestResult = await CallVerticalCommandService(request, startActionInvoke);
            if (!requestResult.confirm)
                return requestResult;
            return await WaitActionDone(wait_action_down_cts.Token, timeout);
        }

        protected virtual async Task<(bool success, string message)> WaitActionDone(CancellationToken token, int timeout = 300)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(timeout), token);
            }
            catch (TaskCanceledException)
            {
                logger.Info($"等待action done 任務已被取消(action done or stop command executed)");
                return (true, "");
            }

            return (false, "Timeout");
        }

        private bool VerticalDoneActionCallback(VerticalCommandRequest tin, out VerticalCommandResponse response)
        {
            WaitActionDoneFlag = false;
            logger.Info($"{CurrentForkActionRequesting.command} command action ack({tin.ToJson(Newtonsoft.Json.Formatting.None)}). AGVC Reply command =  {tin.command}");
            IsActionDone = true;
            response = new VerticalCommandResponse()
            {
                confirm = true
            };
            bool command_reply_done = tin.command == "done";
            if (!command_reply_done)
            {
                logger.Info($"{CurrentForkActionRequesting.command} command   action not done.. AGVC Reply command =  {tin.command}");
            }
            try
            {
                wait_action_down_cts?.Cancel();
                wait_action_down_cts?.Dispose();
            }
            catch (Exception ex)
            {
                logger.Warn("VerticalDoneActionCallback:" + ex.Message);
            }
            CurrentForkActionRequesting = new VerticalCommandRequest();
            return true;
        }

    }
}
