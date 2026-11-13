using System;
using System.Numerics;

namespace Fps.Core
{
    /// <summary>
    /// プレイヤーの移動量を計算する (歩き・ダッシュ・しゃがみ・ジャンプ・重力)。衝突判定はせず、
    /// <see cref="Step"/> の戻り値をそのまま CharacterController.Move に渡す想定。座標は Y が上。
    /// </summary>
    public sealed class PlayerMotor
    {
        /// <summary>接地中かつジャンプしないときの縦速度。地面に吸着させるための小さな負値。</summary>
        public const float GroundedVerticalSpeed = -2f;

        /// <summary>接地中にジャンプした瞬間に 1 度だけ呼ばれる。</summary>
        public event Action Jumped;

        public MotorSpec Spec { get; }

        /// <summary>直近の <see cref="Step"/> 終了時点の速度 (m/s)。</summary>
        public Vector3 Velocity { get; private set; }

        /// <param name="spec">移動の設定。null 不可。</param>
        public PlayerMotor(MotorSpec spec)
        {
            Spec = spec ?? throw new ArgumentNullException(nameof(spec));
        }

        /// <summary>
        /// 1 フレーム進め、今フレームの変位 (m) を返す。
        /// </summary>
        /// <param name="moveInput">移動入力。X が右、Y が前。長さ 1 を超える分は切り詰める。</param>
        /// <param name="yawRadians">向き (Y 軸まわり、ラジアン)。0 で +Z が前、正で右回り (+X 側) へ向く。</param>
        /// <param name="sprint">ダッシュ中か。crouched が true なら無視する。</param>
        /// <param name="crouched">しゃがみ中か。<see cref="MotorSpec.CrouchSpeed"/> で動く。</param>
        /// <param name="jumpPressed">今フレームにジャンプが押されたか。接地中でなければ無視する。</param>
        /// <param name="isGrounded">接地しているか。</param>
        /// <param name="deltaSeconds">経過秒数。0 以上。</param>
        public Vector3 Step(
            Vector2 moveInput, float yawRadians, bool sprint, bool crouched, bool jumpPressed, bool isGrounded, float deltaSeconds)
        {
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

            var speed = crouched ? Spec.CrouchSpeed : sprint ? Spec.SprintSpeed : Spec.WalkSpeed;
            var desired = ToWorld(ClampLength(moveInput), yawRadians) * speed;
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

        /// <summary>速度を 0 に戻す (リスポーンやテレポート時など)。</summary>
        public void ResetVelocity()
        {
            Velocity = Vector3.Zero;
        }

        /// <summary>
        /// 速度を直接設定する。クライアント予測で過去の状態に巻き戻して再シミュレーションするときに使う。
        /// </summary>
        /// <param name="velocity">速度 (m/s)。各成分が有限値。</param>
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

        /// <summary>入力 (X 右・Y 前) を yaw で回し、ワールドの水平ベクトル (X, Z) にする。</summary>
        private static Vector2 ToWorld(Vector2 input, float yawRadians)
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
