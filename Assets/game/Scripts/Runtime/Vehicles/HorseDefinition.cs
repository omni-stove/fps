using System;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    [CreateAssetMenu(fileName = "Horse", menuName = "Shooter/Horse Definition")]
    public sealed class HorseDefinition : ScriptableObject
    {
        [SerializeField, Min(0.01f), Tooltip("馬の質量 (kg)。Rigidbody に入れる")]
        private float _mass = HorseSpec.DefaultMass;

        [SerializeField, Min(0.01f), Tooltip("スロットルを半分まで入れたときの Walk の最高速度 (m/s)")]
        private float _walkSpeed = HorseSpec.DefaultWalkSpeed;

        [SerializeField, Min(0.01f), Tooltip("スロットルを入れ切ったときの Trot の最高速度 (m/s)。Walk 以上にする")]
        private float _trotSpeed = HorseSpec.DefaultTrotSpeed;

        [SerializeField, Min(0.01f), Tooltip("Gallop を入れ切ったときの Canter の最高速度 (m/s)。Trot 以上にする")]
        private float _canterSpeed = HorseSpec.DefaultCanterSpeed;

        [SerializeField, Min(0f), Tooltip("後ずさりの最高速度 (m/s)。Walk 以下にする")]
        private float _reverseSpeed = HorseSpec.DefaultReverseSpeed;

        [SerializeField, Min(0.01f), Tooltip("速さを上げるときの加速 (m/s²)")]
        private float _acceleration = HorseSpec.DefaultAcceleration;

        [SerializeField, Min(0.01f), Tooltip("速さを下げるときの減速 (m/s²)")]
        private float _deceleration = HorseSpec.DefaultDeceleration;

        [SerializeField, Min(0f), Tooltip("止まっているときに曲がる速さ (度/秒)")]
        private float _turnRate = HorseSpec.DefaultTurnRate;

        [SerializeField, Range(0f, 1f), Tooltip("Canter の最高速度で曲がる速さに掛ける割合。小さいほど速いときに曲がりにくい")]
        private float _canterTurnScale = HorseSpec.DefaultCanterTurnScale;

        [SerializeField, Min(0f), Tooltip("跳ぶときに上へ与える速さ (m/s)。0 なら跳ばない")]
        private float _jumpSpeed = HorseSpec.DefaultJumpSpeed;

        [SerializeField, Range(0.01f, 89.99f), Tooltip("登れる坂の角度 (度)。これより急な坂では前へ押さない")]
        private float _maxSlopeAngle = HorseSpec.DefaultMaxSlopeAngle;

        [SerializeField, Min(0.01f), Tooltip("この速さ (m/s) 以上で Walk とみなす。Walk の最高速度以下にする")]
        private float _walkThreshold = HorseSpec.DefaultWalkThreshold;

        [SerializeField, Min(0.01f), Tooltip("この速さ (m/s) 以上で Trot とみなす。Walk の境目より上、Trot の最高速度以下にする")]
        private float _trotThreshold = HorseSpec.DefaultTrotThreshold;

        [SerializeField, Min(0.01f), Tooltip("この速さ (m/s) 以上で Canter とみなす。Trot の境目より上、Canter の最高速度以下にする")]
        private float _canterThreshold = HorseSpec.DefaultCanterThreshold;

        public float Mass
        {
            get => _mass;
            set => _mass = value;
        }

        public float WalkSpeed
        {
            get => _walkSpeed;
            set => _walkSpeed = value;
        }

        public float TrotSpeed
        {
            get => _trotSpeed;
            set => _trotSpeed = value;
        }

        public float CanterSpeed
        {
            get => _canterSpeed;
            set => _canterSpeed = value;
        }

        public float ReverseSpeed
        {
            get => _reverseSpeed;
            set => _reverseSpeed = value;
        }

        public float Acceleration
        {
            get => _acceleration;
            set => _acceleration = value;
        }

        public float Deceleration
        {
            get => _deceleration;
            set => _deceleration = value;
        }

        public float TurnRate
        {
            get => _turnRate;
            set => _turnRate = value;
        }

        public float CanterTurnScale
        {
            get => _canterTurnScale;
            set => _canterTurnScale = value;
        }

        public float JumpSpeed
        {
            get => _jumpSpeed;
            set => _jumpSpeed = value;
        }

        public float MaxSlopeAngle
        {
            get => _maxSlopeAngle;
            set => _maxSlopeAngle = value;
        }

        public float WalkThreshold
        {
            get => _walkThreshold;
            set => _walkThreshold = value;
        }

        public float TrotThreshold
        {
            get => _trotThreshold;
            set => _trotThreshold = value;
        }

        public float CanterThreshold
        {
            get => _canterThreshold;
            set => _canterThreshold = value;
        }

        public HorseSpec ToSpec()
        {
            try
            {
                return new HorseSpec(
                    _mass,
                    _walkSpeed,
                    _trotSpeed,
                    _canterSpeed,
                    _reverseSpeed,
                    _acceleration,
                    _deceleration,
                    _turnRate,
                    _canterTurnScale,
                    _jumpSpeed,
                    _maxSlopeAngle,
                    _walkThreshold,
                    _trotThreshold,
                    _canterThreshold);
            }
            catch (ArgumentException e)
            {
                // 値の誤りにアセットの名前を添えるのは、どの馬のアセットを直せばよいかを例外だけで分かるようにするため。
                var source = string.IsNullOrEmpty(name) ? "(unnamed horse)" : name;
                throw new ArgumentException($"horse '{source}' is invalid: {e.Message}", e);
            }
        }

        public void CopyFrom(HorseSpec spec)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            _mass = spec.Mass;
            _walkSpeed = spec.WalkSpeed;
            _trotSpeed = spec.TrotSpeed;
            _canterSpeed = spec.CanterSpeed;
            _reverseSpeed = spec.ReverseSpeed;
            _acceleration = spec.Acceleration;
            _deceleration = spec.Deceleration;
            _turnRate = spec.TurnRate;
            _canterTurnScale = spec.CanterTurnScale;
            _jumpSpeed = spec.JumpSpeed;
            _maxSlopeAngle = spec.MaxSlopeAngle;
            _walkThreshold = spec.WalkThreshold;
            _trotThreshold = spec.TrotThreshold;
            _canterThreshold = spec.CanterThreshold;
        }
    }
}
