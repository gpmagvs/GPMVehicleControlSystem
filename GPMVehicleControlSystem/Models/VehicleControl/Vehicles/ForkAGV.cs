using AGVSystemCommonNet6.Alarm.VMS_ALARM;
using AGVSystemCommonNet6.GPMRosMessageNet.Messages;
using AGVSystemCommonNet6.Log;
using GPMVehicleControlSystem.Models.VehicleControl.AGVControl;
using GPMVehicleControlSystem.Models.VehicleControl.VehicleComponent;
using static GPMVehicleControlSystem.VehicleControl.DIOModule.clsDOModule;

namespace GPMVehicleControlSystem.Models.VehicleControl.Vehicles
{
    /// <summary>
    /// 叉車AGV
    /// </summary>
    public partial class ForkAGV : SubmarinAGV
    {

        public clsDriver ForkState = new clsDriver()
        {
            location = clsDriver.DRIVER_LOCATION.FORK
        };
        /// <summary>
        /// Fork車控
        /// </summary>
        private ForkAGVController ForkAGVC => AGVC as ForkAGVController;
        public ForkAGV()
        {
        }
        public override async Task<bool> ResetMotor()
        {
            try
            {
                await base.ResetMotor();
                WagoDO.SetState(DO_ITEM.Vertical_Motor_Reset, true);
                await Task.Delay(100);
                WagoDO.SetState(DO_ITEM.Vertical_Motor_Reset, false);
                return true;
            }
            catch (Exception ex)
            {
                LOG.Critical(ex.Message, ex);
                return false;
            }
        }

        internal override async Task<(bool confirm, string message)> Initialize()
        {
            (bool confirm, string message) baseInitize = await base.Initialize();
            if (!baseInitize.confirm)
                return baseInitize;
            (bool done, AlarmCodes alarm_code) forkInitizeResult = await ForkInitialize();
            return (forkInitizeResult.done, forkInitizeResult.alarm_code.ToString());
        }

        protected internal override void InitAGVControl(string RosBridge_IP, int RosBridge_Port)
        {
            AGVC = new ForkAGVController(RosBridge_IP, RosBridge_Port);
            AGVC.Connect();
            AGVC.ManualController.vehicle = this;
        }

        protected override void CarController_OnModuleInformationUpdated(object? sender, ModuleInformation _ModuleInformation)
        {
            base.CarController_OnModuleInformationUpdated(sender, _ModuleInformation);
            ForkState.StateData = _ModuleInformation.Action_Driver;
        }

    }
}
