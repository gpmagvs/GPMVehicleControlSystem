namespace GPMVehicleControlSystem.VehicleControl.DIOModule
{
    public partial class clsDIModule
    {
        public enum DI_ITEM : byte
        {
            Unknown,
            EMO,
            EMO_Button,
            Bumper_Sensor,
            Panel_Reset_PB,
            Horizon_Motor_Switch,
            Vertical_Motor_Switch,
            Monitor_Switch,
            /// <summary>
            /// 牙叉電動缸_伸出到位檢
            /// </summary>
            Fork_Extend_Exist_Sensor,
            /// <summary>
            /// 牙叉電動缸_縮回到位檢
            /// </summary>
            Fork_Short_Exist_Sensor,
            /// <summary>
            /// 牙叉_左在席檢知(框)
            /// </summary>
            Fork_RACK_Left_Exist_Sensor,
            /// <summary>
            /// 牙叉_右在席檢知(框)
            /// </summary>
            Fork_RACK_Right_Exist_Sensor,

            /// <summary>
            /// 牙叉_左在席檢知(TRAY)
            /// </summary>
            Fork_TRAY_Left_Exist_Sensor,
            /// <summary>
            /// 牙叉_右在席檢知(TRAY)
            /// </summary>
            Fork_TRAY_Right_Exist_Sensor,
            /// <summary>
            /// 牙叉_障礙檢知 (前方)
            /// </summary>
            Fork_Frontend_Abstacle_Sensor,
            /// <summary>
            /// 牙叉_防壓檢知
            /// </summary>
            Fork_Under_Pressing_Sensor,

            Horizon_Motor_Alarm_1,
            Horizon_Motor_Alarm_2,
            Horizon_Motor_Alarm_3,
            Horizon_Motor_Alarm_4,
            Horizon_Motor_Busy_1,
            Horizon_Motor_Busy_2,
            Horizon_Motor_Busy_3,
            Horizon_Motor_Busy_4,

            Vertical_Motor_Alarm,
            Vertical_Motor_Busy,
            /// <summary>
            /// 升降軸待命位(Home)
            /// </summary>
            Vertical_Home_Pos,

            /// <summary>
            /// 升降軸上定位
            /// </summary>
            Vertical_Up_Pose,
            /// <summary>
            /// 升降軸下定位
            /// </summary>
            Vertical_Down_Pose,
            /// <summary>
            /// 升降軸上極限
            /// </summary>
            Vertical_Up_Hardware_limit,
            /// <summary>
            /// 升降軸下極限
            /// </summary>
            Vertical_Down_Hardware_limit,
            /// <summary>
            /// 升降軸皮帶檢知
            /// </summary>
            Vertical_Belt_Sensor,
            EQ_L_REQ,
            EQ_U_REQ,
            EQ_READY,
            EQ_UP_READY,
            EQ_LOW_READY,
            EQ_BUSY,
            EQ_GO,
            EQ_COMPT,
            EQ_VALID,
            EQ_TR_REQ,
            EQ_Check_Result,
            EQ_Check_Ready,
            Cst_Sensor_1,
            Cst_Sensor_2,
            FrontProtection_Obstacle_Sensor,
            FrontProtection_Area_Sensor_1,
            FrontProtection_Area_Sensor_2,
            FrontProtection_Area_Sensor_3,
            FrontProtection_Area_Sensor_4,
            BackProtection_Area_Sensor_1,
            BackProtection_Area_Sensor_2,
            BackProtection_Area_Sensor_3,
            BackProtection_Area_Sensor_4,
            LeftProtection_Area_Sensor_1,
            LeftProtection_Area_Sensor_2,
            LeftProtection_Area_Sensor_3,
            RightProtection_Area_Sensor_1,
            RightProtection_Area_Sensor_2,
            RightProtection_Area_Sensor_3,
            Battery_2_Exist_1,
            Battery_2_Exist_2,
            Battery_2_Exist_3,
            Battery_2_Exist_4,
            Battery_1_Exist_1,
            Battery_1_Exist_2,
            Battery_1_Exist_3,
            Battery_1_Exist_4,
            Battery_1_Lock_Sensor,
            Battery_1_Unlock_Sensor,
            Battery_2_Lock_Sensor,
            Battery_2_Unlock_Sensor,
            SMS_Error,
            Ground_Hole_CCD_1,
            Ground_Hole_CCD_2,
            Ground_Hole_CCD_3,
            Ground_Hole_CCD_4,
            Ground_Hole_Sensor_1,
            Ground_Hole_Sensor_2,
            Ground_Hole_Sensor_3,
            Ground_Hole_Sensor_4,
            Smoke_Sensor_1,
            N2_Sensor,
            /// <summary>
            /// 上層物料檢知
            /// </summary>
            Up_Cargo_Exist_Sernsor,
            /// <summary>
            /// 下層物料檢知
            /// </summary>
            Down_Cargo_Exist_Sernsor,


        }

    }
}
