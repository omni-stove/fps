using System;
using System.Numerics;

namespace Shooter.Core
{
    public sealed class ParachuteMotion
    {
        public const float DefaultMinOpenHeight = 6f;

        // MotorSpec.DefaultSafeFallSpeed より遅くし、開いたまま着地しても落下ダメージの閾値に届かないようにしてある。
        public const float DefaultDescentSpeed = 4f;

        public const float DefaultHorizontalSpeed = 6f;

        public const float DefaultSteerAcceleration = 8f;

        // 接地して少しめり込んでいても、Ray の始点が地面の中に入って地面を外さないよう、足元より上から飛ばす。
        public const float ProbeLift = 0.25f;

        public const float ProbeDistance = 1000f;

        private Vector2 _horizontalVelocity;

        public ParachuteMotion(
            float minOpenHeight = DefaultMinOpenHeight,
            float descentSpeed = DefaultDescentSpeed,
            float horizontalSpeed = DefaultHorizontalSpeed,
            float steerAcceleration = DefaultSteerAcceleration)
        {
            if (!(minOpenHeight > 0f) || float.IsInfinity(minOpenHeight))
            {
                throw new ArgumentOutOfRangeException(nameof(minOpenHeight), minOpenHeight, "minOpenHeight must be positive and finite");
            }

            if (!(descentSpeed > 0f) || float.IsInfinity(descentSpeed))
            {
                throw new ArgumentOutOfRangeException(nameof(descentSpeed), descentSpeed, "descentSpeed must be positive and finite");
            }

            if (!(horizontalSpeed >= 0f) || float.IsInfinity(horizontalSpeed))
            {
                throw new ArgumentOutOfRangeException(nameof(horizontalSpeed), horizontalSpeed, "horizontalSpeed must be finite and not negative");
            }

            if (!(steerAcceleration > 0f) || float.IsInfinity(steerAcceleration))
            {
                throw new ArgumentOutOfRangeException(nameof(steerAcceleration), steerAcceleration, "steerAcceleration must be positive and finite");
            }

            MinOpenHeight = minOpenHeight;
            DescentSpeed = descentSpeed;
            HorizontalSpeed = horizontalSpeed;
            SteerAcceleration = steerAcceleration;
        }

        public float MinOpenHeight { get; }

        public float DescentSpeed { get; }

        public float HorizontalSpeed { get; }

        public float SteerAcceleration { get; }

        public Vector2 HorizontalVelocity => _horizontalVelocity;

        public Vector3 Velocity => new Vector3(_horizontalVelocity.X, -DescentSpeed, _horizontalVelocity.Y);

        public ParachuteOpenVerdict Evaluate(Parachute rule, bool isOpen, bool isBusy, float heightAboveGround)
        {
            if (!Enum.IsDefined(typeof(Parachute), rule))
            {
                throw new ArgumentOutOfRangeException(nameof(rule), rule, "rule is not a defined Parachute");
            }

            if (!(heightAboveGround >= 0f))
            {
                throw new ArgumentOutOfRangeException(nameof(heightAboveGround), heightAboveGround, "heightAboveGround must not be negative or NaN");
            }

            if (rule != Parachute.On)
            {
                return ParachuteOpenVerdict.RuleOff;
            }

            if (isOpen)
            {
                return ParachuteOpenVerdict.AlreadyOpen;
            }

            if (isBusy)
            {
                return ParachuteOpenVerdict.Busy;
            }

            return heightAboveGround < MinOpenHeight ? ParachuteOpenVerdict.TooLow : ParachuteOpenVerdict.Open;
        }

        public void Open(Vector2 horizontalVelocity)
        {
            if (!IsFinite(horizontalVelocity.X) || !IsFinite(horizontalVelocity.Y))
            {
                throw new ArgumentOutOfRangeException(nameof(horizontalVelocity), horizontalVelocity, "horizontalVelocity must be finite");
            }

            _horizontalVelocity = ClampLength(horizontalVelocity, HorizontalSpeed);
        }

        public Vector3 Step(Vector2 moveInput, float yawRadians, float deltaSeconds)
        {
            if (!IsFinite(moveInput.X) || !IsFinite(moveInput.Y))
            {
                throw new ArgumentOutOfRangeException(nameof(moveInput), moveInput, "moveInput must be finite");
            }

            if (!IsFinite(yawRadians))
            {
                throw new ArgumentOutOfRangeException(nameof(yawRadians), yawRadians, "yawRadians must be finite");
            }

            if (!(deltaSeconds >= 0f) || float.IsInfinity(deltaSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds), deltaSeconds, "deltaSeconds must be finite and not negative");
            }

            var target = PlayerMotor.ToWorld(ClampLength(moveInput, 1f), yawRadians) * HorizontalSpeed;
            var toTarget = target - _horizontalVelocity;
            var maxChange = SteerAcceleration * deltaSeconds;
            var distance = toTarget.Length();

            // 残りが 1 Tick で寄せられる量以下なら、行き過ぎて揺れないよう目標ちょうどに合わせる。
            _horizontalVelocity = distance <= maxChange ? target : _horizontalVelocity + (toTarget * (maxChange / distance));
            return Velocity;
        }

        // Open で戻さないのは、上限で縮め直すと丸めで値が変わり、予測とサーバーで同じ状態から同じ答えにならないため。
        public void Restore(Vector2 horizontalVelocity)
        {
            if (!IsFinite(horizontalVelocity.X) || !IsFinite(horizontalVelocity.Y))
            {
                throw new ArgumentOutOfRangeException(nameof(horizontalVelocity), horizontalVelocity, "horizontalVelocity must be finite");
            }

            _horizontalVelocity = horizontalVelocity;
        }

        public void Reset()
        {
            _horizontalVelocity = Vector2.Zero;
        }

        private static Vector2 ClampLength(Vector2 value, float max)
        {
            var length = value.Length();
            return length > max && length > 0f ? value * (max / length) : value;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
