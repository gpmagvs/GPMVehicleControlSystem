using AGVSystemCommonNet6;
using AGVSystemCommonNet6.Abstracts;
using AGVSystemCommonNet6.Alarm.VMS_ALARM;
using AGVSystemCommonNet6.GPMRosMessageNet.Messages;
using AGVSystemCommonNet6.Log;
using GPMVehicleControlSystem.Models.VehicleControl.Vehicles.Params;
using RosSharp.RosBridgeClient.MessageTypes.Geometry;
using RosSharp.RosBridgeClient.MessageTypes.Sensor;

namespace GPMVehicleControlSystem.Models.VehicleControl.VehicleComponent
{
    public class clsIMU : CarComponent
    {
        public clsImpactDetectionParams Options { get; set; } = new clsImpactDetectionParams();

        public event EventHandler<Vector3> OnImpactDetecting;

        public override COMPOENT_NAME component_name => COMPOENT_NAME.IMU;

        public override string alarm_locate_in_name => component_name.ToString();

        public Imu IMUData { get; private set; } = new Imu();

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
            if (Options.Enabled)
                ImpactDetection(_imu_state);
            IMUData = _imu_state.imuData;
        }

        private void ImpactDetection(GpmImuMsg _imu_state)
        {
            var currentAccx = _imu_state.imuData.linear_acceleration.x;
            var currentAccy = _imu_state.imuData.linear_acceleration.y;

            var previousAccx = IMUData.linear_acceleration.x;
            var previousAccy = IMUData.linear_acceleration.y;

            bool impactXDir = currentAccx > previousAccx & Math.Abs(currentAccx - previousAccx) > Options.ThresHold_XDir;
            bool impactYDir = currentAccy > previousAccy & Math.Abs(currentAccy - previousAccy) > Options.ThresHold_YDir;
            if (impactXDir || impactYDir)
            {
                if (impactXDir & impactYDir)
                    Current_Warning_Code = AlarmCodes.IMU_Impacting;
                else
                    Current_Warning_Code = impactXDir ? AlarmCodes.IMU_Impacting_X_Dir : AlarmCodes.IMU_Impacting_Y_Dir;
                OnImpactDetecting?.Invoke(this, _imu_state.imuData.linear_acceleration);
            }
            else
            {
                Current_Warning_Code = AlarmCodes.None;
            }

        }
    }
}
