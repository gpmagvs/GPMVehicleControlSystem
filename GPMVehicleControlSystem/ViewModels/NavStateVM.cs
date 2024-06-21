using AGVSystemCommonNet6.MAP;

namespace GPMVehicleControlSystem.ViewModels
{
    public class NavStateVM
    {
        public string Destination { get; set; } = "";
        public MapPoint DestinationMapPoint { get; set; } = new MapPoint() { Name = "Unknown" };

        public int[] PathPlan { get; set; } = new int[0];
        public bool IsSegmentTaskExecuting { get; set; } = false;
    }
}
