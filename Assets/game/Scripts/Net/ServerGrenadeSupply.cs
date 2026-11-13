using System;
using Fps.Core;

namespace Fps.Net
{
    /// <summary>
    /// サーバーが 1 人ごとに持つグレネードの残りと、次に投げられる時刻。出現のたびに <see cref="Refill"/> で試合のルールの数に戻し、
    /// 投げる要求は <see cref="Check"/> で確かめ、グレネードを出せたら <see cref="Take"/> で 1 つ減らす。
    /// 確かめると減らすを分けるのは、プレハブの生成に失敗した要求で数を減らさないため。
    /// </summary>
    public sealed class ServerGrenadeSupply
    {
        private readonly float _throwSeconds;
        private GrenadePouch _pouch = new GrenadePouch(0);
        private bool _enabled;
        private double _nextThrowAt = double.NegativeInfinity;

        /// <param name="throwSeconds">投げてから次を投げられるまでの秒数。0 以上の有限値。</param>
        public ServerGrenadeSupply(float throwSeconds)
        {
            if (!(throwSeconds >= 0f) || float.IsInfinity(throwSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(throwSeconds), throwSeconds, "throwSeconds must be a non-negative finite number");
            }

            _throwSeconds = throwSeconds;
        }

        /// <summary>試合のルールでグレネードを使うか。<see cref="Refill"/> の前は false。</summary>
        public bool Enabled => _enabled;

        /// <summary>残りの数。</summary>
        public int Remaining => _pouch.Remaining;

        /// <summary>持てる数。</summary>
        public int Capacity => _pouch.Capacity;

        /// <summary>持ち主に同期する値。</summary>
        public NetGrenadeState State => new NetGrenadeState(_enabled, _pouch.Remaining);

        /// <summary>actions の数 (<see cref="GrenadePouch.For"/>) で満タンにし、次を投げるまでの待ちを解く。</summary>
        public void Refill(ActionRules actions)
        {
            if (actions == null)
            {
                throw new ArgumentNullException(nameof(actions));
            }

            _pouch = GrenadePouch.For(actions);
            _enabled = actions.Grenade == Grenade.On;
            _nextThrowAt = double.NegativeInfinity;
        }

        /// <summary>サーバーの時刻 nowSeconds に投げてよいか。減らさない。</summary>
        public GrenadeThrowRejection Check(double nowSeconds)
        {
            RequireTime(nowSeconds);
            if (!_enabled)
            {
                return GrenadeThrowRejection.Disabled;
            }

            if (_pouch.Remaining <= 0)
            {
                return GrenadeThrowRejection.Empty;
            }

            return nowSeconds < _nextThrowAt ? GrenadeThrowRejection.TooSoon : GrenadeThrowRejection.None;
        }

        /// <summary>
        /// <see cref="Check"/> が通れば 1 つ減らし、次に投げられる時刻を nowSeconds の <c>throwSeconds</c> 後にして <see cref="GrenadeThrowRejection.None"/>。
        /// 通らなければ何も変えずにその理由を返す。
        /// </summary>
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

        private static void RequireTime(double nowSeconds)
        {
            if (double.IsNaN(nowSeconds) || double.IsInfinity(nowSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(nowSeconds), nowSeconds, "nowSeconds must be finite");
            }
        }
    }
}
