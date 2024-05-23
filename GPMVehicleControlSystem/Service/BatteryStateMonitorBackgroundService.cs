
using AGVSystemCommonNet6.Log;
using AGVSystemCommonNet6.Vehicle_Control.VCS_ALARM;
using GPMVehicleControlSystem.Models;
using GPMVehicleControlSystem.Models.VehicleControl.Vehicles;
using GPMVehicleControlSystem.VehicleControl.DIOModule;
using RosSharp.RosBridgeClient.Actionlib;
using static AGVSystemCommonNet6.clsEnums;
using static GPMVehicleControlSystem.VehicleControl.DIOModule.clsDOModule;

namespace GPMVehicleControlSystem.Service
{
    public class BatteryStateMonitorBackgroundService : BackgroundService
    {
        private Vehicle Vehicle => StaStored.CurrentVechicle;
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {

            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(1000);

                if (Vehicle == null)
                    continue;

                await MonitorBatteryOverVoltage();

            }
        }

        private async Task MonitorBatteryOverVoltage()
        {
            double threshold = Vehicle.Parameters.BatteryModule.CutOffChargeRelayVoltageThreshodlval;//mV
            List<ushort> currentVoltages = Vehicle.Batteries.Select(bat => bat.Value.Data.Voltage).ToList();

            bool isOverVotage = currentVoltages.Any(voltag => voltag >= threshold);

            if (isOverVotage)
            {
                if (Vehicle.IsChargeCircuitOpened)
                {
                    LOG.ERROR($"Battery over-voltage when charging. Cut-off charge circuit!");
                    await Vehicle.WagoDO.SetState(DO_ITEM.Recharge_Circuit, false);
                    Vehicle.SetIsCharging(false);
                    AlarmManager.AddAlarm(AlarmCodes.Battery_Over_Voltage_When_Charging, true);
                    Vehicle.SetSub_Status(Vehicle.IsInitialized ? Vehicle.AGVC.ActionStatus == ActionStatus.ACTIVE ? SUB_STATUS.RUN : SUB_STATUS.IDLE : SUB_STATUS.DOWN);
                }
            }
        }
    }
}
