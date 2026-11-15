using System;

namespace Fps.Core
{
    public sealed class ChargeTrigger : ITriggerModel
    {
        // フレームの刻みを足した秒数は浮動小数の誤差で溜め切りや最小にわずかに届かないことがあり、溜めた弾を撃てなくしないため。
        private const float _fractionTolerance = 1e-4f;

        private readonly ITriggerModel _inner;
        private float _chargedSeconds;
        private bool _held;
        private bool _spent;

        public ChargeTrigger(ChargeSpec charge, ITriggerModel inner)
        {
            Spec = charge ?? throw new ArgumentNullException(nameof(charge));
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        }

        public ChargeSpec Spec { get; }

        public float MinShotSpacingSeconds => _inner.MinShotSpacingSeconds;

        public bool BlocksAds => _inner.BlocksAds;

        // 溜め切って撃った後も離すまで満タンを返すのは、その弾の溜めを撃った後に読む側 (ClientTrigger・TriggerDriver) が 1 を送れるようにするため。
        public float ChargeFraction => !_held ? 0f : _spent ? 1f : Fraction(Spec, _chargedSeconds);

        public static float Fraction(ChargeSpec charge, double heldSeconds)
        {
            if (charge == null)
            {
                throw new ArgumentNullException(nameof(charge));
            }

            if (!(heldSeconds > 0d))
            {
                return 0f;
            }

            var fraction = (float)Math.Min(1d, heldSeconds / charge.ChargeSeconds);
            return fraction >= 1f - _fractionTolerance ? 1f : fraction;
        }

        public static bool Fires(ChargeSpec charge, float chargeFraction)
        {
            if (charge == null)
            {
                throw new ArgumentNullException(nameof(charge));
            }

            return charge.FiresBelowMin || chargeFraction >= charge.MinChargeFraction - _fractionTolerance;
        }

        // 押していた秒数では届かない溜めを申告された弾は、届く分まで下げる。サーバーが届いた溜めをそのまま信じないため。
        public static float Reachable(ChargeSpec charge, float requested, double heldSeconds)
        {
            if (float.IsNaN(requested))
            {
                return 0f;
            }

            return Math.Min(Math.Max(0f, Math.Min(1f, requested)), Fraction(charge, heldSeconds));
        }

        public int Press()
        {
            if (_held)
            {
                return 0;
            }

            _held = true;
            _spent = false;
            _chargedSeconds = 0f;
            return 0;
        }

        public int Release()
        {
            if (!_held)
            {
                return 0;
            }

            var fraction = ChargeFraction;
            var spent = _spent;
            _held = false;
            _spent = false;
            _chargedSeconds = 0f;
            return spent || !Fires(Spec, fraction) ? 0 : Discharge();
        }

        public int Tick(float deltaSeconds)
        {
            SpecGuard.NonNegativeFinite(deltaSeconds, nameof(deltaSeconds));
            var shots = _inner.Tick(deltaSeconds);
            if (!_held || _spent)
            {
                return shots;
            }

            _chargedSeconds = Math.Min(Spec.ChargeSeconds, _chargedSeconds + deltaSeconds);
            if (Spec.FiresWhenFull && ChargeFraction >= 1f)
            {
                // 連射の待ちで撃てなかった間は撃ったことにせず、待ちが明けた tick で撃つ。
                var fired = Discharge();
                _spent = fired > 0;
                shots += fired;
            }

            return shots;
        }

        // 内側の撃ち方は 1 回押して離したことにする。押したままにすると Auto が溜めずに撃ち続けるため。
        private int Discharge()
        {
            var shots = _inner.Press();
            return shots + _inner.Release();
        }
    }
}
