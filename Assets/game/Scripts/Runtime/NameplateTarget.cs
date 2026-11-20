using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    // 名前を文字で持たないのは、表示名を Id から引くのをビュー (INameplateTargets.Participants) に任せるため。
    public readonly struct NameplateTarget
    {
        public NameplateTarget(int id, Vector3 headPosition, TeamRelation relation, bool isAlive, bool isInSight)
        {
            Id = id;
            HeadPosition = headPosition;
            Relation = relation;
            IsAlive = isAlive;
            IsInSight = isInSight;
        }

        public int Id { get; }

        public Vector3 HeadPosition { get; }

        public TeamRelation Relation { get; }

        public bool IsAlive { get; }

        public bool IsInSight { get; }
    }
}
