using AGVSystemCommonNet6.Abstracts;
using AGVSystemCommonNet6.Alarm.VMS_ALARM;
using AGVSystemCommonNet6.GPMRosMessageNet.Messages;

namespace GPMVehicleControlSystem.Models.VehicleControl.VehicleComponent
{
    public class clsBarcodeReader : CarComponent
    {
        public event EventHandler<int> OnTagLeave;
        public override COMPOENT_NAME component_name => COMPOENT_NAME.BARCODE_READER;

        public new BarcodeReaderState Data => StateData == null ? new BarcodeReaderState() : (BarcodeReaderState)StateData;
        public int CurrentTag => Data == null ? 0 : (int)Data.tagID;

        public double CurrentAngle => Data == null ? 0 : (int)Data.theta;
        public double CurrentX => Data == null ? 0 : (int)Data.xValue;
        public double CurrentY => Data == null ? 0 : (int)Data.yValue;

        public override string alarm_locate_in_name => component_name.ToString();

        private uint PreviousTag = 0;
        public override void CheckStateDataContent()
        {
            BarcodeReaderState _brState = (BarcodeReaderState)StateData;
            if (_brState.tagID == 0 && PreviousTag != _brState.tagID)
                OnTagLeave?.Invoke(this, (int)_brState.tagID);

            PreviousTag = _brState.tagID;
            if (_brState.state == -1)
            {
                Current_Warning_Code = AlarmCodes.Barcode_Module_Error;
            }
            else
            {
                Current_Warning_Code = AlarmCodes.None;
            }
        }
    }
}
