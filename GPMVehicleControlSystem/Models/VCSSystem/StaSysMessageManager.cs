using AGVSystemCommonNet6.Log;

namespace GPMVehicleControlSystem.Models.VCSSystem
{
    public static class StaSysMessageManager
    {
        public static clsSystemMessageReport SysMessages { get; set; } = new clsSystemMessageReport();
        public static event EventHandler<clsSysMessage> OnSystemMessageAdd;
        public static void AddNewMessage(string message, int level = 0)
        {
            if (level == 0)
                LOG.INFO(message);
            if (level == 1)
                LOG.WARN(message);
            if (level == 2)
                LOG.ERROR(message);

            var sameMessage = SysMessages.Messages.FirstOrDefault(x => x.Message == message);

            if (sameMessage != null)
            {
                var index = SysMessages.Messages.IndexOf(sameMessage);
                SysMessages.Messages[index].Time = DateTime.Now;
                return;
            }

            if (SysMessages.Messages.Count > 100)
                SysMessages.Messages.Clear();

            if (SysMessages.ReportIndex == int.MaxValue)
                SysMessages.ReportIndex = 0;
            SysMessages.ReportIndex += 1;

            var msg = new clsSysMessage
            {
                Level = level,
                Message = message,
                Time = DateTime.Now,
            };
            SysMessages.Messages.Add(msg);
            OnSystemMessageAdd?.Invoke("", msg);
        }

        internal static void Clear()
        {
            SysMessages.Messages.Clear();
            SysMessages.ReportIndex += 1;
        }
    }
}
