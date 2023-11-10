using Microsoft.VisualStudio.TestTools.UnitTesting;
using AGVSystemCommonNet6.Log;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestPlatform.Utilities;
using System.IO;
using RosSharp.RosBridgeClient.MessageTypes.Nav;
using System.Collections.Concurrent;

namespace AGVSystemCommonNet6.Log.Tests
{
    [TestClass()]
    public class clsAGVSLogAnaylsisTests
    {
        [TestMethod()]
        public void GetTaskFeedataTest()
        {
            clsAGVSLogAnaylsis logAnalysiser = new clsAGVSLogAnaylsis();
            logAnalysiser.logFolder = @"D:\AGVS_Message_Log1030_1106";
            var timerange = new DateTime[2] { new DateTime(2023, 11, 01), new DateTime(2023, 11, 06, 11, 0, 0) };
            var results = logAnalysiser.AnalysisTransferTasks(timerange);
            Assert.IsTrue(results.Count != 0);
        }
    }
}