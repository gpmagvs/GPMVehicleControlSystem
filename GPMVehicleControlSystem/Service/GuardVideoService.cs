namespace GPMVehicleControlSystem.Service
{
    public class GuardVideoService
    {
        public async Task<bool> StartRecord(int time = 5)
        {
            try
            {
                using (var httpClient = new HttpClient())
                {
                    var response = await httpClient.GetAsync($"http://localhost:5000/api/Recorder/StartRecord?time={time}");
                    response.EnsureSuccessStatusCode();
                    if (response.StatusCode == System.Net.HttpStatusCode.OK)
                        return true;
                    else
                        return false;
                }
            }
            catch (Exception)
            {
                return false;
            }

        }

        public async Task<bool> StopRecord()
        {
            try
            {
                using (var httpClient = new HttpClient())
                {
                    var response = await httpClient.GetAsync($"http://localhost:5000/api/Recorder/StopRecord");
                    response.EnsureSuccessStatusCode();
                    if (response.StatusCode == System.Net.HttpStatusCode.OK)
                        return true;
                    else
                        return false;
                }
            }
            catch (Exception)
            {
                return false;
            }

        }
    }
}
