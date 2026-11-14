namespace Fps.Net
{
    /// <summary>サーバーがグレネードを投げる要求を断った理由 (<see cref="NetTeamDeathmatch"/> の投げる処理)。</summary>
    public enum GrenadeThrowRejection
    {
        /// <summary>断っていない。</summary>
        None,

        /// <summary>試合のルールでグレネードを使わない。</summary>
        Disabled,

        /// <summary>残りが無い。</summary>
        Empty,

        /// <summary>前に投げてから <see cref="Fps.Core.GrenadeSpec.ThrowSeconds"/> 秒経っていない。</summary>
        TooSoon,

        /// <summary>投げる向きが有限でないか長さが 0。</summary>
        InvalidDirection,

        /// <summary>試合にグレネードのプレハブが設定されていない。</summary>
        NoPrefab,
    }
}
