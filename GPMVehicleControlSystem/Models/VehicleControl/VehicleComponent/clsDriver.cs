using AGVSystemCommonNet6.GPMRosMessageNet.Messages;
using AGVSystemCommonNet6.Vehicle_Control.VCS_ALARM;
using System;

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
        public bool isInitMode { get; private set; } = false;
        public double CurrentPosition { get => Data.position; }

        public override string alarm_locate_in_name => component_name.ToString();

        public static DriverAlarmTryAddelegate OnTryAddDriverAlarm;

        public delegate (bool needAdd, bool recoveryable) DriverAlarmTryAddelegate(clsDriver driverEntity, AlarmCodes alarmCode);

        public override AlarmCodes Current_Alarm_Code
        {
            get => base.Current_Alarm_Code;
            set
            {
                if (_current_alarm_code != value)
                {
                    try
                    {

                        if (value == AlarmCodes.None)
                            return;

                        if (OnTryAddDriverAlarm != null && !OnTryAddDriverAlarm.Invoke(this, value).needAdd)
                            return;

                        AlarmManager.AddAlarm(value, false);
                    }
                    finally
                    {
                        _current_alarm_code = value;
                    }
                }
            }
        }

        public override bool CheckStateDataContent()
        {
            if (!base.CheckStateDataContent())
                return false;
            DriverState _driverState = (DriverState)StateData;
            bool _isHasAlarm = _driverState.errorCode != 0;

            if (!_isHasAlarm)
                return true;

            if (isInitMode)
            {
                return true;
            }

            AlarmCodes _Current_Alarm_Code = _driverState.errorCode.ToDriverAlarmCode();

            bool _isAlarmCodeChanged = _Current_Alarm_Code != Current_Alarm_Code;
            if (!_isAlarmCodeChanged)
                return true;

            logger.Error($"{location} Driver Alarm , Code=_{_Current_Alarm_Code}({_driverState.errorCode})");
            Current_Alarm_Code = _Current_Alarm_Code;
            return true;
        }

        internal void SetAsInitMode()
        {
            isInitMode = true;
        }
        internal void SetAsNormalMode()
        {
            isInitMode = false;
        }
    }
}
