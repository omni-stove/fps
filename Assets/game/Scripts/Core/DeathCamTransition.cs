namespace Fps.Core
{
    /// <summary><see cref="DeathCamSwitch.Update"/> で起きた切り替え。</summary>
    public enum DeathCamTransition
    {
        /// <summary>切り替わらなかった。</summary>
        None,

        /// <summary>倒れたのでデスカムを始めた。</summary>
        Started,

        /// <summary>リスポーンしたので元の視点に戻した。</summary>
        Respawned,

        /// <summary>試合が終わったので、試合の終わりの見せ方に譲って止めた。</summary>
        MatchEnded,

        /// <summary>自分の視点 (カメラ) が無くなったので止めた。</summary>
        ViewLost,

        /// <summary>ルールが <see cref="DeathCamera.None"/> になったので止めた。</summary>
        RuleDisabled,
    }
}
