using AGVSystemCommonNet6;
using AGVSystemCommonNet6.AGVDispatch.Model;
using AGVSystemCommonNet6.GPMRosMessageNet.Messages;
using AGVSystemCommonNet6.Log;
using AGVSystemCommonNet6.Vehicle_Control;
using AGVSystemCommonNet6.Vehicle_Control.VCS_ALARM;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using RosSharp.RosBridgeClient.MessageTypes.Geometry;
using static GPMVehicleControlSystem.Models.VehicleControl.VehicleComponent.clsNavigation;

namespace GPMVehicleControlSystem.Models.VehicleControl.VehicleComponent
{
    public class clsNavigation : CarComponent
    {

        public enum AGV_DIRECTION : ushort
        {
            FORWARD,
            LEFT,
            RIGHT,
            STOP,
            BYPASS = 11
        }
        public override COMPOENT_NAME component_name => COMPOENT_NAME.NAVIGATION;

        public new NavigationState Data => StateData == null ? new NavigationState() : (NavigationState)StateData;

        public event EventHandler<AGV_DIRECTION> OnDirectionChanged;
        public event EventHandler<int> OnLastVisitedTagUpdate;

        public double LinearSpeed { get; private set; } = 0;
        public double AngularSpeed { get; private set; } = 0;

        private int _previousTag = 0;
        private Point last_position { get; set; } = new Point();
        private double last_theta { get; set; } = 0;

        private AGV_DIRECTION _previousDirection = AGV_DIRECTION.STOP;
        public AGV_DIRECTION Direction
        {
            get => _previousDirection;
            set
            {
                if (_previousDirection != value)
                {
                    LOG.TRACE($"AGVC Direction changed to : {value}");
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

        public override async Task<bool> CheckStateDataContent()
        {

            if (!await base.CheckStateDataContent())
                return false;

            LastVisitedTag = Data.lastVisitedNode.data;
            LinearSpeed = CalculateLinearSpeed(Data.robotPose.pose.position);
            AngularSpeed = CalculateAngularSpeed(Angle);
            Direction = Data.robotDirect.ToAGVDirection();
            last_position = Data.robotPose.pose.position;
            last_theta = Angle;
            if (Data.errorCode != 0)
                Current_Alarm_Code = Data.errorCode.ToMotionAlarmCode();
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
    }


}
