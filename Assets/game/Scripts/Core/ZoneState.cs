using System.Numerics;

namespace Fps.Core
{
    /// <summary>
    /// 縮むエリア (<see cref="ZoneSchedule.Evaluate"/>) のある時刻での様子。位置は水平面 (X がワールドの x、Y がワールドの z) で持つ。
    /// </summary>
    public readonly struct ZoneState
    {
        /// <summary>今の段階の番号 (0 始まり)。<see cref="ZoneStage.Closed"/> では最後の段階の番号。</summary>
        public int PhaseIndex { get; }

        public ZoneStage Stage { get; }

        /// <summary>今の円の中心。</summary>
        public Vector2 Center { get; }

        /// <summary>今の円の半径 (m)。</summary>
        public float Radius { get; }

        /// <summary>この段階の終わりの円の中心。<see cref="ZoneStage.Closed"/> では <see cref="Center"/> と同じ。</summary>
        public Vector2 TargetCenter { get; }

        /// <summary>この段階の終わりの円の半径 (m)。<see cref="ZoneStage.Closed"/> では <see cref="Radius"/> と同じ。</summary>
        public float TargetRadius { get; }

        /// <summary>今、円の外で受けるダメージ (毎秒)。</summary>
        public float DamagePerSecond { get; }

        /// <summary>縮み始めるまでの秒数。<see cref="ZoneStage.Waiting"/> 以外では 0。</summary>
        public float SecondsUntilShrink { get; }

        /// <summary>今の動き (待ち・縮み) が終わるまでの秒数。<see cref="ZoneStage.Closed"/> では 0。</summary>
        public float SecondsUntilStageEnds { get; }

        public ZoneState(
            int phaseIndex,
            ZoneStage stage,
            Vector2 center,
            float radius,
            Vector2 targetCenter,
            float targetRadius,
            float damagePerSecond,
            float secondsUntilShrink,
            float secondsUntilStageEnds)
        {
            PhaseIndex = phaseIndex;
            Stage = stage;
            Center = center;
            Radius = radius;
            TargetCenter = targetCenter;
            TargetRadius = targetRadius;
            DamagePerSecond = damagePerSecond;
            SecondsUntilShrink = secondsUntilShrink;
            SecondsUntilStageEnds = secondsUntilStageEnds;
        }

        /// <summary>position (水平面) が今の円の中 (境界を含む) なら true。NaN を含む位置は中に入れない。</summary>
        public bool Contains(Vector2 position)
        {
            if (float.IsNaN(position.X) || float.IsNaN(position.Y) || float.IsInfinity(position.X) || float.IsInfinity(position.Y))
            {
                return false;
            }

            return Vector2.DistanceSquared(position, Center) <= Radius * Radius;
        }
    }
}
