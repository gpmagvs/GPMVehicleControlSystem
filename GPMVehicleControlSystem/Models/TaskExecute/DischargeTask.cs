using AGVSystemCommonNet6.AGVDispatch.Messages;
using AGVSystemCommonNet6.Vehicle_Control.VCS_ALARM;
using GPMVehicleControlSystem.Models.Buzzer;
using GPMVehicleControlSystem.Models.VehicleControl.AGVControl;
using GPMVehicleControlSystem.Models.VehicleControl.Vehicles;
using System.Diagnostics;
using static AGVSystemCommonNet6.clsEnums;
using static GPMVehicleControlSystem.Models.VehicleControl.VehicleComponent.clsLaser;
using static GPMVehicleControlSystem.VehicleControl.DIOModule.clsDIModule;
using static GPMVehicleControlSystem.VehicleControl.DIOModule.clsDOModule;

namespace GPMVehicleControlSystem.Models.TaskExecute
{
    public class DischargeTask : ChargeTask
    {
        public override ACTION_TYPE action { get; set; } = ACTION_TYPE.Discharge;
        protected override int ExitPointTag => RunningTaskData.Homing_Trajectory.Last().Point_ID;
        public DischargeTask(Vehicle Agv, clsTaskDownloadData taskDownloadData) : base(Agv, taskDownloadData)
        {
        }
        public override async Task<bool> LaserSettingBeforeTaskExecute()
        {
            try
            {
                await base.LaserSettingBeforeTaskExecute();
                await Agv.Laser.ModeSwitch(LASER_MODE.Secondary);
                await Agv.Laser.FrontBackLasersEnable(false, true);
                return true;
            }
            catch (Exception ex)
            {
                logger.Error(ex);
                return false;
            }
        }
        protected override void BuzzerPlayMusic(ACTION_TYPE action)
        {
            BuzzerPlayer.SoundPlaying = SOUNDS.Action;
        }
        public override void DirectionLighterSwitchBeforeTaskExecute()
        {
            Task.Factory.StartNew(async () =>
            {
                await Task.Delay(1000);
                if (Agv.lastVisitedMapPoint.DodgeMode == 13)
                    Agv.DirectionLighter.ForwardAndBackward();
                else
                    Agv.DirectionLighter.Backward();
            });
        }

        internal override async Task<(bool success, AlarmCodes alarmCode)> HandleAGVCActionSucceess()
        {
            IsBackToSecondaryPt = false;
            if (forkGoHomeTask != null)
            {
                Task.WaitAll(new Task[] { forkGoHomeTask });
                if (ForkGoHomeResultAlarmCode != AlarmCodes.None)
                    return (false, ForkGoHomeResultAlarmCode);
            }
            Agv.SetIsCharging(false);
            return (true, AlarmCodes.None);
        }
        protected override async Task<CarController.SendActionCheckResult> TransferTaskToAGVC()
        {
            _ = Task.Run(async () =>
            {
                await Task.Delay(1000);
                Agv.SetIsCharging(false);
            });
            IsBackToSecondaryPt = true;
            await Agv.Laser.SideLasersEnable(false);
            return await base.TransferTaskToAGVC();
        }
        public override async Task<(bool confirm, AlarmCodes alarm_code)> BeforeTaskExecuteActions()
        {
            Agv.WagoDO.SetState(DO_ITEM.Recharge_Circuit, false);
            ForkHomeProcess();
            return (true, AlarmCodes.None);
        }


    }
}
