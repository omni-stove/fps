using System;

namespace Fps.Core
{
    /// <summary>
    /// キルフィードに出す 1 行。<see cref="KillFeedEntry"/> に、受け取った時点で引いた名前と武器の表示名を添える
    /// (後で抜けたプレイヤーや消えた敵でも、出している間は同じ名前のままにするため)。
    /// </summary>
    public readonly struct KillFeedLine : IEquatable<KillFeedLine>
    {
        private readonly string _weaponName;

        /// <summary>元の 1 件。</summary>
        public KillFeedEntry Entry { get; }

        /// <summary>倒した側の名前。自滅なら <see cref="Victim"/> と同じ人。</summary>
        public KillFeedName Killer { get; }

        /// <summary>倒された側の名前。</summary>
        public KillFeedName Victim { get; }

        /// <summary>武器の表示名。武器によらない死か、表示名が分からなければ空文字。null にならない。</summary>
        public string WeaponName => _weaponName ?? string.Empty;

        /// <param name="entry">元の 1 件。</param>
        /// <param name="killer">倒した側の名前。</param>
        /// <param name="victim">倒された側の名前。</param>
        /// <param name="weaponName">武器の表示名。null は空文字。</param>
        public KillFeedLine(KillFeedEntry entry, KillFeedName killer, KillFeedName victim, string weaponName)
        {
            Entry = entry;
            Killer = killer;
            Victim = victim;
            _weaponName = weaponName ?? string.Empty;
        }

        /// <summary>この画面のプレイヤーが倒したか倒された行か。</summary>
        public bool Highlighted => Killer.IsLocal || Victim.IsLocal;

        /// <summary>倒した側の名前を出すか。自滅では倒された側だけを出す。</summary>
        public bool ShowsKiller => !Entry.IsSuicide;

        /// <summary>倒した側と倒された側の間に出す文字列 (<see cref="KillFeedFormat.Weapon"/>)。</summary>
        public string WeaponText => KillFeedFormat.Weapon(WeaponName, Entry.Headshot, Entry.IsSuicide);

        public bool Equals(KillFeedLine other)
        {
            return Entry == other.Entry && Killer == other.Killer && Victim == other.Victim
                && string.Equals(WeaponName, other.WeaponName, StringComparison.Ordinal);
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
            return HashCode.Combine(Entry, Killer, Victim, StringComparer.Ordinal.GetHashCode(WeaponName));
        }

        /// <summary><see cref="KillFeedFormat.Line"/> と同じ文字列。</summary>
        public override string ToString()
        {
            return KillFeedFormat.Line(this);
        }
    }
}
