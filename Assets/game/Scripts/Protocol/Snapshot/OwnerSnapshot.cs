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

        /// <summary>
        /// サーバーが処理したこのプレイヤーの最後の入力の tick。まだ無ければ <see cref="InputMessage.NoSnapshot"/> と同じ -1。
        /// <see cref="PlayerSnapshot"/> の移動の状態はこの入力の後の値。
        /// </summary>
        public int LastProcessedInputTick { get; set; } = -1;

        public WeaponSync Weapon { get; set; }

        public GrenadePouchSync Grenades { get; set; }

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
            Grenades = other.Grenades;
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
                Grenades = GrenadePouchSync.Read(reader);
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
                Grenades = baseline.Grenades;
            }

            Present = OwnerFields.All;
        }
    }
}
