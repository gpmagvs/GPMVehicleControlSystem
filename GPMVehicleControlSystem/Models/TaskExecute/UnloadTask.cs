using AGVSystemCommonNet6.AGVDispatch.Messages;
using AGVSystemCommonNet6.Vehicle_Control.VCS_ALARM;
using GPMVehicleControlSystem.Models.Buzzer;
using GPMVehicleControlSystem.Models.VehicleControl.Vehicles;
using GPMVehicleControlSystem.Models.VehicleControl.Vehicles.CargoStates;
using GPMVehicleControlSystem.Models.VehicleControl.Vehicles.Params;
using GPMVehicleControlSystem.Models.WorkStation;
using GPMVehicleControlSystem.Tools;
using Microsoft.AspNetCore.SignalR;
using RosSharp.RosBridgeClient.MessageTypes.Std;
using System.Diagnostics;
using System.Threading.Tasks;
using static GPMVehicleControlSystem.Models.VehicleControl.Vehicles.Params.clsManualCheckCargoStatusParams;
using static GPMVehicleControlSystem.VehicleControl.DIOModule.clsDIModule;

namespace GPMVehicleControlSystem.Models.TaskExecute
{
    public class UnloadTask : LoadTask
    {
        public override ACTION_TYPE action { get; set; } = ACTION_TYPE.Unload;

        public UnloadTask(Vehicle Agv, clsTaskDownloadData taskDownloadData) : base(Agv, taskDownloadData)
        {
        }

        internal override async Task<(bool confirm, AlarmCodes alarmCode)> CSTBarcodeReadAfterAction(CancellationToken cancellationToken)
        {
            string cstIDFromAGVS = RunningTaskData.CST == null || !RunningTaskData.CST.Any() ? "" : RunningTaskData.CST.First().CST_ID;
            bool isCstIDFromAGVSUnknown = string.IsNullOrEmpty(cstIDFromAGVS) || cstIDFromAGVS.ToLower().Contains("un");
            if (!CSTTrigger || isCstIDFromAGVSUnknown)
            {
                Agv.CSTReader.ValidCSTID = cstIDFromAGVS;
                return (true, AlarmCodes.None);
            }
            return CSTBarcodeRead(cancellationToken).Result;
        }

        /// <summary>
        /// 準備Unload(取貨)=>車上應該無貨
        /// </summary>
        /// <returns></returns>
        protected override (bool confirm, AlarmCodes alarmCode) CstExistCheckBeforeHSStartInFrontOfEQ()
        {
            if (!Agv.Parameters.CST_EXIST_DETECTION.Before_In)
                return (true, AlarmCodes.None);

            if (Agv.CargoStateStorer.HasAnyCargoOnAGV(Agv.Parameters.LDULD_Task_No_Entry))
                return (false, AlarmCodes.Has_Cst_Without_Job);

            return (true, AlarmCodes.None);
        }

