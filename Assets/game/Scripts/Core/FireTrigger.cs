using System;

namespace Shooter.Core
{
    public sealed class FireTrigger
    {
        private readonly ITriggerModel _model;
        private float _unspentSeconds;
        private bool _held;

        public FireMode Mode { get; }

        public float FireInterval { get; }

        public ITriggerModel Model => _model;

        public FireTrigger(FireMode mode, float fireInterval)
        {
            ValidateMode(mode);
            _model = TriggerModels.For(mode, fireInterval);
            Mode = mode;
            FireInterval = fireInterval;
        }

        public FireTrigger(WeaponSpec spec)
        {
            _model = TriggerModels.For(spec ?? throw new ArgumentNullException(nameof(spec)));
            Mode = spec.FireMode;
            FireInterval = spec.FireInterval;
        }

        public static bool Wants(FireMode mode, bool isPressed, bool wasPressedThisFrame)
        {
            ValidateMode(mode);
            return mode == FireMode.Auto ? isPressed : wasPressedThisFrame;
        }

        // 弾数をサーバーが持つ側 (オンラインの持ち主) が Wants でなくこちらで連射間隔も見るのは、
        // サーバーの ShotBudget に弾かれる速さで射撃を送らないため。
        public bool Pull(bool isPressed, bool wasPressedThisFrame)
        {
            var shots = 0;
            if (_held && (!isPressed || wasPressedThisFrame))
            {
                shots += _model.Release();
                _held = false;
            }

            // 離したかを先に伝えてから時間を進めるのは、離した後のフレームで押しっぱなしの撃ち方が撃たないようにするため。
            shots += _model.Tick(_unspentSeconds);
            _unspentSeconds = 0f;

            // Auto はフレーム内に押して離した入力を押していないと見なす (Wants と同じ)。
            var starts = Mode == FireMode.Auto ? isPressed && !_held : wasPressedThisFrame;
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

            return shots > 0;
        }

        public void Tick(float deltaSeconds)
        {
            if (!(deltaSeconds >= 0f) || float.IsInfinity(deltaSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds), deltaSeconds, "deltaSeconds must be a non-negative finite number");
            }

            // 次の Pull まで溜めるのは、押しっぱなしの撃ち方が Tick の時点で撃つと、その間に離した入力を見られないため。
            _unspentSeconds += deltaSeconds;
        }

        private static void ValidateMode(FireMode mode)
        {
            if (mode != FireMode.Auto && mode != FireMode.Semi)
            {
                throw new ArgumentOutOfRangeException(nameof(mode), mode, "mode must be Auto or Semi");
            }
        }
    }
}
