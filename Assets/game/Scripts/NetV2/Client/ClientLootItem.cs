using System.Numerics;
using Shooter.Protocol;

namespace Shooter.NetV2
{
    // 箱 (LootBox・SupplyCrate・DeathBox) と脱出地点を 1 つの型で持つのは、スナップショットの物 (ObjectiveItemSnapshot) が
    // どちらも「位置・開いているか・半径」で描けるから
    public readonly struct ClientLootItem
    {
        public ClientLootItem(int id, ObjectiveItemKind kind, Vector3 position, bool opened, float radius)
        {
            Id = id;
            Kind = kind;
            Position = position;
            Opened = opened;
            Radius = radius;
        }

        public int Id { get; }

        public ObjectiveItemKind Kind { get; }

        public Vector3 Position { get; }

        // 箱なら開けたか、脱出地点なら開いているか
        public bool Opened { get; }

        // 箱なら開けられる距離、脱出地点なら待つ範囲の半径
        public float Radius { get; }

        public bool IsBox => ClientLoot.IsBox(Kind);

        // オフラインの LootContainer と同じく水平の距離で測る。段差の上の箱でも足元から届くようにするため
        public float HorizontalDistanceTo(Vector3 position)
        {
            var offset = position - Position;
            return new Vector2(offset.X, offset.Z).Length();
        }

        public bool IsInReach(Vector3 position)
        {
            return HorizontalDistanceTo(position) <= Radius;
        }
    }
}