        /// <summary>
        ///  Unload(取貨)完成後=>車上應該有貨
        /// </summary>
        /// <returns></returns>
        protected override async Task<(bool confirm, AlarmCodes alarmCode)> CstExistCheckAfterEQActionFinishInEQAsync()
        {
            clsCST cstInfoFromAGVS = this.RunningTaskData.CST.Any() ? this.RunningTaskData.CST.First() : new clsCST() { CST_ID = "", CST_Type = CST_TYPE.Unknown };
            bool isUnknowCstIDFromAGVS = string.IsNullOrEmpty(cstInfoFromAGVS.CST_ID) || cstInfoFromAGVS.CST_ID.ToLower().Contains("un");
            Agv.HandshakeStatusText = "檢查在席狀態.(車上應有物料)";
            if (!Agv.Parameters.CST_EXIST_DETECTION.After_EQ_Busy_Off)
            {
                Agv.CSTReader.ValidCSTID = "TrayUnknow";
                return (true, AlarmCodes.None);
            }

            Agv.CSTReader.ValidCSTID = "TrayUnknow";

            if (!IsCargoPlacedNormal())
            {
                Agv.CSTReader.ValidCSTID = "";
                //
                if (Agv.Parameters.LDULDParams.MaunalCheckAndResumableWhenUnloadButCargoBias)
                {
                    bool manualCheckDone = await WaitOperatorCheckCargoStatusProcess(destineTag);
                    if (!manualCheckDone)
                        return (false, AlarmCodes.Has_Job_Without_Cst);
                }
                else
                    return (false, AlarmCodes.Has_Job_Without_Cst);
            }

            try
            {
                CST_TYPE orderCstTypeRequest = cstInfoFromAGVS.CST_Type;
                CST_TYPE currentCargoType = Agv.CargoStateStorer.GetCargoType();
                if (!isUnknowCstIDFromAGVS && currentCargoType != orderCstTypeRequest)
                {
                    if (currentCargoType == CST_TYPE.Tray)
                        return (false, AlarmCodes.Cst_Type_Not_Match_Rack_But_Get_Tray);
                    else
                        return (false, AlarmCodes.Cst_Type_Not_Match_Tray_But_Get_Rack);

                }
            }
            catch (Exception ex)
            {
                logger.Error(ex);
            }

            return (true, AlarmCodes.None);

            bool IsCargoPlacedNormal()
            {
                return Agv.CargoStateStorer.GetCargoStatus(Agv.Parameters.LDULD_Task_No_Entry) == CARGO_STATUS.HAS_CARGO_NORMAL;
            }

        }


        public static async Task<bool> WaitOperatorCheckCargoStatusProcess(int destineTag)
        {
            var Agv = StaStored.CurrentVechicle;
            bool _isCargoStatusRecoveryNoraml = false;
            BuzzerPlayer.Alarm();
            while (!_isCargoStatusRecoveryNoraml)
            {
                //進入確認流程
                Agv.CargoStateStorer.waitOperatorConfirmCargoStatus.Reset();
                AlarmManager.AddWarning(AlarmCodes.Has_Job_Without_Cst);
                Agv.frontendHubContext.Clients.All.SendAsync("CheckCargoStatus", destineTag);
                bool inTime = Agv.CargoStateStorer.waitOperatorConfirmCargoStatus.WaitOne(Debugger.IsAttached ? TimeSpan.FromSeconds(10) : TimeSpan.FromMinutes(5));
                if (!inTime)
                    return false;
                if (Agv.GetSub_Status() == AGVSystemCommonNet6.clsEnums.SUB_STATUS.DOWN)
                    return false;
                _isCargoStatusRecoveryNoraml = Agv.CargoStateStorer.GetCargoStatus(Agv.Parameters.LDULD_Task_No_Entry) == CARGO_STATUS.HAS_CARGO_NORMAL;

                if (!_isCargoStatusRecoveryNoraml)
                {
                    Agv.logger.LogWarning($"人員完成貨物狀態在席確認 但狀態仍不正確");
                }
                await Task.Delay(1000);
            }
            BuzzerPlayer.Action();
            return true;
        }
        protected override async Task<(double position, bool success, AlarmCodes alarm_code)> ChangeForkPositionInWorkStation()
        {
            var forkHeightChangeReuslt = await ForkLifter.ForkGoTeachedPoseAsync(destineTag, height, FORK_HEIGHT_POSITION.UP_, 0.5, invokeActionStart: false);
            return forkHeightChangeReuslt;
        }

        protected override async Task ManualCheckCargoStatusPrcessBeforeAction()
        {
            //Do nothing
            return;
        }
        protected override async Task ManualCheckCargoStatusPrcessAfterAction()
        {
            clsManualCheckCargoStatusParams manualCheckSettings = Agv.Parameters.ManualCheckCargoStatus;
            if (!manualCheckSettings.Enabled)
                return;

            bool modelExist = TryGetCheckPointModelByTag(Agv.Navigation.LastVisitedTag, ACTION_TYPE.Unload, out CheckPointModel checkPointModel);
            if (!modelExist || checkPointModel.TriggerMoment != CHECK_MOMENT.AFTER_UNLOAD)
                return;
            InvokeCargoManualCheckNotify(checkPointModel);
        }
    }
}
