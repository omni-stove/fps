using System;

namespace Shooter.Protocol
{
    public readonly struct EosP2PNotice
    {
        private readonly string _remoteUserId;
        private readonly string _text;

        public EosP2PNotice(EosP2PNoticeKind kind, string remoteUserId, string text)
        {
            if (!Enum.IsDefined(typeof(EosP2PNoticeKind), kind))
            {
                throw new ArgumentOutOfRangeException(nameof(kind), kind, "unknown notice kind");
            }

            Kind = kind;
            _remoteUserId = remoteUserId;
            _text = text;
        }

        public EosP2PNoticeKind Kind { get; }

        public string RemoteUserId => _remoteUserId ?? "";

        public string Text => _text ?? "";

        public static EosP2PNotice SignedIn(string localUserId)
        {
            return new EosP2PNotice(EosP2PNoticeKind.SignedIn, null, localUserId);
        }

        public static EosP2PNotice SignInFailed(string error)
        {
            return new EosP2PNotice(EosP2PNoticeKind.SignInFailed, null, error);
        }

        public static EosP2PNotice ConnectionRequested(string remoteUserId)
        {
            return new EosP2PNotice(EosP2PNoticeKind.ConnectionRequested, remoteUserId, null);
        }

        public static EosP2PNotice ConnectionEstablished(string remoteUserId)
        {
            return new EosP2PNotice(EosP2PNoticeKind.ConnectionEstablished, remoteUserId, null);
        }

        public static EosP2PNotice ConnectionClosed(string remoteUserId, string reason)
        {
            return new EosP2PNotice(EosP2PNoticeKind.ConnectionClosed, remoteUserId, reason);
        }
    }
}
