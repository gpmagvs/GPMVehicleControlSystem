using AGVSystemCommonNet6;
using AGVSystemCommonNet6.AGVDispatch.Messages;
using AGVSystemCommonNet6.GPMRosMessageNet.Services;
using AGVSystemCommonNet6.Vehicle_Control.VCS_ALARM;
using System.Diagnostics;

namespace GPMVehicleControlSystem.Models.VehicleControl.AGVControl
{
    public partial class SubmarinAGVControl
    {
        /// <summary>
        /// 當Reader拍照完成事件
        /// </summary>
        public event EventHandler<string> OnCSTReaderActionDone;
        private bool CSTActionDone = false;
        private ManualResetEvent WaitCSTReadActionDone = new ManualResetEvent(false);
        private ManualResetEvent WaitCSTStopActionDone = new ManualResetEvent(false);
        private string CSTActionResult = "";
        /// <summary>
        /// CST READER 完成動作的 callback
        /// </summary>
        /// <param name="request"></param>
        /// <param name="response"></param>
        /// <returns></returns>
        private bool CSTReaderDoneActionHandle(CSTReaderCommandRequest request, out CSTReaderCommandResponse response)
        {
            logger.Trace($"CST Reader Action done, Response = {request.ToJson()}");
            CSTActionResult = request.command;
            response = new CSTReaderCommandResponse
            {
                confirm = true
            };
            CSTActionDone = true;
            WaitCSTReadActionDone.Set();
            return true;
        }
        /// <summary>
        /// 中止 Reader 拍照
        /// </summary>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        public override async Task<(bool request_success, bool action_done)> AbortCSTReader()
        {
            WaitCSTStopActionDone.Reset();
            var request = new CSTReaderCommandRequest() { command = "stop", model = "FORK" };
            logger.Info($"AbortCSTReader start. Request Message={request.ToJson(Newtonsoft.Json.Formatting.None)}");
            rosSocket.CallService<CSTReaderCommandRequest, CSTReaderCommandResponse>("/CSTReader_action", StopCmdAckHandler, request);

            bool inTime = WaitCSTStopActionDone.WaitOne(TimeSpan.FromSeconds(3));
            if (!inTime)
                return (true, false);
            return (true, true);
        }

        private void StopCmdAckHandler(CSTReaderCommandResponse t)
        {
            logger.Info($"Stop CST Reader Cmd, CST READER ACK = {t.ToJson()}.");
            WaitCSTStopActionDone.Set();
        }

        CSTReaderCommandResponse? cst_reader_confirm_ack = null;
        ManualResetEvent wait_cst_ack_MRE = new ManualResetEvent(false);

        public override async Task<(string readCommand, bool request_success, bool action_done)> TriggerCSTReader(CST_TYPE cst_type)
        {
            var cst_reader_command = "read_try";
            bool _request_success = false;
            bool _action_done = false;
            try
            {
                await CSTReadServiceSemaphoreSlim.WaitAsync();
                await AbortCSTReader();
                wait_cst_ack_MRE = new ManualResetEvent(false);
                cst_reader_confirm_ack = null;
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

                bool isUnknownCstTypeOnAGV = cst_type == CST_TYPE.Unknown;

                //Local Method



                cst_reader_command = cst_type == CST_TYPE.Tray ? "read_try" : "read";//read_try=>上方reader(讀取tray stack 最上方2的barcode), read=>中下方reader
                logger.Warn($"CST TYPE={cst_type}({(int)cst_type})|Use {cst_reader_command} command to trigger reader");
                WaitCSTReadActionDone.Reset();
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
                    return (cst_reader_command, false, false);
                }
                if (!cst_reader_confirm_ack.confirm)
                {
                    logger.Info("Trigger CST Reader fail. Confirm=False");
                    OnCSTReaderActionDone?.Invoke(this, "");
                    return (cst_reader_command, false, false);
                }
                else
                {
                    _request_success = true;
                    logger.Info("Trigger CST Reader Success. Wait CST Reader Action Done.", false);
                    CSTActionDone = false;

                    try
                    {
                        bool isActionDoneInTime = WaitCSTReadActionDone.WaitOne(TimeSpan.FromSeconds(10));
                        _action_done = isActionDoneInTime;
                        bool ActionDoneReturn = CSTActionResult == "done";
                        if (!ActionDoneReturn || !isActionDoneInTime)
                        {
                            AbortCSTReader();
                            logger.Error($"Wait CST Action  Fail  Return Result Done:{ActionDoneReturn}, In-Time:{isActionDoneInTime}");
                            OnCSTReaderActionDone?.Invoke(this, "ERROR");
                            return (cst_reader_command, true, false);
                        }

                        await Task.Delay(800);
                        string cst_id = "";

                        CancellationTokenSource cancel = new CancellationTokenSource(TimeSpan.FromSeconds(3)); //這邊是要檢查 module_info.CSTReader.data 結果 

                        while (isReadFail(out cst_id))
                        {
                            await Task.Delay(1000);
                            logger.Warn($"Wait and Checkout ModuleInfo.CSTReader.data:{cst_id}");
                            if (cancel.IsCancellationRequested)
                            {
                                AbortCSTReader();
                                OnCSTReaderActionDone?.Invoke(this, "ERROR");
                                return (cst_reader_command, false, false);
                            }
                        }

                        await Task.Delay(800);
                        cst_id = module_info.CSTReader.data.Trim();
                        logger.Info($"CST Reader Action Done, Action Result : command = {CSTActionResult}--CST DATA = {cst_id}");
                        bool isReadFail(out string cst_id_ret)
                        {
                            cst_id_ret = CSTActionResult == "error" ? "ERROR" : this.module_info.CSTReader.data.Trim();
                            return cst_id_ret == "ERROR" || string.IsNullOrEmpty(cst_id_ret);
                        }

                        OnCSTReaderActionDone?.Invoke(this, cst_id);
                        return (cst_reader_command, true, true);
                    }
                    catch (OperationCanceledException)
                    {
                        _action_done = false;
                        logger.Warn("Trigger CST Reader Timeout");
                        AbortCSTReader();
                        OnCSTReaderActionDone?.Invoke(this, "");
                        return (cst_reader_command, true, false);
                    }

                }

            }
            catch (Exception ex)
            {
                logger.Error(ex);
                return (cst_reader_command, false, false);
            }
            finally
            {
            }


        }
        /// <summary>
        /// 請求CST拍照
        /// </summary>
        /// <returns></returns>
        public override async Task<(bool request_success, bool action_done)> TriggerCSTReader()
        {
            (string readCommand, bool request_success, bool action_done) = await TriggerCSTReader(CST_TYPE.Tray);
            return (request_success, action_done);
        }

        private void CstReaderConfirmedAckHandler(CSTReaderCommandResponse ack)
        {
            cst_reader_confirm_ack = ack;
            logger.Trace($"Service /CSTReader_action, ACK = {ack.ToJson()} ");
            wait_cst_ack_MRE.Set();
        }
    }
}
