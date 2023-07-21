using Microsoft.VisualStudio.TestTools.UnitTesting;
using GPMVehicleControlSystem.Models.VehicleControl.AGVControl;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static GPMVehicleControlSystem.Models.VehicleControl.AGVControl.CarController;
using AGVSystemCommonNet6.GPMRosMessageNet.Messages.SickMsg;
using GPMVehicleControlSystem.Models.VehicleControl.Vehicles;

namespace GPMVehicleControlSystem.Models.VehicleControl.AGVControl.Tests
{
    [TestClass()]
    public class CarControllerTests
    {
        [TestMethod()]
        public void LocalizeStateEmuTest()
        {
            LocalizationControllerResultMessage0502 LocalizationControllerResult = new LocalizationControllerResultMessage0502();
            LocalizationControllerResult.loc_status = 40;
            LOCALIZE_STATE state = (LOCALIZE_STATE)LocalizationControllerResult.loc_status;
            Assert.AreEqual(LOCALIZE_STATE.System_Error, state);
            LocalizationControllerResult.loc_status = 30;
            state = (LOCALIZE_STATE)LocalizationControllerResult.loc_status;
            Assert.AreEqual(LOCALIZE_STATE.Not_Localized, state);

        }

        [TestMethod()]
        public void CarControllerTest()
        {
            CarController agvc = new SubmarinAGVControl("10.22.141.217", 9090);
            agvc.Connect();
        }

        [TestMethod()]
        public void CarControllerTest1()
        {
            Assert.Fail();
        }

        [TestMethod()]
        public void ConnectTest()
        {
            Assert.Fail();
        }

        [TestMethod()]
        public void SubscribeROSTopicsTest()
        {
            Assert.Fail();
        }

        [TestMethod()]
        public void AdertiseROSServicesTest()
        {
            Assert.Fail();
        }

        [TestMethod()]
        public void DisconnectTest()
        {
            Assert.Fail();
        }

        [TestMethod()]
        public void IsConnectedTest()
        {
            Assert.Fail();
        }

        [TestMethod()]
        public void CarSpeedControlTest()
        {
            Assert.Fail();
        }

        [TestMethod()]
        public void TriggerCSTReaderTest()
        {
            Assert.Fail();
        }

        [TestMethod()]
        public void AbortCSTReaderTest()
        {
            Assert.Fail();
        }
    }
}