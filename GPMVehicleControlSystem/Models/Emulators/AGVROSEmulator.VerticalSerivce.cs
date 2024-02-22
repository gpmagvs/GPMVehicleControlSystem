using AGVSystemCommonNet6;
using AGVSystemCommonNet6.GPMRosMessageNet.Services;
using Microsoft.EntityFrameworkCore.Storage;

namespace GPMVehicleControlSystem.Models.Emulators
{
    public partial class AGVROSEmulator
    {
        private double _currentVerticalPosition = 0;
        private double currentVerticalPosition
        {
            get => _currentVerticalPosition;
            set
            {
                _currentVerticalPosition = value;
                StaEmuManager.wagoEmu.SetState(GPMVehicleControlSystem.VehicleControl.DIOModule.clsDIModule.DI_ITEM.Vertical_Home_Pos, value == 0);
            }
        }
        private bool StopFlag = true;
        private CancellationTokenSource _cancel = new CancellationTokenSource();
        private bool VerticalActionCallback(VerticalCommandRequest request, out VerticalCommandResponse response)
        {
            EmuLog($"recieve command_action : {request.ToJson()}");
            double _speed = request.speed;
            if (request.command == "orig")
            {
                _cancel?.Cancel();
                Thread.Sleep(100);
                _cancel = new CancellationTokenSource();
                Task.Run(() =>
                {
                    VerticalPositionMoveSimulation(0, _speed);
                });
            }

            if (request.command == "up" || request.command == "up_search")
            {
                _cancel?.Cancel();
                Thread.Sleep(100);
                _cancel = new CancellationTokenSource();
                Task.Run(() => VerticalUpSimulation(_speed));
            }
            if (request.command == "down" || request.command == "down_search")
            {
                _cancel?.Cancel();
                Thread.Sleep(100);
                _cancel = new CancellationTokenSource();
                Task.Run(() => VerticalDownSimulation(_speed));
            }
            if (request.command == "pose")
            {
                _cancel?.Cancel();
                Thread.Sleep(100);
                _cancel = new CancellationTokenSource();
                Task.Run(() => VerticalPositionMoveSimulation(request.target, _speed));
            }
            if (request.command == "stop")
            {
                StopFlag = true;
            }
            if (request.command == "resume")
            {
                StopFlag = false;
            }
            response = new VerticalCommandResponse()
            {
                confirm = true,
            };
            return true;
        }
        private async void ActionDone()
        {
            _ = Task.Run(async () =>
            {
                await Task.Delay(400);
                await rosSocket.CallServiceAndWait<VerticalCommandRequest, VerticalCommandResponse>("/done_action", new VerticalCommandRequest
                {
                    command = "done"
                });
            });
        }
        private async void VerticalUpSimulation(double speed_ = 1.0)
        {
            StaEmuManager.wagoEmu.SetState(GPMVehicleControlSystem.VehicleControl.DIOModule.clsDIModule.DI_ITEM.Vertical_Home_Pos, false);

            StopFlag = false;
            while (!_cancel.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(1.0 / speed_));
                if (StopFlag)
                    continue;
                currentVerticalPosition += 0.1;
            }
        }
        private async void VerticalDownSimulation(double speed_ = 1.0)
        {
            StaEmuManager.wagoEmu.SetState(GPMVehicleControlSystem.VehicleControl.DIOModule.clsDIModule.DI_ITEM.Vertical_Home_Pos, false);
            StopFlag = false;
            while (!_cancel.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(1.0 / speed_));
                if (StopFlag)
                    continue;
                currentVerticalPosition -= 0.1;

            }
        }
        private async void VerticalPositionMoveSimulation(double target, double speed = 1)
        {
            StaEmuManager.wagoEmu.SetState(GPMVehicleControlSystem.VehicleControl.DIOModule.clsDIModule.DI_ITEM.Vertical_Home_Pos, false);
            StopFlag = false;
            bool isGoUp = currentVerticalPosition < target;
            bool isReachGoal(double target)
            {
                if (isGoUp)
                {
                    return currentVerticalPosition > target;
                }
                else
                    return currentVerticalPosition < target;
            }
            while (!isReachGoal(target)&& !_cancel.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(1.0 / speed));
                if (StopFlag)
                    continue;

                if (isGoUp)
                    currentVerticalPosition += 0.1;
                else
                    currentVerticalPosition -= 0.1;

            }
            if (!StopFlag)
                currentVerticalPosition = target;
            ActionDone();
        }
    }
}
