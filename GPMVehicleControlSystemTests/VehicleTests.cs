using Microsoft.VisualStudio.TestTools.UnitTesting;
using GPMVehicleControlSystem.Models.VehicleControl.Vehicles;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;

namespace GPMVehicleControlSystem.Models.VehicleControl.Vehicles.Tests
{
    [TestClass()]
    public class VehicleTests
    {
        [TestMethod()]
        public void ModbusTcpConnectTest()
        {

        }
        [TestMethod()]
        public void AlarmCodesDistinctByTest()
        {
            AGVSystemCommonNet6.AGVDispatch.Messages.clsAlarmCode[] alarms = new AGVSystemCommonNet6.AGVDispatch.Messages.clsAlarmCode[]
            {
                 new AGVSystemCommonNet6.AGVDispatch.Messages.clsAlarmCode
                 {
                      Alarm_ID = 1,
                 },new AGVSystemCommonNet6.AGVDispatch.Messages.clsAlarmCode
                 {
                      Alarm_ID = 4,
                 },new AGVSystemCommonNet6.AGVDispatch.Messages.clsAlarmCode
                 {
                      Alarm_ID = 1,
                 },new AGVSystemCommonNet6.AGVDispatch.Messages.clsAlarmCode
                 {
                      Alarm_ID = 4,
                 },new AGVSystemCommonNet6.AGVDispatch.Messages.clsAlarmCode
                 {
                      Alarm_ID = 3,
                 }
            };
            var _alarms = alarms.DistinctBy(alarm => alarm.Alarm_ID);
            Assert.AreEqual(3, _alarms.ToArray().Length);
        }

        [TestMethod()]
        public void GetWorkStationEQInformationTest()
        {
            Vehicle agv = new ForkAGV();
            agv.AGVS = new AGVSystemCommonNet6.AGVDispatch.clsAGVSConnection("127.0.0.1", 5036)
            {
                UseWebAPI = true,
            };
            var opt = agv.GetWorkStationEQInformation().Result;
            Assert.Fail();
        }
    }
}