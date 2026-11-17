namespace Shooter.Protocol
{
    /// <summary>
    /// 持ち主にだけ送る今の武器の状態 (Netcode の <c>NetWeaponState</c> の中身)。他の人の弾数を送ると、リロードや持ち替えの隙が分かってしまうため。
    /// </summary>
    /// <remarks>
    /// リロードの時間の値は、持ち主の画面がアクティブリロードの窓を描くのにだけ使い、無効な値なら描かない (<c>NetWeaponState.HasReloadProgress</c>)。
    /// そのため有限かどうかを読むときに確かめない。
    /// </remarks>
    public struct WeaponSync
    {
        public int AmmoInMagazine { get; set; }

        public int ReserveAmmo { get; set; }

        public bool IsReloading { get; set; }

        /// <summary>持っている武器のうち今の武器の番号。</summary>
        public byte EquippedIndex { get; set; }

        /// <summary>持っている武器の数。</summary>
        public byte WeaponCount { get; set; }

        public bool IsSwitching { get; set; }

        /// <summary>リロードを始めたサーバーの時刻 (秒)。</summary>
        public double ReloadStartedAt { get; set; }

        public float ReloadSeconds { get; set; }

        /// <summary>アクティブリロードの窓の始まり (リロードの進みの 0〜1)。</summary>
        public float ActiveReloadWindowStart { get; set; }

        public float ActiveReloadWindowEnd { get; set; }

        public float Heat { get; set; }

        /// <summary>オーバーヒート中か。overheatSeconds が 1 tick より短いと、熱が 1 のスナップショットが届かないことがあるため熱とは別に運ぶ。</summary>
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
