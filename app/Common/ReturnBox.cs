namespace xpra
{
    public class ReturnBox
    {
        public ReturnBox()
        {
            ConnectStatus = ConnectStatus.UNKNOWN;
            ApStatus = ApStatus.UNKNOWN;
            Success = false;
            ExitCode = -999;
        }
        public Ap Drive { get; set; }
        public string Error { get; set; }
        public string Output { get; set; }
        public int ExitCode { get; set; }
        public bool Success { get; set; }
        public ConnectStatus ConnectStatus { get; set; }
        public ApStatus ApStatus { get; set; }
    }
}
