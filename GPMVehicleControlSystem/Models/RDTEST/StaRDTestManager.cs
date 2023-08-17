using GPMVehicleControlSystem.ViewModels;
using GPMVehicleControlSystem.ViewModels.RDTEST;

namespace GPMVehicleControlSystem.Models.RDTEST
{
    public class StaRDTestManager
    {
        public static clsMoveTester MoveTester { get; set; } = new clsMoveTester();
        public static void StartMoveTest(clsMoveTestModel options)
        {
            MoveTester.options = options;
            MoveTester.Start();
        }

        internal static void StopMoveTest()
        {
            MoveTester.Stop();
        }
    }
}
