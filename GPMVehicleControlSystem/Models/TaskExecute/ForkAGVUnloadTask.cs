using AGVSystemCommonNet6.AGVDispatch.Messages;
using AGVSystemCommonNet6.Vehicle_Control.VCS_ALARM;
using GPMVehicleControlSystem.Models.VehicleControl.Vehicles;
using GPMVehicleControlSystem.Models.WorkStation;
using System.Threading;
using System.Threading.Tasks;

namespace GPMVehicleControlSystem.Models.TaskExecute
{
    /// <summary>
    /// ForkAGV卸貨任務類別
    /// </summary>
    public class ForkAGVUnloadTask : UnloadTask
    {
        public ForkAGVUnloadTask(Vehicle Agv, clsTaskDownloadData taskDownloadData) : base(Agv, taskDownloadData)
        {
        }

        protected override async Task<(bool confirm, AlarmCodes alarmCode)> CstExistCheckAfterEQActionFinishInEQAsync()
        {
            (bool confirm, AlarmCodes alarmCode) result = await base.CstExistCheckAfterEQActionFinishInEQAsync();
            if (result.confirm)
                return result;

            if (result.alarmCode == AlarmCodes.Has_Job_Without_Cst)
            {
                //應有貨卻無貨異常 retry
                logger.Error($"ForkAGV卸貨任務: 車上應有貨但無貨, 進行重試.");
                Agv.HandshakeStatusText = "車上應有貨但無貨, 進行重試...";
                var forkHeightChangeReuslt = await _GoLowPoseAsync();
                if (!forkHeightChangeReuslt.success)
                {
                    logger.Error($"ForkAGV卸貨任務: 重試卸貨失敗, 原因: {forkHeightChangeReuslt.alarm_code}");
                    return (false, forkHeightChangeReuslt.alarm_code);
                }

                await Task.Delay(1000);

                forkHeightChangeReuslt = await _GoHighPoseAsync();
                if (!forkHeightChangeReuslt.success)
                {
                    logger.Error($"ForkAGV卸貨任務: 重試卸貨失敗, 原因: {forkHeightChangeReuslt.alarm_code}");
                    return (false, forkHeightChangeReuslt.alarm_code);
                }
                await Task.Delay(1000);
                result = await base.CstExistCheckAfterEQActionFinishInEQAsync();
                //將牙叉位置調整到低位 方便脫出

                if (!result.confirm && Agv.WorkStations.Stations.TryGetValue(destineTag, out clsWorkStationData? data) && data.CargoTransferMode == CARGO_TRANSFER_MODE.AGV_Pick_and_Place)
                    await _GoLowPoseAsync();
            }

            return result;


            async Task<(double position, bool success, AlarmCodes alarm_code)> _GoLowPoseAsync()
            {
                return await ForkLifter.ForkGoTeachedPoseAsync(destineTag, height, FORK_HEIGHT_POSITION.DOWN_, 0.5, timeout: 20, invokeActionStart: false);

            }

            async Task<(double position, bool success, AlarmCodes alarm_code)> _GoHighPoseAsync()
            {
                return await ForkLifter.ForkGoTeachedPoseAsync(destineTag, height, FORK_HEIGHT_POSITION.UP_, 0.5, timeout: 20, invokeActionStart: false);
            }
        }
    }
}
