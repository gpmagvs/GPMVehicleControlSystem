using AGVSystemCommonNet6.Abstracts;
using AGVSystemCommonNet6.Alarm.VMS_ALARM;
using AGVSystemCommonNet6.GPMRosMessageNet.Messages;

namespace GPMVehicleControlSystem.Models.VehicleControl.VehicleComponent
{
    public class clsIMU : CarComponent
    {
        public override COMPOENT_NAME component_name => COMPOENT_NAME.IMU;

        public override void CheckStateDataContent()
        {
            GpmImuMsg _imu_state = (GpmImuMsg)base.StateData;
            if (_imu_state.state != 0)
            {
                current_alarm_code = AlarmCodes.IMU_Module_Error;
            }
            else
            {
                current_alarm_code = AlarmCodes.None;
            }
        }
    }
}
