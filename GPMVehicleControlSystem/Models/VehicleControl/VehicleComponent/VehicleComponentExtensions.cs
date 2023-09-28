using AGVSystemCommonNet6;
using AGVSystemCommonNet6.Alarm.VMS_ALARM;
using static GPMVehicleControlSystem.Models.VehicleControl.VehicleComponent.clsNavigation;

namespace GPMVehicleControlSystem.Models.VehicleControl.VehicleComponent
{

    public static class VehicleComponentExtensions
    {
        /// <summary>
        /// 將雷測組數整數值轉換成DO 16 bits 之bool型態陣列值
        /// </summary>
        /// <param name="laser_mode"></param>
        /// <returns></returns>
        public static bool[] ToLaserDOSettingBits(this int laser_mode)
        {
            bool[] lsSet = laser_mode.To4Booleans();
            bool IN_1 = lsSet[0];
            bool IN_2 = lsSet[1];
            bool IN_3 = lsSet[2];
            bool IN_4 = lsSet[3];
            bool[] bits_bool_state= new bool[]
            {
                IN_1,!IN_1,  IN_2,!IN_2,  IN_3,!IN_3,  IN_4,!IN_4,IN_1,!IN_1,  IN_2,!IN_2,  IN_3,!IN_3,  IN_4,!IN_4,
            };
            return bits_bool_state;
        }

        /// <summary>
        /// 將車控發布的Direction轉換成AGV_DIRECTION 列舉
        /// </summary>
        /// <param name="agvc_direction"></param>
        /// <returns></returns>
        public static AGV_DIRECTION ToAGVDirection(this ushort agvc_direction)
        {
            if (agvc_direction == 0)
                return AGV_DIRECTION.FORWARD;
            else if (agvc_direction == 1)
                return AGV_DIRECTION.LEFT;
            else if (agvc_direction == 2)
                return AGV_DIRECTION.RIGHT;
            else if (agvc_direction == 11)
                return AGV_DIRECTION.BYPASS;
            else
                return AGV_DIRECTION.STOP;
        }

        public static AlarmCodes ToMotionAlarmCode(this ushort code)
        {
            var Alarm_Code = AlarmCodes.Motion_control_Wrong_Unknown_Code;
            if (code == 1)
                Alarm_Code = AlarmCodes.Motion_control_Wrong_Received_Msg;
            else if (code == 2)
                Alarm_Code = AlarmCodes.Motion_control_Wrong_Extend_Path;
            else if (code == 3)
                Alarm_Code = AlarmCodes.Motion_control_Out_Of_Line_While_Forwarding_End;
            else if (code == 4)
                Alarm_Code = AlarmCodes.Motion_control_Out_Of_Line_While_Tracking_End_Point;
            else if (code == 5)
                Alarm_Code = AlarmCodes.Motion_control_Out_Of_Line_While_Moving;
            else if (code == 6)
                Alarm_Code = AlarmCodes.Motion_control_Out_Of_Line_While_Secondary;
            else if (code == 7)
                Alarm_Code = AlarmCodes.Motion_control_Missing_Tag_On_End_Point;
            else if (code == 8)
                Alarm_Code = AlarmCodes.Motion_control_Missing_Tag_While_Moving;
            else if (code == 9)
                Alarm_Code = AlarmCodes.Motion_control_Missing_Tag_While_Secondary;
            else if (code == 10)
                Alarm_Code = AlarmCodes.Motion_control_Wrong_Initial_Position_In_Secondary;
            else if (code == 11)
                Alarm_Code = AlarmCodes.Motion_control_Wrong_Initial_Angle_In_Secondary;
            else if (code == 12)
                Alarm_Code = AlarmCodes.Motion_control_Wrong_Unknown_Code;
            else if (code == 13)
                Alarm_Code = AlarmCodes.Map_Recognition_Rate_Too_Low;
            else if (code == 999)
                Alarm_Code = AlarmCodes.Task_Path_Road_Closed;
            else
                Alarm_Code = AlarmCodes.Motion_control_Wrong_Unknown_Code;

            return Alarm_Code;
        }

        /// <summary>
        /// 將車控發布的Driver Alarm Code轉換成AlarmCodes列舉
        /// </summary>
        /// <param name="code"></param>
        /// <returns></returns>
        public static AlarmCodes ToDriverAlarmCode(this byte code)
        {
            var DriverAlarmCode = AlarmCodes.None;
            if (code != 0)
            {
                if (code == 1)
                    DriverAlarmCode = AlarmCodes.Over_current_protection;
                else if (code == 2)
                    DriverAlarmCode = AlarmCodes.Over_load_protection;
                else if (code == 3)
                    DriverAlarmCode = AlarmCodes.Motor_Feedback_Signal_Error;
                else if (code == 4)
                    DriverAlarmCode = AlarmCodes.Over_voltage_protection;
                else if (code == 5)
                    DriverAlarmCode = AlarmCodes.Under_voltage_protection;
                else if (code == 6)
                    DriverAlarmCode = AlarmCodes.Motor_Driver_Over_Heat_Error;
                else if (code == 7)
                    DriverAlarmCode = AlarmCodes.Motor_Active_Error;
                else if (code == 8)
                    DriverAlarmCode = AlarmCodes.Over_speed_protection;
                else if (code == 10)
                    DriverAlarmCode = AlarmCodes.Over_heat_protection;
                else if (code == 12)
                    DriverAlarmCode = AlarmCodes.Motor_Over_Speed_Error;
                else if (code == 13)
                    DriverAlarmCode = AlarmCodes.Motor_Encoder_Error;
                else if (code == 14)
                    DriverAlarmCode = AlarmCodes.Motor_Run_Forbid;
                else if (code == 15)
                    DriverAlarmCode = AlarmCodes.Motor_Extern_Stop;
                else if (code == 20)
                    DriverAlarmCode = AlarmCodes.Motor_Hall_Sequence_Error;
                else if (code == 21)
                    DriverAlarmCode = AlarmCodes.Command_error;
                else if (code == 22)
                    DriverAlarmCode = AlarmCodes.Motor_Parameters_Error;
                else
                    DriverAlarmCode = AlarmCodes.Other_error;
            }
            else
                DriverAlarmCode = AlarmCodes.None;

            return DriverAlarmCode;
        }

        public static AlarmCodes ToBatteryAlarmCode(this byte error_code)
        {
            var Battery_Warning_Code = AlarmCodes.None;
            if (error_code != 0)
            {
                if (error_code == 1)
                    Battery_Warning_Code = AlarmCodes.Over_Voltage;
                else if (error_code == 2)
                    Battery_Warning_Code = AlarmCodes.Under_Voltage;
                else if (error_code == 4)
                    Battery_Warning_Code = AlarmCodes.Over_Current_Charge;
                else if (error_code == 8)
                    Battery_Warning_Code = AlarmCodes.Over_Current_Discharge;
                else if (error_code == 16)
                    Battery_Warning_Code = AlarmCodes.Under_Current_Charge;
                else if (error_code == 32)
                    Battery_Warning_Code = AlarmCodes.Over_Temperature;
                else if (error_code == 64)
                    Battery_Warning_Code = AlarmCodes.Under_Temperature;
            }
            else
            {
                Battery_Warning_Code = AlarmCodes.None;
            }
            return Battery_Warning_Code;
        }
    }

}
