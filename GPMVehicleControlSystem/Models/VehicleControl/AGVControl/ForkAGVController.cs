using AGVSystemCommonNet6;
using AGVSystemCommonNet6.GPMRosMessageNet.Services;
using AGVSystemCommonNet6.Log;
using System.Diagnostics;
using static AGVSystemCommonNet6.GPMRosMessageNet.Services.VerticalCommandRequest;

namespace GPMVehicleControlSystem.Models.VehicleControl.AGVControl
{
    public class ForkAGVController : SubmarinAGVControl
    {
        protected override string cst_reader_command { get; set; } = "read";
        /// <summary>
        /// Z軸完成伺服動作的事件, bool false =>異常;true =>已完成伺服動作
        /// </summary>
        public Action<bool> OnZAxisActionDone;
        public bool _IsZAxisActionDone = false;
        public bool IsZAxisActionDone
        {
            get => _IsZAxisActionDone;
            set
            {
                _IsZAxisActionDone = value;
                if (_IsZAxisActionDone)
                {
                    WaitActionDoneFlag = false;
                }
            }
        }
        public bool WaitActionDoneFlag { get; private set; } = false;
        public double HSafeSetting { get; private set; }

        public double CurrentPosition { get; set; }

        public delegate double OnForkStartGoHomeDelage();
        public OnForkStartGoHomeDelage OnForkStartGoHome;

        public VerticalCommandRequest CurrentForkActionRequesting { get; set; } = new VerticalCommandRequest();
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
            LOG.INFO($"{CurrentForkActionRequesting.command} command action ack. AGVC Reply command =  {tin.command}");
            IsZAxisActionDone = true;
            response = new VerticalCommandResponse()
            {
                confirm = true
            };
            bool command_reply_done = tin.command == "done";
            if (!command_reply_done)
            {
                LOG.INFO($"{CurrentForkActionRequesting.command} command   action not done.. AGVC Reply command =  {tin.command}");
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
            return await CallVerticalCommandService(request, false);
        }

        private double PoseTarget = 0;
        private double Speed = 0;
        public async Task<(bool success, string message)> ZAxisGoTo(double target, double speed = 1.0, bool wait_done = true)
        {
            PoseTarget = target;
            Speed = speed;
            IsZAxisActionDone = false;
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
        private async Task<(bool success, string message)> WaitActionDone(int timeout = 60)
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
                        LOG.Critical(log_);
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
            IsZAxisActionDone = false;
            WaitActionDoneFlag = wait_done;
            HSafeSetting = OnForkStartGoHome == null ? 0 : OnForkStartGoHome();
            LOG.INFO($"Fork ready Go Home Position,HSafe={HSafeSetting}");
            VerticalCommandRequest request = new VerticalCommandRequest
            {
                model = "FORK",
                command = "orig",
                speed = speed,
            };
            try
            {
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
            return await CallVerticalCommandService(request, false);
        }
        public async Task<(bool confirm, string message)> ZAxisResume()
        {
            VerticalCommandRequest request = new VerticalCommandRequest
            {
                model = "FORK",
                command = "resum",
                speed = Speed,
                target = PoseTarget
            };
            return await CallVerticalCommandService(request);
        }
        private async Task<(bool confirm, string message)> CallVerticalCommandService(VerticalCommandRequest request, bool wait_reply = true)
        {
            try
            {
                CurrentForkActionRequesting = request;

                LOG.INFO($"Try rosservice call /command_action : {request.ToJson()}");

                VerticalCommandResponse? response = await rosSocket?.CallServiceAndWait<VerticalCommandRequest, VerticalCommandResponse>("/command_action", request, 1000);

                if (response == null)
                    throw new TimeoutException();

                return (response.confirm, "");

            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }
    }
}
