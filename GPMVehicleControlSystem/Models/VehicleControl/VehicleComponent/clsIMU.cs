using AGVSystemCommonNet6.GPMRosMessageNet.Messages;
using AGVSystemCommonNet6.Vehicle_Control;
using AGVSystemCommonNet6.Vehicle_Control.VCS_ALARM;
using GPMVehicleControlSystem.Models.VehicleControl.Vehicles.Params;
using MathNet.Numerics.LinearAlgebra;
using Newtonsoft.Json.Converters;
using RosSharp.RosBridgeClient.MessageTypes.Geometry;
using RosSharp.RosBridgeClient.MessageTypes.Sensor;
using System.Text.Json.Serialization;
using static GPMVehicleControlSystem.Models.VehicleControl.VehicleComponent.clsIMU;

namespace GPMVehicleControlSystem.Models.VehicleControl.VehicleComponent
{
    public class clsIMU : CarComponent
    {
        public enum PITCH_STATES
        {
            /// <summary>
            /// 正常狀態
            /// </summary>
            NORMAL,
            /// <summary>
            /// 傾斜狀態
            /// </summary>
            INCLINED,
            /// <summary>
            /// 側翻
            /// </summary>
            SIDE_FLIP

        }
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
        internal event EventHandler<Vector3> OnAccelermeterDataChanged;
        public override COMPOENT_NAME component_name => COMPOENT_NAME.IMU;
        private Vector3 _AccData;
        public Vector3 AccData
        {
            get => _AccData;
            set
            {
                OnAccelermeterDataChanged?.Invoke(this, value);
                _AccData = value;
            }
        }
        public Vector3 GyroData => StateData == null ? new Vector3(0, 0, 0) : ((GpmImuMsg)StateData).imuData.angular_velocity;
        public bool IsAccSensorError => AccData.x == 0 && AccData.y == 0 && AccData.z == 0;

        private PITCH_STATES _PitchState = PITCH_STATES.NORMAL;
        public PITCH_STATES PitchState
        {
            get => _PitchState;
            set
            {
                if (_PitchState != value)
                {
                    _PitchState = value;
                    if (value != PITCH_STATES.NORMAL && Options.PitchErrorDetection)
                    {
                        OnImuStatesError?.Invoke(this, new IMUStateErrorEventData(AccData, AlarmCodes.IMU_Pitch_State_Error, value));
                    }
                }
            }
        }
        public override string alarm_locate_in_name => component_name.ToString();
        private bool _IsImpacting = false;
        public bool IsImpacting
        {
            get => _IsImpacting;
            private set
            {
                if (_IsImpacting != value)
                {
                    _IsImpacting = value;
                    if (value && Options.Enabled)
                    {
                        OnImuStatesError?.Invoke(this, new IMUStateErrorEventData(AccData, AlarmCodes.IMU_Impacting, PitchState));
                    }
                }
            }
        }
        public Imu IMUData { get; private set; } = new Imu();

        public override async Task<bool> CheckStateDataContent()
        {

            if (!await base.CheckStateDataContent())
                return false;

            GpmImuMsg _imu_state = null;
            try
            {
                _imu_state = (GpmImuMsg)StateData;
            }
            catch (Exception ex)
            {
                _AccData = new Vector3(0, 0, 0);
                return false;
            }
            if (_imu_state.state != 0)
            {
                Current_Warning_Code = AlarmCodes.IMU_Module_Error;
            }
            else
            {
                Current_Warning_Code = AlarmCodes.None;
            }
            AccData = _imu_state == null ? new Vector3(0, 0, 0) : _imu_state.imuData.linear_acceleration;
            if (IsAccSensorError) //加速規異常
            {

            }
            else
            {
                PitchState = DeterminePitchState(AccData);
                IsImpacting = ImpactDetection(AccData, out var mag);
            }
            IMUData = _imu_state.imuData;
            return true;
        }

        private bool ImpactDetection(Vector3 acc_raw, out double mag)
        {
            double[] raw = new double[] { acc_raw.x, acc_raw.y, acc_raw.z };//9.8 m/s2
            mag = Vector<double>.Build.DenseOfArray(raw).L2Norm() / 9.8;
            return mag > Options.ThresHold;
        }

        private PITCH_STATES DeterminePitchState(Vector3 AccData)
        {
            double error_threshold = 0.5;//9.8 9.2
            double Xaxis_g_error = Math.Abs(Math.Abs(AccData.x) - 9.8);
            double Yaxis_g_error = Math.Abs(Math.Abs(AccData.y) - 9.8);
            double Zaxis_g_error = Math.Abs(Math.Abs(AccData.z) - 9.8);

            if ((Xaxis_g_error < error_threshold | Yaxis_g_error < error_threshold) && Zaxis_g_error > (9.8 - error_threshold))
                return PITCH_STATES.SIDE_FLIP;
            else if (Zaxis_g_error > error_threshold && AccData.z < 9.8)
                return PITCH_STATES.INCLINED;
            else
                return PITCH_STATES.NORMAL;
        }
        public class IMUStateErrorEventData
        {
            public AlarmCodes Imu_AlarmCode { get; }
            public Vector3 AccRaw { get; }
            [JsonConverter(typeof(StringEnumConverter))]
            public PITCH_STATES PitchState { get; } = PITCH_STATES.NORMAL;
            public double Mag => Vector<double>.Build.DenseOfArray(new double[] { AccRaw.x, AccRaw.y, AccRaw.z }).L2Norm() / 9.8;

            public IMUStateErrorEventData(Vector3 acc_raw, AlarmCodes Imu_AlarmCode, PITCH_STATES PitchState = PITCH_STATES.NORMAL)
            {
                AccRaw = acc_raw;
                this.PitchState = PitchState;
                this.Imu_AlarmCode = Imu_AlarmCode;
            }
        }
    }
}
