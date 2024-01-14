namespace GPMVehicleControlSystem.Models.VehicleControl.Vehicles.Params
{
    public class clsModbusDIOParams
    {
        public enum IO_VALUE_TYPE
        {
            /// <summary>
            /// Inputs 讀 / CoilS 寫 
            /// </summary>
            INPUT,
            /// <summary>
            /// 歐迪爾,使用 InputRegist 讀/ SingleRegister 寫 
            /// </summary>
            INPUT_REGISTER

        }
        public IO_VALUE_TYPE IO_VAL_TYPE { get; set; } = IO_VALUE_TYPE.INPUT;
        public ushort Input_Read_Start { get; set; } = 0;
        public ushort Input_Read_Num { get; set; } = 8;
        public ushort Input_Write_Start { get; set; } = 1;

        public ushort InputRegister_Read_Start { get; set; } = 0;
        public ushort InputRegister_Read_Num { get; set; } = 1;
        public ushort InputRegister_Write_Start { get; set; } = 0;
    }
}
