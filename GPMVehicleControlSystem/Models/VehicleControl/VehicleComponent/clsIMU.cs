using AGVSystemCommonNet6.Abstracts;
using AGVSystemCommonNet6.Alarm.VMS_ALARM;
using AGVSystemCommonNet6.GPMRosMessageNet.Messages;

namespace GPMVehicleControlSystem.Models.VehicleControl.VehicleComponent
{
    public class clsIMU : CarComponent
    {
        public override COMPOENT_NAME component_name => COMPOENT_NAME.IMU;

        public override string alarm_locate_in_name => component_name.ToString();

        public override void CheckStateDataContent()
        {
            GpmImuMsg _imu_state = (GpmImuMsg)base.StateData;
            if (_imu_state.state != 0)
            {
                Current_Warning_Code = AlarmCodes.IMU_Module_Error;
            }
            else
            {
                Current_Warning_Code = AlarmCodes.None;
            }
        }
    }
}
