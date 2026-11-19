using System.Numerics;
using Shooter.Protocol;

namespace Shooter.Server
{
    // 目的 (BR の Bot の行き先) に見せる、まだ開けていない箱の写し。中身の並びは見せず、武器が入っているかだけを渡す
    internal readonly struct ServerLootBox
    {
        public ServerLootBox(int itemId, ObjectiveItemKind kind, Vector3 position, float reach, bool hasWeapon)
        {
            ItemId = itemId;
            Kind = kind;
            Position = position;
            Reach = reach;
            HasWeapon = hasWeapon;
        }

        public int ItemId { get; }

        public ObjectiveItemKind Kind { get; }

        public Vector3 Position { get; }

        public float Reach { get; }

        public bool HasWeapon { get; }
    }
}
