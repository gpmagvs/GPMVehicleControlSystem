
using AGVSystemCommonNet6.Vehicle_Control.VCS_ALARM;
using GPMVehicleControlSystem.Models.VehicleControl.Vehicles;

namespace GPMVehicleControlSystem.Models.VehicleControl.VehicleComponent.Forks
{
    /// <summary>
    /// 牙叉模組但伸縮方式是用馬達驅動的方式
    /// </summary>
    public class clsForkLifterWithDriverBaseExtener : clsForkLifter
    {
        public clsForkLifterWithDriverBaseExtener(ForkAGV forkAGV) : base(forkAGV)
        {
        }

        public override async Task<(bool done, AlarmCodes alarm_code)> HorizonForkInitialize(double InitForkSpeed = 0.5)
        {
            HorizonForkHomeSearchHelper horizonForkHomeSearchHelper = new HorizonForkHomeSearchHelper(forkAGV);
            return await horizonForkHomeSearchHelper.StartSearchAsync();
        }

        public override Task<bool> ForkARMStop()
        {
            return base.ForkARMStop();
        }

        public override Task<(bool confirm, AlarmCodes)> ForkExtendOutAsync(bool wait_reach_end = true)
        {
            return base.ForkExtendOutAsync(wait_reach_end);
        }

        public override Task<(bool confirm, string message)> ForkShortenInAsync(bool wait_reach_home = true)
        {
            return base.ForkShortenInAsync(wait_reach_home);
        }
    }
}
