using System.Numerics;

namespace Shooter.Core
{
    // 名前を文字で持たないのは、表示名を Id から引くのを名簿 (IParticipants) に任せ、ここを表示の文字から切り離すため。
    public readonly struct NameplateContact
    {
        public NameplateContact(int id, Vector3 headPosition, TeamRelation relation, bool isAlive, bool isInSight)
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
