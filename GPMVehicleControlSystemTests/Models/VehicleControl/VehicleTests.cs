using Microsoft.VisualStudio.TestTools.UnitTesting;
using GPMVehicleControlSystem.Models.VehicleControl;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GPMVehicleControlSystem.Models.VehicleControl.Vehicles;
using AGVSystemCommonNet6.GPMRosMessageNet.Messages;

namespace GPMVehicleControlSystem.Models.VehicleControl.Tests
{
    [TestClass()]
    public class VehicleTests
    {
        [TestMethod()]
        public void VehicleTest()
        {
            Vehicle submarin_agv = new SubmarinAGV();
        }

        [TestMethod()]
        public void IOTableEqualTest()
        {
            IOlistMsg[] lastInputsIOTable = new IOlistMsg[2]
            {
                new IOlistMsg("X",1,0),
                new IOlistMsg("X",1,1),
            };



            IOlistMsg[] currentInputsIOTable = new IOlistMsg[2]
            {
                new IOlistMsg("X",1,0),
                new IOlistMsg("X",0,1),
            };

            Assert.IsFalse(currentInputsIOTable.Select(io => io.Coil).SequenceEqual(lastInputsIOTable.Select(io => io.Coil)));

            lastInputsIOTable = currentInputsIOTable;
            Assert.IsTrue(currentInputsIOTable.Select(io => io.Coil).SequenceEqual(lastInputsIOTable.Select(io => io.Coil)));

        }
    }
}