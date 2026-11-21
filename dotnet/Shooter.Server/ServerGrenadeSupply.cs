using System;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.Server
{
    internal enum GrenadeThrowRejection
    {
        None,
        Disabled,
        Empty,
        TooSoon,
        InvalidDirection,
        TooMany,
        UnknownType,
    }

    // 要求の届く時刻は揺れるので、持ち主の時計どおりに送られた次の要求が間隔より jitterSeconds まで早く届いても断らない。
    internal sealed class ServerGrenadeSupply
    {
        private readonly GrenadeCatalog _catalog;
        private readonly float _jitterSeconds;
        private GrenadeInventory _inventory = GrenadeInventory.Empty;
        private double _nextThrowAt = double.NegativeInfinity;

        public ServerGrenadeSupply(GrenadeCatalog catalog, float jitterSeconds = 0f)
        {
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            if (!(jitterSeconds >= 0f) || float.IsInfinity(jitterSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(jitterSeconds), jitterSeconds, "jitterSeconds must be a non-negative finite number");
            }

            _jitterSeconds = jitterSeconds;
        }

        public bool Enabled { get; private set; }

        public int RemainingOf(string typeId)
        {
            var at = _inventory.IndexOf(typeId);
            return at < 0 ? 0 : _inventory.RemainingAt(at);
        }

        public void Refill(ActionRules actions)
        {
            ArgumentNullException.ThrowIfNull(actions);
            _inventory = new GrenadeInventory(_catalog, actions);
            Enabled = actions.Grenade == Grenade.On;
            _nextThrowAt = double.NegativeInfinity;
        }

        // 投げている間を別に数えず次を投げられる時刻で決めるのは、オフラインの GrenadeThrower.IsThrowing と同じ ThrowSeconds にそろえるため
        public bool IsThrowing(double nowSeconds) => nowSeconds < _nextThrowAt;

        public GrenadeThrowRejection Check(double nowSeconds, string typeId, out GrenadeSpec spec)
        {
            if (!double.IsFinite(nowSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(nowSeconds), nowSeconds, "nowSeconds must be finite");
            }

            spec = null;
            if (!Enabled)
            {
                return GrenadeThrowRejection.Disabled;
            }

            var at = IndexOf(typeId);
            if (at < 0)
            {
                return GrenadeThrowRejection.UnknownType;
            }

            spec = _inventory.TypeAt(at);
            if (_inventory.RemainingAt(at) <= 0)
            {
                return GrenadeThrowRejection.Empty;
            }

            return nowSeconds < _nextThrowAt - _jitterSeconds ? GrenadeThrowRejection.TooSoon : GrenadeThrowRejection.None;
        }

        public GrenadeThrowRejection Take(double nowSeconds, string typeId, out GrenadeSpec spec)
        {
            var rejection = Check(nowSeconds, typeId, out spec);
            if (rejection != GrenadeThrowRejection.None)
            {
                return rejection;
            }

            _inventory.TryTake(IndexOf(typeId));
            _nextThrowAt = nowSeconds + spec.ThrowSeconds;
            return GrenadeThrowRejection.None;
        }

        // 煙や閃光を敵の足元へ投げても倒せないので、Bot には爆発する種類だけを選ばせる
        public GrenadeSpec FirstReadyExplosive(double nowSeconds)
        {
            for (var i = 0; i < _inventory.Count; i++)
            {
                var type = _inventory.TypeAt(i);
                if (type.Impact.Explosion != null && Check(nowSeconds, type.Id, out _) == GrenadeThrowRejection.None)
                {
                    return type;
                }
            }

            return null;
        }

        public void Fill(GrenadePouchSync sync)
        {
            ArgumentNullException.ThrowIfNull(sync);
            sync.Clear();
            sync.Enabled = Enabled;
            for (var i = 0; i < _inventory.Count; i++)
            {
                sync.Add(_inventory.TypeAt(i).Id, _inventory.RemainingAt(i));
            }
        }

        // 種類を送らない要求 (種類が 1 つだった頃のクライアント) は、今までどおり持っている最初の種類を投げる
        private int IndexOf(string typeId)
        {
            if (string.IsNullOrEmpty(typeId))
            {
                return _inventory.Count > 0 ? 0 : -1;
            }

            return _inventory.IndexOf(typeId);
        }
    }
}
