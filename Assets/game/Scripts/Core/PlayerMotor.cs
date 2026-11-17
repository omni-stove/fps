using System;
using System.Numerics;

namespace Shooter.Core
{
    public sealed class PlayerMotor
    {
        // 接地中に 0 でなく小さな負値にするのは、地面に吸着させるため。
        public const float GroundedVerticalSpeed = -2f;

        // CharacterMotorSimulation が吹き飛ばし中に入力で寄せるかを決める閾値と同じ
        private const float _minTurnInputSquared = 1e-4f;

        public const int GroundJumpNumber = 1;

        public event Action<int> Jumped;

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
                Jumped?.Invoke(GroundJumpNumber);
            }

            return new Vector3(horizontal.X * deltaSeconds, verticalDisplacement, horizontal.Y * deltaSeconds);
        }

        /// <summary>
        /// <see cref="Step"/> の前に呼ぶ。地上のジャンプは <see cref="Step"/> の中で跳ぶが、空中で跳ぶかは跳んだ回数を持つ呼び手しか決められないため、
        /// 回数を受け取って返す別の口にする。
        /// </summary>
        /// <remarks>
        /// 跳んだら縦の速さを <see cref="MotorSpec.AirJumpSpeed"/> にし、横の速さはその大きさのまま入力の向きへ回す
        /// (入力が無ければ今の向きのまま)。続く <see cref="Step"/> は空中なので、そこから <see cref="MotorSpec.AirControl"/> で寄せる。
        /// </remarks>
        /// <param name="airJumpsUsed">着地してから空中で跳んだ回数。0 以上。</param>
        /// <param name="jumpPressed">今フレームにジャンプが押されたか。</param>
        /// <param name="isGrounded">接地しているか。</param>
        /// <param name="moveInput">移動入力 (<see cref="Step"/> と同じ)。</param>
        /// <param name="yawRadians">向き (<see cref="Step"/> と同じ)。</param>
        /// <returns>この後の空中で跳んだ回数。接地中は 0。</returns>
        public int StepAirJump(int airJumpsUsed, bool jumpPressed, bool isGrounded, Vector2 moveInput, float yawRadians)
        {
            if (airJumpsUsed < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(airJumpsUsed), airJumpsUsed, "airJumpsUsed must not be negative");
            }

            if (!IsFinite(moveInput.X) || !IsFinite(moveInput.Y))
            {
                throw new ArgumentOutOfRangeException(nameof(moveInput), moveInput, "moveInput must be finite");
            }

            if (!IsFinite(yawRadians))
            {
                throw new ArgumentOutOfRangeException(nameof(yawRadians), yawRadians, "yawRadians must be finite");
            }

            var used = AirJumpStep.Step(airJumpsUsed, isGrounded, jumpPressed, Spec, out var jumped);
            if (jumped)
            {
                var horizontal = TurnToInput(new Vector2(Velocity.X, Velocity.Z), moveInput, yawRadians);
                Velocity = new Vector3(horizontal.X, Spec.AirJumpSpeed, horizontal.Y);
                Jumped?.Invoke(GroundJumpNumber + used);
            }

            return used;
        }

        /// <summary>
        /// 速度を入れて <see cref="Jumped"/> を知らせる。壁から跳ぶ速さは <see cref="WallRunStep"/> が決めるが、
        /// 跳んだ知らせは地上と空中のジャンプと同じくこのクラスの 1 か所から出すため。
        /// </summary>
        /// <param name="velocity">跳んだ速度 (m/s)。各成分が有限値。</param>
        public void JumpWith(Vector3 velocity)
        {
            SetVelocity(velocity);
            Jumped?.Invoke(GroundJumpNumber);
        }

        private static Vector2 TurnToInput(Vector2 horizontal, Vector2 moveInput, float yawRadians)
        {
            // スティックの遊び程度の入力で向きを変えると、手を離しかけただけで横へ跳んでしまう
            var input = ClampLength(moveInput);
            if (input.LengthSquared() <= _minTurnInputSquared)
            {
                return horizontal;
            }

            return Vector2.Normalize(ToWorld(input, yawRadians)) * horizontal.Length();
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
