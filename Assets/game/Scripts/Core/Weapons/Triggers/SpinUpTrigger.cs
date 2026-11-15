using System;

namespace Fps.Core
{
    public sealed class SpinUpTrigger : ITriggerModel
    {
        private readonly ITriggerModel _inner;
        private readonly float _fullSpinSeconds;
        private float _spinSeconds;
        private float _shotProgress;
        private bool _held;

        // 撃つ速さを回転の速さに比例させると、撃ち始めの回転 (1 / 倍率) に SpinUpSeconds かかると決めるだけで
        // 満回転までの時間も決まり、間隔の縮み方を別の値で持たずに済む。
        public SpinUpTrigger(SpinUpSpec spinUp, ITriggerModel inner)
        {
            Spec = spinUp ?? throw new ArgumentNullException(nameof(spinUp));
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            _fullSpinSeconds = spinUp.SpinUpSeconds * spinUp.StartIntervalMultiplier;
        }

        public SpinUpSpec Spec { get; }

        public float MinShotSpacingSeconds => _inner.MinShotSpacingSeconds;

        public bool BlocksAds => _inner.BlocksAds;

        public float ChargeFraction => _inner.ChargeFraction;

        public float SpinFraction => _spinSeconds / _fullSpinSeconds;

        public int Press()
        {
            _held = true;
            return TryShoot();
        }

        public int Release()
        {
            _held = false;
            return 0;
        }

        public int Tick(float deltaSeconds)
        {
            var shots = _inner.Tick(SpecGuard.NonNegativeFinite(deltaSeconds, nameof(deltaSeconds)));
            if (_held)
            {
                _spinSeconds = Math.Min(_fullSpinSeconds, _spinSeconds + deltaSeconds);
            }
            else
            {
                var drop = Spec.SpinDownSeconds > 0f ? deltaSeconds * _fullSpinSeconds / Spec.SpinDownSeconds : _fullSpinSeconds;
                _spinSeconds = Math.Max(0f, _spinSeconds - drop);
            }

            if (!SpunUp)
            {
                // 回り始めた瞬間に 1 発目を撃つため、撃てる回転に届くまでは次の弾を撃てる状態にしておく。
                _shotProgress = 1f;
            }
            else if (_shotProgress < 1f)
            {
                var interval = MinShotSpacingSeconds;
                _shotProgress = interval > 0f ? Math.Min(1f, _shotProgress + deltaSeconds * SpinFraction / interval) : 1f;
            }

            return shots + (_held ? TryShoot() : 0);
        }

        private bool SpunUp => _spinSeconds >= Spec.SpinUpSeconds;

        // 内側の撃ち方は押してすぐ離し、押しっぱなしで撃たせない。間隔を縮めながら撃つのはこちらが決めるため。
        private int TryShoot()
        {
            if (!SpunUp || _shotProgress < 1f)
            {
                return 0;
            }

            var shots = _inner.Press();
            shots += _inner.Release();
            if (shots > 0)
            {
                _shotProgress = 0f;
            }

            return shots;
        }
    }
}
