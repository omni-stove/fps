using System;
using Shooter.Core;

namespace Shooter.Runtime
{
    public sealed class TriggerDriver
    {
        private ITriggerModel _model;
        private float _unspentSeconds;
        private bool _held;

        public ITriggerModel Model => _model;

        public bool IsHeld => _held;

        public float ChargeFraction => _model != null ? _model.ChargeFraction : 0f;

        // 撃つ前に読んだ溜めでは、溜め切りで撃つ撃ち方の最後のフレームの溜めが入らず、離して撃つ撃ち方は撃った後に読むと 0 に戻っている。
        // 撃った時点の溜めをここに残し、ネット対戦のクライアント (ClientTrigger) と同じ値で撃つ。
        public float ShotChargeFraction { get; private set; }

        // しまった撃ち方の溜めや押しっぱなしを持ち越すと、持ち替えた先で押していないのに撃つ。
        public void Bind(ITriggerModel model)
        {
            if (model == _model)
            {
                return;
            }

            Drop();
            _model?.Stow();
            _model = model;
            _unspentSeconds = 0f;
            _held = false;
        }

        public void Advance(float deltaSeconds)
        {
            if (!(deltaSeconds >= 0f) || float.IsInfinity(deltaSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds), deltaSeconds, "deltaSeconds must be a non-negative finite number");
            }

            // 次に押す・離すを渡すまで溜めるのは、押しっぱなしの撃ち方が時間だけで撃つと、その間に離した入力を見られないため。
            _unspentSeconds += deltaSeconds;
        }

        // FireTrigger.Pull と同じ順で渡す。Auto はフレーム内に押して離した入力を押していないと見なす。
        public int Pull(FireMode mode, bool isPressed, bool wasPressedThisFrame)
        {
            if (_model == null)
            {
                return 0;
            }

            var shots = 0;
            ShotChargeFraction = _model.ChargeFraction;
            if (_held && (!isPressed || wasPressedThisFrame))
            {
                shots += _model.Release();
                _held = false;
            }

            var spent = Spend();
            if (spent > 0)
            {
                ShotChargeFraction = _model.ChargeFraction;
            }

            shots += spent;
            var starts = mode == FireMode.Auto ? isPressed && !_held : wasPressedThisFrame;
            if (starts)
            {
                shots += _model.Press();
                _held = true;
            }

            if (_held && !isPressed)
            {
                shots += _model.Release();
                _held = false;
            }

            return shots;
        }

        public int PullAsBot(WeaponSpec spec)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            if (spec.Trigger.Charge != null)
            {
                var pressed = !(_held && ChargeFraction >= 1f);
                return Pull(spec.FireMode, pressed, pressed && !_held);
            }

            if (spec.FireMode == FireMode.Auto)
            {
                return Pull(FireMode.Auto, true, !_held);
            }

            return Pull(spec.FireMode, true, true);
        }

        // 撃てない間に押しっぱなしの撃ち方が弾を数えると、撃てるようになった最初の弾が連射間隔のぶん遅れるので、離したことにする。
        public void Drop()
        {
            if (_model == null)
            {
                return;
            }

            if (_held)
            {
                _model.Release();
                _held = false;
            }

            _model.Cancel();

            Spend();
        }

        private int Spend()
        {
            var shots = _model.Tick(_unspentSeconds);
            _unspentSeconds = 0f;
            return shots;
        }
    }
}
