using System;
using System.Numerics;

namespace Fps.Core
{
    // 動かす物理 (CharacterController か自前の当たり判定か) は持ち主で違うので、ここでは速度と時間だけを進める。
    public sealed class KnockbackFlight
    {
        public bool IsFlying { get; private set; }

        public Vector3 Velocity { get; private set; }

        public float ElapsedSeconds { get; private set; }

        // 飛んでいる最中にまた押されたら足す。置き換えると、2 つ目の爆発が近くても弱ければ飛ぶ距離が縮んでしまう。
        public void Launch(Vector3 velocity)
        {
            RequireFinite(velocity, nameof(velocity));
            if (IsFlying)
            {
                Velocity += velocity;
                return;
            }

            IsFlying = true;
            Velocity = velocity;
            ElapsedSeconds = 0f;
        }

        // 縦は PlayerMotor と同じく台形則で動かす。フレームの長さで飛ぶ高さが変わらず、プレイヤーのジャンプと落ち方がそろう。
        public Vector3 Step(float deltaSeconds, float gravity, Vector2 steerVelocity, float airControl)
        {
            RequireNonNegativeFinite(deltaSeconds, nameof(deltaSeconds));
            RequireNonNegativeFinite(gravity, nameof(gravity));
            RequireNonNegativeFinite(airControl, nameof(airControl));
            if (float.IsNaN(steerVelocity.X) || float.IsInfinity(steerVelocity.X) || float.IsNaN(steerVelocity.Y) || float.IsInfinity(steerVelocity.Y))
            {
                throw new ArgumentException($"steerVelocity must be finite: {steerVelocity}", nameof(steerVelocity));
            }

            if (!IsFlying)
            {
                return Vector3.Zero;
            }

            ElapsedSeconds += deltaSeconds;

            // 1 Tick あたりの割合でなく毎秒の速さで寄せる。フレームの長さで空中の効きが変わらないように。
            var blend = 1f - MathF.Exp(-airControl * deltaSeconds);
            var velocity = Velocity;
            var verticalStart = velocity.Y;
            velocity.X += (steerVelocity.X - velocity.X) * blend;
            velocity.Z += (steerVelocity.Y - velocity.Z) * blend;
            velocity.Y -= gravity * deltaSeconds;
            Velocity = velocity;
            return new Vector3(velocity.X, (verticalStart + velocity.Y) * 0.5f, velocity.Z) * deltaSeconds;
        }

        public Vector3 Step(float deltaSeconds, float gravity)
        {
            return Step(deltaSeconds, gravity, Vector2.Zero, 0f);
        }

        public bool CanLand(float minAirSeconds)
        {
            return IsFlying && ElapsedSeconds >= minAirSeconds && Velocity.Y <= 0f;
        }

        public bool HasTimedOut(float maxAirSeconds)
        {
            return IsFlying && ElapsedSeconds >= maxAirSeconds;
        }

        public void StopHorizontal()
        {
            Velocity = new Vector3(0f, Velocity.Y, 0f);
        }

        public void StopRising()
        {
            if (Velocity.Y > 0f)
            {
                Velocity = new Vector3(Velocity.X, 0f, Velocity.Z);
            }
        }

        public float Land()
        {
            var seconds = ElapsedSeconds;
            Clear();
            return seconds;
        }

        public void Clear()
        {
            IsFlying = false;
            Velocity = Vector3.Zero;
            ElapsedSeconds = 0f;
        }

        private static void RequireFinite(Vector3 value, string name)
        {
            if (float.IsNaN(value.X) || float.IsInfinity(value.X)
                || float.IsNaN(value.Y) || float.IsInfinity(value.Y)
                || float.IsNaN(value.Z) || float.IsInfinity(value.Z))
            {
                throw new ArgumentException($"{name} must be finite: {value}", name);
            }
        }

        private static void RequireNonNegativeFinite(float value, string name)
        {
            if (!(value >= 0f) || float.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(name, value, name + " must be a non-negative finite number");
            }
        }
    }
}
