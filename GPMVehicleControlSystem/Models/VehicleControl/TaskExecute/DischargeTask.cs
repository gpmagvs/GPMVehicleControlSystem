using AGVSystemCommonNet6.AGVDispatch.Messages;
using AGVSystemCommonNet6.Log;
using AGVSystemCommonNet6.Vehicle_Control.VCS_ALARM;
using GPMVehicleControlSystem.Models.Buzzer;
using GPMVehicleControlSystem.Models.VehicleControl.Vehicles;
using static AGVSystemCommonNet6.clsEnums;
using static GPMVehicleControlSystem.Models.VehicleControl.VehicleComponent.clsForkLifter;
using static GPMVehicleControlSystem.Models.VehicleControl.VehicleComponent.clsLaser;
using static GPMVehicleControlSystem.VehicleControl.DIOModule.clsDIModule;
using static GPMVehicleControlSystem.VehicleControl.DIOModule.clsDOModule;

namespace GPMVehicleControlSystem.Models.VehicleControl.TaskExecute
{
    public class DischargeTask : ChargeTask
    {
        public override ACTION_TYPE action { get; set; } = ACTION_TYPE.Discharge;

        public DischargeTask(Vehicle Agv, clsTaskDownloadData taskDownloadData) : base(Agv, taskDownloadData)
        {
        }
        public override async Task<bool> LaserSettingBeforeTaskExecute()
        {
            try
            {
                await Agv.Laser.AllLaserDisable();
                await Agv.Laser.ModeSwitch(LASER_MODE.Loading);
                await Agv.Laser.FrontBackLasersEnable(false, true);
                return true;
            }
            catch (Exception ex)
            {
                LOG.ERROR(ex);
                return false;
            }
        }
        public override void DirectionLighterSwitchBeforeTaskExecute()
        {
            Task.Factory.StartNew(async () =>
            {
                await Task.Delay(1000);
                Agv.DirectionLighter.Backward();
            });
        }

        internal override async Task<(bool success, AlarmCodes alarmCode)> HandleAGVCActionSucceess()
        {
            if (ForkLifter != null)
            {
                if (!Agv.Parameters.ForkAGV.NoWaitParkingFinishAndForkGoHomeWhenBackToSecondaryAtChargeStation)
                {
                    ForkHomeProcess(false);
                };
                if (IsNeedWaitForkHome)
                {
                    LOG.TRACE($"[Async Action] AGV Park Finish In Secondary, Waiting Fork Go Home Finish ");
                    Task.WaitAll(new Task[] { forkGoHomeTask });
                    LOG.TRACE($"[Async Action] Fork is Home Now");
                }
                LOG.WARN($"Fork Go Home When AGVC Action Finish , {ForkGoHomeResultAlarmCode}");
                if (ForkGoHomeResultAlarmCode != AlarmCodes.None)
                {
                    return (false, ForkGoHomeResultAlarmCode);
                }
            }
            Agv.SetSub_Status(SUB_STATUS.IDLE);
            return (true, AlarmCodes.None);
        }
        public override async Task<(bool confirm, AlarmCodes alarm_code)> BeforeTaskExecuteActions()
        {
            Agv.WagoDO.SetState(DO_ITEM.Recharge_Circuit, false);
            if (Agv.Parameters.ForkAGV.NoWaitParkingFinishAndForkGoHomeWhenBackToSecondaryAtChargeStation)
            {
                ForkHomeProcess(false);
            }
            return (true, AlarmCodes.None);
        }


    }
}
