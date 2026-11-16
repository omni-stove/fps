using System;
using System.Numerics;

namespace Fps.Core
{
    /// <summary>
    /// 移動入力の向きごとに速さへ掛ける倍率 (前・後ろ・横)。入力の前後の成分に前か後ろの倍率、左右の成分に横の倍率を掛けるので、
    /// 斜めはその間を楕円状につなぐ。全部 1 (<see cref="Uniform"/>) なら向きによらず同じ速さ。
    /// </summary>
    public sealed class MoveDirectionScale
    {
        public const float DefaultForward = 1f;
        public const float DefaultBackward = 1f;
        public const float DefaultSide = 1f;

        /// <summary>どの向きも同じ速さ。</summary>
        public static readonly MoveDirectionScale Uniform = new MoveDirectionScale(DefaultForward, DefaultBackward, DefaultSide);

        /// <summary>前へ動くときの倍率。</summary>
        public float Forward { get; }

        /// <summary>後ろへ動くときの倍率。</summary>
        public float Backward { get; }

        /// <summary>左右へ動くときの倍率。</summary>
        public float Side { get; }

        /// <param name="forward">前の倍率。0 以上の有限値。</param>
        /// <param name="backward">後ろの倍率。0 以上の有限値。</param>
        /// <param name="side">左右の倍率。0 以上の有限値。</param>
        public MoveDirectionScale(float forward, float backward, float side)
        {
            Forward = Check(forward, nameof(forward));
            Backward = Check(backward, nameof(backward));
            Side = Check(side, nameof(side));
        }

        /// <summary>移動入力 (X 右・Y 前) の各成分に倍率を掛ける。</summary>
        public Vector2 Apply(Vector2 input)
        {
            return new Vector2(input.X * Side, input.Y * (input.Y >= 0f ? Forward : Backward));
        }

        private static float Check(float value, string name)
        {
            if (!(value >= 0f) || float.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(name, value, name + " must be finite and not negative");
            }

            return value;
        }
    }
}
