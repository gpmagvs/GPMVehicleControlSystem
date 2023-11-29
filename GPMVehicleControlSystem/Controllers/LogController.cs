using AGVSystemCommonNet6.Log;
using AGVSystemCommonNet6.MAP;
using AGVSystemCommonNet6.Vehicle_Control.Models;
using AGVSystemCommonNet6.Vehicle_Control.VCSDatabase;
using GPMVehicleControlSystem.Models;
using GPMVehicleControlSystem.Models.Log;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using System.IO.Compression;

namespace GPMVehicleControlSystem.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class LogController : ControllerBase
    {

        [HttpPost("Query")]
        public async Task<IActionResult> QueryLog([FromBody] clsLogQueryOptions option)
        {
            clsLogQueryResults result = await LogQueryService.QueryLog(option);
            return Ok(result);
        }

        [HttpGet("GetTransferLogToday")]
        public async Task<IActionResult> GetTransferLogToday()
        {
            Dictionary<int, AGVSystemCommonNet6.MAP.MapPoint>.ValueCollection mapPointsUsing = StaStored.CurrentVechicle.NavingMap.Points.Values;
            List<clsAGVSLogAnaylsis.clsTransferResult> results = DBhelper.QueryTodayTransferRecord();
            foreach (var item in results)
            {
                var FromStation = mapPointsUsing.FirstOrDefault(pt => pt.TagNumber == item.From);
                var ToStation = mapPointsUsing.FirstOrDefault(pt => pt.TagNumber == item.To);
                var StartLocStation = mapPointsUsing.FirstOrDefault(pt => pt.TagNumber == item.StartLoc);

                item.FromName = FromStation == null ? item.From + "" : FromStation.Graph.Display;
                item.ToName = FromStation == null ? item.To + "" : ToStation.Graph.Display;
                item.StartLocName = FromStation == null ? item.StartLoc + "" : StartLocStation.Graph.Display;
            }
            results = results.OrderByDescending(t => t.StartTime).ToList();
            return Ok(results);
        }

        [HttpGet("DownloadLog")]
        public async Task<IActionResult> DownloadLog()
        {
            string agvName = StaStored.CurrentVechicle.Parameters.VehicleName;
            var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), StaStored.CurrentVechicle.Parameters.LogFolder);
            string zipFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), $"Downloads/{agvName}_log_{DateTime.Now.ToString("yyMMddHHmmssfff")}.7z");
            if (System.IO.File.Exists(zipFilePath))
                System.IO.File.Delete(zipFilePath);
            Directory.CreateDirectory(Path.GetDirectoryName(zipFilePath));
            for (int i = 0; i < 5; i++)
            {
                try
                {
                    ZipFile.CreateFromDirectory(folder, zipFilePath);
                    Console.WriteLine("資料夾壓縮成功！");
                    break;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"壓縮資料夾時發生錯誤：{ex.Message}");
                }
            }

            // 讀取檔案內容
            byte[] fileBytes = System.IO.File.ReadAllBytes(zipFilePath);
            // 指定要下載的檔案名稱
            string fileName = Path.GetFileName(zipFilePath);
            // 返回檔案作為HTTP回應
            return File(fileBytes, "application/octet-stream", fileName);
        }

    }
}
