using AGVSystemCommonNet6.Alarm.VMS_ALARM;
using AGVSystemCommonNet6.GPMRosMessageNet.Messages;
using AGVSystemCommonNet6.Log;
using GPMVehicleControlSystem.Models.VehicleControl.AGVControl;
using GPMVehicleControlSystem.Models.VehicleControl.VehicleComponent;
using System.Diagnostics;
using static AGVSystemCommonNet6.clsEnums;
using static GPMVehicleControlSystem.VehicleControl.DIOModule.clsDIModule;
using static GPMVehicleControlSystem.VehicleControl.DIOModule.clsDOModule;

namespace GPMVehicleControlSystem.Models.VehicleControl.Vehicles
{
    /// <summary>
    /// 叉車AGV
    /// </summary>
    public partial class ForkAGV : SubmarinAGV
    {
        public override string WagoIOConfigFilePath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "param/IO_Wago_Fork_AGV.ini");
        /// <summary>
        /// Fork車控
        /// </summary>
        private ForkAGVController ForkAGVC => AGVC as ForkAGVController;
        public bool IsForkInitialized => ForkLifter.IsInitialized;

        public clsForkLifter ForkLifter = new clsForkLifter();

        public ForkAGV()
        {
            ForkLifter.Driver = VerticalDriverState;
            ForkLifter.DIModule = WagoDI;
            ForkLifter.DOModule = WagoDO;
        }


        public override async Task<bool> ResetMotor()
        {
            try
            {
                await base.ResetMotor();
                await WagoDO.SetState(DO_ITEM.Vertical_Motor_Reset, true);
                await Task.Delay(100);
                await WagoDO.SetState(DO_ITEM.Vertical_Motor_Reset, false);
                return true;
            }
            catch (Exception ex)
            {
                LOG.Critical(ex.Message, ex);
                return false;
            }
        }

        protected override async Task<(bool confirm, string message)> InitializeActions()
        {

            (bool confirm, string message) baseInitize = await base.InitializeActions();
            if (!baseInitize.confirm)
                return baseInitize;
            (bool done, AlarmCodes alarm_code) forkInitizeResult = await ForkLifter.ForkInitialize();
            return (forkInitizeResult.done, forkInitizeResult.alarm_code.ToString());
        }

        protected override void CreateAGVCInstance(string RosBridge_IP, int RosBridge_Port)
        {
            AGVC = new ForkAGVController(RosBridge_IP, RosBridge_Port);
            ForkLifter.fork_ros_controller = ForkAGVC;
        }

        protected internal override void SoftwareEMO()
        {
            ForkAGVC.ZAxisStop();
            base.SoftwareEMO();
        }

        protected override async void DOSignalDefaultSetting()
        {
            base.DOSignalDefaultSetting();
            await WagoDO.SetState(DO_ITEM.Vertical_Hardware_limit_bypass, false);
            await WagoDO.SetState(DO_ITEM.Vertical_Belt_SensorBypass, false);
            await WagoDO.SetState(DO_ITEM.Fork_Under_Pressing_SensorBypass, false);

        }
        protected override void WagoDIEventRegist()
        {
            base.WagoDIEventRegist();
        }
    }
}
