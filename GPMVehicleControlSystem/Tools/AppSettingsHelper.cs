using Newtonsoft.Json;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace GPMVehicleControlSystem.Tools
{
    public class AppSettingsHelper
    {

        private static IConfiguration configuration
        {
            get
            {
                try
                {
                    var configBuilder = new ConfigurationBuilder()
                        .SetBasePath(Environment.CurrentDirectory)
                    //.AddJsonFile(Debugger.IsAttached ? "appsettings.Development_forkAGV.json" : "appsettings.json"); //測試FORK AGV
                    .AddJsonFile(Debugger.IsAttached ? "appsettings.Development_InspectAGV.json" : "appsettings.json"); //測試巡檢AGV
                    //.AddJsonFile(Debugger.IsAttached ? "appsettings.Development.json" : "appsettings.json"); //測試潛盾AGV

                    var configuration = configBuilder.Build();
                    return configuration;
                }
                catch (Exception ex)
                {
                    throw ex;
                }
            }
        }
        public static T GetValue<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>(string key)
        {
            return configuration.GetValue(key, default(T));
        }
    }
}
