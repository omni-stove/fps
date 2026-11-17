namespace Shooter.Core
{
    public sealed class DeathCamSwitch
    {
        private bool _wasAlive;

        public bool IsActive { get; private set; }

        // 生きていた状態から倒れた瞬間だけで始めるのは、参加前 (体力 0) や視点を得る前から倒れていた分で始めないため。
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
