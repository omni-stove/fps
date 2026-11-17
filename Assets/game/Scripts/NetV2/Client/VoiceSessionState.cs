namespace Shooter.NetV2
{
    /// <summary><see cref="IVoiceSession"/> が部屋にどこまで入っているか。</summary>
    public enum VoiceSessionState
    {
        /// <summary>部屋に入っていない。</summary>
        Idle,

        /// <summary>部屋に入る要求を出し、通るのを待っている。</summary>
        Joining,

        /// <summary>部屋に入ったが、まだつながっていない (つながりが切れて、サービスがつなぎ直している間も含む)。</summary>
        Connecting,

        /// <summary>部屋につながっていて、話せる。</summary>
        Connected,
    }
}
