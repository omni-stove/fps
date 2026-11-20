using System;
using System.Collections.Generic;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    // 自分に陣営が無いときに全員を敵とみなすのは、MinimapPublisher と同じく味方のいない扱いにし、壁越しに名前を出さないため。
    public sealed class OfflineNameplateTargets : INameplateTargets
    {
        private readonly OfflineParticipants _participants = new OfflineParticipants();

        public IParticipants Participants => _participants;

        public void CopyTo(Vector3 eye, List<NameplateTarget> targets)
        {
            if (targets == null)
            {
                throw new ArgumentNullException(nameof(targets));
            }

            var player = GameSignals.LocalPlayer.Value?.Player;
            var mine = SideMember.Of(player);
            var viewerRoot = player != null ? player.transform : null;
            foreach (var member in SideMember.FindAll())
            {
                if (member == null || member == mine || (viewerRoot != null && member.transform.IsChildOf(viewerRoot)))
                {
                    continue;
                }

                var relation = mine != null && mine.IsAllyOf(member) ? TeamRelation.Friendly : TeamRelation.Enemy;
                var damageable = member.Damageable;
                var head = HeadOf(member, damageable);

                // 味方は壁越しでも出すので、遮る物を調べるのは敵だけにする。
                var inSight = relation == TeamRelation.Enemy
                    && damageable != null
                    && Hitscan.HasLineOfSight(eye, damageable, float.MaxValue, viewerRoot);
                var id = CombatEvents.OfflineId(damageable != null ? damageable.gameObject : member.gameObject);
                targets.Add(new NameplateTarget(id, head, relation, member.IsAlive, inSight));
            }
        }

        public static Vector3 HeadOf(SideMember member, Damageable damageable)
        {
            if (member == null)
            {
                throw new ArgumentNullException(nameof(member));
            }

            if (damageable != null && damageable.TryGetBounds(out var bounds))
            {
                var center = bounds.center;
                return new Vector3(center.x, bounds.max.y, center.z);
            }

            return member.transform.position;
        }
    }
}
