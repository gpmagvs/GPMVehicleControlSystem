using AGVSystemCommonNet6;
using AGVSystemCommonNet6.AGVDispatch.Messages;
using AGVSystemCommonNet6.GPMRosMessageNet.Services;
using AGVSystemCommonNet6.Log;
using RosSharp.RosBridgeClient.MessageTypes.Tf2;

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
            LOG.TRACE($"CST Reader Action done,  {request.ToString()}");
            CSTActionResult = request.command;
            response = new CSTReaderCommandResponse
            {
                confirm = true
            };
            CSTActionDone = true;

            return true;
        }
        /// <summary>
        /// 中止 Reader 拍照
        /// </summary>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        public override async Task<(bool request_success, bool action_done)> AbortCSTReader()
        {
            rosSocket.CallService<CSTReaderCommandRequest, CSTReaderCommandResponse>("/CSTReader_action", StopCmdAckHandler,
              new CSTReaderCommandRequest() { command = "stop", model = "FORK" });
            return (true, true);
        }

        private void StopCmdAckHandler(CSTReaderCommandResponse t)
        {
            LOG.INFO($"Stop CST Reader Cmd, CST READER ACK = {t.ToJson()}.");
        }

        CSTReaderCommandResponse? cst_reader_confirm_ack = null;
        ManualResetEvent wait_cst_ack_MRE = new ManualResetEvent(false);

        public override async Task<(bool request_success, bool action_done)> TriggerCSTReader(CST_TYPE cst_type)
        {
            Thread.Sleep(1);
            wait_cst_ack_MRE = new ManualResetEvent(false);
            cst_reader_confirm_ack = null;
            int retry_cnt = 0;
            var cst_reader_command = cst_type == CST_TYPE.Tray ? "read_try" : "read";
            while (cst_reader_confirm_ack == null)
            {
                Thread.Sleep(1);
                await Task.Run(() =>
                {
                    LOG.TRACE($"Call Service /CSTReader_action, command = {cst_reader_command} , model = FORK", false);
                    var id = rosSocket.CallService<CSTReaderCommandRequest, CSTReaderCommandResponse>("/CSTReader_action", CstReaderConfirmedAckHandler, new CSTReaderCommandRequest()
                    {
                        command = cst_reader_command,
                        model = "FORK"
                    });
                    LOG.TRACE($"Call Service /CSTReader_action done. id={id} ", false);
                });

                LOG.TRACE($"Call Service /CSTReader_action, command ,WaitOne", false);
                wait_cst_ack_MRE.WaitOne(TimeSpan.FromSeconds(10));
                if (cst_reader_confirm_ack != null)
                {
                    break;
                }
                else
                {
                    await AbortCSTReader();
                    LOG.ERROR($"Call Service  /CSTReader_action, command  Timeout. CST READER NO ACK.  Retry... ");
                    wait_cst_ack_MRE.Reset();
                    retry_cnt++;
                    if (retry_cnt > 3)
                    {
                        break;
                    }
                }
            }

            if (cst_reader_confirm_ack == null)
            {
                LOG.INFO("Trigger CST Reader fail. CSTReader no reply");
                OnCSTReaderActionDone?.Invoke(this, "");
                return (false, false);
            }
            if (!cst_reader_confirm_ack.confirm)
            {
                LOG.INFO("Trigger CST Reader fail. Confirm=False");
                OnCSTReaderActionDone?.Invoke(this, "");
                return (false, false);
            }
            else
            {
                LOG.INFO("Trigger CST Reader Success. Wait CST Reader Action Done.", false);
                CSTActionDone = false;
                CancellationTokenSource waitCstActionDoneCts = new CancellationTokenSource(TimeSpan.FromSeconds(9));
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
                    LOG.INFO($"CST Reader Action Done, Action Result : command = {CSTActionResult}--");
                    if (CSTActionResult != "done")
                        AbortCSTReader();

                    Thread.Sleep(1000);
                    var cst_id = CSTActionResult == "error" ? "ERROR" : this.module_info.CSTReader.data.Trim();
                    LOG.TRACE($"Inovke CSTReaderAction Done event with CST ID = {cst_id}", false);
                    OnCSTReaderActionDone?.Invoke(this, cst_id);
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
        /// <summary>
        /// 請求CST拍照
        /// </summary>
        /// <returns></returns>
        public override async Task<(bool request_success, bool action_done)> TriggerCSTReader()
        {
            return await TriggerCSTReader(CST_TYPE.Tray);
        }

        private void CstReaderConfirmedAckHandler(CSTReaderCommandResponse ack)
        {
            cst_reader_confirm_ack = ack;
            LOG.TRACE($"Service /CSTReader_action, ACK = {ack.ToJson()} ");
            wait_cst_ack_MRE.Set();
        }
    }
}
