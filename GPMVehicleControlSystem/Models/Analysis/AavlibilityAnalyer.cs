
using AGVSystemCommonNet6.Vehicle_Control.VCSDatabase;
using static AGVSystemCommonNet6.clsEnums;

namespace GPMVehicleControlSystem.Models.Analysis
{
    public class AavlibilityAnalyer
    {
        public AavlibilityAnalyer() { }

        public clsAvalibilityData Query(DateTime from, DateTime to)
        {
            var statusHistory = DBhelper.Query.QueryStatusWithTimeRange(from, to);
            if (statusHistory.Count < 2)
            {
                return new clsAvalibilityData();
            }
            clsAvalibilityData result = new clsAvalibilityData();
            for (int i = 0; i < statusHistory.Count; i++)
            {
                if ((i + 1) == statusHistory.Count)
                    break;
                var init_status = statusHistory[i]; //
                var next_status = statusHistory[i + 1];
                var period = (next_status.Time - init_status.Time).TotalSeconds;
                var timeRange = new DateTime[2] { init_status.Time, next_status.Time };
                result.statusTimeList.Add(new clsAvalibilityData.clsStatus
                {
                    sec = (int)period,
                    status = init_status.Status
                });
                if (init_status.Status == AGVSystemCommonNet6.clsEnums.SUB_STATUS.IDLE || init_status.Status == AGVSystemCommonNet6.clsEnums.SUB_STATUS.Initializing)
                {
                    result.IDLE_Sec += period;
                    result.IDLE_Times.Add(timeRange);
                }
                else if (init_status.Status == AGVSystemCommonNet6.clsEnums.SUB_STATUS.RUN)
                {
                    result.RUN_Sec += period;
                    result.RUN_Times.Add(timeRange);
                }
                else if (init_status.Status == AGVSystemCommonNet6.clsEnums.SUB_STATUS.DOWN)
                {
                    result.DOWN_Sec += period;
                    result.DOWN_Times.Add(timeRange);
                }
                else if (init_status.Status == AGVSystemCommonNet6.clsEnums.SUB_STATUS.Charging)
                {
                    result.CHARGING_Sec += period;
                    result.CHARGING_Times.Add(timeRange);
                }
            }
            return result;
        }
        public class clsAvalibilityData
        {
            public class clsStatus
            {
                public int sec { get; set; }
                public SUB_STATUS status { get; set; }
            }
            public double IDLE_Sec { get; internal set; }
            public double RUN_Sec { get; internal set; }
            public double DOWN_Sec { get; internal set; }
            public double CHARGING_Sec { get; internal set; }
            public double TotalTime => IDLE_Sec + RUN_Sec + DOWN_Sec + CHARGING_Sec;

            public double IDLE_Percentage => TotalTime == 0 ? 0 : IDLE_Sec / TotalTime * 100;
            public double RUN_Percentage => TotalTime == 0 ? 0 : RUN_Sec / TotalTime * 100;
            public double DOWN_Percentage => TotalTime == 0 ? 0 : DOWN_Sec / TotalTime * 100;
            public double CHARGING_Percentage => TotalTime == 0 ? 0 : CHARGING_Sec / TotalTime * 100;


            public List<DateTime[]> IDLE_Times { get; internal set; } = new List<DateTime[]>();
            public List<DateTime[]> RUN_Times { get; internal set; } = new List<DateTime[]>();
            public List<DateTime[]> DOWN_Times { get; internal set; } = new List<DateTime[]>();
            public List<DateTime[]> CHARGING_Times { get; internal set; } = new List<DateTime[]>();

            public List<clsStatus> statusTimeList { get; set; } = new List<clsStatus>();
        }
    }
}
