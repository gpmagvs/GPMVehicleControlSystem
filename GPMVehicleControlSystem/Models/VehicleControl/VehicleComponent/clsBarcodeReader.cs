using AGVSystemCommonNet6.GPMRosMessageNet.Messages;
using AGVSystemCommonNet6.Vehicle_Control.VCS_ALARM;

namespace GPMVehicleControlSystem.Models.VehicleControl.VehicleComponent
{
    public class clsBarcodeReader : CarComponent
    {
        public override COMPOENT_NAME component_name => COMPOENT_NAME.BARCODE_READER;

        public event EventHandler<uint> OnAGVReachingTag;
        public event EventHandler<uint> OnAGVLeavingTag;

        public new BarcodeReaderState Data => _StateData == null ? new BarcodeReaderState() : (BarcodeReaderState)_StateData;
        public int CurrentTag => Data == null ? 0 : (int)Data.tagID;

        /// <summary>
        /// 距離Tag中心的距離
        /// </summary>
        public double DistanceToTagCenter
        {
            get
            {
                if (CurrentTag == 0)
                    return 99999.0;
                return Math.Sqrt(Math.Pow(CurrentX, 2) + Math.Pow(CurrentY, 2));
            }
        }

        public double CurrentAngle => Data == null ? 0 : (int)Data.theta;
        public double CurrentX => Data == null ? 0 : (int)Data.xValue;
        public double CurrentY => Data == null ? 0 : (int)Data.yValue;

        public override string alarm_locate_in_name => component_name.ToString();

        private uint PreviousTag = 0;
        public override bool CheckStateDataContent()
        {
            if (!base.CheckStateDataContent())
                return false;

            BarcodeReaderState _brState = (BarcodeReaderState)StateData;
            var currentTag = _brState.tagID;
            if (currentTag != PreviousTag)
            {
                if (currentTag == 0)
                {
                    logger.Info($"Leave Tag {PreviousTag}", true);
                    OnAGVLeavingTag?.Invoke(this, PreviousTag);
                }
                else
                {
                    logger.Info($"Reach Tag {currentTag}", true);
                    OnAGVReachingTag?.Invoke(this, currentTag);
                }

            }

            PreviousTag = currentTag;
            if (_brState.state == -1)
            {
                Current_Warning_Code = AlarmCodes.Barcode_Module_Error;
            }
            else
            {
                Current_Warning_Code = AlarmCodes.None;
            }

            return true;
        }
    }
}
