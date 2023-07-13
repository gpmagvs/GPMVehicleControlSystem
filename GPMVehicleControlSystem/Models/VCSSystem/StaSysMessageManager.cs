namespace GPMVehicleControlSystem.Models.VCSSystem
{
    public static class StaSysMessageManager
    {
        public static clsSystemMessageReport SysMessages { get; set; } = new clsSystemMessageReport();

        public static void AddNewMessage(string message, int level = 0)
        {
            if (SysMessages.ReportIndex == int.MaxValue)
                SysMessages.ReportIndex = 0;
            SysMessages.ReportIndex += 1;

            SysMessages.Messages.Add(new clsSysMessage
            {
                Level = level,
                Message = message,
                Time = DateTime.Now,
            });

        }
    }
}
