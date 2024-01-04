using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AGV_Offline_Data_Analysis
{
    internal interface IDatabaseUse
    {
         AGVDatabase database { get; set; }
    }
}
