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
    LOG.SetLogFolderName(AppSettingsHelper.LogFolder);  //這個程式碼片段是軟體應用程式的初始化或設置程序的一部分，涉及到車輛控制系統和相關配置。它初始化日誌記錄、擷取版本資訊、從 JSON 檔案加載警報清單、初始化數據庫連接、擷取配置值，並根據該值進行配置。
    StaStored.APPVersion = Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>().InformationalVersion;
    bool alarmListLoaded = AlarmManager.LoadAlarmList(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "param/AlarmList.json"), out string message);
    if (!alarmListLoaded)
        StaSysMessageManager.AddNewMessage(message, 1);
    DBhelper.Initialize();
    int AgvTypeInt = AppSettingsHelper.GetValue<int>("VCS:AgvType");
    VehicheAndWagoIOConfiguraltion(AgvTypeInt);

});

void VehicheAndWagoIOConfiguraltion(int agvTypeInt)  //車子控制
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

    LOG.INFO($"AGV-{StaStored.CurrentVechicle.AgvType} Created！！");
}

var builder = WebApplication.CreateBuilder(args);  //Web API build in here
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
