using System;
using System.Numerics;
using Shooter.Core;

namespace Shooter.Protocol
{
    // 旗・爆弾・タグを 1 つの並びで送るのは、どれも「置き場所・持ち主・運んでいる人」で描けるから。
    // 箱と脱出地点も別の並びにしないのは、それに「開いているか・半径」を足すだけで描けるから。
    public struct ObjectiveItemSnapshot : ISnapshotEntity<ObjectiveItemSnapshot>
    {
        public int ItemId { get; set; }
        public ObjectiveItemFields Present { get; set; }

        public ObjectiveItemKind Kind { get; set; }

        public Team Team { get; set; }

        public bool HasOwner { get; set; }

        public int OwnerId { get; set; }

        public Vector3 Position { get; set; }

        public ObjectiveItemState State { get; set; }

        public bool HasCarrier { get; set; }

        public int CarrierId { get; set; }

        // 箱なら開けたか、脱出地点なら開いているか
        public bool Opened { get; set; }

        // 脱出地点なら待つ範囲の半径、箱なら開けられる距離。持たない物は 0
        public float Radius { get; set; }

        public int EntityId => ItemId;

        public int PresentBits => (int)Present;

        public int AllBits => (int)ObjectiveItemFields.All;

        public int OptionalBits => (int)ObjectiveItemFields.Optional;

        public int ChangedBits(in ObjectiveItemSnapshot baseline)
        {
            var bits = ObjectiveItemFields.None;
            if (Kind != baseline.Kind
                || Team != baseline.Team
                || HasOwner != baseline.HasOwner
                || (HasOwner && OwnerId != baseline.OwnerId))
            {
                bits |= ObjectiveItemFields.Identity;
            }

            if (!Same.Bits(Position, baseline.Position))
            {
                bits |= ObjectiveItemFields.Position;
            }

            if (State != baseline.State
                || HasCarrier != baseline.HasCarrier
                || (HasCarrier && CarrierId != baseline.CarrierId))
            {
                bits |= ObjectiveItemFields.State;
            }

            if (Opened != baseline.Opened || !Same.Bits(Radius, baseline.Radius))
            {
                bits |= ObjectiveItemFields.Access;
            }

            return (int)bits;
        }

        public void WriteBits(MessageWriter writer, int bits)
        {
            var fields = (ObjectiveItemFields)bits;
            if ((fields & ObjectiveItemFields.Identity) != 0)
            {
                if (HasOwner && OwnerId < 0)
                {
                    throw new InvalidOperationException("objective item owner id must not be negative");
                }

                writer.WriteByte((byte)Kind);
                writer.WriteByte((byte)Team);
                writer.WriteInt32(HasOwner ? OwnerId : PlayerIds.None);
            }

            if ((fields & ObjectiveItemFields.Position) != 0)
            {
                writer.WriteVector3(Position);
            }

            if ((fields & ObjectiveItemFields.State) != 0)
            {
                if (HasCarrier != (State == ObjectiveItemState.Carried))
                {
                    throw new InvalidOperationException("an objective item has a carrier exactly when it is carried");
                }

                if (HasCarrier && CarrierId < 0)
                {
                    throw new InvalidOperationException("objective item carrier id must not be negative");
                }

                writer.WriteByte((byte)State);
                writer.WriteInt32(HasCarrier ? CarrierId : PlayerIds.None);
            }

            if ((fields & ObjectiveItemFields.Access) != 0)
            {
                if (!(Radius >= 0f) || float.IsInfinity(Radius))
                {
                    throw new InvalidOperationException("objective item radius must be finite and not negative");
                }

                writer.WriteBool(Opened);
                writer.WriteSingle(Radius);
            }
        }

        public void ReadBits(MessageReader reader, int id, int bits)
        {
            ItemId = id;
            var fields = (ObjectiveItemFields)bits;
            Present = fields;
            if ((fields & ObjectiveItemFields.Identity) != 0)
            {
                Kind = (ObjectiveItemKind)reader.ReadByteBelow(EnumRange<ObjectiveItemKind>.Count, "objective item kind");
                Team = (Team)reader.ReadByteBelow(EnumRange<Team>.Count, "objective item team");
                var owner = reader.ReadInt32AtLeast(PlayerIds.None, "objective item owner");
                HasOwner = owner != PlayerIds.None;
                OwnerId = HasOwner ? owner : 0;
            }

            if ((fields & ObjectiveItemFields.Position) != 0)
            {
                Position = reader.ReadFiniteVector3("objective item position");
            }

            if ((fields & ObjectiveItemFields.State) != 0)
            {
                State = (ObjectiveItemState)reader.ReadByteBelow(EnumRange<ObjectiveItemState>.Count, "objective item state");
                var carrier = reader.ReadInt32AtLeast(PlayerIds.None, "objective item carrier");
                HasCarrier = carrier != PlayerIds.None;
                CarrierId = HasCarrier ? carrier : 0;
                if (HasCarrier != (State == ObjectiveItemState.Carried))
                {
                    throw new MessageReadException("an objective item must have a carrier exactly when it is carried");
                }
            }

            if ((fields & ObjectiveItemFields.Access) != 0)
            {
                Opened = reader.ReadBool();
                Radius = reader.ReadFiniteSingleBetween(0f, float.MaxValue, "objective item radius");
            }
        }

        public void FillFrom(in ObjectiveItemSnapshot baseline)
        {
            var missing = ObjectiveItemFields.All & ~Present;
            if ((missing & ObjectiveItemFields.Identity) != 0)
            {
                Kind = baseline.Kind;
                Team = baseline.Team;
                HasOwner = baseline.HasOwner;
                OwnerId = baseline.OwnerId;
            }

            if ((missing & ObjectiveItemFields.Position) != 0)
            {
                Position = baseline.Position;
            }

            if ((missing & ObjectiveItemFields.State) != 0)
            {
                State = baseline.State;
                HasCarrier = baseline.HasCarrier;
                CarrierId = baseline.CarrierId;
            }

            if ((missing & ObjectiveItemFields.Access) != 0)
            {
                Opened = baseline.Opened;
                Radius = baseline.Radius;
            }

            Present = ObjectiveItemFields.All;
        }
    }
}
