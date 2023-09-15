using AGVSystemCommonNet6.Abstracts;
using AGVSystemCommonNet6.AGVDispatch.Messages;
using AGVSystemCommonNet6.AGVDispatch.Model;
using AGVSystemCommonNet6.GPMRosMessageNet.Messages;
using GPMVehicleControlSystem.Models.VehicleControl.AGVControl;
using GPMVehicleControlSystem.Models.VehicleControl.VehicleComponent;
using GPMVehicleControlSystem.Models.WorkStation;
using Newtonsoft.Json;

namespace GPMVehicleControlSystem.Models.VehicleControl.Vehicles
{
    /// <summary>
    /// 潛盾
    /// </summary>
    public partial class SubmarinAGV : Vehicle
    {

        public SubmarinAGV() : base()
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

        protected override RunningStatus HandleTcpIPProtocolGetRunningStatus()
        {
            var status = base.HandleTcpIPProtocolGetRunningStatus();
            status.CSTID = new string[] { CSTReader.ValidCSTID };
            return status;
        }
        public override clsRunningStatus HandleWebAPIProtocolGetRunningStatus()
        {
            var status = base.HandleWebAPIProtocolGetRunningStatus();
            status.CSTID = new string[] { CSTReader.ValidCSTID };
            return status;
        }


        protected override void ModuleInformationHandler(object? sender, ModuleInformation _ModuleInformation)
        {
            CSTReader.StateData = _ModuleInformation.CSTReader;
            base.ModuleInformationHandler(sender, _ModuleInformation);
        }

        /// <summary>
        /// 移除卡夾 
        /// </summary>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        internal async Task<RETURN_CODE> RemoveCstData()
        {
            //向AGVS請求移除卡匣
            CSTReader.ValidCSTID = "";
            return RETURN_CODE.OK;
        }

        protected override async Task<(bool confirm, string message)> InitializeActions(CancellationTokenSource cancellation)
        {
            return (true, "");
        }

        protected override void CreateAGVCInstance(string RosBridge_IP, int RosBridge_Port)
        {
            AGVC = new SubmarinAGVControl(RosBridge_IP, RosBridge_Port);
            (AGVC as SubmarinAGVControl).OnCSTReaderActionDone += CSTReader.UpdateCSTIDDataHandler;

        }

    }
}
