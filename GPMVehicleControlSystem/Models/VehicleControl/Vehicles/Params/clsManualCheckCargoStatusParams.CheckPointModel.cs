namespace GPMVehicleControlSystem.Models.VehicleControl.Vehicles.Params
{
    public partial class clsManualCheckCargoStatusParams
    {
        public class CheckPointModel
        {
            /// <summary>
            /// 開關
            /// </summary>
            public bool Enabled { get; set; } = false;
            public int CheckPointTag { get; set; } = 0;

            /// <summary>
            /// Unit: second
            /// </summary>
            public int Timeout { get; set; } = 1;
            public CHECK_MOMENT TriggerMoment { get; set; } = CHECK_MOMENT.BEFORE_LOAD;

        }

    }

}
