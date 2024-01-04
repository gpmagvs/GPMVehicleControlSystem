using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AGV_Offline_Data_Analysis.Model
{
    public enum ACTION_TYPE
    {
        None,
        Unload,
        LoadAndPark,
        Forward,
        Backward,
        FaB,
        Measure,
        Load,
        Charge,
        Carry,
        Discharge,
        Escape,
        Park,
        Unpark,
        ExchangeBattery,
        Hold,
        Break,
        Unknown,
        NoAction = 999
    }
    public enum SUB_STATUS
    {
        IDLE = 1,
        RUN = 2,
        DOWN = 3,
        Charging = 4,
        Initializing = 5,
        ALARM = 6,
        WARNING = 7,
        STOP,
        UNKNOWN
    }
}
