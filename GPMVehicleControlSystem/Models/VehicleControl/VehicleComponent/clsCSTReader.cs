using AGVSystemCommonNet6.Abstracts;
using AGVSystemCommonNet6.GPMRosMessageNet.Messages;
using AGVSystemCommonNet6.Alarm.VMS_ALARM;
using RosSharp.RosBridgeClient;
using AGVSystemCommonNet6.Log;

namespace GPMVehicleControlSystem.Models.VehicleControl.VehicleComponent
{
    public partial class clsCSTReader : CarComponent
    {
        public override COMPOENT_NAME component_name => COMPOENT_NAME.CST_READER;
        public new CSTReaderState Data => StateData == null ? new CSTReaderState() : (CSTReaderState)StateData;

        public string _ValidCSTID = "";
        public string ValidCSTID
        {
            get => _ValidCSTID;
            set
            {
                if (_ValidCSTID != value)
                {
                    LOG.TRACE($"CST ID CHANGED TO {value} (Old= {_ValidCSTID})");
                    _ValidCSTID = value;
                }
            }
        }

        public override string alarm_locate_in_name => component_name.ToString();

        public override void CheckStateDataContent()
        {
        }

        internal void UpdateCSTIDDataHandler(object? sender, string cst_id)
        {
            ValidCSTID = cst_id;
        }
    }


}
