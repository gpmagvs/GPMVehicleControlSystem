using AGVSystemCommonNet6;
using AGVSystemCommonNet6.AGVDispatch.Messages;
using AGVSystemCommonNet6.GPMRosMessageNet.Services;
using System.Diagnostics;
using static AGVSystemCommonNet6.GPMRosMessageNet.Services.VerticalCommandRequest;

namespace GPMVehicleControlSystem.Models.VehicleControl.AGVControl
{
    public class ForkAGVController : SubmarinAGVControl
    {
        /// <summary>
        /// Z軸完成伺服動作的事件, bool false =>異常;true =>已完成伺服動作
        /// </summary>
        public Action<bool> OnZAxisActionDone;
        public bool _IsZAxisActionDone = false;

        private double PoseTarget = 0;
        private double Speed = 0;
        public event EventHandler<VerticalCommandRequest> OnForkStartMove;
        public event EventHandler OnForkStopMove;
        public bool IsZAxisActionDone
        {
            get => _IsZAxisActionDone;
            set
            {
                _IsZAxisActionDone = value;
                if (_IsZAxisActionDone)
                {
                    WaitActionDoneFlag = false;

                    if (IsForkStartRunCommand(CurrentForkActionRequesting))
                        OnForkStopMove?.Invoke(this, EventArgs.Empty);
                }
            }
        }
        public bool WaitActionDoneFlag { get; private set; } = false;
        public double HSafeSetting { get; private set; }

        public double CurrentPosition { get; set; }

        public delegate double OnForkStartGoHomeDelage();
        public OnForkStartGoHomeDelage OnForkStartGoHome;

        public VerticalCommandRequest CurrentForkActionRequesting { get; set; } = new VerticalCommandRequest();
        public VerticalCommandRequest BeforeForkStopActionRequesting { get; set; } = new VerticalCommandRequest();
        public double InitForkSpeed = 1;
        public ForkAGVController()
        {
        }

        public ForkAGVController(string IP, int Port) : base(IP, Port)
        {
        }

        public override void AdertiseROSServices()
        {
            base.AdertiseROSServices();
            rosSocket?.AdvertiseService<VerticalCommandRequest, VerticalCommandResponse>("/done_action", VerticalDoneActionCallback);
        }

        private bool VerticalDoneActionCallback(VerticalCommandRequest tin, out VerticalCommandResponse response)
        {
            logger.Info($"{CurrentForkActionRequesting.command} command action ack. AGVC Reply command =  {tin.command}");
            IsZAxisActionDone = true;
            response = new VerticalCommandResponse()
            {
                confirm = true
            };
            bool command_reply_done = tin.command == "done";
            if (!command_reply_done)
            {
                logger.Info($"{CurrentForkActionRequesting.command} command   action not done.. AGVC Reply command =  {tin.command}");
            }
            CurrentForkActionRequesting = new VerticalCommandRequest();
            return IsZAxisActionDone;
        }
        public async Task<(bool confirm, string message)> ZAxisInit()
        {
            WaitActionDoneFlag = false;
            VerticalCommandRequest request = new VerticalCommandRequest
            {
                model = "FORK",
                command = "init",
                target = 0,
                speed = 0
            };
            return await CallVerticalCommandService(request);
        }

        public async Task<(bool success, string message)> ZAxisGoTo(double target, double speed = 1.0, bool wait_done = true)
        {
            PoseTarget = target;
            Speed = speed;
            WaitActionDoneFlag = wait_done;
            VerticalCommandRequest request = new VerticalCommandRequest
            {
                model = "FORK",
                command = "pose",
                speed = speed,
                target = target
            };
            try
            {
                IsZAxisActionDone = false;
                (bool confirm, string message) callSerivceResult = await CallVerticalCommandService(request);
                if (!wait_done)
                    return callSerivceResult;
                else
                {
                    if (IsZAxisActionDone)
                        return (true, "");
                    return await WaitActionDone();
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }
        internal CancellationTokenSource wait_action_down_cts = new CancellationTokenSource();
        private async Task<(bool success, string message)> WaitActionDone(int timeout = 300)
        {
            return await Task.Run(() =>
            {
                wait_action_down_cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeout));
                Stopwatch sw = Stopwatch.StartNew();
                while (!IsZAxisActionDone)
                {
                    Thread.Sleep(1);

                    if (wait_action_down_cts.IsCancellationRequested)
                    {
                        sw.Stop();
                        string reason = sw.ElapsedMilliseconds >= timeout * 1000 ? "Timeout" : "Abort By Cancel Process";
                        string log_ = $"Fork Lifter Wait Action Done Fail- {reason}";
                        logger.Error(log_);
                        return (false, log_);
                    }
                    if (CurrentForkActionRequesting.command == "orig" && CurrentPosition < HSafeSetting)
                    {
                        WaitActionDoneFlag = false;
                        return (true, "Position under safety height.");
                    }
                }
                WaitActionDoneFlag = false;
                return (true, "");
            });
        }

