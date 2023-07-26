namespace GPMVehicleControlSystem.Models.ForkTeach
{
    public class clsForkTeach
    {
        public double Max_Speed { get; set; } = 1.0;
        public double Min_Speed { get; set; } = 0.1;

        public double Up_Pose_Limit { get; set; } = 30;
        public double Down_Pose_Limit { get; } = 0;

        public string Last_Editor { get; set; } = "dev";
        public string Version { get; set; } = "2023.07.26.08.44.1";

        public Dictionary<int, Dictionary<int, clsTeachData>> Teaches { get; set; } = new Dictionary<int, Dictionary<int, clsTeachData>>();
    }
}
