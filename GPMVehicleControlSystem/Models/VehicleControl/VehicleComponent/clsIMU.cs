using AGVSystemCommonNet6;
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
using AGVSystemCommonNet6.MAP;
using SQLitePCL;
using System.Diagnostics;
using AGVSystemCommonNet6.Vehicle_Control;
using AGVSystemCommonNet6.Vehicle_Control.VMS_ALARM;

namespace GPMVehicleControlSystem.Models.VehicleControl.VehicleComponent
{
    public class clsIMU : CarComponent
    {
        private clsImpactDetectionParams _Options = new clsImpactDetectionParams();
        public clsImpactDetectionParams Options
        {
            get
            {
                return OnOptionsFetching == null ? _Options : OnOptionsFetching();
            }
            set
            {
                _Options = value;
            }
        }

        public event EventHandler<IMUStateErrorEventData> OnImuStatesError;
        internal delegate clsImpactDetectionParams OptionsFetchDelegate();
        internal OptionsFetchDelegate OnOptionsFetching;
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
            if (_imu_state.imuData.linear_acceleration.z < 9.4)
            {
                OnImuStatesError?.Invoke(this, new IMUStateErrorEventData(_imu_state.imuData.linear_acceleration, AlarmCodes.IMU_Pitch_State_Error));
            }
            if (Options.Enabled)
            {
                var acc_raw = _imu_state.imuData.linear_acceleration;
                bool Isdetected = ImpactDetection(acc_raw, out var mag);
                if (Isdetected)
                {
                    OnImuStatesError?.Invoke(this, new IMUStateErrorEventData(acc_raw, AlarmCodes.IMU_Impacting));
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


        public class IMUStateErrorEventData
        {
            public AlarmCodes Imu_AlarmCode { get; }
            public Vector3 AccRaw { get; }
            public double Mag => Vector<double>.Build.DenseOfArray(new double[] { AccRaw.x, AccRaw.y, AccRaw.z }).L2Norm() / 9.8;

            public IMUStateErrorEventData(Vector3 acc_raw, AlarmCodes Imu_AlarmCode)
            {
                AccRaw = acc_raw;
                this.Imu_AlarmCode = Imu_AlarmCode;
            }
        }
    }
}
