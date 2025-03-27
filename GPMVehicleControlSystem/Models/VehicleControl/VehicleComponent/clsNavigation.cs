using AGVSystemCommonNet6;
using AGVSystemCommonNet6.AGVDispatch.Model;
using AGVSystemCommonNet6.GPMRosMessageNet.Messages;
using AGVSystemCommonNet6.Vehicle_Control.VCS_ALARM;
using RosSharp.RosBridgeClient;
using RosSharp.RosBridgeClient.MessageTypes.Geometry;

namespace GPMVehicleControlSystem.Models.VehicleControl.VehicleComponent
{
    public class clsNavigation : CarComponent
    {

        public enum AGV_DIRECTION : ushort
        {
            /// <summary>
            /// 直走
            /// </summary>
            FORWARD = 0,
            /// <summary>
            /// 左轉
            /// </summary>
            LEFT = 1,
            /// <summary>
            /// 右轉
            /// </summary>
            RIGHT = 2,
            /// <summary>
            /// 電池交換橫向移動
            /// </summary>
            BAT_EXCHANGE_TRANSVERSE = 3,
            /// <summary>
            /// 後退
            /// </summary>
            BACKWARD = 4,
            /// <summary>
            /// 電梯
            /// </summary>
            ELEVATOR = 5,
            /// <summary>
            /// 向左橫移
            /// </summary>
            LEFT_TRANSVERSE = 6,
            /// <summary>
            /// 向右橫移
            /// </summary>
            RIGHT_TRANSVERSE = 7,
            /// <summary>
            /// 障礙物偵測停止移動
            /// </summary>
            STOP_OBSTACLE_DETECTED = 8,
            /// <summary>
            /// 抵達目標點
            /// </summary>
            REACH_GOAL = 9,
            /// <summary>
            /// 進出port位
            /// </summary>
            PORT = 10,
            BYPASS = 11,
            /// <summary>
            /// 過自動門向左切
            /// </summary>
            PASS_AUTO_DOOR_TURN_LEFT = 12,
            /// <summary>
            /// 過自動門向右切
            /// </summary>
            PASS_AUTO_DOOR_TURN_RIGHT = 13,
            /// <summary>
            /// 進出特定PORT位
            /// </summary>
            SPFIEC_PORT = 20,
            /// <summary>
            /// 遇到障礙物後退
            /// </summary>
            BACKWARD_OBSTACLE = 98,
            /// <summary>
            /// 遇到障礙物避障
            /// </summary>
            AVOID_OBSTACLE = 99,
        }
        public override COMPOENT_NAME component_name => COMPOENT_NAME.NAVIGATION;

        private uint _lastRoboPoseHeaderSec = uint.MaxValue;

        public uint lastRoboPoseHeaderSec
        {
            get => _lastRoboPoseHeaderSec;
            set
            {
                if (_lastRoboPoseHeaderSec != value)
                {
                    _lastRoboPoseHeaderSec = value;
                    lastUpdateTime = DateTime.Now;
                }
            }
        }

        public new NavigationState Data => StateData == null ? new NavigationState() : (NavigationState)StateData;

        public event EventHandler<AGV_DIRECTION> OnDirectionChanged;
        public event EventHandler<int> OnLastVisitedTagUpdate;
        public event EventHandler OnRoboPoseUpdateTimeout;

        public double LinearSpeed { get; private set; } = 0;
        public double AngularSpeed { get; private set; } = 0;

        private int _previousTag = 0;
        private Point last_position { get; set; } = new Point();
        private double last_theta { get; set; } = 0;

        private AGV_DIRECTION _previousDirection = AGV_DIRECTION.REACH_GOAL;
        public override Message StateData
        {
            get => _StateData;
            set
            {
                _StateData = value;
                CheckStateDataContent();
            }
        }

        public AGV_DIRECTION Direction
        {
            get => _previousDirection;
            set
            {
                if (_previousDirection != value)
                {
                    logger.Info($"AGVC Direction changed to : {value}");
                    OnDirectionChanged?.Invoke(this, value);
                    _previousDirection = value;
                }
            }
        }
        public int LastVisitedTag
        {
            get => _previousTag;
            set
            {
                if (value != _previousTag)
                {
                    if (value != 0)
                        OnLastVisitedTagUpdate?.Invoke(this, value);
                    _previousTag = value;
                }
            }
        }

        public double Angle => Data.robotPose.pose.orientation.ToTheta();

        public override string alarm_locate_in_name => component_name.ToString();

        protected override void HandleCommunicationError()
        {
            base.HandleCommunicationError();
            OnRoboPoseUpdateTimeout?.Invoke(this, EventArgs.Empty);
        }

        public clsCoordination CurrentCoordination
        {
            get
            {
                double X = Math.Round(Data.robotPose.pose.position.x, 3);
                double Y = Math.Round(Data.robotPose.pose.position.y, 3);
                double Theta = Math.Round(Angle, 3);
                clsCoordination coordination = new clsCoordination(X, Y, Theta);
                return coordination;
            }
        }

        public override bool CheckStateDataContent()
        {
            lastRoboPoseHeaderSec = Data.robotPose.header.stamp.secs;

            if (!base.CheckStateDataContent())
                return false;
            LastVisitedTag = Data.lastVisitedNode.data;
            LinearSpeed = CalculateLinearSpeed(Data.robotPose.pose.position);
            AngularSpeed = CalculateAngularSpeed(Angle);
            Direction = Data.robotDirect.ToAGVDirection();
            last_position = Data.robotPose.pose.position;
            last_theta = Angle;
            if (Data.errorCode != 0)
            {
                AlarmCodes _Alarm_Code = Data.errorCode.ToMotionAlarmCode();
                if (_Alarm_Code == AlarmCodes.Task_Path_Road_Closed)
                    Current_Warning_Code = _Alarm_Code;
                else
                    Current_Alarm_Code = _Alarm_Code;
            }
            else
                Current_Alarm_Code = AlarmCodes.None;
            return true;
        }
        //180d 3.14  
        private double CalculateAngularSpeed(double angle)
        {
            double angle_diff = angle - last_theta;
            double time_period = (DateTime.Now - lastUpdateTime).TotalSeconds;
            return Math.Round(Math.Abs(angle_diff) / time_period * Math.PI / 180.0, 2);
        }

        private double CalculateLinearSpeed(Point currentPosition)
        {
            double time_period = (DateTime.Now - lastUpdateTime).TotalSeconds;
            //移動距離 (m)
            var displacement = Math.Sqrt(Math.Pow((currentPosition.x - last_position.x), 2) + Math.Pow((currentPosition.y - last_position.y), 2)); //m
            return Math.Round(displacement / time_period, 1);
        }
        protected override void _CommunicationErrorJudge()
        {
            double timeDiff = (DateTime.Now - lastUpdateTime).TotalSeconds;
            IsCommunicationError = timeDiff > 2;
        }
        public override bool IsCommunicationError { get => base.IsCommunicationError; set => base.IsCommunicationError = value; }
    }


}
