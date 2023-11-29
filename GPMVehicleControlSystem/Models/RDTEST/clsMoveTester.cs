using AGVSystemCommonNet6.Log;
using AGVSystemCommonNet6.Vehicle_Control.VCS_ALARM;
using GPMVehicleControlSystem.Models.Buzzer;
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
            if (testing_data.state == TEST_STATE.RUNNING)
                testCancelCts.Cancel();
            Task.Run(async () =>
            {
                Console.WriteLine($"旋轉測試即將開始...");
                await Task.Delay(1000);
                testCancelCts = new CancellationTokenSource();
                MoveTestWorker();
            });
        }

        private async void MoveTestWorker()
        {
            Stopwatch sw = Stopwatch.StartNew();
            LOG.INFO($"旋轉測試開始!");
            int count = 0;
            testing_data.duration = 0;
            while (sw.ElapsedMilliseconds < options.duration * 1000)
            {
                testing_data.duration = (int)(sw.ElapsedMilliseconds / 1000.0);
                count += 1;
                Thread.Sleep(TimeSpan.FromSeconds(options.delay_time));
                if (testCancelCts.IsCancellationRequested)
                    break;
                testing_data.state = TEST_STATE.RUNNING;


                //radian 1度=  Math.PI/ 180  
                double radian_delta = options.theta_move * Math.PI / 180.0;
                double time_ = radian_delta / options.rotation_speed; //秒
                LOG.INFO($"預估旋轉時間:{time_}秒");
                Stopwatch timer = Stopwatch.StartNew();


                double currentTheta = AGV.BarcodeReader.Data.theta;
                double expect_Theta = 0.0;
                bool isTurnRight = false;
                if (count % 2 == 0)
                {
                    isTurnRight = true;
                    expect_Theta = currentTheta - options.theta_move;
                    AGV.ManualController.TurnRight(options.rotation_speed, false);
                }
                else
                {
                    isTurnRight = false;
                    expect_Theta = currentTheta + options.theta_move;
                    AGV.ManualController.TurnLeft(options.rotation_speed, false);
                }
                expect_Theta = expect_Theta > 180 ? expect_Theta - 360 : expect_Theta;
                LOG.INFO($"預期角度 {expect_Theta}");
                while (isTurnRight ? (AGV.BarcodeReader.Data.theta > expect_Theta) : (AGV.BarcodeReader.Data.theta < expect_Theta))
                {
                    testing_data.duration = (int)(sw.ElapsedMilliseconds / 1000.0);
                    Thread.Sleep(TimeSpan.FromMilliseconds(0.05));
                    if (AGV.BarcodeReader.Data.tagID == 0)
                    {
                        LOG.INFO($"測試過程中脫離Tag", false);
                        BuzzerPlayer.Alarm();
                        AlarmManager.AddAlarm(AlarmCodes.Motion_control_Missing_Tag_On_End_Point);
                        TestEnd();
                        return;
                    }
                    if (Math.Abs(AGV.BarcodeReader.Data.theta - expect_Theta) < 20)
                    {
                        var speed_changed = options.rotation_speed / 4;
                        if (isTurnRight)
                            AGV.ManualController.TurnRight(speed_changed,false);
                        else
                            AGV.ManualController.TurnLeft(speed_changed, false);
                    }
                    if (testCancelCts.IsCancellationRequested)
                    {
                        TestEnd();
                        return;
                    }
                }
                AGV.ManualController.Stop();
            }
            TestEnd();

        }

        private void TestEnd()
        {
            LOG.INFO($"測試結束");
            AGV._Sub_Status = AGVSystemCommonNet6.clsEnums.SUB_STATUS.IDLE;
            AGV.ManualController.Stop();
            testing_data.state = TEST_STATE.IDLE;
        }
    }
}
