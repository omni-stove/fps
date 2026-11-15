using System;

namespace Fps.Core
{
    public sealed class SpinUpTrigger : ITriggerModel
    {
        private readonly ITriggerModel _inner;
        private readonly float _fullSpinSeconds;
        private float _spinSeconds;
        private float _shotProgress;
        private readonly bool _firesWhileHeld;
        private bool _held;
        private bool _pending;
        private bool _graceTick;

        // 撃つ速さを回転の速さに比例させると、撃ち始めの回転 (1 / 倍率) に SpinUpSeconds かかると決めるだけで
        // 満回転までの時間も決まり、間隔の縮み方を別の値で持たずに済む。
        public SpinUpTrigger(SpinUpSpec spinUp, ITriggerModel inner, bool firesWhileHeld = true)
        {
            Spec = spinUp ?? throw new ArgumentNullException(nameof(spinUp));
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            _firesWhileHeld = firesWhileHeld;
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
            _graceTick = false;
            _pending = !_firesWhileHeld;
            return TryShootPending();
        }

        // Semi の敵・タレットは離してすぐ押し直すため、離した直後の 1 回の Tick は回転を落とさず、押し直しで回転が戻らなくなるのを防ぐ。
        public int Release()
        {
            _held = false;
            _graceTick = !_firesWhileHeld;
            return 0;
        }

        public int Tick(float deltaSeconds)
        {
            var shots = _inner.Tick(SpecGuard.NonNegativeFinite(deltaSeconds, nameof(deltaSeconds)));
            var spinning = _held || _graceTick;
            _graceTick = false;
            if (spinning)
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

            if (_firesWhileHeld)
            {
                return shots + (_held ? TryShoot() : 0);
            }

            // Semi は押すたびに 1 発なので、回り切る前に押した分は回り切った Tick で撃ち、押していなければ持ち越さない。
            shots += TryShootPending();
            if (!spinning)
            {
                _pending = false;
            }

            return shots;
        }

        private int TryShootPending()
        {
            if (!_pending)
            {
                return _firesWhileHeld ? TryShoot() : 0;
            }

            var shots = TryShoot();
            if (shots > 0)
            {
                _pending = false;
            }

            return shots;
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
