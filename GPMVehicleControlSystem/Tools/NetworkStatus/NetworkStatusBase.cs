namespace GPMVehicleControlSystem.Tools.NetworkStatus
{
    public class NetworkStatusBase
    {
        public NetworkStatusBase()
        {

        }
        public virtual async Task<(double trasmited, double recieved)> GetNetworkStatus()
        {
            return (0, 0);
        }
    }
}
