using AGVSystemCommonNet6.AGVDispatch.Messages;
using AGVSystemCommonNet6.Vehicle_Control.VCS_ALARM;
using GPMVehicleControlSystem.Models.Buzzer;
using GPMVehicleControlSystem.Models.VehicleControl.AGVControl;
using GPMVehicleControlSystem.Models.VehicleControl.Vehicles;
using static AGVSystemCommonNet6.clsEnums;
using static GPMVehicleControlSystem.Models.VehicleControl.VehicleComponent.clsLaser;
using static GPMVehicleControlSystem.VehicleControl.DIOModule.clsDOModule;

namespace GPMVehicleControlSystem.Models.TaskExecute
{
    public class ChargeTask : TaskBase
    {
        public delegate bool BeforeOpenChargeCircuitdelegate();
        public static BeforeOpenChargeCircuitdelegate OnChargeCircuitOpening;
        public override ACTION_TYPE action { get; set; } = ACTION_TYPE.Charge;
        public override int MoveActionTimeout => Agv.Parameters.LDULDParams.MoveActionTimeoutInSec * 1000;

        public ChargeTask(Vehicle Agv, clsTaskDownloadData taskDownloadData) : base(Agv, taskDownloadData)
        {
        }

        public override async Task<bool> LaserSettingBeforeTaskExecute()
        {
            try
            {
                await base.LaserSettingBeforeTaskExecute();
                await Agv.Laser.FrontBackLasersEnable(true, false);
                await Agv.Laser.ModeSwitch(LASER_MODE.Secondary);
                return true;
            }
            catch (Exception ex)
            {
                logger.Error(ex);
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
        protected override void BuzzerPlayMusic(ACTION_TYPE action)
        {
            BuzzerPlayer.SoundPlaying = SOUNDS.GoToChargeStation;
        }
        public override void DirectionLighterSwitchBeforeTaskExecute()
        {
            Agv.DirectionLighter.Forward();
        }
        protected override async Task<CarController.SendActionCheckResult> TransferTaskToAGVC()
        {
            await Agv.Laser.SideLasersEnable(false);
            return await base.TransferTaskToAGVC();
        }
        internal override async Task<(bool success, AlarmCodes alarmCode)> HandleAGVCActionSucceess()
        {
            DelayChargeStatusJudgeWork();
            Agv._IsCharging = true;
            BuzzerPlayer.SoundPlaying = SOUNDS.Stop;
            return (true, AlarmCodes.None);
        }

        private async Task DelayChargeStatusJudgeWork()
        {
            Agv.WaitingForChargeStatusChangeFlag = true;
            var _delayTime = Agv.Parameters.BatteryModule.WaitChargeStartDelayTimeWhenReachChargeTaskFinish;
            int _time_count_down = _delayTime;
            Timer timer = new Timer(new TimerCallback((s) =>
            {
                logger.Info($"AGV Sub Status Will Changed by charge state after {_time_count_down} second ");
                _time_count_down--;
            }), null, 0, 1000);
            await Task.Delay(TimeSpan.FromSeconds(_delayTime));
            logger.Info($"Agv.WaitingForChargeStatusChangeFlag = false");
            timer.Dispose();
            Agv.WaitingForChargeStatusChangeFlag = false;
        }
    }
}
