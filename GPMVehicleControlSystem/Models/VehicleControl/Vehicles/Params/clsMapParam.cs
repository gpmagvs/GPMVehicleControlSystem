namespace GPMVehicleControlSystem.Models.VehicleControl.Vehicles.Params
{
    public class clsMapParam
    {
        public string LocalMapFileName { get; set; } = "local-Map_UMTC_3F_Yellow.json";
        internal string LocalMapFileFullName
        {
            get
            {
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), $"param/{LocalMapFileName}");
            }
        }
    }
}
