using System;

namespace Fps.Core
{
    // 弾倉やリロードは持たせない。タレットは置いた時点の弾の数を撃ち切るか、持続の秒数が過ぎたら消える。
    public sealed class TurretState
    {
        private readonly float _fireInterval;
        private float _cooldown;

        public TurretState(TurretSpec spec, float fireInterval)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            if (!(fireInterval > 0f) || float.IsInfinity(fireInterval))
            {
                throw new ArgumentOutOfRangeException(nameof(fireInterval), fireInterval, "fireInterval must be positive and finite");
            }

            _fireInterval = fireInterval;
            RoundsLeft = spec.Rounds;
            SecondsLeft = spec.LifetimeSeconds;
        }

        public int RoundsLeft { get; private set; }

        public float SecondsLeft { get; private set; }

        // 撃ち尽くしたのと時間切れが同じ Tick で重なったら、撃ち尽くした方を返す (最後の 1 発を撃ったことが先に起きている)。
        public DeployRemoval? Over
        {
            get
            {
                if (RoundsLeft == 0)
                {
                    return DeployRemoval.Expended;
                }

                return SecondsLeft <= 0f ? DeployRemoval.Expired : (DeployRemoval?)null;
            }
        }

        public bool IsReady => !Over.HasValue && _cooldown <= 0f;

        public void Tick(float deltaSeconds)
        {
            if (!(deltaSeconds >= 0f) || float.IsInfinity(deltaSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds), deltaSeconds, "deltaSeconds must not be negative and must be finite");
            }

            SecondsLeft = Math.Max(0f, SecondsLeft - deltaSeconds);
            _cooldown = Math.Max(0f, _cooldown - deltaSeconds);
        }

        public void Fire()
        {
            if (!IsReady)
            {
                throw new InvalidOperationException("the turret is not ready to fire");
            }

            RoundsLeft--;
            _cooldown = _fireInterval;
        }
    }
}
