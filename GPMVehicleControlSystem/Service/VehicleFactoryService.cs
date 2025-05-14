
using AGVSystemCommonNet6.AGVDispatch;
using AGVSystemCommonNet6.Vehicle_Control.VCS_ALARM;
using AGVSystemCommonNet6.Vehicle_Control.VCSDatabase;
using GPMVehicleControlSystem.Models;
using GPMVehicleControlSystem.Models.Exceptions;
using GPMVehicleControlSystem.Models.VehicleControl.Vehicles;
using GPMVehicleControlSystem.Tools.DiskUsage;
using GPMVehicleControlSystem.ViewModels;
using Microsoft.AspNetCore.SignalR;
using static AGVSystemCommonNet6.clsEnums;

namespace GPMVehicleControlSystem.Service
{
    public class VehicleFactoryService : IHostedService
    {
        VehicleServiceAggregator vehicleCreateFactoryServiceAggregator;
        public VehicleFactoryService(VehicleServiceAggregator vehicleCreateFactoryServiceAggregator)
        {
            this.vehicleCreateFactoryServiceAggregator = vehicleCreateFactoryServiceAggregator;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            try
            {
                Models.VehicleControl.Vehicles.Params.clsVehicelParam param = await Vehicle.LoadParameters();

                AGVSystemCommonNet6.Log.LOG.SetLogFolderName(param.LogFolder);
                bool alarmListLoaded = AlarmManager.LoadAlarmList(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "param", "AlarmList.json"), out string message);
                DBhelper.Initialize();
                AlarmManager.RecoveryAlarmDB();

                await _DeleteOldLogAndAlarm(param.Log.LogKeepDays);

                vehicleCreateFactoryServiceAggregator.logger.LogTrace("Database Initialize done");
                var iniFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "param", "IO_Wago.ini");
                if (!File.Exists(iniFilePath))
                {
                    string src_ini_file_name = "IO_Wago_Inspection_AGV.ini";
                    if (param.AgvType == AGV_TYPE.FORK)
                        src_ini_file_name = "IO_Wago_Fork_AGV.ini";
                    if (param.AgvType == AGV_TYPE.SUBMERGED_SHIELD)
                        src_ini_file_name = "IO_Wago_Submarine_AGV.ini";
                    if (param.AgvType == AGV_TYPE.SUBMERGED_SHIELD_Parts)
                        src_ini_file_name = "IO_Wago_Submarine_AGV_Parts.ini";
                    if (param.AgvType == AGV_TYPE.INSPECTION_AGV)
                    {
                        src_ini_file_name = param.Version == 1 ? "IO_Wago_Inspection_AGV.ini" : "IO_Wago_Inspection_AGV_V2.ini";
                    }
                    File.Copy(Path.Combine(Environment.CurrentDirectory, $"src/{src_ini_file_name}"), iniFilePath);

                    vehicleCreateFactoryServiceAggregator.logger.LogTrace("New IO_Wago.ini file sync done.");
                }

                vehicleCreateFactoryServiceAggregator.logger.LogInformation($"Vehicle Model = {param.AgvType}. Start Create Instance...");
                if (param.AgvType == AGV_TYPE.FORK)
                {
                    StaStored.CurrentVechicle = new ForkAGV(param, vehicleCreateFactoryServiceAggregator);
                }
                else if (param.AgvType == AGV_TYPE.SUBMERGED_SHIELD || param.AgvType == AGV_TYPE.SUBMERGED_SHIELD_Parts)
                {
                    StaStored.CurrentVechicle = new SubmarinAGV(param, vehicleCreateFactoryServiceAggregator);
                }
                else if (param.AgvType == AGV_TYPE.INSPECTION_AGV)
                {
                    if (param.Version == 1)
                        StaStored.CurrentVechicle = new TsmcMiniAGV(param, vehicleCreateFactoryServiceAggregator);
                    else
                        StaStored.CurrentVechicle = new DemoMiniAGV(param, vehicleCreateFactoryServiceAggregator);
                }

                StaStored.CurrentVechicle.memoryCache = vehicleCreateFactoryServiceAggregator.memoryCache;

                await StaStored.CurrentVechicle.CreateAsync();
                vehicleCreateFactoryServiceAggregator.logger.LogInformation($"Vehicle-{param.AgvType} Created！！");
                //LinuxTools.SysLoadingLogProcess();
            }
            catch (VehicleInstanceInitializeFailException ex)
            {
                vehicleCreateFactoryServiceAggregator.logger.LogCritical(ex, $"建立車輛時發生錯誤:{ex.Message}");
                ViewModelFactory.VehicleInstanceCreateFailException = ex;
                AlarmManager.AddAlarm(AlarmCodes.Code_Error_In_System, true);
            }
            catch (Exception ex)
            {
                vehicleCreateFactoryServiceAggregator.logger.LogCritical(ex, $"建立車輛時發生錯誤");
                Environment.Exit(4);
            }
            finally
            {
                AlarmManager.AddAlarm(AlarmCodes.None, true);

            }
        }

        private async Task _DeleteOldLogAndAlarm(int days)
        {
            DateTime timeLimit = DateTime.Now.AddDays(-1 * days);
            // delete log
            IDiskUsageMonitor diskUsageMonitor = new LinuxDiskUsageMonitor();
            await diskUsageMonitor.DeleteOldVCSLogData(timeLimit);
            // delete alarms of database
            AlarmManager.RemoveOldAlarmFromDB(timeLimit);
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }
    }
}
