namespace Shooter.Protocol
{
    public static class MatchProtocol
    {
        // メッセージの組み合わせの版をつなぐときに確かめるのは、個々のメッセージの版 (IMessage.Version) だけだと、
        // つないだ後に初めて読めない種類が届くまで食い違いに気付けないため。
        public const ushort Version = 19;

        public const int MaxReasonBytes = 128;

        public const int MaxContentIdBytes = 64;

        public const int MaxChatBytes = 512;
    }
}
