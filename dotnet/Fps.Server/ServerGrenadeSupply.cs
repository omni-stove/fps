using System;
using Fps.Core;

namespace Fps.Server
{
    internal enum GrenadeThrowRejection
    {
        None,
        Disabled,
        Empty,
        TooSoon,
        InvalidDirection,
        TooMany,
    }

    /// <summary>
    /// 1 人ぶんのグレネードの残りと、前に投げてからの間隔 (Unity の <c>ServerGrenadeSupply</c> と同じ)。
    /// 要求の届く時刻は揺れるので、持ち主の時計どおりに送られた次の要求が間隔より jitterSeconds まで早く届いても断らない。
    /// </summary>
    internal sealed class ServerGrenadeSupply
    {
        private readonly float _throwSeconds;
        private readonly float _jitterSeconds;
        private GrenadePouch _pouch = new GrenadePouch(0);
        private double _nextThrowAt = double.NegativeInfinity;

        public ServerGrenadeSupply(float throwSeconds, float jitterSeconds = 0f)
        {
            if (!(throwSeconds >= 0f) || float.IsInfinity(throwSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(throwSeconds), throwSeconds, "throwSeconds must be a non-negative finite number");
            }

            if (!(jitterSeconds >= 0f) || float.IsInfinity(jitterSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(jitterSeconds), jitterSeconds, "jitterSeconds must be a non-negative finite number");
            }

            _throwSeconds = throwSeconds;
            _jitterSeconds = jitterSeconds;
        }

        public bool Enabled { get; private set; }

        public int Remaining => _pouch.Remaining;

        public void Refill(ActionRules actions)
        {
            ArgumentNullException.ThrowIfNull(actions);
            _pouch = GrenadePouch.For(actions);
            Enabled = actions.Grenade == Grenade.On;
            _nextThrowAt = double.NegativeInfinity;
        }

        public GrenadeThrowRejection Check(double nowSeconds)
        {
            if (!double.IsFinite(nowSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(nowSeconds), nowSeconds, "nowSeconds must be finite");
            }

            if (!Enabled)
            {
                return GrenadeThrowRejection.Disabled;
            }

            if (_pouch.Remaining <= 0)
            {
                return GrenadeThrowRejection.Empty;
            }

            return nowSeconds < _nextThrowAt - _jitterSeconds ? GrenadeThrowRejection.TooSoon : GrenadeThrowRejection.None;
        }

        public GrenadeThrowRejection Take(double nowSeconds)
        {
            var rejection = Check(nowSeconds);
            if (rejection != GrenadeThrowRejection.None)
            {
                return rejection;
            }

            _pouch.TryTake();
            _nextThrowAt = nowSeconds + _throwSeconds;
            return GrenadeThrowRejection.None;
        }
    }
}
