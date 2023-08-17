using GPMVehicleControlSystem.ViewModels.RDTEST;
using System.Diagnostics;

namespace GPMVehicleControlSystem.Models.RDTEST
{
    public class clsMoveTester : RDTesterAbstract
    {
        public clsMoveTestModel options;

        public clsMoveTester()
        {

        }
        public override void Start()
        {
            if (test_state == TEST_STATE.RUNNING)
                testCancelCts.Cancel();
            Task.Run(async () =>
            {
                AGV._Sub_Status = AGVSystemCommonNet6.clsEnums.SUB_STATUS.RUN;
                Console.WriteLine($"旋轉測試即將開始...");
                await Task.Delay(1000);
                testCancelCts = new CancellationTokenSource();
                MoveTestWorker();
            });
        }

        private async void MoveTestWorker()
        {
            Stopwatch sw = Stopwatch.StartNew();

            Console.WriteLine($"旋轉測試開始!");
            while (sw.ElapsedMilliseconds < options.duration * 1000)
            {
                Thread.Sleep(TimeSpan.FromSeconds(options.delay_time));
                if (testCancelCts.IsCancellationRequested)
                    break;
                test_state = TEST_STATE.RUNNING;


                //radian 1度=  Math.PI/ 180  
                double radian_delta = options.theta_move * Math.PI / 180.0;
                double time_ = radian_delta / options.rotation_speed; //秒
                Console.WriteLine($"預估旋轉時間:{time_}秒");
                Stopwatch timer = Stopwatch.StartNew();
                double dec_a = 0.05;//減速度
                var stop_spend_time = options.rotation_speed / dec_a;//減速停止時間

                AGV.ManualController.TurnRight(options.rotation_speed);
                while (timer.ElapsedMilliseconds < (time_ * (0.8)) * 1000)
                {
                    if (testCancelCts.IsCancellationRequested)
                        break;
                    Thread.Sleep(1);

                }
                double speed = options.rotation_speed;
                while (speed >= 0)
                {
                    AGV.ManualController.TurnRight(speed);
                    speed -= dec_a;
                    await Task.Delay(200);
                }
                AGV.ManualController.Stop();
            }
            Console.WriteLine($"測試結束");

            AGV._Sub_Status = AGVSystemCommonNet6.clsEnums.SUB_STATUS.IDLE;
            AGV.ManualController.Stop();
            test_state = TEST_STATE.IDLE;
        }
    }
}
