using System;

namespace Fps.Core
{
    /// <summary>
    /// チュートリアルの手順を進めるために、1 フレーム分のプレイヤーの様子をまとめた値 (<see cref="TutorialSteps.Observe"/> に渡す)。
    /// 生成時に値を検査する。
    /// </summary>
    public readonly struct TutorialObservation
    {
        /// <param name="deltaSeconds">前の観測からの秒数。0 以上の有限値。</param>
        /// <param name="movedMeters">前の観測から水平に動いた距離 (m)。0 以上の有限値。</param>
        /// <param name="aiming">覗き込んでいるか。</param>
        /// <param name="hits">前の観測から標的に当てた回数。0 以上。</param>
        /// <param name="reloading">リロード中か、前の観測からリロードを始めたか。</param>
        /// <param name="crouching">しゃがんでいるか。</param>
        /// <param name="inCover">遮蔽物に張り付いているか。</param>
        public TutorialObservation(
            float deltaSeconds,
            float movedMeters,
            bool aiming,
            int hits,
            bool reloading,
            bool crouching,
            bool inCover)
        {
            if (!(deltaSeconds >= 0f) || float.IsInfinity(deltaSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds), deltaSeconds, "deltaSeconds must be a non-negative finite number");
            }

            if (!(movedMeters >= 0f) || float.IsInfinity(movedMeters))
            {
                throw new ArgumentOutOfRangeException(nameof(movedMeters), movedMeters, "movedMeters must be a non-negative finite number");
            }

            if (hits < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(hits), hits, "hits must not be negative");
            }

            DeltaSeconds = deltaSeconds;
            MovedMeters = movedMeters;
            Aiming = aiming;
            Hits = hits;
            Reloading = reloading;
            Crouching = crouching;
            InCover = inCover;
        }

        public float DeltaSeconds { get; }

        public float MovedMeters { get; }

        public bool Aiming { get; }

        public int Hits { get; }

        public bool Reloading { get; }

        public bool Crouching { get; }

        public bool InCover { get; }
    }
}
