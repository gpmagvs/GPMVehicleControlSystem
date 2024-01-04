using Microsoft.VisualStudio.TestTools.UnitTesting;
using AGV_Offline_Data_Analysis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AGV_Offline_Data_Analysis.Tests
{
    [TestClass()]
    public class AGVDatabaseTests
    {

        private AGVDatabase CreateDatabaseInstance()
        {
            AGVDatabase db = new AGVDatabase();
            db.Open(@"C:\Users\jinwei\Documents\AOI_AGV2_1224-0101\VMS.db", out string errmsg);
            return db;
        }

        [TestMethod()]
        public void OpenTest()
        {
            AGVDatabase db = new AGVDatabase();
            db.Open(@"C:\Users\jinwei\Documents\AOI_AGV2_1224-0101\VMS.db", out string errmsg);
            Assert.Fail();
        }

        [TestMethod()]
        public void QueryStatusTest()
        {
            var instance = CreateDatabaseInstance();
            var status_data_list = instance.QueryStatus();
        }
    }
}