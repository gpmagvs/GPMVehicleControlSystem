namespace GPMVehicleControlSystem.Models.VehicleControl.Vehicles.Params
{
    public class clsBatteryParam
    {
        public string BatteryLogFolder { get; set; } = "/home/gpm/GPMLog";
        public bool Recharge_Circuit_Auto_Control_In_ManualMode { get; set; } = true;
        public bool ChargeWhenLevelLowerThanThreshold { get; set; } = true;

        /// <summary>
        /// 若ChargeWhenLevelLowerThanThreshold為true,電量低於此閥值才需開啟充電迴路
        /// </summary>
        public double ChargeLevelThreshold { get; set; } = 30;
        /// <summary>
        /// 切斷充電迴路的電壓閥值,當電壓大於此數值，將會切斷充電迴路，避免電池過度充電。
        /// 單位: mV
        /// </summary>
        public int CutOffChargeRelayVoltageThreshodlval { get; set; } = 28800;
        /// <summary>
        /// 當充電任務完成後 等待此秒數後 變更車子狀態
        /// </summary>
        public int WaitChargeStartDelayTimeWhenReachChargeTaskFinish { get; set; } = 10;

    }
}
