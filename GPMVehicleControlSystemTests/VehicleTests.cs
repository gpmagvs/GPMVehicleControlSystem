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
            Vehicle vehicle = new  ForkAGV();
            bool result = vehicle.ModbusTcpConnect(6502).Result;
            while (result)
            {
                Thread.Sleep(1);
            }
        }
    }
}