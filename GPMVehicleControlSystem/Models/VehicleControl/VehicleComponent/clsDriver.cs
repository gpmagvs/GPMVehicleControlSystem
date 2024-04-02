using AGVSystemCommonNet6.GPMRosMessageNet.Messages;
using AGVSystemCommonNet6.Log;
using AGVSystemCommonNet6.Vehicle_Control;
using AGVSystemCommonNet6.Vehicle_Control.VCS_ALARM;
using Microsoft.EntityFrameworkCore.Metadata.Internal;

namespace GPMVehicleControlSystem.Models.VehicleControl.VehicleComponent
{
    public class clsDriver : CarComponent
    {
        public enum DRIVER_LOCATION
        {
            LEFT,
            RIGHT,
            LEFT_FORWARD,
            RIGHT_FORWARD,
            LEFT_BACKWARD,
            RIGHT_BACKWARD,
            FORK
        }
        public override COMPOENT_NAME component_name => COMPOENT_NAME.DRIVER;
        public DRIVER_LOCATION location = DRIVER_LOCATION.RIGHT;
        public new DriverState Data => StateData == null ? new DriverState() : (DriverState)StateData;

        public double CurrentPosition { get => Data.position; }

        public override string alarm_locate_in_name => component_name.ToString();

        public override bool CheckStateDataContent()
        {
            if (!base.CheckStateDataContent())
                return false;
            DriverState _driverState = (DriverState)StateData;
            AlarmCodes _Current_Alarm_Code = _driverState.errorCode.ToDriverAlarmCode();
            if (_Current_Alarm_Code == Current_Alarm_Code)
                return true;

            Task.Run(async () =>
            {
                if (OnAlarmHappened != null && _Current_Alarm_Code != AlarmCodes.None)
                {
                    bool allow_added = await OnAlarmHappened(_Current_Alarm_Code);
                    if (!allow_added)
                    {
                        _Current_Alarm_Code = AlarmCodes.None;
                    }
                }
                if (_Current_Alarm_Code != AlarmCodes.None)
                {
                    LOG.Critical($"{location} Driver Alarm , Code=_{_Current_Alarm_Code}({_driverState.errorCode})");
                }
                if (_Current_Alarm_Code == AlarmCodes.Other_error && location == DRIVER_LOCATION.FORK)
                {
                    _Current_Alarm_Code = AlarmCodes.Fork_Pose_Change_Too_Large;
                }
                await Task.Delay(1000);
                if (((DriverState)StateData).errorCode == 0)
                    return;
                Current_Alarm_Code = _Current_Alarm_Code;
            });

            return true;
        }
    }
}
