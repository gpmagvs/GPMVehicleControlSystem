using AGVSystemCommonNet6.Vehicle_Control.Models;
using AGVSystemCommonNet6.Vehicle_Control.VCSDatabase;

namespace GPMVehicleControlSystem.Models.WorkStation
{
    public class WorkStationStatistics
    {
        public static Dictionary<int, clsParkingAccuracyThreshold> AutoCreateParkingAccuracyThresholds(IEnumerable<int> station_tags)
        {
            return station_tags.ToDictionary(tag => tag, tag => CalculationThresHoldOfStation(tag));
        }

        private static clsParkingAccuracyThreshold CalculationThresHoldOfStation(int tag)
        {
            try
            {

                DateTime from = DateTime.Now.AddDays(-30);
                List<clsParkingAccuracy> parkingHistory = DBhelper.Query.QueryParkingAccuracy(tag, from.ToString(), DateTime.Now.ToString(), "");

                // 第一次計算平均值和標準差
                var (avgX, stdDevX) = CalculateAverageAndStdDev(parkingHistory.Select(r => Math.Abs(r.X)).ToList());
                var (avgY, stdDevY) = CalculateAverageAndStdDev(parkingHistory.Select(r => Math.Abs(r.Y)).ToList());

                // 識別並剔除離群點
                var filteredRecords = parkingHistory.Where(r =>
                    Math.Abs(Math.Abs(r.X) - avgX) <= 5 * stdDevX &&
                    Math.Abs(Math.Abs(r.Y) - avgY) <= 5 * stdDevY).ToList();

                // 重新計算平均值和標準差
                var (newAvgX, newStdDevX) = CalculateAverageAndStdDev(filteredRecords.Select(r =>Math.Abs( r.X)).ToList());
                var (newAvgY, newStdDevY) = CalculateAverageAndStdDev(filteredRecords.Select(r => Math.Abs(r.Y)).ToList());

                return new clsParkingAccuracyThreshold { XDirection = stdDevX * 5, YDirection = stdDevY * 5 };
            }
            catch (Exception ex)
            {
                return new clsParkingAccuracyThreshold();
            }
        }
        static (double Average, double StdDev) CalculateAverageAndStdDev(List<double> values)
        {
            double avg = values.Average();
            double stdDev = Math.Sqrt(values.Average(v => Math.Pow(v - avg, 2)));
            return (avg, stdDev);
        }
    }
}
