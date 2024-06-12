
using GPMVehicleControlSystem.Models.VehicleControl.Vehicles;
using GPMVehicleControlSystem.Models;
using static AGVSystemCommonNet6.clsEnums;
using AGVSystemCommonNet6.Vehicle_Control.VCS_ALARM;
using AGVSystemCommonNet6.Vehicle_Control.VCSDatabase;
using GPMVehicleControlSystem.Tools;
using AGVSystemCommonNet6.AGVDispatch;

namespace GPMVehicleControlSystem.Service
{
    public class VehicleFactoryService : IHostedService
    {

        ILogger<VehicleFactoryService> logger;
        ILogger<Vehicle> vehicleLogger;
        ILogger<clsAGVSConnection> agvsLogger;
        public VehicleFactoryService(ILogger<VehicleFactoryService> _logger, ILogger<Vehicle> _vehicleLogger, ILogger<clsAGVSConnection> _agvsLogger)
        {
            logger = _logger;
            vehicleLogger = _vehicleLogger;
            agvsLogger = _agvsLogger;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            try
            {
                var param = Vehicle.LoadParameters();

                logger.LogTrace("Parameters Load done");

                AGVSystemCommonNet6.Log.LOG.SetLogFolderName(param.LogFolder);
                bool alarmListLoaded = AlarmManager.LoadAlarmList(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "param/AlarmList.json"), out string message);
                DBhelper.Initialize();

                logger.LogTrace("Database Initialize done");
                var iniFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), $"param/IO_Wago.ini");
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

                    logger.LogTrace("New IO_Wago.ini file sync done.");
                }

                logger.LogInformation($"Vehicle Model = {param.AgvType}. Start Create Instance...");
                if (param.AgvType == AGV_TYPE.FORK)
                {
                    StaStored.CurrentVechicle = new ForkAGV(vehicleLogger, agvsLogger);
                }
                else if (param.AgvType == AGV_TYPE.SUBMERGED_SHIELD || param.AgvType == AGV_TYPE.SUBMERGED_SHIELD_Parts)
                {
                    StaStored.CurrentVechicle = new SubmarinAGV(vehicleLogger, agvsLogger);
                }
                else if (param.AgvType == AGV_TYPE.INSPECTION_AGV)
                {
                    if (param.Version == 1)
                        StaStored.CurrentVechicle = new TsmcMiniAGV(vehicleLogger, agvsLogger);
                    else
                        StaStored.CurrentVechicle = new DemoMiniAGV(vehicleLogger, agvsLogger);
                }

                logger.LogInformation($"Vehicle-{param.AgvType} Created！！");
                //LinuxTools.SysLoadingLogProcess();
            }
            catch (Exception ex)
            {
                logger.LogCritical(ex, $"建立車輛時發生錯誤");
                Environment.Exit(4);
            }
            finally
            {
                AlarmManager.AddAlarm(AlarmCodes.None, true);

            }
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }
    }
}
