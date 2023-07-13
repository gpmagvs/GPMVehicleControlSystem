using GPMVehicleControlSystem.Models.Buzzer;
using GPMVehicleControlSystem.Models.VehicleControl.AGVControl;
using GPMVehicleControlSystem.Models.VehicleControl.VehicleComponent;

namespace GPMVehicleControlSystem.Models.VehicleControl
{
    /// <summary>
    /// 巡檢AGV
    /// </summary>
    public partial class InspectionAGV : Vehicle
    {
        public InspectionAGV()
        {
            WheelDrivers = new clsDriver[] {
             new clsDriver{ location = clsDriver.DRIVER_LOCATION.RIGHT_FORWARD},
             new clsDriver{ location = clsDriver.DRIVER_LOCATION.LEFT_FORWARD},
             new clsDriver{ location = clsDriver.DRIVER_LOCATION.RIGHT_BACKWARD},
             new clsDriver{ location = clsDriver.DRIVER_LOCATION.LEFT_BACKWARD},
        };
        }

        private InspectorAGVCarController? InspectorAGVC => AGVC as InspectorAGVCarController;

        public override clsCSTReader CSTReader { get; set; } = null;

        internal override async Task<(bool confirm, string message)> Initialize()
        {
            //初始化儀器
            (bool confirm, string message) measurementInitResult = await InspectorAGVC?.MeasurementInit();
            if (!measurementInitResult.confirm)
                return (false, measurementInitResult.message);

            return await base.Initialize();
        }
        protected internal override void InitAGVControl(string RosBridge_IP, int RosBridge_Port)
        {
            AGVC = new InspectorAGVCarController(RosBridge_IP, RosBridge_Port);
            AGVC.Connect();
            AGVC.ManualController.vehicle = this;
            BuzzerPlayer.rossocket = AGVC.rosSocket;
            BuzzerPlayer.Alarm();
        }
    }
}
