namespace GPMVehicleControlSystem.Models.VehicleControl.Vehicles.Params
{
    public class clsInspectionAGVParams
    {
        public bool CheckBatteryLockStateWhenInit { get; set; } = false;
        /// <summary>
        /// 低於此電量不換電池
        /// </summary>
        public byte ExchangeBatLevelThresholdVal { get; set; } = 100;

        public bool MeasureSimulation { get; set; } = true;

        public bool BatteryExhcnageSimulation { get; set; } = true;

        public int BatteryChangeNum { get; set; } = 1;

        public clsBatExchangeTimeout BatExchangeTimeout { get; set; } = new clsBatExchangeTimeout();
    }

    public class clsBatExchangeTimeout
    {
        public int TP1 { get; set; } = 60;
        public int TP2 { get; set; } = 10;
        public int TP3 { get; set; } = 30;
        public int TP4 { get; set; } = 30;
        public int TP5 { get; set; } = 2;
    }
}
