using AGVSystemCommonNet6.Abstracts;
using AGVSystemCommonNet6.AGVDispatch.Messages;
using AGVSystemCommonNet6.GPMRosMessageNet.Messages;
using GPMVehicleControlSystem.Models.VehicleControl.AGVControl;
using GPMVehicleControlSystem.Models.VehicleControl.VehicleComponent;

namespace GPMVehicleControlSystem.Models.VehicleControl.Vehicles
{
    /// <summary>
    /// 潛盾
    /// </summary>
    public partial class SubmarinAGV : Vehicle
    {

        public SubmarinAGV()
        {

        }
        protected override List<CarComponent> CarComponents
        {
            get
            {
                var baseCompos = base.CarComponents;
                baseCompos.Add(CSTReader);
                return baseCompos;
            }
        }

        public override clsCSTReader CSTReader { get; set; } = new clsCSTReader();
        public override clsDirectionLighter DirectionLighter { get; set; } = new clsDirectionLighter();
        public override Dictionary<ushort, clsBattery> Batteries { get; set; } = new Dictionary<ushort, clsBattery>();

        internal override Task<(bool confirm, string message)> Initialize()
        {
            (AGVC as SubmarinAGVControl).OnCSTReaderActionDone += CSTReader.UpdateCSTIDDataHandler;
            return base.Initialize();
        }

        internal override RunningStatus GenRunningStateReportData(bool getLastPtPoseOfTrajectory = false)
        {
            var baseRunningStatus = base.GenRunningStateReportData(getLastPtPoseOfTrajectory);
            baseRunningStatus.CSTID = CSTReader.ValidCSTID == "" ? new string[0] : new string[] { CSTReader.ValidCSTID };
            return baseRunningStatus;

        }

        protected override void CarController_OnModuleInformationUpdated(object? sender, ModuleInformation _ModuleInformation)
        {
            CSTReader.StateData = _ModuleInformation.CSTReader;
            base.CarController_OnModuleInformationUpdated(sender, _ModuleInformation);
        }

        /// <summary>
        /// 移除卡夾 
        /// </summary>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        internal async Task<RETURN_CODE> RemoveCstData()
        {
            //向AGVS請求移除卡匣
            string currentCSTID = CSTReader.Data.data;
            string toRemoveCSTID = currentCSTID.ToLower() == "error" ? "" : currentCSTID;
            var retCode = await AGVS.TryRemoveCSTData(toRemoveCSTID, "");
            //清帳
            if (retCode == RETURN_CODE.OK)
                CSTReader.ValidCSTID = "";

            return retCode;
        }



    }
}
