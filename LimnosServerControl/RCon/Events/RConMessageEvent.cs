namespace LimnosServerControl.RCon.Events
{
    public delegate void RConMessageEventHandler(RConMessageEventArgs args);

    public class RConMessageEventArgs : EventArgs
    {
        public RConMessageEventArgs(string message, int id)
        {
            Message = message;
            Id = id;
        }

        public string Message { get; }
        public int Id { get; }
    }
}
