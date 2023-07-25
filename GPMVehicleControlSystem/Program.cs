using AGVSystemCommonNet6.Alarm.VMS_ALARM;
using AGVSystemCommonNet6.Log;
using AGVSystemCommonNet6.Tools.Database;
using GPMVehicleControlSystem;
using GPMVehicleControlSystem.Models;
using GPMVehicleControlSystem.Models.Buzzer;
using GPMVehicleControlSystem.Models.Emulators;
using GPMVehicleControlSystem.Models.VCSSystem;
using GPMVehicleControlSystem.ViewModels;
using Microsoft.AspNetCore.Http.Json;
using System.Reflection;

_ = Task.Run(() =>
{
    LOG.SetLogFolderName("GPM_AGV_LOG");
    StaStored.APPVersion = Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>().InformationalVersion;
    bool alarmListLoaded = AlarmManager.LoadAlarmList(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "param/AlarmList.json"), out string message);
    if (!alarmListLoaded)
        StaSysMessageManager.AddNewMessage(message, 1);
    DBhelper.Initialize();
    int AgvTypeInt = AppSettingsHelper.GetValue<int>("VCS:AgvType");
    VehicheAndWagoIOConfiguraltion(AgvTypeInt);

});

void VehicheAndWagoIOConfiguraltion(int agvTypeInt)
{
    if (agvTypeInt == 0)
    {
        StaStored.CurrentVechicle = new GPMVehicleControlSystem.Models.VehicleControl.Vehicles.ForkAGV();
    }
    else if (agvTypeInt == 1)
    {
        StaStored.CurrentVechicle = new GPMVehicleControlSystem.Models.VehicleControl.Vehicles.SubmarinAGV();
    }
    else if (agvTypeInt == 2)
    {
        StaStored.CurrentVechicle = new GPMVehicleControlSystem.Models.VehicleControl.Vehicles.TsmcMiniAGV();
    }

    LOG.INFO($"AGV-{StaStored.CurrentVechicle.AgvType} Created¡I¡I");
}

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddControllers().AddJsonOptions(options => options.JsonSerializerOptions.PropertyNamingPolicy = null);
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
app.UseStaticFiles();
app.UseRouting();
app.UseVueRouterHistory();
app.UseAuthorization();
app.MapControllers();

app.Run();
