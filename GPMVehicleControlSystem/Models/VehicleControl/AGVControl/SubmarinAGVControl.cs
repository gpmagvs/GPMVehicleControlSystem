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

        public override void AdertiseROSServices()
        {
            rosSocket.AdvertiseService<CSTReaderCommandRequest, CSTReaderCommandResponse>("/CSTReader_done_action", CSTReaderDoneActionHandle);
        }
    }
}
