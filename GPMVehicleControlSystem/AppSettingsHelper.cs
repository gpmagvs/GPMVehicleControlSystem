using Newtonsoft.Json;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace GPMVehicleControlSystem
{
    public class AppSettingsHelper
    {

        private static IConfiguration configuration
        {
            get
            {
                try
                {
                    var baseDir = Debugger.IsAttached ? Environment.CurrentDirectory : AppDomain.CurrentDomain.BaseDirectory;
                    var configBuilder = new ConfigurationBuilder().SetBasePath(baseDir)
                    //.AddJsonFile(Debugger.IsAttached ? "appsettings.Development.json" : "appsettings.json"); //測試潛盾AGV
                    //.AddJsonFile(Debugger.IsAttached ? "appsettings.Development_forkAGV.json" : "appsettings.json"); //測試FORK AGV
                    //.AddJsonFile(Debugger.IsAttached ? "appsettings.Development_InspectAGV.json" : "appsettings.json"); //測試巡檢AGV
                    //.AddJsonFile("appsettings.Development_YMYellowForkAGV.json"); //測試黃光FORK AGV
                    .AddJsonFile(Debugger.IsAttached ? "appsettings.Development_VMware.json" : "appsettings.json"); //VMware 模擬器AGV

                    var configuration = configBuilder.Build();
                    return configuration;
                }
                catch (Exception ex)
                {
                    throw ex;
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

        public static T GetValue<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>(string key)
        {
            return configuration.GetValue(key, default(T));
        }
    }
}
