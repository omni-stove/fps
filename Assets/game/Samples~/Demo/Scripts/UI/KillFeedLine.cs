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

        public bool LocalAssisted { get; }

        public KillFeedLine(KillFeedEntry entry, KillFeedName killer, KillFeedName victim, LocalizedText weaponName, bool localAssisted = false)
        {
            Entry = entry;
            Killer = killer;
            Victim = victim;
            WeaponName = weaponName;
            LocalAssisted = localAssisted && !killer.IsLocal && !victim.IsLocal;
        }

        public bool Highlighted => Killer.IsLocal || Victim.IsLocal;

        public bool ShowsKiller => !Entry.IsSuicide;

        public LocalizedText WeaponText => KillFeedText.Weapon(WeaponName, Entry.Headshot, Entry.IsSuicide);

        public bool Equals(KillFeedLine other)
        {
            return Entry == other.Entry && Killer == other.Killer && Victim == other.Victim
                && WeaponName == other.WeaponName && LocalAssisted == other.LocalAssisted;
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
            return HashCode.Combine(Entry, Killer, Victim, WeaponName, LocalAssisted);
        }

        public override string ToString()
        {
            // アシストしていない行は前と同じ文字列のままにして、それを読むログやテストを変えずに済ませる。
            var assist = LocalAssisted ? " localAssist=True" : string.Empty;
            return Entry + " killerName=" + Killer.Text + " victimName=" + Victim.Text + assist;
        }
    }
}
