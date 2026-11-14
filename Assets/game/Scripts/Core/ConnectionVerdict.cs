namespace Fps.Core
{
    /// <summary><see cref="ConnectionApproval.Judge(string, byte[], bool)"/> の結果。</summary>
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

        /// <summary>承認データから読めたクライアントの版。読めなければ null。</summary>
        public string ClientVersion { get; }

        /// <summary>承認したときの、切断して戻ってきた人を見分ける鍵。断ったときは null。</summary>
        public string RejoinKey { get; }

        /// <summary>断ったときにクライアントへ送る切断理由 (<see cref="DisconnectNotice"/> の符号)。承認したときは null。</summary>
        public string RejectReason { get; }

        /// <summary>サーバーのログに出す、断った訳。承認したときは null。</summary>
        public string LogText { get; }
    }
}
