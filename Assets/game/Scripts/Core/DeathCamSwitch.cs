namespace Fps.Core
{
    /// <summary>
    /// デスカムを映すかの判断。毎フレーム <see cref="Update"/> に今の状態を渡す。
    /// ルールが <see cref="DeathCamera.None"/> 以外で、自分の視点があり、試合が終わっていないときに、生きていた状態から倒れたら始め、
    /// 生き返る・試合が終わる・視点が無くなる・ルールが None になるのどれかで止める。
    /// 映している間にルールが None 以外の別の演出 (Killer から Ally など) に変わっても止めず、何を映すかは呼び出し側が今のルールで決める。
    /// 始めるのは倒れた瞬間だけなので、参加前 (体力 0) や視点を得る前から倒れていた分では始めない。
    /// </summary>
    public sealed class DeathCamSwitch
    {
        private bool _wasAlive;

        /// <summary>いまデスカムを映しているか。</summary>
        public bool IsActive { get; private set; }

        /// <param name="mode">試合のルールのやられ演出。</param>
        /// <param name="hasLocalView">この端末で操作する自分のプレイヤーの視点があるか。</param>
        /// <param name="isAlive">自分のプレイヤーが生きているか。</param>
        /// <param name="matchEnded">試合が終わって次の試合を待っているか。</param>
        /// <returns>この呼び出しで起きた切り替え。</returns>
        public DeathCamTransition Update(DeathCamera mode, bool hasLocalView, bool isAlive, bool matchEnded)
        {
            var died = _wasAlive && !isAlive;
            _wasAlive = isAlive;

            if (IsActive)
            {
                var reason = StopReason(mode, hasLocalView, isAlive, matchEnded);
                if (reason != DeathCamTransition.None)
                {
                    IsActive = false;
                }

                return reason;
            }

            if (died && mode != DeathCamera.None && hasLocalView && !matchEnded)
            {
                IsActive = true;
                return DeathCamTransition.Started;
            }

            return DeathCamTransition.None;
        }

        /// <summary>映すのをやめ、生きていた記録も消す (プレイヤーが消えるときなど)。止めたなら true。</summary>
        public bool Reset()
        {
            var wasActive = IsActive;
            IsActive = false;
            _wasAlive = false;
            return wasActive;
        }

        // 試合の終わりと視点の喪失を先に見て、生き返ったのと同時に起きてもそちらを理由にする
        private static DeathCamTransition StopReason(DeathCamera mode, bool hasLocalView, bool isAlive, bool matchEnded)
        {
            if (!hasLocalView)
            {
                return DeathCamTransition.ViewLost;
            }

            if (matchEnded)
            {
                return DeathCamTransition.MatchEnded;
            }

            if (mode == DeathCamera.None)
            {
                return DeathCamTransition.RuleDisabled;
            }

            return isAlive ? DeathCamTransition.Respawned : DeathCamTransition.None;
        }
    }
}
