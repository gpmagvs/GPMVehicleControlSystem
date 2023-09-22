using AGVSystemCommonNet6.GPMRosMessageNet.Services;
using AGVSystemCommonNet6.Log;
using Polly.Caching;
using RosSharp.RosBridgeClient;
using static GPMVehicleControlSystem.Models.VehicleControl.Vehicles.TsmcMiniAGV;

namespace GPMVehicleControlSystem.Models.Emulators
{
    public class MeasureServiceEmulator : AGVROSEmulator
    {
        public MeasureServiceEmulator() : base()
        {
        }

        internal override void TopicsPublish()
        {
        }
        internal override void AdvertiseServices()
        {
            rosSocket.AdvertiseService<VerticalCommandRequest, VerticalCommandResponse>("/command_action", CommandActionCallback);
            LOG.INFO($"Measure Service Advertise Now!   (Service Name : /command_action)");

        }

        internal override void TopicsAdvertise()
        {
        }
        internal override void InitNewTaskCommandActionServer()
        {
        }
        private bool CommandActionCallback(VerticalCommandRequest tin, out VerticalCommandResponse tout)
        {
            bool confirm = false;
            LOG.WARN($"[Measure Emu] Recieve service request : {tin.command}");
            if (tin.command.Contains("pose"))
            {
                try
                {
                    string[] splited = tin.command.Split(',');
                    string time_date = splited[1]; //yyyyMMdd
                    string time_time = splited[2]; //HHmmss
                    Task.Factory.StartNew(async () =>
                    {
                        await Task.Delay(5000);
                        clsMeasureResult result = new clsMeasureResult(0)
                        {
                            result = "done",
                            location = "AAA123",
                            illuminance = 23,
                            decibel = 44,
                            temperature = 23.23,
                            humudity = 55.23,
                            IPA = 10,
                            TVOC = 12.4,
                            Acetone = 1,
                            time = $"{DateTime.Now.ToString("yyyyMMdd HHmmss")}",
                            partical_03um = 3,
                            partical_05um = 5,
                            partical_10um = 10,
                            partical_30um = 30,
                            partical_50um = 50,
                            partical_100um = 100,
                            PID = 2
                        };
                        rosSocket.CallServiceAndWait<VerticalCommandRequest, VerticalCommandResponse>("/done_action", new VerticalCommandRequest
                        {
                            command = result.GetCommandStr(),
                        }); ;
                    });
                    confirm = true;
                }
                catch (Exception ex)
                {
                    LOG.ERROR($"command parse fail.({tin.command}){ex.Message}", ex);
                    confirm = false;
                }
            }

            tout = new VerticalCommandResponse
            {
                confirm = confirm,
            };
            return true;
        }

    }
}
