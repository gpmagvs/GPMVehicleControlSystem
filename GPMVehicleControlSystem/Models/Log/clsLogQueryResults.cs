namespace GPMVehicleControlSystem.Models.Log
{
    public class clsLogQueryResults : clsLogQueryOptions
    {
        public List<clsLogQuResultDto> LogMessageList { get; set; } = new List<clsLogQuResultDto>();
        public int TotalCount { get; set; } = 0;
    }

    public class clsLogQuResultDto
    {
        public string Time { get; set; } = "";
        public string Message { get; set; } = "";
    }
}
