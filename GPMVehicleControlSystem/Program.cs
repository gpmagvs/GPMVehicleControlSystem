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
System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
KillRunningVCSProcesses();
StaStored.APPVersion = Assembly.GetExecutingAssembly().GetName().Version.ToString();
LinuxTools.SaveCurrentProcessPID();
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

    var iniFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), $"param/IO_Wago.ini");
    if (!File.Exists(iniFilePath))
    {
        string src_ini_file_name = "IO_Wago_Inspection_AGV.ini";
        if (param.AgvType == AGV_TYPE.FORK)
            src_ini_file_name = "IO_Wago_Fork_AGV.ini";
        if (param.AgvType == AGV_TYPE.SUBMERGED_SHIELD)
            src_ini_file_name = "IO_Wago_Submarine_AGV.ini";
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
    else if (param.AgvType == AGV_TYPE.SUBMERGED_SHIELD)
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

_ = Task.Run(() =>
{
    while (true)
    {
        Thread.Sleep(1);
        var _input = Console.ReadLine()?.ToLower();
        Console.WriteLine(_input);
        if (_input == "clear" || _input == "clc")
        {
            Console.Clear();
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("=======================GPM AGV Console=======================");
            Console.ForegroundColor = ConsoleColor.White;
        }
    }
});

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
