using AGVSystemCommonNet6.AGVDispatch.Messages;
using AGVSystemCommonNet6.Log;
using AGVSystemCommonNet6.Vehicle_Control.VCS_ALARM;
using GPMVehicleControlSystem.Models.Buzzer;
using GPMVehicleControlSystem.Models.VehicleControl.Vehicles;
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

        protected override async Task<(bool success, AlarmCodes alarmCode)> HandleAGVCActionSucceess()
        {
            Agv.WaitingForChargeStatusChangeFlag = true;
            await Task.Delay(1000);
            Agv._IsCharging = true;
            Agv.SetSub_Status(SUB_STATUS.Charging);
            BuzzerPlayer.Stop();
            //將狀態設為充電中後 ,開始等待電池真正開始充電

            _ = Task.Run(async () =>
            {
                var _delayTime = Agv.Parameters.BatteryModule.WaitChargeStartDelayTimeWhenReachChargeTaskFinish;
                int _time_count_down = _delayTime;
                Timer timer = new Timer(new TimerCallback((s) =>
                {
                    LOG.INFO($"AGV Sub Status Will Changed by charge state after {_time_count_down} second ");
                    _time_count_down--;
                }), null, 0, 1000);
                Thread.Sleep(TimeSpan.FromSeconds(_delayTime));
                LOG.INFO($"Agv.WaitingForChargeStatusChangeFlag = false");
                timer.Dispose();
                Agv.WaitingForChargeStatusChangeFlag = false;
            });

            return (true, AlarmCodes.None);
        }
    }
}
