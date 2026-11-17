using System;

namespace Shooter.Core
{
    // 残りの秒数を持ち、離れてからの経過秒数を持たないのは、跳んだ後に猶予を閉じるのを 0 にするだけで表せ、
    // 経過秒数だと跳んだかを別に持たないと跳んだ直後にもう一度地上のジャンプができてしまうため。
    public static class JumpGraceStep
    {
        // 接地している tick を開いているとしないのは、そこでの押下は猶予を使わずに地上のジャンプになり、AirJumpStep も接地で回数を 0 に戻すため。
        public static bool IsOpen(bool grounded, float graceLeft)
        {
            return !grounded && graceLeft > 0f;
        }

        // 接地して終えた tick に JumpGraceSeconds へ戻すのをここでしないのは、着地を知るのが体を動かした後の呼び手で、
        // 梯子・泳ぎ・吹き飛ばしなどこの段を通らない道で着地したときも同じく戻すため。
        public static float Step(float graceLeft, bool startedGrounded, bool jumpPressed, MotorSpec spec, float deltaSeconds)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            if (!(graceLeft >= 0f) || float.IsInfinity(graceLeft))
            {
                throw new ArgumentOutOfRangeException(nameof(graceLeft), graceLeft, "graceLeft must be finite and not negative");
            }

            if (!(deltaSeconds >= 0f) || float.IsInfinity(deltaSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds), deltaSeconds, "deltaSeconds must be finite and not negative");
            }

            if (jumpPressed)
            {
                return 0f;
            }

            // 接地して始めた tick で離れたなら、離れたのはこの tick の途中なので、まだ減らさない
            if (startedGrounded)
            {
                return spec.JumpGraceSeconds;
            }

            return Math.Max(0f, graceLeft - deltaSeconds);
        }
    }
}
