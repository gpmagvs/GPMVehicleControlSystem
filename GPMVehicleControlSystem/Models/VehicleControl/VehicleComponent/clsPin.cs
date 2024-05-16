using AGVSystemCommonNet6;
using AGVSystemCommonNet6.GPMRosMessageNet.Services;
using AGVSystemCommonNet6.Log;
using AGVSystemCommonNet6.Vehicle_Control;
using RosSharp.RosBridgeClient;
using System.Diagnostics;

namespace GPMVehicleControlSystem.Models.VehicleControl.VehicleComponent
{
    public class clsPin : CarComponent, IRosSocket
    {

        public string PinActionServiceName = "/pin_action";
        public string PinActionDonwServiceName = "/pin_done_action";

        public bool IsPinActionDone;

        private RosSocket _rosSocket;

        public PinCommandRequest pin_command = new PinCommandRequest()
        {
            model = "FORK",
        };

        public RosSocket rosSocket
        {
            get => _rosSocket;
            set
            {
                _rosSocket = value;
                _rosSocket?.AdvertiseService<PinCommandRequest, PinCommandResponse>(PinActionDonwServiceName, PinDoneActionCallback);
                LOG.TRACE($"[Pin] {PinActionDonwServiceName} Service advertised (Action request service name={PinActionServiceName})");
            }
        }

        public override COMPOENT_NAME component_name => COMPOENT_NAME.PIN;

        public override string alarm_locate_in_name => "PIN_DRIVER";

        public clsPin()
        {
        }

        /// <summary>
        /// 清除異常初始化回到原點
        /// </summary>
        /// <returns></returns>
        public async Task Init()
        {
            pin_command.command = "init";
            await _CallPinCommandActionService(pin_command);

        }

        public async Task Orig()
        {
            pin_command.command = "orig";
            await _CallPinCommandActionService(pin_command);
        }
        public async Task Up()
        {
            pin_command.command = "up";
            await _CallPinCommandActionService(pin_command);
        }
        public async Task Down()
        {
            pin_command.command = "down";
            await _CallPinCommandActionService(pin_command);
        }
        public async Task Lock()
        {
            pin_command.command = "lock";
            await _CallPinCommandActionService(pin_command);
        }
        public async Task Release()
        {
            pin_command.command = "release";
            await _CallPinCommandActionService(pin_command);
        }


        private async Task<bool> _CallPinCommandActionService(PinCommandRequest request)
        {
            IsPinActionDone = false;
            PinCommandResponse _resonpse = await _rosSocket.CallServiceAndWait<PinCommandRequest, PinCommandResponse>(PinActionServiceName, request);
            if (_resonpse == null)
                throw new Exception("Call Service Fail");
            if (_resonpse.confirm)
            {
                CancellationTokenSource _wait = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                while (!IsPinActionDone)
                {
                    await Task.Delay(1);
                    if (_wait.IsCancellationRequested)
                    {
                        LOG.ERROR($"Pin-{request.command} request timeout");
                        Current_Alarm_Code = AGVSystemCommonNet6.Vehicle_Control.VCS_ALARM.AlarmCodes.Pin_Action_Error;
                        throw new TimeoutException();
                    }
                }
                LOG.INFO($"Pin-{request.command} action finish done.");
                return true;
            }
            else
            {
                LOG.ERROR($"Pin-{request.command} request is rejected.");
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
            LOG.TRACE($"/pin_done_action , agvc response={tin.ToJson()}");
            bool command_reply_done = tin.command == "done";
            if (!command_reply_done)
            {
                LOG.INFO($"Pin command action not done.. AGVC Reply command =  {tin.command}");
            }
            return true;
        }
    }
}
