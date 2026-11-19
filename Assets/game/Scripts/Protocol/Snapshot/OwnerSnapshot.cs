using System;

namespace Shooter.Protocol
{
    public sealed class OwnerSnapshot
    {
        public const int MaxWeapons = byte.MaxValue;

        private readonly ContentIdList _equippedWeaponIds = new ContentIdList(MaxWeapons, "equipped weapon");
        private readonly GrenadePouchSync _grenades = new GrenadePouchSync();

        public int LastProcessedInputTick { get; set; } = -1;

        public WeaponSync Weapon { get; set; }

        // クライアントが武器の値を持たなくても予測をサーバーと揃えられるよう、倍率そのものを送る。
        public float CarrySpeedMultiplier { get; set; } = 1f;

        // 他の人には位置の補間で足りるが、持ち主の予測はこれが無いと飛んでいる間ずっと地上を歩いたつもりで巻き戻されるため、持ち主にだけ送る。
        public Shooter.Core.CharacterKnockbackState Knockback { get; set; }

        // 端数の秒で送らないのは、倒れている間ずっと毎 tick 差分に載ってしまうため。表示も整数の秒 (HudFormat.DisplayedSeconds) で足りる。
        // 人ごとの行 (PlayerSnapshot) に載せないのは、部分のビットに空きが無く、ほかの人の出直しの残りは要らないため。
        public int? RespawnSeconds { get; set; }

        public GrenadePouchSync Grenades => _grenades;

        public ContentIdList EquippedWeaponIds => _equippedWeaponIds;

        internal OwnerFields Present { get; set; }

        public void CopyFrom(OwnerSnapshot other)
        {
            if (other == null)
            {
                throw new ArgumentNullException(nameof(other));
            }

            LastProcessedInputTick = other.LastProcessedInputTick;
            Weapon = other.Weapon;
            CarrySpeedMultiplier = other.CarrySpeedMultiplier;
            Knockback = other.Knockback;
            RespawnSeconds = other.RespawnSeconds;
            _grenades.CopyFrom(other._grenades);
            _equippedWeaponIds.CopyFrom(other._equippedWeaponIds);
            Present = other.Present;
        }

        internal OwnerFields ChangedFields(OwnerSnapshot baseline)
        {
            var fields = OwnerFields.None;
            if (!Weapon.SameAs(baseline.Weapon))
            {
                fields |= OwnerFields.Weapon;
            }

            if (!_equippedWeaponIds.SameAs(baseline._equippedWeaponIds))
            {
                fields |= OwnerFields.Loadout;
            }

            if (!Grenades.SameAs(baseline.Grenades))
            {
                fields |= OwnerFields.Grenades;
            }

            if (CarrySpeedMultiplier != baseline.CarrySpeedMultiplier)
            {
                fields |= OwnerFields.CarrySpeed;
            }

            if (!Knockback.Equals(baseline.Knockback))
            {
                fields |= OwnerFields.Knockback;
            }

            if (RespawnSeconds != baseline.RespawnSeconds)
            {
                fields |= OwnerFields.Respawn;
            }

            return fields;
        }

        internal void Write(MessageWriter writer, OwnerFields fields)
        {
            writer.WriteInt32(LastProcessedInputTick);
            if ((fields & OwnerFields.Weapon) != 0)
            {
                Weapon.Write(writer);
            }

            if ((fields & OwnerFields.Loadout) != 0)
            {
                _equippedWeaponIds.Write(writer);
            }

            if ((fields & OwnerFields.Grenades) != 0)
            {
                Grenades.Write(writer);
            }

            if ((fields & OwnerFields.CarrySpeed) != 0)
            {
                writer.WriteSingle(CarrySpeedMultiplier);
            }

            if ((fields & OwnerFields.Knockback) != 0)
            {
                var knockback = Knockback;
                writer.WriteBool(knockback.IsFlying);
                if (knockback.IsFlying)
                {
                    writer.WriteVector3(knockback.Velocity);
                    writer.WriteSingle(knockback.ElapsedSeconds);
                }
            }

            if ((fields & OwnerFields.Respawn) != 0)
            {
                var respawn = RespawnSeconds;
                if (respawn < 0)
                {
                    throw new InvalidOperationException("respawn seconds must not be negative");
                }

                writer.WriteBool(respawn.HasValue);
                if (respawn.HasValue)
                {
                    writer.WriteInt32(respawn.Value);
                }
            }
        }

        internal void Read(MessageReader reader, OwnerFields fields)
        {
            Present = fields;
            LastProcessedInputTick = reader.ReadInt32AtLeast(-1, "last processed input tick");
            if ((fields & OwnerFields.Weapon) != 0)
            {
                Weapon = WeaponSync.Read(reader);
            }

            if ((fields & OwnerFields.Loadout) != 0)
            {
                _equippedWeaponIds.Read(reader);
            }

            if ((fields & OwnerFields.Grenades) != 0)
            {
                _grenades.Read(reader);
            }

            if ((fields & OwnerFields.CarrySpeed) != 0)
            {
                CarrySpeedMultiplier = reader.ReadFiniteSingleBetween(0f, float.MaxValue, "carry speed multiplier");
            }

            if ((fields & OwnerFields.Knockback) != 0)
            {
                Knockback = reader.ReadBool()
                    ? Shooter.Core.CharacterKnockbackState.Flying(
                        reader.ReadFiniteVector3("knockback velocity"),
                        reader.ReadFiniteSingleBetween(0f, float.MaxValue, "knockback seconds"))
                    : Shooter.Core.CharacterKnockbackState.None;
            }

            if ((fields & OwnerFields.Respawn) != 0)
            {
                RespawnSeconds = reader.ReadBool() ? reader.ReadInt32AtLeast(0, "respawn seconds") : (int?)null;
            }
        }

        internal void FillFrom(OwnerSnapshot baseline)
        {
            var missing = OwnerFields.All & ~Present;
            if ((missing & OwnerFields.Weapon) != 0)
            {
                Weapon = baseline.Weapon;
            }

            if ((missing & OwnerFields.Loadout) != 0)
            {
                _equippedWeaponIds.CopyFrom(baseline._equippedWeaponIds);
            }

            if ((missing & OwnerFields.Grenades) != 0)
            {
                _grenades.CopyFrom(baseline._grenades);
            }

            if ((missing & OwnerFields.CarrySpeed) != 0)
            {
                CarrySpeedMultiplier = baseline.CarrySpeedMultiplier;
            }

            if ((missing & OwnerFields.Knockback) != 0)
            {
                Knockback = baseline.Knockback;
            }

            if ((missing & OwnerFields.Respawn) != 0)
            {
                RespawnSeconds = baseline.RespawnSeconds;
            }

            Present = OwnerFields.All;
        }
    }
}
