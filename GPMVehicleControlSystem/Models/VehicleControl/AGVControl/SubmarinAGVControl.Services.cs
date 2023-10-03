using AGVSystemCommonNet6.GPMRosMessageNet.Services;
using AGVSystemCommonNet6.Log;

namespace GPMVehicleControlSystem.Models.VehicleControl.AGVControl
{
    public partial class SubmarinAGVControl
    {
        /// <summary>
        /// 當Reader拍照完成事件
        /// </summary>
        public event EventHandler<string> OnCSTReaderActionDone;
        private bool CSTActionDone = false;
        private string CSTActionResult = "";
        /// <summary>
        /// CST READER 完成動作的 callback
        /// </summary>
        /// <param name="request"></param>
        /// <param name="response"></param>
        /// <returns></returns>
        private bool CSTReaderDoneActionHandle(CSTReaderCommandRequest request, out CSTReaderCommandResponse response)
        {
            CSTActionResult = request.command;

            response = new CSTReaderCommandResponse
            {
                confirm = true
            };
            CSTActionDone = true;
            return true;
        }
        protected virtual string cst_reader_command { get; set; } = "read_try";
        /// <summary>
        /// 中止 Reader 拍照
        /// </summary>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        public override async Task<(bool request_success, bool action_done)> AbortCSTReader()
        {
            CSTReaderCommandResponse? response = await rosSocket.CallServiceAndWait<CSTReaderCommandRequest, CSTReaderCommandResponse>("/CSTReader_action",
              new CSTReaderCommandRequest() { command = "stop", model = "FORK" });
            if (response == null)
            {
                LOG.INFO("Stop CST Reader fail. CSTReader no reply");
                return (false, false);
            }
            else
            {
                LOG.INFO("Stop CST Reader Success.");
                return (true, true);
            }
        }
        /// <summary>
        /// 請求CST拍照
        /// </summary>
        /// <returns></returns>
        public override async Task<(bool request_success, bool action_done)> TriggerCSTReader()
        {
            LOG.TRACE($"Call Service /CSTReader_action, command = {cst_reader_command} , model = FORK");
            CSTReaderCommandResponse? response = null;
            int retry_cnt = 0;
            while (response == null)
            {
                response = await rosSocket.CallServiceAndWait<CSTReaderCommandRequest, CSTReaderCommandResponse>("/CSTReader_action",
                      new CSTReaderCommandRequest() { command = cst_reader_command, model = "FORK" }, timeout: 5000);
                retry_cnt++;
                if (retry_cnt > 5)
                {
                    break;
                }
            }
            if (response == null)
            {
                AbortCSTReader();
                LOG.INFO("Trigger CST Reader fail. CSTReader no reply");
                OnCSTReaderActionDone?.Invoke(this, "");
                return (false, false);
            }
            if (!response.confirm)
            {
                LOG.INFO("Trigger CST Reader fail. Confirm=False");
                OnCSTReaderActionDone?.Invoke(this, "");
                return (false, false);
            }
            else
            {
                LOG.INFO("Trigger CST Reader Success. Wait CST Reader Action Done.");
                CSTActionDone = false;
                CancellationTokenSource waitCstActionDoneCts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
                Task TK = new Task(async () =>
                {
                    while (!CSTActionDone)
                    {
                        if (waitCstActionDoneCts.IsCancellationRequested)
                            break;
                        Thread.Sleep(1);
                    }

                });
                TK.Start();
                try
                {
                    TK.Wait(waitCstActionDoneCts.Token);
                    LOG.INFO($"CST Reader  Action Done ..{CSTActionResult}--");
                    _ = Task.Factory.StartNew(async () =>
                    {
                        await Task.Delay(100);
                        OnCSTReaderActionDone?.Invoke(this, this.module_info.CSTReader.data);
                    });
                    return (true, true);
                }
                catch (OperationCanceledException)
                {
                    LOG.WARN("Trigger CST Reader Timeout");
                    AbortCSTReader();
                    OnCSTReaderActionDone?.Invoke(this, "");
                    return (true, false);
                }

            }
        }
    }
}
