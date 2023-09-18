using AGVSystemCommonNet6;
using AGVSystemCommonNet6.AGVDispatch.Messages;
using AGVSystemCommonNet6.Alarm.VMS_ALARM;
using AGVSystemCommonNet6.Log;
using AGVSystemCommonNet6.MAP;
using GPMVehicleControlSystem.Models.Buzzer;
using GPMVehicleControlSystem.Models.NaviMap;
using GPMVehicleControlSystem.Models.VehicleControl.Vehicles;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using System.Diagnostics;
using static AGVSystemCommonNet6.clsEnums;

namespace GPMVehicleControlSystem.Models.VehicleControl.TaskExecute
{
    public class NormalMoveTask : TaskBase
    {
        public override ACTION_TYPE action { get; set; } = ACTION_TYPE.None;
        public NormalMoveTask(Vehicle Agv, clsTaskDownloadData taskDownloadData) : base(Agv, taskDownloadData)
        {
        }


        public override void DirectionLighterSwitchBeforeTaskExecute()
        {
        }

        public override Task<(bool confirm, AlarmCodes alarm_code)> BeforeTaskExecuteActions()
        {
            if (Agv.Parameters.CargoBiasDetectionWhenNormalMoving)
            {
                LOG.INFO($"Start Cargo Bias Detection.");
                StartMonitorCargoBias();
            }
            Task.Run(() => WatchVirtualPtAndStopWorker());
            return base.BeforeTaskExecuteActions();
        }


        /// <summary>
        /// 偵測貨物傾倒
        /// </summary>
        /// <returns></returns>
        private async Task StartMonitorCargoBias()
        {
            await Task.Delay(1).ContinueWith(async (Task) =>
            {
                while (Agv.CargoStatus == Vehicle.CARGO_STATUS.HAS_CARGO_NORMAL && Agv.ExecutingTask.action == ACTION_TYPE.None)
                {
                    await Task.Delay(1);
                    if (Agv.CargoStatus == Vehicle.CARGO_STATUS.HAS_CARGO_BUT_BIAS | Agv.CargoStatus == Vehicle.CARGO_STATUS.NO_CARGO)
                    {
                        LOG.WARN($"貨物傾倒偵測觸發-Check1");
                        await Task.Delay(500); //避免訊號瞬閃導致誤偵測
                        if (Agv.CargoStatus != Vehicle.CARGO_STATUS.HAS_CARGO_NORMAL)
                        {
                            LOG.ERROR($"貨物傾倒偵測觸發-Check2_Actual Trigger. AGV Will Cycle Stop");
                            Agv.AGVC.AbortTask(RESET_MODE.CYCLE_STOP);
                            return;
                        }
                    }
                }
                LOG.INFO($"");
            });
        }

        private async void WatchVirtualPtAndStopWorker()
        {
            if (lastPt == null)
                return;

            if (!lastPt.IsVirtualPoint)
                return;
            LOG.WARN("終點站為虛擬點 Start Monitor .");
            double distance = 0;
            Stopwatch sw = Stopwatch.StartNew();

            while ((distance = lastPt.CalculateDistance(Agv.Navigation.Data.robotPose.pose.position.x, Agv.Navigation.Data.robotPose.pose.position.y)) > 0.05)
            {
                if (sw.ElapsedMilliseconds > 3000)
                {
                    sw.Restart();
                    LOG.WARN($"與虛擬點終點站距離:{distance} m");
                }
                await Task.Delay(1);
            }
            LOG.WARN("抵達虛擬點終點站 Stop Monitor .");
            AlarmManager.AddAlarm(AlarmCodes.Destine_Point_Is_Virtual_Point);
            Agv.AGVSTaskResetReqHandle(RESET_MODE.ABORT);

        }

        public override async Task<bool> LaserSettingBeforeTaskExecute()
        {
            return await Agv.Laser.ModeSwitch(this.RunningTaskData.ExecutingTrajecory.First().Laser, true);
        }

    }
}
