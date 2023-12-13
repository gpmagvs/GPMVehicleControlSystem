using AGVSystemCommonNet6.Log;
using GPMVehicleControlSystem.Models;
using GPMVehicleControlSystem.ViewModels.BatteryQuery;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace GPMVehicleControlSystem.Controllers.AGVInternal
{
    [Route("api/[controller]")]
    [ApiController]
    public class BatteryController : ControllerBase
    {
        [HttpPost]
        public async Task<IActionResult> Query([FromBody] clsBatQueryOptions options)
        {
            clsBatteryInfoQuery queryer = new clsBatteryInfoQuery(options);
            var results = await queryer.Query();
            results = results.OrderBy(p => p.Key).ToDictionary(p => p.Key, p => p.Value);
            return Ok(results);
        }

        [HttpPost("QueryBatteryStatus")]
        public async Task<IActionResult> QueryBatteryStatus([FromBody] clsBatQueryOptions options)
        {
            clsAGVSLogAnaylsis logAnalysiser = new clsAGVSLogAnaylsis();
            logAnalysiser.logFolder = Path.Combine(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), StaStored.CurrentVechicle.Parameters.LogFolder), "AGVS_Message_Log");
            Console.WriteLine(logAnalysiser.logFolder);
            var outputs = logAnalysiser.GetDatas(options.timedt_range);
            var batteryStatus = outputs.Item2.Select(x => new { Time = x.Time_Stamp, BatteryLevel = x.Electric_Volume.First(), Status = x.AGV_Status });
            var outputFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "battery_status.csv");
            System.IO.File.WriteAllText(outputFile, "Time,Battery Level,AGV Status\r\n");
            using (StreamWriter sw = new StreamWriter(outputFile, true))
            {
                foreach (var item in batteryStatus)
                {
                    sw.WriteLine($"{item.Time},{item.BatteryLevel},{item.Status}");
                }
            }
            return Ok(new { file_path = outputFile, count = batteryStatus.Count() });
        }
        /// <summary>
        /// 控制充電迴路
        /// </summary>
        /// <param name="enabled"></param>
        /// <returns></returns>
        [HttpGet("RechargeSwitch")]
        public async Task<IActionResult> RechargeSwitch(bool enabled)
        {
            bool result = await StaStored.CurrentVechicle.WagoDO.SetState(VehicleControl.DIOModule.clsDOModule.DO_ITEM.Recharge_Circuit, enabled);
            return Ok(result);
        }

        [HttpGet("ChargeCicuitState")]
        public async Task<IActionResult> ChargeCicuitState()
        {
            bool result = StaStored.CurrentVechicle.IsChargeCircuitOpened;
            return Ok(result);
        }
    }
}
