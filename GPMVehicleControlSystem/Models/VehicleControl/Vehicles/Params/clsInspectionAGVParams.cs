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
    }
}
