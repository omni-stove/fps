namespace Fps.Core
{
    /// <summary>能力を今使えない理由。使えるなら <see cref="None"/>。</summary>
    public enum AbilityUseBlock
    {
        /// <summary>使える。</summary>
        None,

        /// <summary>ルールの <see cref="ActionRules.Abilities"/> が Off。</summary>
        RuleOff,

        /// <summary>枠に能力が入っていない。</summary>
        Empty,

        /// <summary>倒れている・ダウンしている・操作を手放しているなど、持ち主が使える状態にない。</summary>
        Incapacitated,

        /// <summary>同じ枠の能力をまだ使っている。</summary>
        Running,

        /// <summary>使ってから次に使えるまでの待ち時間の途中。</summary>
        Cooldown,

        /// <summary>回数が残っていない。</summary>
        NoCharges,

        /// <summary>能力そのものが今は使えないと答えた。</summary>
        Refused,
    }
}
