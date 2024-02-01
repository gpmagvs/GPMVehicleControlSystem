using AGVSystemCommonNet6.AGVDispatch.Messages;
using AGVSystemCommonNet6.Log;
using AGVSystemCommonNet6.Vehicle_Control.VCS_ALARM;
using GPMVehicleControlSystem.Models.Buzzer;
using GPMVehicleControlSystem.Models.VehicleControl.Vehicles;
using RosSharp.RosBridgeClient.Actionlib;
using static AGVSystemCommonNet6.clsEnums;
using static GPMVehicleControlSystem.Models.VehicleControl.VehicleComponent.clsLaser;
using static GPMVehicleControlSystem.VehicleControl.DIOModule.clsDOModule;

namespace GPMVehicleControlSystem.Models.VehicleControl.TaskExecute
{
    public class ChargeTask : TaskBase
    {
        public delegate bool BeforeOpenChargeCircuitdelegate();
        public static BeforeOpenChargeCircuitdelegate OnChargeCircuitOpening;
        public override ACTION_TYPE action { get; set; } = ACTION_TYPE.Charge;

        public ChargeTask(Vehicle Agv, clsTaskDownloadData taskDownloadData) : base(Agv, taskDownloadData)
        {
        }

        public override async Task<bool> LaserSettingBeforeTaskExecute()
        {
            try
            {
                await Agv.Laser.FrontBackLasersEnable(false);
                await Agv.Laser.SideLasersEnable(false);
                await Agv.Laser.ModeSwitch(LASER_MODE.Bypass);
                return true;
            }
            catch (Exception ex)
            {
                LOG.ERROR(ex);
                return false;
            }
        }

        public override async Task<(bool confirm, AlarmCodes alarm_code)> BeforeTaskExecuteActions()
        {
            bool open_charge_circuit = true;
            if (OnChargeCircuitOpening != null)
                open_charge_circuit = OnChargeCircuitOpening();
            await Agv.WagoDO.SetState(DO_ITEM.Recharge_Circuit, open_charge_circuit);
            return await base.BeforeTaskExecuteActions();
        }

        public override void DirectionLighterSwitchBeforeTaskExecute()
        {
            Agv.DirectionLighter.Forward();
        }

        protected override Task<(bool success, AlarmCodes alarmCode)> HandleAGVCActionSucceess()
        {
            var result = base.HandleAGVCActionSucceess();

            Agv.Sub_Status = SUB_STATUS.Charging;

            if (!Agv.Parameters.BatteryModule.ChargeWhenLevelLowerThanThreshold)
            {
                int _time_to_wait_change_status = 15;//sec
                Task.Factory.StartNew(async () =>
                {
                    var _count_down = _time_to_wait_change_status;
                    Timer _count_down_timer = new Timer((_obj) =>
                    {
                        LOG.TRACE($"Sub Status will Change after {_count_down} sec");
                        _count_down--;
                    }, null, 1, 1000);
                    await Task.Delay(TimeSpan.FromSeconds(_time_to_wait_change_status));
                    _count_down_timer.Dispose();
                    bool isAGVRunning = Agv.AGVC.ActionStatus == ActionStatus.PENDING || Agv.AGVC.ActionStatus == ActionStatus.ACTIVE;
                    if (Agv.Sub_Status == SUB_STATUS.RUN || isAGVRunning)
                        return;
                    Agv.Sub_Status = !Agv.IsCharging ? (isAGVRunning ? SUB_STATUS.RUN : SUB_STATUS.IDLE) : SUB_STATUS.Charging;
                });
            }


            return result;
        }
    }
}
