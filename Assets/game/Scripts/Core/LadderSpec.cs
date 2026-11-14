using System;

namespace Fps.Core
{
    /// <summary>
    /// はしご 1 本の上り下りの調整値。はしごのコンポーネントがシーンに持つ値から作り、生成時に値を検査する。
    /// </summary>
    public sealed class LadderSpec
    {
        /// <summary><see cref="ClimbSpeed"/> の既定値 (m/s)。</summary>
        public const float DefaultClimbSpeed = 2.5f;

        /// <summary><see cref="GrabDistance"/> の既定値 (m)。</summary>
        public const float DefaultGrabDistance = 0.4f;

        /// <summary><see cref="JumpOffSpeed"/> の既定値 (m/s)。</summary>
        public const float DefaultJumpOffSpeed = 4f;

        /// <summary><see cref="RegrabSeconds"/> の既定値 (秒)。</summary>
        public const float DefaultRegrabSeconds = 0.4f;

        /// <summary>すべて既定値の設定。</summary>
        public static readonly LadderSpec Default = new LadderSpec(
            DefaultClimbSpeed, DefaultGrabDistance, DefaultJumpOffSpeed, DefaultRegrabSeconds);

        /// <param name="climbSpeed">上り下りの速さ (m/s)。0 より大きい有限値。</param>
        /// <param name="grabDistance">体の表面からはしごの面までがこれ以内なら、はしごへ進むとつかまる (m)。0 以上の有限値。</param>
        /// <param name="jumpOffSpeed">ジャンプで離れたときに、はしごから遠ざかる水平の速さ (m/s)。0 以上の有限値。</param>
        /// <param name="regrabSeconds">離れてから次につかまれるまでの秒数。0 以上の有限値。</param>
        public LadderSpec(float climbSpeed, float grabDistance, float jumpOffSpeed, float regrabSeconds)
        {
            if (!(climbSpeed > 0f) || float.IsInfinity(climbSpeed))
            {
                throw new ArgumentOutOfRangeException(nameof(climbSpeed), climbSpeed, "climbSpeed must be positive and finite");
            }

            RequireNonNegative(grabDistance, nameof(grabDistance));
            RequireNonNegative(jumpOffSpeed, nameof(jumpOffSpeed));
            RequireNonNegative(regrabSeconds, nameof(regrabSeconds));

            ClimbSpeed = climbSpeed;
            GrabDistance = grabDistance;
            JumpOffSpeed = jumpOffSpeed;
            RegrabSeconds = regrabSeconds;
        }

        /// <summary>上り下りの速さ (m/s)。</summary>
        public float ClimbSpeed { get; }

        /// <summary>体の表面からはしごの面までがこれ以内なら、はしごへ進むとつかまる (m)。</summary>
        public float GrabDistance { get; }

        /// <summary>ジャンプで離れたときに、はしごから遠ざかる水平の速さ (m/s)。</summary>
        public float JumpOffSpeed { get; }

        /// <summary>離れてから次につかまれるまでの秒数。</summary>
        public float RegrabSeconds { get; }

        private static void RequireNonNegative(float value, string paramName)
        {
            if (!(value >= 0f) || float.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(paramName, value, paramName + " must be finite and not negative");
            }
        }
    }
}
