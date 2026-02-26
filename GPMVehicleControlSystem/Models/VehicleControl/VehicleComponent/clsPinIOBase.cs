using GPMVehicleControlSystem.VehicleControl.DIOModule;
using RosSharp.RosBridgeClient;
using System.Threading;
using System.Threading.Tasks;
using static GPMVehicleControlSystem.VehicleControl.DIOModule.clsDOModule;

namespace GPMVehicleControlSystem.Models.VehicleControl.VehicleComponent
{
    /// <summary>
    /// 控制牙叉是否可浮動的組件，是基於 IO 控制
    /// OUPUT ON => Free; OUTPUT OFF => Lock
    /// </summary>
    public class clsPinIOBase : clsPin
    {
        public override bool isRosBase => false;
        clsDOModule wagoOutput;
        DO_ITEM floatOutput = DO_ITEM.Fork_Floating;

        public override RosSocket rosSocket { get; set; } = null;

        public override PIN_STATUS pinStatus
        {
            get
            {
                var outputDict = wagoOutput.VCSOutputs.ToDictionary(v => v.Output, v => v.State);
                if (!outputDict.TryGetValue(floatOutput, out bool state))
                    return PIN_STATUS.UNKNOW;

                bool isLock = !state;
                return isLock ? PIN_STATUS.LOCK : PIN_STATUS.RELEASE;
            }
        }
        public clsPinIOBase(clsDOModule wagoOutput) : base()
        {
            this.wagoOutput = wagoOutput;
        }

        public override async Task<bool> Init(CancellationToken token = default)
        {
            if (!await BeforePinActionStartInvokeAsync(PIN_STATUS.INITIALIZING))
                return false;

            return true;
        }

        public override async Task Lock(CancellationToken token = default)
        {
            if (!await BeforePinActionStartInvokeAsync(PIN_STATUS.LOCK))
                return;

            var success = await wagoOutput.SetState(floatOutput, false);
            if (!success)
                throw new Exception("浮動牙叉 LOCK 失敗-請確認 output 輸出控制");
        }

        public override async Task Release(CancellationToken token = default)
        {
            if (!await BeforePinActionStartInvokeAsync(PIN_STATUS.RELEASE))
                return;

            var success = await wagoOutput.SetState(floatOutput, true); if (!success)
                throw new Exception("浮動牙叉 Release 失敗-請確認 output 輸出控制");
        }
    }
}
