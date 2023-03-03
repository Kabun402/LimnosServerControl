using LimnosServerControl.RCon.Helper;

namespace LimnosServerControl.RCon.Events
{
    public delegate void RConDisconnectEventHandler(RConDisconnectEventArgs args);

    public class RConDisconnectEventArgs : EventArgs
    {
        public RConDisconnectEventArgs(RConLoginCredentials loginDetails)
        {
            LoginDetails = loginDetails;
            Message = "Disconnected";
        }

        public RConLoginCredentials LoginDetails { get; }
        public string Message { get; }
    }
}
