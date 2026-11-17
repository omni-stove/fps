namespace Shooter.Core
{
    public sealed class ConnectionVerdict
    {
        internal ConnectionVerdict(bool approved, string clientVersion, string rejoinKey, string rejectReason, string logText)
        {
            Approved = approved;
            ClientVersion = clientVersion;
            RejoinKey = rejoinKey;
            RejectReason = rejectReason;
            LogText = logText;
        }

        public bool Approved { get; }

        public string ClientVersion { get; }

        public string RejoinKey { get; }

        public string RejectReason { get; }

        public string LogText { get; }
    }
}
