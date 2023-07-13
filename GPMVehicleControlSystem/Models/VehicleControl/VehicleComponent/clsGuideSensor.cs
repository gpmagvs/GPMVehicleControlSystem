using AGVSystemCommonNet6.Abstracts;
using AGVSystemCommonNet6.Alarm.VMS_ALARM;
using AGVSystemCommonNet6.GPMRosMessageNet.Messages;

namespace GPMVehicleControlSystem.Models.VehicleControl.VehicleComponent
{
    public class clsGuideSensor : CarComponent
    {
        public override COMPOENT_NAME component_name => COMPOENT_NAME.GUID_SENSOR;

        public override STATE CheckStateDataContent()
        {
            STATE _state = STATE.NORMAL;
            GuideSensor _guide_sensor = (GuideSensor)StateData;
            ////Console.WriteLine($"Guide Sensor States: {_guide_sensor.state1},{_guide_sensor.state2}");
            //Console.WriteLine($"Guid1 data = {string.Join(",", _guide_sensor.guide1)}");
            //Console.WriteLine($"Guid2 data = {string.Join(",", _guide_sensor.guide2)}");
            //if (_guide_sensor.state1 != 1 | _guide_sensor.state2 != 1)
            //{
            //    _state = STATE.ABNORMAL;
            //    AddAlarm(AlarmCodes.Guide_Module_Error);
            //}
            //else
            //{
            //    RemoveAlarm(AlarmCodes.Guide_Module_Error);
            //}
            return _state;
        }
    }
}
