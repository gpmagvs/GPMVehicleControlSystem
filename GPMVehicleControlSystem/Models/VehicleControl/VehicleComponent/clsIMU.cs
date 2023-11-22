using AGVSystemCommonNet6;
using AGVSystemCommonNet6.Abstracts;
using AGVSystemCommonNet6.Alarm.VMS_ALARM;
using AGVSystemCommonNet6.GPMRosMessageNet.Messages;
using AGVSystemCommonNet6.Log;
using GPMVehicleControlSystem.Models.VehicleControl.Vehicles.Params;
using RosSharp.RosBridgeClient.MessageTypes.Geometry;
using RosSharp.RosBridgeClient.MessageTypes.Sensor;
using MathNet.Numerics;
using AGVSystemCommonNet6.Tools;
using System.Runtime.CompilerServices;
using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.LinearAlgebra.Double;

namespace GPMVehicleControlSystem.Models.VehicleControl.VehicleComponent
{
    public class clsIMU : CarComponent
    {
        public clsImpactDetectionParams Options { get; set; } = new clsImpactDetectionParams();

        public event EventHandler<ImpactingData> OnImpactDetecting;

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
            {
                var acc_raw = _imu_state.imuData.linear_acceleration;
                bool Isdetected = ImpactDetection(acc_raw, out var mag);
                if (Isdetected)
                {
                    OnImpactDetecting?.Invoke(this, new ImpactingData(acc_raw, mag));
                }
            }
            IMUData = _imu_state.imuData;
        }

        private bool ImpactDetection(Vector3 acc_raw, out double mag)
        {
            double[] raw = new double[] { acc_raw.x, acc_raw.y, acc_raw.z };//9.8 m/s2
            mag = Vector<double>.Build.DenseOfArray(raw).L2Norm() / 9.8;
            return mag > Options.ThresHold;
        }


        public class ImpactingData
        {
            public Vector3 AccRaw { get; }
            public double Mag { get; }

            public ImpactingData(Vector3 acc_raw, double mag)
            {
                AccRaw = acc_raw;
                Mag = mag;
            }
        }
    }
}
