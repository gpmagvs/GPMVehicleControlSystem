using GPMVehicleControlSystem.Models.VehicleControl.Vehicles;
using RosSharp.RosBridgeClient;
using RosSharp.RosBridgeClient.MessageTypes.Geometry;

namespace GPMVehicleControlSystem.Models.VehicleControl.VehicleComponent
{
    public class MoveControl
    {
        public MoveControl(Vehicle vehicle, RosSocket rosSocket)
        {
            this.rosSocket = rosSocket;
            this.vehicle = vehicle;
        }
        public MoveControl(RosSocket rosSocket)
        {
            this.rosSocket = rosSocket;
        }
        public RosSocket rosSocket { get; set; }

        public Vehicle vehicle;
        private bool CheckSwitchState = false;

        public void Stop()
        {
            PublishCmdVel(0, 0);
            vehicle.DirectionLighter.CloseAll();
        }
        public void Backward(double speed = 0.08)
        {
            PublishCmdVel(-speed, 0);
        }

        public void Forward(double speed = 0.08)
        {
            PublishCmdVel(speed, 0);
        }

        /// <summary>
        /// 向左轉
        /// </summary>
        public void TurnLeft(double speed = 0.1)
        {
            PublishCmdVel(0, speed);
        }

        public void TurnRight(double speed = 0.1)
        {
            PublishCmdVel(0, -speed);
        }

        internal void FordwardRight(double speed)
        {
            PublishCmdVel(speed, -0.08);
        }

        internal void FordwardLeft(double speed)
        {
            PublishCmdVel(speed, 0.08);
        }

        internal void BackwardRight(double speed)
        {
            PublishCmdVel(-speed, 0.08);
        }

        internal void BackwardLeft(double speed)
        {
            PublishCmdVel(-speed, -0.08);
        }
        private void PublishCmdVel(double linear_speed, double angular_speed)
        {
            if (rosSocket == null)
                return;
            string id = rosSocket.Advertise<Twist>("/cmd_vel");
            Twist message = new Twist();
            message.linear.x = linear_speed;
            message.linear.y = 0;
            message.linear.z = 0;
            message.angular.x = 0;
            message.angular.y = 0;
            message.angular.z = angular_speed;
            rosSocket.Publish(id, message);
            vehicle?.DirectionLighter.CloseAll();
            if (angular_speed > 0)
                vehicle?.DirectionLighter.TurnRight(true);
            else
                vehicle?.DirectionLighter.TurnLeft(true);

            if (linear_speed > 0)
                vehicle?.DirectionLighter.Forward();
            else
                vehicle?.DirectionLighter.Backward();

        }
    }
}