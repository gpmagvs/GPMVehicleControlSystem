using Microsoft.VisualStudio.TestTools.UnitTesting;
using AGVSystemCommonNet6.Log;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestPlatform.Utilities;
using System.IO;

namespace AGVSystemCommonNet6.Log.Tests
{
    [TestClass()]
    public class clsAGVSLogAnaylsisTests
    {
        [TestMethod()]
        public void GetRunningStatusDtoTest()
        {
            clsAGVSLogAnaylsis logAnalysiser = new clsAGVSLogAnaylsis();
            var outputs = logAnalysiser.GetRunningStatusDto(@"C:\Users\MyUser\Documents\2023-11-06 10.log");
            var batteryStatus = outputs.Select(x => new { Time = x.Time_Stamp, BatteryLevel = x.Electric_Volume.First(), Status = x.AGV_Status });

            Assert.Fail();
        }

        [TestMethod()]
        public void GetRunningStatusDtoTest1()
        {
            clsAGVSLogAnaylsis logAnalysiser = new clsAGVSLogAnaylsis();
            logAnalysiser.logFolder = @"C:\Users\MyUser\Documents\AGVS_Message_Log1030_1106";
            var outputs = logAnalysiser.GetRunningStatusDto(new DateTime[2] { new DateTime(2023, 11, 01), new DateTime(2023, 11, 03, 11, 0, 0) });
            var batteryStatus = outputs.Select(x => new { Time = x.Time_Stamp_dt, BatteryLevel = x.Electric_Volume.First(), Status = x.AGV_Status });
            var outputFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), $"battery_status_.csv");
            System.IO.File.WriteAllText(outputFile, "Time,Battery Level,AGV Status\r\n");
            using (StreamWriter sw = new StreamWriter(outputFile, true))
            {
                foreach (var item in batteryStatus)
                {
                    sw.WriteLine($"{item.Time},{item.BatteryLevel},{item.Status}");
                }
            }
            Assert.Fail();
        }
    }
}