using AGVSystemCommonNet6.Alarm.VMS_ALARM;
using AGVSystemCommonNet6.Log;
using AGVSystemCommonNet6.Tools.Database;
using GPMVehicleControlSystem;
using GPMVehicleControlSystem.Models;
using GPMVehicleControlSystem.Models.Buzzer;
using GPMVehicleControlSystem.Models.Emulators;

using GPMVehicleControlSystem.Models.VehicleControl.Vehicles;
using GPMVehicleControlSystem.Tools;
using GPMVehicleControlSystem.ViewModels;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.FileProviders;
using System.Diagnostics;
using System.Reflection;
using static AGVSystemCommonNet6.clsEnums;

KillRunningVCSProcesses();
StaStored.APPVersion = Assembly.GetExecutingAssembly().GetName().Version.ToString();
LinuxTools.SaveCurrentProcessPID();
var param = Vehicle.LoadParameters();
_ = Task.Run(() =>
{
    LOG.SetLogFolderName(param.LogFolder);
    bool alarmListLoaded = AlarmManager.LoadAlarmList(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "param/AlarmList.json"), out string message);
    DBhelper.Initialize();
    Console.WriteLine(LinuxTools.GetMemUsedMB());
    VehicheAndWagoIOConfiguraltion();
});

void VehicheAndWagoIOConfiguraltion()
{

    var iniFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), $"param/IO_Wago.ini");
    if (!File.Exists(iniFilePath))
    {
        string src_ini_file_name = "IO_Wago_Inspection_AGV.ini";
        if (param.AgvType == AGV_TYPE.FORK)
            src_ini_file_name = "IO_Wago_Fork_AGV.ini";
        if (param.AgvType == AGV_TYPE.SUBMERGED_SHIELD)
            src_ini_file_name = "IO_Wago_Submarine_AGV.ini";
        if (param.AgvType == AGV_TYPE.INSPECTION_AGV)
            src_ini_file_name = "IO_Wago_Inspection_AGV.ini";
        File.Copy(Path.Combine(Environment.CurrentDirectory, $"src/{src_ini_file_name}"), iniFilePath);
    }

    if (param.AgvType == AGV_TYPE.FORK)
    {
        StaStored.CurrentVechicle = new ForkAGV();
    }
    else if (param.AgvType == AGV_TYPE.SUBMERGED_SHIELD)
    {
        StaStored.CurrentVechicle = new SubmarinAGV();
    }
    else if (param.AgvType == AGV_TYPE.INSPECTION_AGV)
    {
        StaStored.CurrentVechicle = new TsmcMiniAGV();
    }
    StaStored.CurrentVechicle.DownloadMapFromServer();
    LOG.INFO($"AGV-{StaStored.CurrentVechicle.Parameters.AgvType} Created¡I¡I");
    LinuxTools.SysLoadingLogProcess();
}

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddControllers().AddJsonOptions(options => options.JsonSerializerOptions.PropertyNamingPolicy = null);
builder.Services.AddDirectoryBrowser();
builder.Services.Configure<JsonOptions>(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = null;
    options.SerializerOptions.PropertyNameCaseInsensitive = false;
    options.SerializerOptions.WriteIndented = true;
});
AlarmManager.AddAlarm(AlarmCodes.None, true);
var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();
app.UseWebSockets();
app.UseCors(c => c.AllowAnyHeader().AllowAnyOrigin().AllowAnyMethod());
app.UseDefaultFiles();
app.UseStaticFiles();

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

app.Run();

static void KillRunningVCSProcesses()
{
    var currentProcess = Process.GetCurrentProcess();
    var porcess = Process.GetProcessesByName("GPM_VCS");
    if (porcess.Length != 0)
    {
        foreach (var p in porcess)
        {
            if (p.Id != currentProcess.Id)
                p.Kill();
        }
    }
}