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
    public enum DisplayStatus
    {
        NOT_USED,
        PAUSED,
        ACTIVE,
        UNKNOWN,
    }
    public enum ApStatus
    {
        NOT_RUNNING,
        BACKGROUND,
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
