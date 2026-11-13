namespace Fps.Core
{
    /// <summary><see cref="ReviveReach.Check"/> が蘇生の要求を受け付けなかった理由。</summary>
    public enum ReviveRejection
    {
        /// <summary>受け付けた。</summary>
        None,

        /// <summary>試合のルールでダウンを使っていない (<see cref="ActionRules.Downed"/> が Off)。</summary>
        DownedOff,

        /// <summary>起こす人か起こされる人が試合に参加していない。</summary>
        NotJoined,

        /// <summary>試合中でない。</summary>
        NotPlaying,

        /// <summary>自分を起こそうとした。</summary>
        Self,

        /// <summary>起こされる人が別のチーム。</summary>
        Enemy,

        /// <summary>起こす人が倒れているかダウン中。</summary>
        ReviverDown,

        /// <summary>起こされる人がダウンしていない。</summary>
        TargetNotDowned,

        /// <summary>起こす人と起こされる人が離れすぎている。</summary>
        TooFar,

        /// <summary>位置が有限値でない。</summary>
        InvalidPosition,
    }
}
