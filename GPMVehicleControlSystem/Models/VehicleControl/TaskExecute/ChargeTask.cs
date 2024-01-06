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
            Agv.Sub_Status = SUB_STATUS.Charging;
            await Agv.FeedbackTaskStatus(TASK_RUN_STATUS.ACTION_FINISH);

            //將狀態設為充電中後 ,開始等待電池真正開始充電

            _ = Task.Run(async () =>
            {
                Thread.Sleep(10000);
                Agv.WaitingForChargeStatusChangeFlag = false;
                //if (!Agv.Parameters.BatteryModule.ChargeWhenLevelLowerThanThreshold)
                //{
                //    LOG.TRACE($"充電站任務完成後10sec, 判斷是否充電中");
                //    Agv.JudgeIsBatteryCharging();
                //}
            });

            //if (Agv.Parameters.BatteryModule.ChargeWhenLevelLowerThanThreshold && !Agv.IsChargeCircuitOpened)
            //{
            //    Task.Run(async () =>
            //    {
            //        await Task.Delay(2000);
            //        Agv.Sub_Status = SUB_STATUS.Charging;
            //    });
            //}
            //if (Agv.IsCharging)
            //    Agv.Sub_Status = SUB_STATUS.Charging;

            return (true, AlarmCodes.None);
        }
    }
}
