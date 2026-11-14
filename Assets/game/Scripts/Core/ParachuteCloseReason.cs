namespace Fps.Core
{
    /// <summary>開いていたパラシュートを閉じた理由。</summary>
    public enum ParachuteCloseReason
    {
        /// <summary>着地した。</summary>
        Landed,

        /// <summary>ルールの <see cref="Parachute"/> が Off になった。</summary>
        RuleOff,

        /// <summary>パラシュートのコンポーネントが無効になったか外された。</summary>
        Disabled,

        /// <summary>移動を受け持てなくなった (優先度の高い動きに替わった・移動そのものが止まった)。</summary>
        Interrupted,
    }
}
