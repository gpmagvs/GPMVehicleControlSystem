using AGVSystemCommonNet6;
using AGVSystemCommonNet6.Abstracts;
using AGVSystemCommonNet6.Alarm.VMS_ALARM;
using AGVSystemCommonNet6.GPMRosMessageNet.Messages;
using AGVSystemCommonNet6.Log;
using RosSharp.RosBridgeClient.MessageTypes.Geometry;

namespace GPMVehicleControlSystem.Models.VehicleControl.VehicleComponent
{
    public class clsNavigation : CarComponent
    {
        public enum AGV_DIRECTION : ushort
        {
            FORWARD, LEFT, RIGHT, STOP
                , BYPASS = 11
        }
        public override COMPOENT_NAME component_name => COMPOENT_NAME.NAVIGATION;

        public new NavigationState Data => (NavigationState)StateData;

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
                    LOG.INFO($"AGVC Direction changed to : {value} ", color: ConsoleColor.DarkBlue);
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

        private AGV_DIRECTION ConvertToDirection(ushort direction)
        {
            if (direction == 0)
                return AGV_DIRECTION.FORWARD;
            else if (direction == 1)
                return AGV_DIRECTION.LEFT;
            else if (direction == 2)
                return AGV_DIRECTION.RIGHT;
            else if (direction == 11)
                return AGV_DIRECTION.BYPASS;
            else
                return AGV_DIRECTION.STOP;
        }

        public override void CheckStateDataContent()
        {
            LastVisitedTag = Data.lastVisitedNode.data;
            LinearSpeed = CalculateLinearSpeed(Data.robotPose.pose.position);
            AngularSpeed = CalculateAngularSpeed(Angle);
            Direction = ConvertToDirection(Data.robotDirect);
            last_position = Data.robotPose.pose.position;
            last_theta = Angle;
            if (Data.errorCode != 0)
            {
                var code = Data.errorCode;
                if (code == 1)
                    Current_Alarm_Code = AlarmCodes.Motion_control_Wrong_Received_Msg;
                else if (code == 2)
                    Current_Alarm_Code = AlarmCodes.Motion_control_Wrong_Extend_Path;
                else if (code == 3)
                    Current_Alarm_Code = AlarmCodes.Motion_control_Out_Of_Line_While_Forwarding_End;
                else if (code == 4)
                    Current_Alarm_Code = AlarmCodes.Motion_control_Out_Of_Line_While_Tracking_End_Point;
                else if (code == 5)
                    Current_Alarm_Code = AlarmCodes.Motion_control_Out_Of_Line_While_Moving;
                else if (code == 6)
                    Current_Alarm_Code = AlarmCodes.Motion_control_Out_Of_Line_While_Secondary;
                else if (code == 7)
                    Current_Alarm_Code = AlarmCodes.Motion_control_Missing_Tag_On_End_Point;
                else if (code == 8)
                    Current_Alarm_Code = AlarmCodes.Motion_control_Missing_Tag_While_Moving;
                else if (code == 9)
                    Current_Alarm_Code = AlarmCodes.Motion_control_Missing_Tag_While_Secondary;
                else if (code == 10)
                    Current_Alarm_Code = AlarmCodes.Motion_control_Wrong_Initial_Position_In_Secondary;
                else if (code == 11)
                    Current_Alarm_Code = AlarmCodes.Motion_control_Wrong_Initial_Angle_In_Secondary;
                else if (code == 12)
                    Current_Alarm_Code = AlarmCodes.Motion_control_Wrong_Unknown_Code;
                else if (code == 13)
                    Current_Alarm_Code = AlarmCodes.Map_Recognition_Rate_Too_Low;
                else
                    Current_Alarm_Code = AlarmCodes.Motion_control_Wrong_Unknown_Code;
            }
            else
            {
                Current_Warning_Code = AlarmCodes.None;
            }
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
