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
    }
}