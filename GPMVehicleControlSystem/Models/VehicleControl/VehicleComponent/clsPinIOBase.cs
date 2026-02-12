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
        public override bool IsReleased => wagoOuput?.GetState(floatOutput) ?? false;
        clsDOModule wagoOuput;
        DO_ITEM floatOutput = DO_ITEM.Fork_Floating;

        public override RosSocket rosSocket { get; set; } = null;
        public clsPinIOBase(clsDOModule wagoOuput) : base()
        {
            this.wagoOuput = wagoOuput;
            SetPinState(IsReleased ? PIN_STATES.RELEASED : PIN_STATES.LOCKED);
        }

        public override Task Init(CancellationToken token = default)
        {
            SetPinState(IsReleased ? PIN_STATES.RELEASED : PIN_STATES.LOCKED);
            return Task.CompletedTask;
        }

        public override async Task Lock(CancellationToken token = default)
        {
            SetPinState(PIN_STATES.UNKNOWN);
            var success = await wagoOuput.SetState(floatOutput, false);
            if (!success)
                throw new Exception("浮動牙叉 LOCK 失敗-請確認 output 輸出控制");
            SetPinState(PIN_STATES.LOCKED);
        }

        public override async Task Release(CancellationToken token = default)
        {
            SetPinState(PIN_STATES.UNKNOWN);
            var success = await wagoOuput.SetState(floatOutput, true); if (!success)
                throw new Exception("浮動牙叉 Release 失敗-請確認 output 輸出控制");
            SetPinState(PIN_STATES.RELEASED);
        }
    }
}
