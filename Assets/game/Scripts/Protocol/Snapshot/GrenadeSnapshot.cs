using System;
using System.Numerics;

namespace Fps.Protocol
{
    /// <summary>
    /// 投げられて飛んでいるグレネード 1 個。Netcode では NetGrenade の NetworkObject を Spawn し、NetworkTransform で位置と回転を配っていた。
    /// 並びから消えたら爆発したか片付けられた。爆発の演出は <see cref="ExplosionMessage"/> で別に届く。
    /// </summary>
    public struct GrenadeSnapshot : ISnapshotEntity<GrenadeSnapshot>
    {
        public int GrenadeId { get; set; }
        public GrenadeFields Present { get; set; }

        /// <summary>投げた人がいるか (もう抜けていれば false)。</summary>
        public bool HasThrower { get; set; }

        /// <summary>投げた人。<see cref="HasThrower"/> が false なら使わない。</summary>
        public int ThrowerId { get; set; }

        /// <summary>投げたグレネードの Id (武器の Id と同じ表)。</summary>
        public string WeaponId { get; set; }

        public Vector3 Position { get; set; }
        public Quaternion Rotation { get; set; }

        public int EntityId => GrenadeId;

        public int PresentBits => (int)Present;

        public int AllBits => (int)GrenadeFields.All;

        public int ChangedBits(in GrenadeSnapshot baseline)
        {
            var bits = GrenadeFields.None;
            if (HasThrower != baseline.HasThrower
                || (HasThrower && ThrowerId != baseline.ThrowerId)
                || !string.Equals(WeaponId ?? "", baseline.WeaponId ?? "", StringComparison.Ordinal))
            {
                bits |= GrenadeFields.Identity;
            }

            if (!Same.Bits(Position, baseline.Position) || !Same.Bits(Rotation, baseline.Rotation))
            {
                bits |= GrenadeFields.Transform;
            }

            return (int)bits;
        }

        public void WriteBits(MessageWriter writer, int bits)
        {
            var fields = (GrenadeFields)bits;
            if ((fields & GrenadeFields.Identity) != 0)
            {
                if (HasThrower && ThrowerId < 0)
                {
                    throw new InvalidOperationException("thrower id must not be negative");
                }

                writer.WriteInt32(HasThrower ? ThrowerId : PlayerIds.None);
                writer.WriteString(WeaponId, MatchProtocol.MaxContentIdBytes);
            }

            if ((fields & GrenadeFields.Transform) != 0)
            {
                writer.WriteVector3(Position);
                writer.WriteQuaternion(Rotation);
            }
        }

        public void ReadBits(MessageReader reader, int id, int bits)
        {
            GrenadeId = id;
            var fields = (GrenadeFields)bits;
            Present = fields;
            if ((fields & GrenadeFields.Identity) != 0)
            {
                var thrower = reader.ReadInt32AtLeast(PlayerIds.None, "grenade thrower");
                HasThrower = thrower != PlayerIds.None;
                ThrowerId = HasThrower ? thrower : 0;
                WeaponId = reader.ReadString(MatchProtocol.MaxContentIdBytes, WeaponId);
            }

            if ((fields & GrenadeFields.Transform) != 0)
            {
                Position = reader.ReadFiniteVector3("grenade position");
                Rotation = reader.ReadFiniteQuaternion("grenade rotation");
            }
        }

        public void FillFrom(in GrenadeSnapshot baseline)
        {
            var missing = GrenadeFields.All & ~Present;
            if ((missing & GrenadeFields.Identity) != 0)
            {
                HasThrower = baseline.HasThrower;
                ThrowerId = baseline.ThrowerId;
                WeaponId = baseline.WeaponId;
            }

            if ((missing & GrenadeFields.Transform) != 0)
            {
                Position = baseline.Position;
                Rotation = baseline.Rotation;
            }

            Present = GrenadeFields.All;
        }
    }
}
