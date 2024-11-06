
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
            logger.LogInformation($"Start");
            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(1000);

                if (Vehicle == null)
                    continue;
                await MonitorBatteryErrorStatus();
            }
        }
        private async Task MonitorBatteryErrorStatus()
        {
            try
            {
                List<AlarmCodes> batAlarmCodes = Vehicle.Batteries.Select(bat => bat.Value.Data.errorCode.ToBatteryAlarmCode())
                                                                  .Where(code => code != AlarmCodes.None)
                                                                  .ToList();
                bool isBatError = batAlarmCodes.Any();
                if (isBatError)
                {
                    byte errorCode = Vehicle.Batteries.FirstOrDefault(bat => bat.Value.Data.errorCode != 0).Value.Data.errorCode;
                    //AlarmCodes alCode = errorCode.ToBatteryAlarmCode();

                    bool _isTimePassEnough = (DateTime.Now - lastOverTemperatureDetectedTime).TotalSeconds > 5;
                    if (_isTimePassEnough) //若電池欠壓，仍要可以初始化上線，趕緊可派去充電
                    {
                        foreach (AlarmCodes alCode in batAlarmCodes)
                        {
                            if (alCode == AlarmCodes.Under_Voltage)
                                AlarmManager.AddWarning(alCode);
                            else
                            {
                                AlarmManager.AddAlarm(alCode, false);
                                lastOverTemperatureDetectedTime = DateTime.Now;
                                Vehicle.BatteryStatusOverview.SetAsDownStatus(alCode);
                                if (alCode == AlarmCodes.Over_Voltage && Vehicle.IsChargeCircuitOpened)
                                {
                                    logger.LogWarning($"{alCode} 異常觸發,且充電迴路開啟中=>需斷開充電迴路");
                                    List<double> currentVoltages = Vehicle.Batteries.Select(bat => (double)bat.Value.Data.voltage).ToList();
                                    string voltagesStr = string.Join(" mV,", currentVoltages);
                                    logger.LogWarning($"Battery over-voltage when charging. Cut-off charge circuit!({voltagesStr})");
                                    await Vehicle.WagoDO.SetState(DO_ITEM.Recharge_Circuit, false);
                                    Vehicle.SetIsCharging(false);
                                    AlarmManager.AddAlarm(AlarmCodes.Battery_Over_Voltage_When_Charging, true);
                                    Vehicle.SetSub_Status(Vehicle.IsInitialized ? Vehicle.AGVC.ActionStatus == ActionStatus.ACTIVE ? SUB_STATUS.RUN : SUB_STATUS.IDLE : SUB_STATUS.DOWN);
                                }


                            }

                        }

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
