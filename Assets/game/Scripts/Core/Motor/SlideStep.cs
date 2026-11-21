using System;
using System.Numerics;

namespace Shooter.Core
{
    // オフラインの SlideMovement とネットの CharacterMotorSimulation が同じ判定で滑り出し・止まり・跳ぶよう、Unity に頼らない部分をここに置く。
    public static class SlideStep
    {
        // 坂の継ぎ目で一瞬浮いただけでは終えないよう、接地が外れてから少し待つ。
        public const float AirborneGraceSeconds = 0.15f;

        // 動かそうとした距離のこの割合より進めなければ、壁などに止められたとみなす。
        public const float BlockedRatio = 0.5f;

        // 坂の上でカプセルの中心の真下が足より低くても届くよう、足元からさらに伸ばす。
        public const float GroundProbeMargin = 0.6f;

        // 水平の速さが歩きより速いときだけ滑り出せる。歩きより遅いとダッシュで走っているとは言えず、
        // 滑り出しの加速が歩きやその場からの瞬間移動になってしまうため。
        public static bool TryMeasureEntry(Vector2 horizontalVelocity, float walkSpeed, out Vector2 direction, out float speed)
        {
            direction = Vector2.Zero;
            speed = horizontalVelocity.Length();
            if (!IsFinite(speed) || !(speed > walkSpeed))
            {
                speed = 0f;
                return false;
            }

            direction = horizontalVelocity / speed;
            return true;
        }

        // 滑る向きを足場の面に沿わせ、下りを正とする傾きの sin を返す。足場が無いときは水平のまま 0 を返す。
        public static float SlopeSine(Vector3 direction, Vector3 normal, bool grounded, out Vector3 along)
        {
            along = direction;
            if (!grounded)
            {
                return 0f;
            }

            var projected = direction - (normal * Vector3.Dot(direction, normal));
            if (projected.LengthSquared() > 1e-6f)
            {
                along = Vector3.Normalize(projected);
            }

            return Math.Max(-1f, Math.Min(1f, -along.Y));
        }

        // 前の動きで進めた距離が狙いの半分に届かなければ、進めた割合まで速さを落とす。
        public static bool TryLimitForBlocked(float speed, float moved, float expected, out float limited)
        {
            limited = speed;
            if (!(expected > 1e-4f) || !(moved < expected * BlockedRatio))
            {
                return false;
            }

            limited = Math.Max(0f, speed * Math.Max(0f, moved) / expected);
            return true;
        }

        // PlayerMotor のジャンプと同じ初速。
        public static float JumpSpeed(float gravity, float jumpHeight)
        {
            return MathF.Sqrt(Math.Max(0f, 2f * gravity * jumpHeight));
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
