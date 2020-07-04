﻿namespace xpra
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
        IDLE,
        ACTIVE,
    }
    public enum ApStatus
    {
        NOT_RUNNING,
        BACKGROUND,
        ACTIVE,
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
