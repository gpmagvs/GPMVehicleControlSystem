
using AGVSystemCommonNet6.Vehicle_Control.VCS_ALARM;
using GPMVehicleControlSystem.Models;
using GPMVehicleControlSystem.Models.VehicleControl.VehicleComponent;
using GPMVehicleControlSystem.Models.VehicleControl.Vehicles;
using RosSharp.RosBridgeClient.Actionlib;
using static AGVSystemCommonNet6.clsEnums;
using static GPMVehicleControlSystem.VehicleControl.DIOModule.clsDOModule;

namespace GPMVehicleControlSystem.Service
{
    public class BatteryStateMonitorBackgroundService : BackgroundService
    {
        private readonly ILogger<BatteryStateMonitorBackgroundService> logger;
        private DateTime lastOverTemperatureDetectedTime = DateTime.MinValue;
        public BatteryStateMonitorBackgroundService(ILogger<BatteryStateMonitorBackgroundService> logger)
        {
            this.logger = logger;
        }
        private Vehicle Vehicle => StaStored.CurrentVechicle;
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {

            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(1000);

                if (Vehicle == null)
                    continue;

                await MonitorBatteryOverVoltage();
                await MonitorBatteryOverTemperature();
            }
        }

        private async Task MonitorBatteryOverVoltage()
        {
            double threshold = Vehicle.Parameters.BatteryModule.CutOffChargeRelayVoltageThreshodlval;//mV
            List<double> currentVoltages = Vehicle.Batteries.Select(bat => (double)bat.Value.Data.Voltage).ToList();

            bool isOverVotage = currentVoltages.Any(voltag => voltag >= threshold);

            if (isOverVotage)
            {
                if (Vehicle.IsChargeCircuitOpened)
                {
                    string voltagesStr = string.Join(" mV,", currentVoltages);
                    logger.LogWarning($"Battery over-voltage when charging. Cut-off charge circuit!({voltagesStr})");
                    await Vehicle.WagoDO.SetState(DO_ITEM.Recharge_Circuit, false);
                    Vehicle.SetIsCharging(false);
                    AlarmManager.AddAlarm(AlarmCodes.Battery_Over_Voltage_When_Charging, true);
                    Vehicle.SetSub_Status(Vehicle.IsInitialized ? Vehicle.AGVC.ActionStatus == ActionStatus.ACTIVE ? SUB_STATUS.RUN : SUB_STATUS.IDLE : SUB_STATUS.DOWN);
                }
            }
        }

        private async Task MonitorBatteryOverTemperature()
        {
            try
            {

                bool isBatError = Vehicle.Batteries.Any(bat => bat.Value.Data.errorCode != 0);
                if (isBatError)
                {

                    byte errorCode = Vehicle.Batteries.FirstOrDefault(bat => bat.Value.Data.errorCode != 0).Value.Data.errorCode;
                    AlarmCodes alCode = errorCode.ToBatteryAlarmCode();
                    bool _isTimePassEnough = (DateTime.Now - lastOverTemperatureDetectedTime).TotalSeconds > 5;
                    if (_isTimePassEnough)
                    {
                        AlarmManager.AddAlarm(alCode, false);
                        lastOverTemperatureDetectedTime = DateTime.Now;
                        Vehicle.BatteryStatusOverview.SetAsDownStatus(alCode);
                    }
                }
                else
                {
                    lastOverTemperatureDetectedTime = DateTime.MinValue;
                    Vehicle.BatteryStatusOverview.ClearDownAlarmCode();
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, ex.Message);
            }
        }
    }
}