        /// <summary>
        /// 向下微動
        /// </summary>
        /// <param name="speed"></param>
        /// <returns></returns>
        public async Task<(bool confirm, string message)> ZAxisDown(double speed = 1.0)
        {
            VerticalCommandRequest request = new VerticalCommandRequest
            {
                model = "FORK",
                command = "down",
                speed = speed,
            };
            return await CallVerticalCommandService(request);
        }       /// <summary>
                /// 向上微動
                /// </summary>
                /// <param name="speed"></param>
                /// <returns></returns>
        public async Task<(bool confirm, string message)> ZAxisUp(double speed = 1.0)
        {
            VerticalCommandRequest request = new VerticalCommandRequest
            {
                model = "FORK",
                command = "up",
                speed = speed,
            };
            return await CallVerticalCommandService(request);
        }

        public async Task<(bool confirm, string message)> ZAxisUpSearch(double speed = 1.0)
        {
            VerticalCommandRequest request = new VerticalCommandRequest
            {
                model = "FORK",
                command = "up_search",
                speed = speed,
            };
            return await CallVerticalCommandService(request);
        }


        public async Task<(bool confirm, string message)> ZAxisDownSearch(double speed = 1.0)
        {
            VerticalCommandRequest request = new VerticalCommandRequest
            {
                model = "FORK",
                command = "down_search",
                speed = speed,
            };
            return await CallVerticalCommandService(request);
        }
        public async Task<(bool confirm, string message)> ZAxisGoHome(double speed = 1.0, bool wait_done = true)
        {
            WaitActionDoneFlag = wait_done;
            HSafeSetting = OnForkStartGoHome == null ? 0 : OnForkStartGoHome();
            logger.Info($"Fork ready Go Home Position,HSafe={HSafeSetting}");
            VerticalCommandRequest request = new VerticalCommandRequest
            {
                model = "FORK",
                command = "orig",
                speed = speed,
            };
            try
            {
                IsZAxisActionDone = false;
                (bool confirm, string message) callSerivceResult = await CallVerticalCommandService(request);
                if (!wait_done)
                    return callSerivceResult;
                else
                {
                    if (IsZAxisActionDone)
                        return (true, "");
                    return await WaitActionDone();
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        /// <summary>
        /// 暫停動作
        /// </summary>
        /// <returns></returns>
        public async Task<(bool confirm, string message)> ZAxisStop()
        {
            WaitActionDoneFlag = false;
            VerticalCommandRequest request = new VerticalCommandRequest
            {
                model = "FORK",
                command = "stop",
                speed = 0,
                target = 0
            };
            return await CallVerticalCommandService(request);
        }
        public async Task<(bool confirm, string message)> ZAxisResume()
        {
            await Task.Delay(800);
            if (BeforeForkStopActionRequesting.command == "")
                return (true, "No Request excuting before fork stopped");
            VerticalCommandRequest request = BeforeForkStopActionRequesting.Clone();
            logger.Warn($"Fork {request.command} resume to action");
            return await CallVerticalCommandService(request);
        }
        internal async Task<(bool confirm, string message)> CallVerticalCommandService(VerticalCommandRequest request)
        {
            try
            {

                logger.Info($"Try rosservice call /command_action : {request.ToJson()}");
                VerticalCommandResponse? response = await rosSocket?.CallServiceAndWait<VerticalCommandRequest, VerticalCommandResponse>("/command_action", request, 1000);
                if (response == null)
                    throw new TimeoutException();


                if (response.confirm)
                {
                    if (IsForkStartRunCommand(request) || (CurrentForkActionRequesting.command == "stop" && request.command == "resume"))
                    {
                        OnForkStartMove?.Invoke(this, request);
                    }
                    if (request.command == "stop")
                    {
                        BeforeForkStopActionRequesting = CurrentForkActionRequesting.Clone();
                        OnForkStopMove?.Invoke(this, EventArgs.Empty);
                    }
                    else
                        BeforeForkStopActionRequesting = new VerticalCommandRequest();//除了停止指令以外，需清空 BeforeForkStopActionRequesting 

                    CurrentForkActionRequesting = request;
                }
                return (response.confirm, "");

            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        private static bool IsForkStartRunCommand(VerticalCommandRequest request)
        {
            return request.command == "pose" || request.command == "orig" || request.command == "up" || request.command == "up_search"
                                                || request.command == "down" || request.command == "down_search";
        }


        public override Task<(bool request_success, bool action_done)> TriggerCSTReader(CST_TYPE cst_type)
        {
            return base.TriggerCSTReader(cst_type == CST_TYPE.None ? CST_TYPE.Rack : cst_type);//cargo 類型未知則預設使用 read rack
        }
    }
}
