using AGVSystemCommonNet6;
using AGVSystemCommonNet6.AGVDispatch.Messages;
using AGVSystemCommonNet6.GPMRosMessageNet.Services;
using AGVSystemCommonNet6.Vehicle_Control.VCS_ALARM;

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
            logger.Trace($"CST Reader Action done,  {request.ToString()}");
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
            logger.Info($"Stop CST Reader Cmd, CST READER ACK = {t.ToJson()}.");
        }

        CSTReaderCommandResponse? cst_reader_confirm_ack = null;
        ManualResetEvent wait_cst_ack_MRE = new ManualResetEvent(false);

        public override async Task<(bool request_success, bool action_done)> TriggerCSTReader(CST_TYPE cst_type)
        {

            await Task.Delay(1);
            wait_cst_ack_MRE = new ManualResetEvent(false);
            cst_reader_confirm_ack = null;
            var cst_reader_command = "read_try";
            int retry_cnt = 0;
            if (cst_type == CST_TYPE.None || (int)cst_type == -1)
            {
                logger.Error($"從派車接收到的 CST TYPE={cst_type}({(int)cst_type})=> 沒有定義");
                if (OnCstTriggerButTypeUnknown == null)
                    AlarmManager.AddWarning(AlarmCodes.Read_Cst_ID_But_Cargo_Type_Known);
                else
                {
                    cst_type = OnCstTriggerButTypeUnknown();
                }
            }
            cst_reader_command = cst_type == CST_TYPE.Tray ? "read_try" : "read";//read_try=>上方reader(讀取tray stack 最上方2的barcode), read=>中下方reader
            logger.Warn($"CST TYPE={cst_type}({(int)cst_type})|Use {cst_reader_command} command to trigger reader");
            while (cst_reader_confirm_ack == null)
            {
                await Task.Delay(1);
                logger.Trace($"Call Service /CSTReader_action, command = {cst_reader_command} , model = FORK", false);
                string id = rosSocket.CallService<CSTReaderCommandRequest, CSTReaderCommandResponse>("/CSTReader_action", CstReaderConfirmedAckHandler, new CSTReaderCommandRequest()
                {
                    command = cst_reader_command,
                    model = "FORK"
                });
                wait_cst_ack_MRE.WaitOne(TimeSpan.FromSeconds(10));
                if (cst_reader_confirm_ack != null)
                {
                    break;
                }
                else
                {
                    await AbortCSTReader();
                    logger.Error($"Call Service  /CSTReader_action, command  Timeout. CST READER NO ACK.  Retry... ");
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
                logger.Info("Trigger CST Reader fail. CSTReader no reply");
                OnCSTReaderActionDone?.Invoke(this, "");
                return (false, false);
            }
            if (!cst_reader_confirm_ack.confirm)
            {
                logger.Info("Trigger CST Reader fail. Confirm=False");
                OnCSTReaderActionDone?.Invoke(this, "");
                return (false, false);
            }
            else
            {
                logger.Info("Trigger CST Reader Success. Wait CST Reader Action Done.", false);
                CSTActionDone = false;
                CancellationTokenSource waitCstActionDoneCts = new CancellationTokenSource(TimeSpan.FromSeconds(9));
                Task TK = new Task(async () =>
                {
                    while (!CSTActionDone)
                    {
                        if (waitCstActionDoneCts.IsCancellationRequested)
                            break;
                        await Task.Delay(1);
                    }

                });
                TK.Start();
                try
                {
                    TK.Wait(waitCstActionDoneCts.Token);
                    logger.Info($"CST Reader Action Done, Action Result : command = {CSTActionResult}--");
                    if (CSTActionResult != "done")
                        AbortCSTReader();

                    await Task.Delay(1000);
                    var cst_id = CSTActionResult == "error" ? "ERROR" : this.module_info.CSTReader.data.Trim();
                    logger.Trace($"Inovke CSTReaderAction Done event with CST ID = {cst_id}", false);
                    OnCSTReaderActionDone?.Invoke(this, cst_id);
                    return (true, true);
                }
                catch (OperationCanceledException)
                {
                    logger.Warn("Trigger CST Reader Timeout");
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
            logger.Trace($"Service /CSTReader_action, ACK = {ack.ToJson()} ");
            wait_cst_ack_MRE.Set();
        }
    }
}
