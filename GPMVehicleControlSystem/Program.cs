using AGVSystemCommonNet6.Configuration.AutomationTransfers;
using GPMVehicleControlSystem.Models;
using GPMVehicleControlSystem.Models.Buzzer;
using GPMVehicleControlSystem.Service;
using GPMVehicleControlSystem.Tools;
using GPMVehicleControlSystem.Tools.DiskUsage;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.Extensions.FileProviders;
using Microsoft.Net.Http.Headers;
using NLog;
using NLog.Web;
using System.Reflection;
using System.Runtime.InteropServices;

Console.WriteLine(@" 佛祖保佑
_      `-._     `-.     `.   \      :      /   .'     .-'     _.-'      _
 `--._     `-._    `-.    `.  `.    :    .'  .'    .-'    _.-'     _.--'
      `--._    `-._   `-.   `.  \   :   /  .'   .-'   _.-'    _.--'
`--.__     `--._   `-._  `-.  `. `. : .' .'  .-'  _.-'   _.--'     __.--'
__    `--.__    `--._  `-._ `-. `. \:/ .' .-' _.-'  _.--'    __.--'    __
  `--..__   `--.__   `--._ `-._`-.`_=_'.-'_.-' _.--'   __.--'   __..--'
--..__   `--..__  `--.__  `--._`-q(-_-)p-'_.--'  __.--'  __..--'   __..--
      ``--..__  `--..__ `--.__ `-'_) (_`-' __.--' __..--'  __..--''
...___        ``--..__ `--..__`--/__/  \--'__..--' __..--''        ___...
      ```---...___    ``--..__`_(<_   _/)_'__..--''    ___...---'''
```-----....._____```---...___(__\_\_|_/__)___...---'''_____.....-----'''
 ___   __  ________   _______   _       _   _______    ___   __   _______
|| \\  ||     ||     ||_____))  \\     //  ||_____||  || \\  ||  ||_____||
||  \\_||  ___||___  ||     \\   \\___//   ||     ||  ||  \\_||  ||     ||
");

var logger = LogManager.Setup().LoadConfigurationFromAppSettings().GetCurrentClassLogger();
BuzzerPlayer.DeterminePlayerUse();
System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
StaSysControl.KillRunningVCSProcesses();
StaStored.APPVersion = Assembly.GetExecutingAssembly().GetName().Version.ToString();
Console.Title = $"車載系統-V{StaStored.APPVersion}";
logger.Info($"車載系統啟動-V{StaStored.APPVersion}");
LinuxTools.SaveCurrentProcessPID();

if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
{
    StaStored.VolumnAdjuster = new LinuxVolumeAdjuster();
}

