using System.Collections.Generic;
using System.Numerics;
using Shooter.Core;

namespace Shooter.Server
{
    // 被弾と弾の通り道をスタミナ・よろけ・制圧のモジュール (ServerPlayerStatus) へ渡す口。無ければ何もしない
    internal sealed partial class ServerCombat
    {
        private readonly HashSet<int> _struck = new HashSet<int>();

        internal ServerPlayerStatus PlayerStatus { get; set; }

        // 倒れた人はよろけない (出現し直すときに ServerPlayerStatus が数え直す) ので、倒したダメージは渡さない
        private void ReportStatusDamage(int hurtId, HitZone? zone, DamageKind kind, int amount, DamageOutcome outcome)
        {
            if (PlayerStatus == null || outcome == DamageOutcome.Ignored || outcome == DamageOutcome.Killed)
            {
                return;
            }

            PlayerStatus.Damaged(hurtId, zone, kind, amount);
        }

        // 当たった人は、その弾ではニアミスではなく被弾として数える (オフラインの Hitscan.ReportNearMisses と同じ)。
        // 当たらなかった区間は射程まで伸ばす。貫いた後の区間は手前の道のりを引かずに射程を使うので、射程より少し先まで数えることがある
        private void ReportNearMisses(in ServerShot shot, List<ShotTraceSegment> segments)
        {
            if (PlayerStatus == null)
            {
                return;
            }

            _struck.Clear();
            foreach (var segment in segments)
            {
                if (segment.HasHit && segment.Hit.HitTarget)
                {
                    _struck.Add(segment.Hit.TargetId);
                }
            }

            foreach (var segment in segments)
            {
                var length = segment.HasHit ? Vector3.Distance(segment.Origin, segment.Hit.Point) : shot.Round.Range;
                PlayerStatus.ShotPassed(shot.ShooterId, segment.Origin, segment.Direction, length, _struck);
            }
        }
    }
}
