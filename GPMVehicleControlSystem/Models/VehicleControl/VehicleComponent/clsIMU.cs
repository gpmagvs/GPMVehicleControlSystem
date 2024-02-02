using AGVSystemCommonNet6;
using AGVSystemCommonNet6.AGVDispatch.Messages;
using AGVSystemCommonNet6.GPMRosMessageNet.Messages;
using AGVSystemCommonNet6.Log;
using AGVSystemCommonNet6.MAP;
using AGVSystemCommonNet6.Vehicle_Control;
using AGVSystemCommonNet6.Vehicle_Control.VCS_ALARM;
using GPMVehicleControlSystem.Models.VehicleControl.Vehicles.Params;
using MathNet.Numerics.LinearAlgebra;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using RosSharp.RosBridgeClient.MessageTypes.Geometry;
using RosSharp.RosBridgeClient.MessageTypes.Sensor;
using SQLitePCL;
using System.Text.Json.Serialization;
using WebSocketSharp;
using static AGVSystemCommonNet6.clsEnums;

namespace GPMVehicleControlSystem.Models.VehicleControl.VehicleComponent
{
    public class clsIMU : CarComponent
    {
        public clsIMU()
        {
            LoadMaxMiniRecords();
        }

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

        public string MinMaxRecordFilePath => Path.Combine(Environment.CurrentDirectory, "IMU_MaxMin_Record.json");

        public clsMaxMinGvalDataSaveModel MaxMinGValRecord = new clsMaxMinGvalDataSaveModel();

