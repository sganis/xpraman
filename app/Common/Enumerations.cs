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
    public enum ApStatus
    {
        NOT_RUNNING,
        IDLE,
        RUNNING,
        UNKNOWN,
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
