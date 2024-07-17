using AGVSystemCommonNet6.Vehicle_Control.VCS_ALARM;
using System.Collections.Concurrent;

namespace GPMVehicleControlSystem.Models.VehicleControl.Vehicles
{
    public class clsBatteryRunStatus
    {
        public bool StatusDown => downReasons.Count > 0;
        public string DownStatusDescription => string.Join("、", downReasons.Values);

        internal ConcurrentDictionary<DateTime, AlarmCodes> downReasons = new ConcurrentDictionary<DateTime, AlarmCodes>();

        public void SetAsDownStatus(AlarmCodes alCode)
        {
            if (downReasons.Values.Any(ac => ac == alCode))
                return;
            downReasons.TryAdd(DateTime.Now, alCode);
        }

        public void ClearDownAlarmCode(AlarmCodes alCode)
        {
            if (!downReasons.Values.Any(ac => ac == alCode))
                return;

            var existPair = downReasons.FirstOrDefault(kp => kp.Value == alCode);
            if (existPair.Value != null)
            {
                DateTime removeKey = existPair.Key;
                downReasons.TryRemove(removeKey, out _);
            }
        }

        internal void ClearDownAlarmCode()
        {
            downReasons.Clear();
        }
    }
}