        public event EventHandler<IMUStateErrorEventData> OnImuStatesError;
        internal delegate clsImpactDetectionParams OptionsFetchDelegate();
        internal OptionsFetchDelegate OnOptionsFetching;
        internal event EventHandler<Vector3> OnAccelermeterDataChanged;
        public delegate (Point coordination, SUB_STATUS sub_status, clsTaskDownloadData taskData) GValMaxMinValChange();
        internal GValMaxMinValChange OnMaxMinGvalChanged;
        public override COMPOENT_NAME component_name => COMPOENT_NAME.IMU;
        private Vector3 _AccData;
        public Vector3 AccData
        {
            get => _AccData;
            set
            {
                if (!IsAccSensorError)
                    OnAccelermeterDataChanged?.Invoke(this, value);
                _AccData = value;
            }
        }
        public Vector3 GyroData => StateData == null ? new Vector3(0, 0, 0) : ((GpmImuMsg)StateData).imuData.angular_velocity;
        public bool IsAccSensorError => AccData == null ? false : (AccData.x == 0 && AccData.y == 0 && AccData.z == 0);

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
                    if (value && Options.PitchErrorDetection)
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
                LOG.Critical(ex.Message, ex);
                _AccData = new Vector3(0, 0, 0);
                return false;
            }
            Current_Warning_Code = _imu_state.state != 0 ? AlarmCodes.IMU_Module_Error : AlarmCodes.None;
            try
            {
                AccData = _imu_state == null ? new Vector3(0, 0, 0) : _imu_state.imuData.linear_acceleration;
                if (!IsAccSensorError) //加速規異常
                {
                    PitchState = DeterminePitchState(AccData);
                    IsImpacting = ImpactDetection(AccData, out var mag);
                    IMUData = _imu_state.imuData;

                    MaxAndMiniGValueUpdate(IMUData.linear_acceleration);
                }
            }
            catch (Exception ex)
            {
                LOG.Critical(ex.Message, ex);
            }
            return true;
        }
        public void ResetMaxAndMinGvalRecord()
        {
            MaxMinGValRecord.Reset(new Vector3(AccData.x / 9.8, AccData.y / 9.8, AccData.z / 9.8));
            SaveRecordValue(MaxMinGValRecord);
            LOG.TRACE($"IMU最大最小值紀錄已重置...{MaxMinGValRecord.ToJson()}");
        }
        private void MaxAndMiniGValueUpdate(Vector3 accData)
        {
            double currentGx = Math.Abs(accData.x / 9.8);
            double currentGy = Math.Abs(accData.y / 9.8);
            double currentGz = Math.Abs(accData.z / 9.8);
            double currentMag = Vector<double>.Build.DenseOfArray(new double[] { currentGx, currentGy, currentGz }).L2Norm();
            bool IsAnyMaxGValChanged = currentMag > MaxMinGValRecord.MaxMag;
            if (IsAnyMaxGValChanged)
            {
                MaxMinGValRecord.AccVal = new Vector3(currentGx, currentGy, currentGz);
                Task.Factory.StartNew(() =>
                {
                    Point _happen_corrdination = new Point();
                    if (OnMaxMinGvalChanged != null)
                    {
                        (Point coordination, SUB_STATUS sub_status, clsTaskDownloadData taskData) agv_status = OnMaxMinGvalChanged();
                        _happen_corrdination = agv_status.coordination;
                        MaxMinGValRecord.AGV_Status = agv_status.sub_status.ToString();
                        MaxMinGValRecord.AGV_TaskName = agv_status.taskData.Task_Name;
                        MaxMinGValRecord.AGV_Action = agv_status.taskData.Action_Type.ToString();
                    }
                    MaxMinGValRecord.Coordination = _happen_corrdination;
                    MaxMinGValRecord.Time = DateTime.Now;
                    SaveRecordValue(MaxMinGValRecord);
                });
            }

        }
        public class clsMaxMinGvalDataSaveModel
        {
            public DateTime Time { get; set; } = DateTime.MinValue;
            public string AGV_Status { get; set; } = "";
            public string AGV_TaskName { get; set; } = "";
            public string AGV_Action { get; set; } = "";
            public Point Coordination { get; set; } = new Point();
            public Vector3 AccVal { get; set; } = new Vector3();
            public double MaxMag => Vector<double>.Build.DenseOfArray(new double[] { AccVal.x, AccVal.y, AccVal.z }).L2Norm();

            public void Reset(Vector3 defaultVal)
            {
                Time = DateTime.Now;
                Coordination = new Point();
                AGV_Status = AGV_TaskName = AGV_Action = "";
                AccVal = defaultVal;
            }
        }
        private void LoadMaxMiniRecords()
        {
            if (File.Exists(MinMaxRecordFilePath))
            {
                MaxMinGValRecord = JsonConvert.DeserializeObject<clsMaxMinGvalDataSaveModel>(File.ReadAllText(MinMaxRecordFilePath));
                LOG.TRACE($"IMU Max/Min GVal Record Loaded.{MaxMinGValRecord.ToJson()}");
            }
        }

        private void SaveRecordValue(clsMaxMinGvalDataSaveModel record)
        {
            try
            {
                var json = Newtonsoft.Json.JsonConvert.SerializeObject(record, Formatting.Indented);
                LOG.TRACE($"Save IMU Max/Min Record:{json} to : {MinMaxRecordFilePath} ");
                File.WriteAllText(MinMaxRecordFilePath, json);
            }
            catch (Exception ex)
            {
                LOG.ERROR($"Exception happen when save IMU Max/Min Records({ex.Message})", ex);
            }
        }

        private bool ImpactDetection(Vector3 acc_raw, out double mag)
        {
            double[] raw = new double[] { acc_raw.x, acc_raw.y, acc_raw.z };//9.8 m/s2
            mag = Vector<double>.Build.DenseOfArray(raw).L2Norm() / 9.8;
            return mag > Options.ThresHold;
        }

        private PITCH_STATES DeterminePitchState(Vector3 AccData)
        {
            var threshold = Options.PitchErrorThresHold;
            double Zaxis_Gval_abs = Math.Abs(AccData.z / 9.8); //轉換成G值
            if (Zaxis_Gval_abs <= threshold)
                return PITCH_STATES.INCLINED;
            else
                return PITCH_STATES.NORMAL;
        }
        public class IMUStateErrorEventData
        {
            public AlarmCodes Imu_AlarmCode { get; }
            public Vector3 AccRaw { get; }
            [System.Text.Json.Serialization.JsonConverter(typeof(StringEnumConverter))]
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
