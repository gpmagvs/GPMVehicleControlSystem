namespace GPMVehicleControlSystem.Models.VCSSystem
{
    public class clsSystemMessageReport
    {
        public int ReportIndex { get; set; } = 0;
        public List<clsSysMessage> Messages = new List<clsSysMessage>();
    }

    public class clsSysMessage
    {
        public DateTime Time { get; set; }
        public string Message { get; set; }
        public int Level { get; set; } = 0;//0 一般訊息 1 warning 2 alarm
    }
}
