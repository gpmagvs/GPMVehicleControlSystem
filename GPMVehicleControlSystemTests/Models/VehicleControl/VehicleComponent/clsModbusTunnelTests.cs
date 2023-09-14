using Microsoft.VisualStudio.TestTools.UnitTesting;
using GPMVehicleControlSystem.Models.VehicleControl.VehicleComponent;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;

namespace GPMVehicleControlSystem.Models.VehicleControl.VehicleComponent.Tests
{
    [TestClass()]
    public class clsModbusTunnelTests
    {
        [TestMethod()]
        public void InitializeTest()
        {
            clsModbusTunnel modbusServer = new clsModbusTunnel();
            Assert.IsTrue(modbusServer.Initialize());
        }

        [TestMethod()]
        public void UpdateAGVPosition30018Test()
        {
            clsModbusTunnel modbusServer = new clsModbusTunnel();
            modbusServer.Initialize();
            bool isReachLoc = true;
            //modbusServer.InitializeTestClient();
            modbusServer.UpdateAGVPosition30018(isReachLoc);

            while (true)
            {
                Thread.Sleep(1);
            }
        }
    }
}