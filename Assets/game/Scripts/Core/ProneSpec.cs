using System;

namespace Fps.Core
{
    /// <summary>
    /// 伏せ (<see cref="Prone.On"/>) の調整値。生成時に値を検査し、以後は変わらない。
    /// プレイヤー (<c>PlayerController.ProneSpec</c>) に渡せばゲームごとに変えられ、渡さなければ <see cref="Default"/> を使う。
    /// </summary>
    public sealed class ProneSpec
    {
        /// <summary>
        /// <see cref="Default"/> の <see cref="Height"/>。半径 0.5m のプレイヤーが CharacterController で取れる最も低いカプセル (直径と同じ高さ)。
        /// </summary>
        public const float DefaultHeight = 1f;

        /// <summary><see cref="Default"/> の <see cref="Speed"/>。しゃがみ (<c>PlayerController.DefaultCrouchSpeed</c>) の半分より遅い。</summary>
        public const float DefaultSpeed = 1.2f;

        /// <summary><see cref="Default"/> の <see cref="MaxPitchDegrees"/>。</summary>
        public const float DefaultMaxPitchDegrees = 30f;

        /// <summary><see cref="Default"/> の <see cref="SpreadScale"/>。</summary>
        public const float DefaultSpreadScale = 0.5f;

        /// <summary><see cref="Default"/> の <see cref="DiveDistance"/>。</summary>
        public const float DefaultDiveDistance = 2.5f;

        /// <summary><see cref="Default"/> の <see cref="DiveSeconds"/>。</summary>
        public const float DefaultDiveSeconds = 0.5f;

        /// <summary>既定の調整値。</summary>
        public static readonly ProneSpec Default = new ProneSpec();

        /// <summary>伏せたときのカプセルの高さ (m)。当たり判定もこの高さになる。</summary>
        public float Height { get; }

        /// <summary>伏せている間の水平速度 (m/s)。伏せている間はダッシュもジャンプもしない。</summary>
        public float Speed { get; }

        /// <summary>伏せている間に上下を向ける角度の上限 (度、水平から上下それぞれ)。</summary>
        public float MaxPitchDegrees { get; }

        /// <summary>伏せている間に弾の散らばりと照準の広がりに掛ける倍率。0 以上 1 以下。</summary>
        public float SpreadScale { get; }

        /// <summary>ダッシュ中に伏せたとき、飛び込んで前へ滑る水平距離 (m)。</summary>
        public float DiveDistance { get; }

        /// <summary>飛び込みにかかる秒数。</summary>
        public float DiveSeconds { get; }

        /// <param name="height">カプセルの高さ。0 より大きい有限値。しゃがみより低いことと半径の 2 倍以上であることは <see cref="StanceGeometry"/> が確かめる。</param>
        /// <param name="speed">伏せている間の水平速度。0 以上の有限値。</param>
        /// <param name="maxPitchDegrees">上下を向ける角度の上限。0 より大きく 90 以下。</param>
        /// <param name="spreadScale">散らばりに掛ける倍率。0 以上 1 以下。</param>
        /// <param name="diveDistance">飛び込みの距離。0 以上の有限値。</param>
        /// <param name="diveSeconds">飛び込みの秒数。0 より大きい有限値。</param>
        public ProneSpec(
            float height = DefaultHeight,
            float speed = DefaultSpeed,
            float maxPitchDegrees = DefaultMaxPitchDegrees,
            float spreadScale = DefaultSpreadScale,
            float diveDistance = DefaultDiveDistance,
            float diveSeconds = DefaultDiveSeconds)
        {
            if (!(height > 0f) || float.IsInfinity(height))
            {
                throw new ArgumentOutOfRangeException(nameof(height), height, "height must be finite and positive");
            }

            if (!(speed >= 0f) || float.IsInfinity(speed))
            {
                throw new ArgumentOutOfRangeException(nameof(speed), speed, "speed must be finite and not negative");
            }

            if (!(maxPitchDegrees > 0f && maxPitchDegrees <= 90f))
            {
                throw new ArgumentOutOfRangeException(nameof(maxPitchDegrees), maxPitchDegrees, "maxPitchDegrees must be in (0, 90]");
            }

            if (!(spreadScale >= 0f && spreadScale <= 1f))
            {
                throw new ArgumentOutOfRangeException(nameof(spreadScale), spreadScale, "spreadScale must be between 0 and 1");
            }

            if (!(diveDistance >= 0f) || float.IsInfinity(diveDistance))
            {
                throw new ArgumentOutOfRangeException(nameof(diveDistance), diveDistance, "diveDistance must be finite and not negative");
            }

            if (!(diveSeconds > 0f) || float.IsInfinity(diveSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(diveSeconds), diveSeconds, "diveSeconds must be finite and positive");
            }

            Height = height;
            Speed = speed;
            MaxPitchDegrees = maxPitchDegrees;
            SpreadScale = spreadScale;
            DiveDistance = diveDistance;
            DiveSeconds = diveSeconds;
        }
    }
}
