namespace GPMVehicleControlSystem.Models.VehicleControl.Vehicles
{
    public class VehicleInitializeException : Exception
    {
        public readonly bool alarmBuzzerOn;
        public VehicleInitializeException(string message, bool alarmBuzzerOn = false) : base(message)
        {
            this.alarmBuzzerOn = alarmBuzzerOn;
        }
    }
}
