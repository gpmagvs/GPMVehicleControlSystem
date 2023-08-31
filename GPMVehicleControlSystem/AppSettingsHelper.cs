using AGVSystemCommonNet6.Alarm.VMS_ALARM;
using AGVSystemCommonNet6.Log;
using GitVersion.Extensions;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace GPMVehicleControlSystem
{
    public class AppSettingsHelper
    {
        public enum APPSETTINGS_READ_STATUS
        {
            SUCCESS,
            FILE_LOCK,
            CONTENT_ERROR
        }
        public static APPSETTINGS_READ_STATUS Status { get; private set; } = APPSETTINGS_READ_STATUS.SUCCESS;
        private static string baseDir => Debugger.IsAttached ? Environment.CurrentDirectory : AppDomain.CurrentDomain.BaseDirectory;
        private static string appsettings_filename
        {
            get
            {
                return Debugger.IsAttached ? "appsettings.Development_forkAGV.json" : "appsettings.json"; //測試FORK AGV
                //return Debugger.IsAttached ? "appsettings.Development.json" : "appsettings.json"; //測試潛盾AGV
                //return Debugger.IsAttached ? "appsettings.Development_VMware.json" : "appsettings.json";//VMware 模擬器AGV
                //return Debugger.IsAttached ? "appsettings.Development_InspectAGV.json" : "appsettings.json"; //測試巡檢AGV
                //return Debugger.IsAttached ? "appsettings.Development_YMYellowForkAGV.json" : "appsettings.json";//測試黃光FORK AGV
            }
        }


        private static IConfigurationBuilder configBuilder
        {
            get
            {
                var baseDir = Debugger.IsAttached ? Environment.CurrentDirectory : AppDomain.CurrentDomain.BaseDirectory;
                var configBuilder = new ConfigurationBuilder().SetBasePath(baseDir)
                .AddJsonFile(appsettings_filename); //測試FORK AGV

                return configBuilder;
            }
        }

        private static IConfigurationRoot configuration
        {
            get
            {
                try
                {

                    var configuration = configBuilder.Build();
                    return configuration;
                }
                catch (IOException ex)
                {
                    Status = APPSETTINGS_READ_STATUS.FILE_LOCK;
                    LOG.Critical($"IConfiguration build fail...-{Status}:{ex.Message}", ex);
                    return null;
                }
                catch (Exception ex)
                {
                    Status = APPSETTINGS_READ_STATUS.CONTENT_ERROR;
                    LOG.Critical($"IConfiguration build fail...-{Status}:{ex.Message}", ex);
                    return null;
                }
            }
        }


        public static string LogFolder
        {
            get
            {
                return GetValue<string>("VCS:LogFolder");
            }
        }

        static bool IsRetry = false;
        public static void WriteValue<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>(string key, T value)
        {
            configuration[key] = value.ToString();
        }

        public static T GetValue<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>(string key)
        {
            bool success = false;
            int try_cont = 0;
            while (!success)
            {
                Thread.Sleep(1);
                try
                {
                    var vale = configuration.GetValue(key, default(T));
                    success = true;
                    if (try_cont > 0)
                        LOG.ERROR($"取得參數檔 {key} 設定值 Retry Success");
                    return vale;
                }
                catch (Exception)
                {
                    IsRetry = true;
                    LOG.ERROR($"無法取得參數檔 {key} 設定值({Status}),Retry.");
                }
                try_cont += 1;
                if (try_cont > 10)
                {
                    break;
                }
            }
            LOG.Critical($"Appsettings.json value fetch fail....{Status}");
            Task.Factory.StartNew(async () =>
            {
                await Task.Delay(10);
                Environment.Exit(4);
            });
            return (T)new object();
        }
    }
}
