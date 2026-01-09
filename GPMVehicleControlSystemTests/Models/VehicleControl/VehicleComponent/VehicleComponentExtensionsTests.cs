using Microsoft.VisualStudio.TestTools.UnitTesting;
using GPMVehicleControlSystem.Models.VehicleControl.VehicleComponent;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GPMVehicleControlSystem.Models.VehicleControl.VehicleComponent.Tests
{
    [TestClass()]
    public class VehicleComponentExtensionsTests
    {
        [TestMethod()]
        public void ToSideLaserDOSettingBitsTest()
        {
            int laserMode = 16;
            bool[] bits = laserMode.ToSideLaserDOSettingBits();
            Assert.IsTrue(bits.All(b => b == true));
            laserMode = 1;
            bits = laserMode.ToSideLaserDOSettingBits();
            Assert.IsTrue(bits.All(b => b == false));
        }
        [TestMethod()]
        public void PartesTEst()
        {
            string name = "RACK_1_3|6|9";
            var tail = name[(name.LastIndexOf('_') + 1)..];
            var slots = tail.Split('|');
            Assert.AreEqual(0, Array.IndexOf(slots, "3"));
            Assert.AreEqual(1, Array.IndexOf(slots, "6"));
            Assert.AreEqual(2, Array.IndexOf(slots, "9"));
        }
    }
}