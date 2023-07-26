using AGVSystemCommonNet6.GPMRosMessageNet.Services;
using static AGVSystemCommonNet6.GPMRosMessageNet.Services.VerticalCommandRequest;

namespace GPMVehicleControlSystem.Models.VehicleControl.AGVControl
{
    public class ForkAGVController : SubmarinAGVControl
    {

        /// <summary>
        /// Z軸完成伺服動作的事件, bool false =>異常;true =>已完成伺服動作
        /// </summary>
        public Action<bool> OnZAxisActionDone;
        public bool IsZAxisActionDone { get; private set; } = false;
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
            response = new VerticalCommandResponse()
            {
                confirm = true
            };
            IsZAxisActionDone = tin.command == "done";
            Task.Factory.StartNew(() =>
            {
                if (OnZAxisActionDone != null)
                    OnZAxisActionDone(IsZAxisActionDone);
            });
            return IsZAxisActionDone;
        }
        public async Task<(bool confirm, string message)> ZAxisInit()
        {
            VerticalCommandRequest request = new VerticalCommandRequest
            {
                model = "FORK",
                command = "init",
                target = 0,
                speed = 0
            };
            return await CallVerticalCommandService(request);
        }


        public async Task<(bool success, string message)> ZAxisGoTo(double target, double? speed = 1.0, bool wait_done = true)
        {
            VerticalCommandRequest request = new VerticalCommandRequest
            {
                model = "FORK",
                command = "pose",
                speed = (double)speed,
                target = target
            };
            try
            {
                (bool confirm, string message) callSerivceResult = await CallVerticalCommandService(request);
                if (!wait_done)
                    return callSerivceResult;
                else
                {
                    IsZAxisActionDone = false;
                    return await WaitActionDone();
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        private async Task<(bool success, string message)> WaitActionDone(int timeout = 60)
        {
            CancellationTokenSource wtd = new CancellationTokenSource(TimeSpan.FromSeconds(timeout));
            while (!IsZAxisActionDone)
            {
                await Task.Delay(1);
                if (wtd.IsCancellationRequested)
                    return (false, "Wait Action Done Timeout");
            }
            return (true, "");
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
            VerticalCommandRequest request = new VerticalCommandRequest
            {
                model = "FORK",
                command = "stop",
            };
            return await CallVerticalCommandService(request);
        }
        public async Task<(bool confirm, string message)> ZAxisResume()
        {
            VerticalCommandRequest request = new VerticalCommandRequest
            {
                model = "FORK",
                command = "resum",
            };
            return await CallVerticalCommandService(request);
        }

        private async Task<(bool confirm, string message)> CallVerticalCommandService(VerticalCommandRequest request)
        {
            try
            {
                IsZAxisActionDone = false;
                VerticalCommandResponse? response = rosSocket?.CallServiceAndWait<VerticalCommandRequest, VerticalCommandResponse>("/command_action",
                     request
                );
                if (response == null)
                    return (false, "Timeout");
                return (response.confirm, "");
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }
    }
}
