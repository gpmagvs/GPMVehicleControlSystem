using AGVSystemCommonNet6.Abstracts;
using AGVSystemCommonNet6.GPMRosMessageNet.Messages;
using AGVSystemCommonNet6.Alarm.VMS_ALARM;
using RosSharp.RosBridgeClient;

namespace GPMVehicleControlSystem.Models.VehicleControl.VehicleComponent
{
    public partial class clsCSTReader : CarComponent
    {
        public override COMPOENT_NAME component_name => COMPOENT_NAME.CST_READER;
        public new CSTReaderState Data => StateData == null ? new CSTReaderState() : (CSTReaderState)StateData;

        public string ValidCSTID { get; set; } = "";

        public override void CheckStateDataContent()
        {
        }

        internal void UpdateCSTIDDataHandler(object? sender, string cst_id)
        {
            ValidCSTID = cst_id;
        }
    }


}
