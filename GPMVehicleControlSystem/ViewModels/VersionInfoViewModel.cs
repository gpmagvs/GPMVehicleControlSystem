namespace GPMVehicleControlSystem.ViewModels
{
    public class VersionInfoViewModel
    {
        public string Version { get; set; } = default;
        public string Description { get; set; } = default;
        public DateTime CreateTime { get; set; } = DateTime.MinValue;
    }
}
