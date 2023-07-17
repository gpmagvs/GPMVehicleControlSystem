namespace GPMVehicleControlSystem.VehicleControl.DIOModule
{
    public partial class clsDOModule
    {
        public enum DO_ITEM : byte
        {
            Unknown,
            EMU_EQ_L_REQ,
            EMU_EQ_U_REQ,
            EMU_EQ_READY,
            EMU_EQ_UP_READY,
            EMU_EQ_LOW_READY,
            EMU_EQ_BUSY,
            Recharge_Circuit,
            Motor_Safety_Relay,
            Safety_Relays_Reset,
            Horizon_Motor_Stop,
            Horizon_Motor_Free,
            Horizon_Motor_Reset,
            Horizon_Motor_Brake,
            Vertical_Motor_Stop,
            Vertical_Motor_Free,
            Vertical_Motor_Reset,

            Front_LsrBypass,
            Back_LsrBypass,
            Left_LsrBypass,
            Right_LsrBypass,
            Fork_Under_Pressing_SensorBypass,
            Vertical_Belt_SensorBypass,

            AGV_DiractionLight_Front,
            AGV_DiractionLight_Back,
            AGV_DiractionLight_R,
            AGV_DiractionLight_Y,
            AGV_DiractionLight_G,
            AGV_DiractionLight_B,
            AGV_DiractionLight_Left,
            AGV_DiractionLight_Right,
            AGV_DiractionLight_Left_2,
            AGV_DiractionLight_Right_2,
            Vertical_Hardware_limit_bypass,

            AGV_VALID,
            AGV_READY,
            AGV_TR_REQ,
            AGV_BUSY,
            AGV_COMPT,
            AGV_L_REQ,
            AGV_U_REQ,
            AGV_CS_0,
            AGV_CS_1,
            AGV_Check_REQ,
            TO_EQ_Low,
            TO_EQ_Up,
            CMD_reserve_Up,
            CMD_reserve_Low,
            Front_Protection_Sensor_IN_1,
            Front_Protection_Sensor_CIN_1,
            Front_Protection_Sensor_IN_2,
            Front_Protection_Sensor_CIN_2,
            Front_Protection_Sensor_IN_3,
            Front_Protection_Sensor_CIN_3,
            Front_Protection_Sensor_IN_4,
            Front_Protection_Sensor_CIN_4,

            Back_Protection_Sensor_IN_1,
            Back_Protection_Sensor_CIN_1,
            Back_Protection_Sensor_IN_2,
            Back_Protection_Sensor_CIN_2,
            Back_Protection_Sensor_IN_3,
            Back_Protection_Sensor_CIN_3,
            Back_Protection_Sensor_IN_4,
            Back_Protection_Sensor_CIN_4,
            Left_Protection_Sensor_IN_1,
            Left_Protection_Sensor_IN_2,
            Left_Protection_Sensor_IN_3,
            Left_Protection_Sensor_IN_4,
            Ultrasound_Bypass,
            N2_Open,
            Instrument_Servo_On,
            Battery_2_Lock,
            Battery_2_Unlock,
            Battery_1_Lock,
            Battery_1_Unlock,
            Infrared_Door_1,
            Infrared_PW_2,
            Infrared_PW_1,
            Infrared_PW_0,
            Infrared_Door_2,

            /// <summary>
            /// 牙叉電動肛伸出
            /// </summary>
            Fork_Extend,
            /// <summary>
            /// 牙叉電動肛縮回
            /// </summary>
            Fork_Shortend,

        }

    }
}
