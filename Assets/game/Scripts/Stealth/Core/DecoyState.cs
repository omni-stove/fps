using System;

namespace Shooter.Core
{
    // 置いた時点で 1 回呼び、そこから間隔ごとに呼ぶ。消える時刻ちょうどの呼び声は出さない (呼んだ直後に消えて、敵が何も無い所へ向かう)。
    public sealed class DecoyState
    {
        private readonly DecoySpec _spec;
        private float _elapsed;
        private float _nextCallAt;

        public DecoyState(DecoySpec spec)
        {
            _spec = spec ?? throw new ArgumentNullException(nameof(spec));
        }

        public int Calls { get; private set; }

        public float SecondsLeft => Math.Max(0f, _spec.LifetimeSeconds - _elapsed);

        public DeployRemoval? Over => SecondsLeft <= 0f ? DeployRemoval.Expired : (DeployRemoval?)null;

        public int Tick(float deltaSeconds)
        {
            if (!(deltaSeconds >= 0f) || float.IsInfinity(deltaSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds), deltaSeconds, "deltaSeconds must not be negative and must be finite");
            }

            if (Over.HasValue)
            {
                return 0;
            }

            _elapsed = Math.Min(_spec.LifetimeSeconds, _elapsed + deltaSeconds);
            var calls = 0;
            while (_nextCallAt <= _elapsed && _nextCallAt < _spec.LifetimeSeconds)
            {
                calls++;
                _nextCallAt += _spec.CallIntervalSeconds;
            }

            Calls += calls;
            return calls;
        }
    }
}
