namespace Shooter.Protocol
{
    // 持ち主にだけ送るのは、他の人の弾数を送ると、リロードや持ち替えの隙が分かってしまうため。
    // リロードの時間の値は、持ち主の画面がアクティブリロードの窓を描くのにだけ使い、無効な値なら描かない (NetWeaponState.HasReloadProgress)。
    // そのため有限かどうかを読むときに確かめない。
    public struct WeaponSync
    {
        public int AmmoInMagazine { get; set; }

        public int ReserveAmmo { get; set; }

        public bool IsReloading { get; set; }

        public byte EquippedIndex { get; set; }

        public byte WeaponCount { get; set; }

        public bool IsSwitching { get; set; }

        public double ReloadStartedAt { get; set; }

        public float ReloadSeconds { get; set; }

        public float ActiveReloadWindowStart { get; set; }

        public float ActiveReloadWindowEnd { get; set; }

        public float Heat { get; set; }

        // overheatSeconds が 1 tick より短いと、熱が 1 のスナップショットが届かないことがあるため熱とは別に運ぶ。
        public bool IsOverheated { get; set; }

        public float ChargeFraction { get; set; }

        public byte AmmoTypeIndex { get; set; }

        public float SpinUpFraction { get; set; }

        public bool SameAs(in WeaponSync other)
        {
            return AmmoInMagazine == other.AmmoInMagazine
                && ReserveAmmo == other.ReserveAmmo
                && IsReloading == other.IsReloading
                && EquippedIndex == other.EquippedIndex
                && WeaponCount == other.WeaponCount
                && IsSwitching == other.IsSwitching
                && Same.Bits(ReloadStartedAt, other.ReloadStartedAt)
                && Same.Bits(ReloadSeconds, other.ReloadSeconds)
                && Same.Bits(ActiveReloadWindowStart, other.ActiveReloadWindowStart)
                && Same.Bits(ActiveReloadWindowEnd, other.ActiveReloadWindowEnd)
                && Same.Bits(Heat, other.Heat)
                && IsOverheated == other.IsOverheated
                && Same.Bits(ChargeFraction, other.ChargeFraction)
                && AmmoTypeIndex == other.AmmoTypeIndex
                && Same.Bits(SpinUpFraction, other.SpinUpFraction);
        }

        internal void Write(MessageWriter writer)
        {
            writer.WriteInt32(AmmoInMagazine);
            writer.WriteInt32(ReserveAmmo);
            writer.WriteBool(IsReloading);
            writer.WriteByte(EquippedIndex);
            writer.WriteByte(WeaponCount);
            writer.WriteBool(IsSwitching);
            writer.WriteDouble(ReloadStartedAt);
            writer.WriteSingle(ReloadSeconds);
            writer.WriteSingle(ActiveReloadWindowStart);
            writer.WriteSingle(ActiveReloadWindowEnd);
            writer.WriteSingle(Heat);
            writer.WriteBool(IsOverheated);
            writer.WriteSingle(ChargeFraction);
            writer.WriteByte(AmmoTypeIndex);
            writer.WriteSingle(SpinUpFraction);
        }

        internal static WeaponSync Read(MessageReader reader)
        {
            return new WeaponSync
            {
                AmmoInMagazine = reader.ReadInt32AtLeast(0, "ammo in magazine"),
                ReserveAmmo = reader.ReadInt32AtLeast(0, "reserve ammo"),
                IsReloading = reader.ReadBool(),
                EquippedIndex = reader.ReadByte(),
                WeaponCount = reader.ReadByte(),
                IsSwitching = reader.ReadBool(),
                ReloadStartedAt = reader.ReadDouble(),
                ReloadSeconds = reader.ReadSingle(),
                ActiveReloadWindowStart = reader.ReadSingle(),
                ActiveReloadWindowEnd = reader.ReadSingle(),
                Heat = reader.ReadFiniteSingleBetween(0f, 1f, "weapon heat"),
                IsOverheated = reader.ReadBool(),
                ChargeFraction = reader.ReadFiniteSingleBetween(0f, 1f, "charge fraction"),
                AmmoTypeIndex = reader.ReadByte(),
                SpinUpFraction = reader.ReadFiniteSingleBetween(0f, 1f, "spin-up fraction"),
            };
        }
    }
}
