using Microsoft.VisualStudio.TestTools.UnitTesting;
using GPMVehicleControlSystem.Tools;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using IniParser;

namespace GPMVehicleControlSystem.Tools.Tests
{
    [TestClass()]
    public class IniHelperTests
    {
        [TestMethod()]
        public void SetValueTest()
        {
            var parser = new FileIniDataParser();
            var data = parser.ReadFile(@"C:\Users\USER\Documents\param\IO_Wago.ini");
            
            data["OUTPUT"]["RegistSize"] = "10";
            parser.WriteFile(@"C:\Users\USER\Documents\param\IO_Wago.ini", data);
            Assert.Fail();
        }
    }
}