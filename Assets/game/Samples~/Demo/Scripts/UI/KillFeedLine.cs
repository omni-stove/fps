using System;
using Shooter.Core;

namespace Shooter.Runtime
{
    // 名前と武器の名前を受け取った時点で引いて持つのは、後で抜けたプレイヤーや消えた敵でも、出している間は同じ名前のままにするため。
    public readonly struct KillFeedLine : IEquatable<KillFeedLine>
    {

        public KillFeedEntry Entry { get; }

        public KillFeedName Killer { get; }

        public KillFeedName Victim { get; }

        public LocalizedText WeaponName { get; }

        public KillFeedLine(KillFeedEntry entry, KillFeedName killer, KillFeedName victim, LocalizedText weaponName)
        {
            Entry = entry;
            Killer = killer;
            Victim = victim;
            WeaponName = weaponName;
        }

        public bool Highlighted => Killer.IsLocal || Victim.IsLocal;

        public bool ShowsKiller => !Entry.IsSuicide;

        public LocalizedText WeaponText => KillFeedText.Weapon(WeaponName, Entry.Headshot, Entry.IsSuicide);

        public bool Equals(KillFeedLine other)
        {
            return Entry == other.Entry && Killer == other.Killer && Victim == other.Victim
                && WeaponName == other.WeaponName;
        }

        public override bool Equals(object obj)
        {
            return obj is KillFeedLine other && Equals(other);
        }

        public static bool operator ==(KillFeedLine left, KillFeedLine right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(KillFeedLine left, KillFeedLine right)
        {
            return !left.Equals(right);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Entry, Killer, Victim, WeaponName);
        }

        public override string ToString()
        {
            return Entry + " killerName=" + Killer.Text + " victimName=" + Victim.Text;
        }
    }
}
