using System;
using System.Numerics;

namespace Shooter.Core
{
    public sealed class PlayerMotor
    {
        // 接地中に 0 でなく小さな負値にするのは、地面に吸着させるため。
        public const float GroundedVerticalSpeed = -2f;

        public event Action Jumped;

        public MotorSpec Spec { get; }

        public Vector3 Velocity { get; private set; }

        public PlayerMotor(MotorSpec spec)
        {
            Spec = spec ?? throw new ArgumentNullException(nameof(spec));
        }

        public Vector3 Step(
            Vector2 moveInput,
            float yawRadians,
            bool sprint,
            bool crouched,
            bool jumpPressed,
            bool isGrounded,
            float deltaSeconds,
            float carrySpeedMultiplier = 1f)
        {
            var sprinting = sprint && !crouched;
            var speed = crouched ? Spec.CrouchSpeed : sprinting ? Spec.SprintSpeed : Spec.WalkSpeed;
            var gait = crouched ? CarryGaits.Crouch : sprinting ? CarryGaits.Sprint : CarryGaits.Walk;
            var direction = sprinting ? Spec.SprintDirection : Spec.WalkDirection;
            return StepAtSpeed(
                moveInput, yawRadians, speed * CarryScale(gait, carrySpeedMultiplier), direction, jumpPressed, isGrounded, deltaSeconds);
        }

        public Vector3 StepProne(
            Vector2 moveInput, float yawRadians, float prone, bool isGrounded, float deltaSeconds, float carrySpeedMultiplier = 1f)
        {
            return StepAtSpeed(moveInput, yawRadians, prone * CarryScale(CarryGaits.Prone, carrySpeedMultiplier), false, isGrounded, deltaSeconds);
        }

        public Vector3 StepAtSpeed(
            Vector2 moveInput, float yawRadians, float speed, bool jumpPressed, bool isGrounded, float deltaSeconds)
        {
            return StepAtSpeed(moveInput, yawRadians, speed, Spec.WalkDirection, jumpPressed, isGrounded, deltaSeconds);
        }

        private Vector3 StepAtSpeed(
            Vector2 moveInput, float yawRadians, float speed, MoveDirectionScale direction, bool jumpPressed, bool isGrounded, float deltaSeconds)
        {
            if (!(speed >= 0f) || float.IsInfinity(speed))
            {
                throw new ArgumentOutOfRangeException(nameof(speed), speed, "speed must be finite and not negative");
            }

            if (!(deltaSeconds >= 0f) || float.IsInfinity(deltaSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds), deltaSeconds, "deltaSeconds must be finite and not negative");
            }

            if (!IsFinite(moveInput.X) || !IsFinite(moveInput.Y))
            {
                throw new ArgumentOutOfRangeException(nameof(moveInput), moveInput, "moveInput must be finite");
            }

            if (!IsFinite(yawRadians))
            {
                throw new ArgumentOutOfRangeException(nameof(yawRadians), yawRadians, "yawRadians must be finite");
            }

            var desired = ToWorld(direction.Apply(ClampLength(moveInput)), yawRadians) * speed;
            var current = new Vector2(Velocity.X, Velocity.Z);

            Vector2 horizontal;
            float verticalStart;
            var applyGravity = true;
            var jumped = false;

            if (isGrounded)
            {
                horizontal = desired;
                if (jumpPressed)
                {
                    verticalStart = (float)Math.Sqrt(2.0 * Spec.Gravity * Spec.JumpHeight);
                    jumped = true;
                }
                else
                {
                    verticalStart = GroundedVerticalSpeed;
                    applyGravity = false;
                }
            }
            else
            {
                horizontal = Vector2.Lerp(current, desired, Spec.AirControl);
                verticalStart = Velocity.Y;
            }

            // 重力下では台形則 (始点と終点の速度の平均) で縦変位を出す。一定加速度なら厳密解と一致し、
            // フレームレートによらずジャンプの到達高さが JumpHeight になる。
            var verticalEnd = applyGravity ? verticalStart - Spec.Gravity * deltaSeconds : verticalStart;
            var verticalDisplacement = (verticalStart + verticalEnd) * 0.5f * deltaSeconds;

            Velocity = new Vector3(horizontal.X, verticalEnd, horizontal.Y);
            if (jumped)
            {
                Jumped?.Invoke();
            }

            return new Vector3(horizontal.X * deltaSeconds, verticalDisplacement, horizontal.Y * deltaSeconds);
        }

        private float CarryScale(CarryGaits gait, float carrySpeedMultiplier)
        {
            if (!(carrySpeedMultiplier >= 0f) || float.IsInfinity(carrySpeedMultiplier))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(carrySpeedMultiplier), carrySpeedMultiplier, "carrySpeedMultiplier must be finite and not negative");
            }

            return Spec.CarryLoad.ForGait(gait, carrySpeedMultiplier);
        }

        public void ResetVelocity()
        {
            Velocity = Vector3.Zero;
        }

        // クライアント予測で過去の状態に巻き戻して再シミュレーションするため、速度を直接設定できるようにする。
        public void SetVelocity(Vector3 velocity)
        {
            if (!IsFinite(velocity.X) || !IsFinite(velocity.Y) || !IsFinite(velocity.Z))
            {
                throw new ArgumentOutOfRangeException(nameof(velocity), velocity, "velocity must be finite");
            }

            Velocity = velocity;
        }

        private static Vector2 ClampLength(Vector2 input)
        {
            var lengthSquared = input.LengthSquared();
            return lengthSquared > 1f ? input / (float)Math.Sqrt(lengthSquared) : input;
        }

        internal static Vector2 ToWorld(Vector2 input, float yawRadians)
        {
            var sin = (float)Math.Sin(yawRadians);
            var cos = (float)Math.Cos(yawRadians);

            // 前 = (sin, cos)、右 = (cos, -sin)。Unity の左手系で Y 軸まわりに yaw 回転したもの。
            return new Vector2(
                input.X * cos + input.Y * sin,
                -input.X * sin + input.Y * cos);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
