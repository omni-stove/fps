using System;
using System.Collections.Generic;
using System.Numerics;

namespace Shooter.Core
{
    // 敵の目標そのものは替えず、知覚の Target だけを囮に差し替える。囮は陣営の参加者ではないので、陣営やタグで選ぶ目標の候補に入れると
    // モードの勝ち負けや選び直しの間隔に巻き込まれる。
    public static class DecoyLure
    {
        public static int SelectSeen(Vector3 self, Vector3? target, bool targetSeen, IReadOnlyList<DecoyOption> decoys)
        {
            var index = Nearest(self, decoys, true);
            if (index < 0 || !targetSeen || !target.HasValue || !IsFinite(target.Value))
            {
                return index;
            }

            return Vector3.Distance(self, decoys[index].Position) < Vector3.Distance(self, target.Value) ? index : -1;
        }

        public static int SelectHeard(Vector3 self, IReadOnlyList<DecoyOption> decoys)
        {
            return Nearest(self, decoys, false);
        }

        // 発見ゲージの量は見えたかどうかから作り直す。発見ゲージが On なら、この後の StealthAwareness がゲージで上書きする。
        public static EnemyPerception Present(EnemyPerception perception, Vector3 decoy)
        {
            return new EnemyPerception(
                perception.Self,
                decoy,
                true,
                perception.HeardNoise,
                perception.NoisePosition,
                perception.WasHit,
                perception.HitSource,
                perception.HealthFraction,
                EnemyPerception.AwarenessFromSight(true),
                perception.IsSuppressed,
                perception.SuppressionLevel);
        }

        // 撃たれた者の位置などの物音がもうあれば、そちらを残す。撃ってきた相手の方が囮の声より差し迫っている。
        public static EnemyPerception Hear(EnemyPerception perception, Vector3 decoy)
        {
            if (perception.HeardNoise)
            {
                return perception;
            }

            return new EnemyPerception(
                perception.Self,
                perception.Target,
                perception.HasLineOfSight,
                true,
                decoy,
                perception.WasHit,
                perception.HitSource,
                perception.HealthFraction,
                perception.Awareness,
                perception.IsSuppressed,
                perception.SuppressionLevel);
        }

        private static int Nearest(Vector3 self, IReadOnlyList<DecoyOption> decoys, bool seen)
        {
            if (decoys == null)
            {
                throw new ArgumentNullException(nameof(decoys));
            }

            var best = -1;
            var bestDistance = float.PositiveInfinity;
            for (var i = 0; i < decoys.Count; i++)
            {
                var decoy = decoys[i];
                if (!decoy.IsHostile || !decoy.IsAlive || !(seen ? decoy.IsSeen : decoy.IsHeard) || !IsFinite(decoy.Position))
                {
                    continue;
                }

                var distance = Vector3.Distance(self, decoy.Position);
                if (distance < bestDistance)
                {
                    best = i;
                    bestDistance = distance;
                }
            }

            return best;
        }

        private static bool IsFinite(Vector3 value)
        {
            return !float.IsNaN(value.X) && !float.IsInfinity(value.X)
                && !float.IsNaN(value.Y) && !float.IsInfinity(value.Y)
                && !float.IsNaN(value.Z) && !float.IsInfinity(value.Z);
        }
    }
}
