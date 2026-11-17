using System;

namespace Fps.Protocol
{
    /// <summary>
    /// スナップショットを受け取るクライアントのプレイヤーにだけ送る状態。Netcode では読む権限を持ち主にしていた同期値
    /// (弾数・持たせている武器・グレネードの残り) と、予測の照合に使う処理済みの入力の tick。
    /// </summary>
    public sealed class OwnerSnapshot
    {
        /// <summary>持たせられる武器の最大数。</summary>
        public const int MaxWeapons = byte.MaxValue;

        private readonly ContentIdList _equippedWeaponIds = new ContentIdList(MaxWeapons, "equipped weapon");
        private readonly GrenadePouchSync _grenades = new GrenadePouchSync();

        /// <summary>
        /// サーバーが処理したこのプレイヤーの最後の入力の tick。まだ無ければ <see cref="InputMessage.NoSnapshot"/> と同じ -1。
        /// <see cref="PlayerSnapshot"/> の移動の状態はこの入力の後の値。
        /// </summary>
        public int LastProcessedInputTick { get; set; } = -1;

        public WeaponSync Weapon { get; set; }

        /// <summary>
        /// サーバーが今このプレイヤーの移動に掛けている持ち物の倍率 (<c>Fps.Core.CarryLoadSpec.SpeedMultiplier</c>)。
        /// クライアントは武器の値を持たなくても、これで予測をサーバーと揃える。0 以上の有限値。
        /// </summary>
        public float CarrySpeedMultiplier { get; set; } = 1f;

        /// <summary>
        /// サーバーが <see cref="LastProcessedInputTick"/> の入力の後にこのプレイヤーを吹き飛ばしている状態。
        /// 他の人には位置の補間で足りるが、持ち主の予測はこれが無いと飛んでいる間ずっと地上を歩いたつもりで巻き戻されるため、持ち主にだけ送る。
        /// </summary>
        public Fps.Core.CharacterKnockbackState Knockback { get; set; }

        public GrenadePouchSync Grenades => _grenades;

        /// <summary>サーバーが今持たせている武器の Id (スロット順)。</summary>
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
                    ? Fps.Core.CharacterKnockbackState.Flying(
                        reader.ReadFiniteVector3("knockback velocity"),
                        reader.ReadFiniteSingleBetween(0f, float.MaxValue, "knockback seconds"))
                    : Fps.Core.CharacterKnockbackState.None;
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

            Present = OwnerFields.All;
        }
    }
}
