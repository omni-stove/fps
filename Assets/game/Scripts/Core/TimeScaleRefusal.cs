namespace Fps.Core
{
    /// <summary>時間の速さの要求を受けたか、断ったならその理由。</summary>
    public enum TimeScaleRefusal
    {
        /// <summary>受けた。</summary>
        None,

        /// <summary>ルールの <see cref="SlowMotion"/> が Off (または、ルールを決めたプレイヤーがもういない)。</summary>
        RuleOff,

        /// <summary>ネット対戦中。全員の時間が変わるので、ルールが On でも断る。</summary>
        NetworkSession,
    }
}
