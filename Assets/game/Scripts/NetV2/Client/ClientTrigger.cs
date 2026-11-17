using System;
using Shooter.Core;

namespace Shooter.NetV2
{
    public readonly struct TriggerPull
    {
        public TriggerPull(int shots, bool pressed, bool released, float chargeFraction)
        {
            Shots = shots;
            Pressed = pressed;
            Released = released;
            ChargeFraction = chargeFraction;
        }

        public int Shots { get; }

        public bool Pressed { get; }

        // 同じフレームに押して離すと Pressed と両方 true になる。押した知らせを捨てると、押しっぱなしの撃ち方が始まらないため。
        public bool Released { get; }

        public float ChargeFraction { get; }
    }

    // FireTrigger を使わないのは、撃った発数 (バースト) と、離したときに撃つ撃ち方 (チャージ) の溜め具合が要るため。
    // 入力の読み方は FireTrigger.Pull と同じにする。オフラインとサーバーと撃つ間合いを揃えるため。
    public sealed class ClientTrigger
    {
        private float _unspentSeconds;

        public ClientTrigger(WeaponSpec spec)
        {
            Spec = spec ?? throw new ArgumentNullException(nameof(spec));
            Model = TriggerModels.For(spec);
        }

        public WeaponSpec Spec { get; }

        public ITriggerModel Model { get; }

        // 押している間ずっと出る撃ち方は、1 発ごとの要求でなく押した・離したを送る。押しっぱなしの間に要求を送り続けないため。
        public bool IsContinuous => IsContinuousDelivery(Spec.Round.Delivery);

        public bool IsHeld { get; private set; }

        public bool BlocksAds => Model.BlocksAds;

        public static bool IsContinuousDelivery(RoundDelivery delivery)
        {
            return delivery == RoundDelivery.Beam || delivery == RoundDelivery.Spray;
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

        public TriggerPull Pull(bool isPressed, bool wasPressedThisFrame)
        {
            var shots = 0;
            var pressed = false;
            var released = false;
            var charge = 0f;
            if (IsHeld && (!isPressed || wasPressedThisFrame))
            {
                shots += Release(out charge);
                released = true;
            }

            // 離したかを先に伝えてから時間を進めるのは、離した後のフレームで押しっぱなしの撃ち方が撃たないようにするため。
            shots += Model.Tick(_unspentSeconds);
            _unspentSeconds = 0f;

            // Auto はフレーム内に押して離した入力を押していないとみなす (FireTrigger.Wants と同じ)。
            var starts = Spec.FireMode == FireMode.Auto ? isPressed && !IsHeld : wasPressedThisFrame;
            if (starts)
            {
                shots += Model.Press();
                IsHeld = true;
                pressed = true;
            }

            if (IsHeld && !isPressed)
            {
                shots += Release(out charge);
                released = true;
            }

            return new TriggerPull(shots, pressed, released, released ? charge : Model.ChargeFraction);
        }

        public bool Drop()
        {
            if (!IsHeld)
            {
                return false;
            }

            Model.Release();
            IsHeld = false;
            return true;
        }

        // 離すと溜めが空になる撃ち方があるので、離す前の溜め具合を撃った弾の値にする。
        private int Release(out float charge)
        {
            charge = Model.ChargeFraction;
            IsHeld = false;
            return Model.Release();
        }
    }
}
