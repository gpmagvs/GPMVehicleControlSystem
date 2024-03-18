namespace GPMVehicleControlSystem.Models.VehicleControl.Vehicles.Params
{
    public class clsMapParam
    {
        public string LocalMapFileName { get; set; } = "temp/3FDemoRoom.json";
        internal string LocalMapFileFullName
        {
            get
            {
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), $"param/{LocalMapFileName}");
            }
        }
    }
}
