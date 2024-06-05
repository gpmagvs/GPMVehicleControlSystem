using AGVSystemCommonNet6.Log;
using AGVSystemCommonNet6.Vehicle_Control.VCSDatabase;
using AGVSystemCommonNet6.Vehicle_Control.VCS_ALARM;
using GPMVehicleControlSystem.Models;
using GPMVehicleControlSystem.Models.VehicleControl.Vehicles;
using GPMVehicleControlSystem.Tools;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.FileProviders;
using System.Diagnostics;
using System.Reflection;
using static AGVSystemCommonNet6.clsEnums;
using System.Runtime.InteropServices;
using GPMVehicleControlSystem.Service;
using Microsoft.Net.Http.Headers;
using Microsoft.AspNetCore.ResponseCompression;
System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
StaSysControl.KillRunningVCSProcesses();
StaStored.APPVersion = Assembly.GetExecutingAssembly().GetName().Version.ToString();
OTAHelper.TryStartOTAServiceAPP();
Console.Title = $"車載系統-V{StaStored.APPVersion}";
LinuxTools.SaveCurrentProcessPID();
if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
{
    StaStored.VolumnAdjuster = new LinuxVolumeAdjuster();
}
var param = Vehicle.LoadParameters();
_ = Task.Run(() =>
{
    LOG.SetLogFolderName(param.LogFolder);
    bool alarmListLoaded = AlarmManager.LoadAlarmList(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "param/AlarmList.json"), out string message);
    DBhelper.Initialize();
    Console.WriteLine($"Memory when system start = {LinuxTools.GetMemUsedMB()} Mb");
    VehicheAndWagoIOConfiguraltion();
});

void VehicheAndWagoIOConfiguraltion()
{
    try
    {

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
        }

        if (param.AgvType == AGV_TYPE.FORK)
        {
            StaStored.CurrentVechicle = new ForkAGV();
        }
        else if (param.AgvType == AGV_TYPE.SUBMERGED_SHIELD || param.AgvType == AGV_TYPE.SUBMERGED_SHIELD_Parts)
        {
            StaStored.CurrentVechicle = new SubmarinAGV();
        }
        else if (param.AgvType == AGV_TYPE.INSPECTION_AGV)
        {
            if (param.Version == 1)
                StaStored.CurrentVechicle = new TsmcMiniAGV();
            else
                StaStored.CurrentVechicle = new DemoMiniAGV();
        }

        LOG.INFO($"AGV-{StaStored.CurrentVechicle?.Parameters.AgvType} Created！！");
        //LinuxTools.SysLoadingLogProcess();
    }
    catch (Exception ex)
    {
        Console.WriteLine(ex.Message + ex.StackTrace);
        Environment.Exit(4);
    }
}

var builder = WebApplication.CreateBuilder(args);
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
builder.Services.AddHostedService<WebsocketBrocastBackgroundService>();
builder.Services.AddHostedService<SystemLoadingMonitorBackgroundServeice>();
builder.Services.AddHostedService<BatteryStateMonitorBackgroundService>();
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

AlarmManager.AddAlarm(AlarmCodes.None, true);

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
        const int duractionInSeconds = 24 * 60 * 60 * 120;
        ctx.Context.Response.Headers[HeaderNames.CacheControl] =
               "public,max-age=" + duractionInSeconds;
    }
});

var imageFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "Downloads");
Directory.CreateDirectory(imageFolder);
var fileProvider = new PhysicalFileProvider(imageFolder);
var requestPath = "/Download";

// Enable displaying browser links.
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = fileProvider,
    RequestPath = requestPath
});
app.UseDirectoryBrowser(new DirectoryBrowserOptions
{
    FileProvider = fileProvider,
    RequestPath = requestPath
});

app.UseRouting();
app.UseVueRouterHistory();
app.UseAuthorization();
app.MapControllers();
app.MapHub<FrontendHub>("/FrontendHub");
app.Run();

