using System;

namespace Fps.Core
{
    /// <summary>
    /// <see cref="CapsuleMover"/> の設定。Unity の CharacterController の slopeLimit・stepOffset・skinWidth に当たる。長さは m。
    /// </summary>
    public sealed class CapsuleMoverSpec
    {
        /// <summary>生成時に指定しなかったときの <see cref="SlopeLimitDegrees"/>。CharacterController の既定と同じ。</summary>
        public const float DefaultSlopeLimitDegrees = 45f;

        /// <summary>生成時に指定しなかったときの <see cref="StepOffset"/>。CharacterController の既定と同じ。</summary>
        public const float DefaultStepOffset = 0.3f;

        /// <summary>生成時に指定しなかったときの <see cref="SkinWidth"/>。CharacterController の既定と同じ。</summary>
        public const float DefaultSkinWidth = 0.08f;

        /// <summary>生成時に指定しなかったときの <see cref="LayerMask"/>。すべてのレイヤーに当たる。</summary>
        public const int DefaultLayerMask = ~0;

        public static readonly CapsuleMoverSpec Default = new CapsuleMoverSpec(
            DefaultSlopeLimitDegrees, DefaultStepOffset, DefaultSkinWidth, DefaultLayerMask);

        /// <param name="slopeLimitDegrees">歩いて上がれる坂の、水平からの最大の角度 (度)。0 以上 90 以下。</param>
        /// <param name="stepOffset">歩いて上がれる段の最大の高さ。0 以上の有限値。</param>
        /// <param name="skinWidth">面との間に空けておく厚さ。正の有限値。</param>
        /// <param name="layerMask">当たるレイヤーのマスク (<see cref="IWorldCollision"/> と同じ形)。</param>
        public CapsuleMoverSpec(float slopeLimitDegrees, float stepOffset, float skinWidth, int layerMask)
        {
            if (!(slopeLimitDegrees >= 0f && slopeLimitDegrees <= 90f))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(slopeLimitDegrees), slopeLimitDegrees, "slopeLimitDegrees must be between 0 and 90");
            }

            if (!(stepOffset >= 0f) || float.IsInfinity(stepOffset))
            {
                throw new ArgumentOutOfRangeException(nameof(stepOffset), stepOffset, "stepOffset must be finite and not negative");
            }

            if (!(skinWidth > 0f) || float.IsInfinity(skinWidth))
            {
                throw new ArgumentOutOfRangeException(nameof(skinWidth), skinWidth, "skinWidth must be finite and positive");
            }

            SlopeLimitDegrees = slopeLimitDegrees;
            StepOffset = stepOffset;
            SkinWidth = skinWidth;
            LayerMask = layerMask;
            MinWalkableNormalY = (float)Math.Cos(slopeLimitDegrees * (Math.PI / 180.0));
        }

        /// <summary>歩いて上がれる坂の、水平からの最大の角度 (度)。</summary>
        public float SlopeLimitDegrees { get; }

        /// <summary>歩いて上がれる段の最大の高さ。</summary>
        public float StepOffset { get; }

        /// <summary>面との間に空けておく厚さ。</summary>
        public float SkinWidth { get; }

        /// <summary>当たるレイヤーのマスク。</summary>
        public int LayerMask { get; }

        /// <summary>歩ける面の法線の Y の下限 (<see cref="SlopeLimitDegrees"/> の cos)。</summary>
        public float MinWalkableNormalY { get; }
    }
}
