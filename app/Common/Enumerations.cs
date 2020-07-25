namespace xpra
{
    public enum Page
    {
        Main,
        Password,
        Host,
        Settings,
        About,
    }
    public enum Status
    {
        STOPPED,
        STARTING,
        ATTACHING,
        ACTIVE,
        DETACHING,
        DETACHED,
        STOPPING,
        CHECKING,
    }
   
    public enum ConnectStatus
    {
        OK,
        BAD_HOST,
        BAD_KEY,
        BAD_PASSWORD,
        BAD_SSH,
        UNKNOWN,
    }
}