try
{

    var builder = WebApplication.CreateBuilder(args);
    builder.Services.AddMemoryCache();
    builder.Logging.ClearProviders();
    builder.Logging.SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Information);
    builder.Host.UseNLog();

    builder.Services.AddControllers();
    // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(c =>
    {
        c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
        {
            Title = "GPM VCS",
            Version = "V1"
        });
        var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
        var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
        if (File.Exists(xmlPath))
            c.IncludeXmlComments(xmlPath);
    });
    builder.Services.AddControllers().AddJsonOptions(options => options.JsonSerializerOptions.PropertyNamingPolicy = null);
    builder.Services.AddDirectoryBrowser();
    builder.Services.AddSingleton<SystemUpdateService>();
    builder.Services.AddScoped<LinuxDiskUsageMonitor>();
    builder.Services.AddScoped<ParameterRestore>();
    builder.Services.AddSingleton<VehicleServiceAggregator>();
    builder.Services.AddHostedService<BackupStartupService>();
    builder.Services.AddHostedService<VehicleFactoryService>();
    builder.Services.AddHostedService<WebsocketBrocastBackgroundService>();
    builder.Services.AddHostedService<SystemLoadingMonitorBackgroundServeice>();
    builder.Services.AddHostedService<BatteryStateMonitorBackgroundService>();
    //builder.Services.AddHostedService<RotaionAndSlowDownBGSoundBackgroundService>();
    builder.Services.Configure<JsonOptions>(options =>
    {
        options.SerializerOptions.PropertyNamingPolicy = null;
        options.SerializerOptions.PropertyNameCaseInsensitive = false;
        options.SerializerOptions.WriteIndented = true;
    });
    builder.Services.AddResponseCompression(options =>
    {
        options.EnableForHttps = true;
        options.Providers.Add<GzipCompressionProvider>();
    });
    builder.Services.Configure<GzipCompressionProviderOptions>(options =>
    {
        options.Level = System.IO.Compression.CompressionLevel.Optimal;
    });
    builder.Services.AddSignalR().AddJsonProtocol(options => { options.PayloadSerializerOptions.PropertyNamingPolicy = null; });
    builder.Services.AddSignalR();



    var config = new ConfigurationBuilder().AddJsonFile("./version.json", optional: true).Build();
    builder.Services.Configure<VersionOptions>(config);

    _ = Task.Run(async () =>
    {
        while (true)
        {
            await Task.Delay(1);
            var _input = Console.ReadLine()?.ToLower();
            Console.WriteLine(_input);
            if (_input == "clear" || _input == "clc")
            {
                Console.Clear();
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"GPM AGV 車載系統-v.{StaStored.APPVersion}");
                Console.ForegroundColor = ConsoleColor.White;
            }
        }
    });


    var app = builder.Build();

    app.UseSwagger();
    app.UseSwaggerUI();
    app.UseWebSockets(new WebSocketOptions { KeepAliveInterval = TimeSpan.FromSeconds(1) });
    app.UseCors(c => c.AllowAnyMethod()
                    .AllowAnyHeader()
                    .SetIsOriginAllowed(origin => true)
                    .AllowCredentials()
               );
    app.UseDefaultFiles();
    app.UseStaticFiles(new StaticFileOptions
    {
        OnPrepareResponse = ctx =>
        {
            // 只對 js、css、images 等靜態資源使用長期緩存
            if (ctx.File.Name.Contains(".js") ||
                ctx.File.Name.Contains(".css") ||
                ctx.File.Name.Contains(".jpg") ||
                ctx.File.Name.Contains(".png"))
            {
                const int durationInSeconds = 24 * 60 * 60 * 365; // 1年
                ctx.Context.Response.Headers[HeaderNames.CacheControl] =
                    "public,max-age=" + durationInSeconds;
            }
            else
            {
                // HTML 檔案使用較短的緩存時間或不緩存
                ctx.Context.Response.Headers[HeaderNames.CacheControl] =
                    "no-cache, must-revalidate";
                ctx.Context.Response.Headers[HeaderNames.Pragma] =
                    "no-cache";
            }
        }
    });

    StaticFileProviderInit(app);

    app.UseRouting();
    app.UseVueRouterHistory();
    app.UseAuthorization();
    app.MapControllers();
    app.MapHub<FrontendHub>("/FrontendHub");
    app.Run();

}
catch (Exception ex)
{
    logger.Error(ex);
}
finally
{
    LogManager.Shutdown();
}


static void StaticFileProviderInit(WebApplication app)
{

    List<clsStaticFileProvider> providers = new List<clsStaticFileProvider>()
    {
        new clsStaticFileProvider()
        {
             folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "Downloads"),
             routePath = "/Download"
        },
        new clsStaticFileProvider()
        {
             folder =  Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "param","sounds"),
             routePath = "/audios"
        },
        new clsStaticFileProvider()
        {
             folder =  Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "param"),
             routePath = "/param"
        },
        new clsStaticFileProvider()
        {
             folder =  Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "GPM_AGV_LOG"),
             routePath = "/log"
        },
        new clsStaticFileProvider()
        {
             folder =  Path.Combine(Environment.CurrentDirectory, "backup"),
             routePath = "/Versions"
        },
    };


    foreach (clsStaticFileProvider provider in providers)
    {
        try
        {
            Directory.CreateDirectory(provider.folder);
            PhysicalFileProvider _FileProvider = new PhysicalFileProvider(Path.GetFullPath(provider.folder));
            // Enable displaying browser links.
            app.UseStaticFiles(new StaticFileOptions
            {
                FileProvider = _FileProvider,
                ServeUnknownFileTypes = true,  // 允許未知檔案類型
                DefaultContentType = "application/octet-stream",  // 預設下載的 content type
                RequestPath = provider.routePath
            });
            app.UseDirectoryBrowser(new DirectoryBrowserOptions
            {
                FileProvider = _FileProvider,
                RequestPath = provider.routePath
            });

        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
        }
    }
}


internal class clsStaticFileProvider
{
    internal string folder { get; set; } = "";
    internal string routePath { get; set; } = "/";

}