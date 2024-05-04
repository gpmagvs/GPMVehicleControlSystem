using AGVSystemCommonNet6.AGVDispatch.Messages;
using AGVSystemCommonNet6.GPMRosMessageNet.Services;

namespace GPMVehicleControlSystem.Models.VehicleControl.AGVControl
{
    public partial class SubmarinAGVControl : CarController
    {
        public SubmarinAGVControl()
        {
        }

        public SubmarinAGVControl(string IP, int Port) : base(IP, Port)
        {
        }

        public override string alarm_locate_in_name => "SubmarinAGVControl";

        public override void AdertiseROSServices()
        {
            base.AdertiseROSServices();
            rosSocket.AdvertiseService<CSTReaderCommandRequest, CSTReaderCommandResponse>("/CSTReader_done_action", CSTReaderDoneActionHandle);
        }

    }
}
