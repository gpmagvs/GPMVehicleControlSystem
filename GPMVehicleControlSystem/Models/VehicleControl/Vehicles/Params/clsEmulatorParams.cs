namespace GPMVehicleControlSystem.Models.VehicleControl.Vehicles.Params
{
    public class clsEmulatorParams
    {
        public enum MOVE_TIME_EMULATION
        {
            DISTANCE,
            FIXED_TIME
        }
        public Dictionary<string, string> Descrption { get; set; } = new Dictionary<string, string>() {
            { "Move_Time_Mode(Tag間移動時間模擬)","0:由距離決定, 1:固定時間" },
            { "Move_Fixed_Time(Move_Time_Mode設定為1時,Tag間移動時間)","單位:秒" },
        };

        public MOVE_TIME_EMULATION Move_Time_Mode { get; set; } = MOVE_TIME_EMULATION.FIXED_TIME;
        public double Move_Fixed_Time { get; set; } = 0.5;
    }
}
