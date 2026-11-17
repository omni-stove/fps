using System;
using Shooter.Core;

namespace Shooter.Runtime
{
    /// <summary>
    /// キルフィードに出す 1 行。<see cref="KillFeedEntry"/> に、受け取った時点で引いた名前と武器の名前を添える
    /// (後で抜けたプレイヤーや消えた敵でも、出している間は同じ名前のままにするため)。
    /// </summary>
    public readonly struct KillFeedLine : IEquatable<KillFeedLine>
    {

        /// <summary>元の 1 件。</summary>
        public KillFeedEntry Entry { get; }

        /// <summary>倒した側の名前。自滅なら <see cref="Victim"/> と同じ人。</summary>
        public KillFeedName Killer { get; }

        /// <summary>倒された側の名前。</summary>
        public KillFeedName Victim { get; }

        /// <summary>武器の名前 (<see cref="WeaponSpec.Name"/> など)。武器によらない死か、名前が分からなければ <c>default</c>。</summary>
        public LocalizedText WeaponName { get; }

        /// <summary>
        /// この画面のプレイヤーが <see cref="KillFeedEntry.AssisterIds"/> に入っているか (倒した本人ではなくアシストした)。
        /// 倒した側か倒された側がこの画面のプレイヤーなら false。
        /// </summary>
        public bool LocalAssisted { get; }

        /// <param name="entry">元の 1 件。</param>
        /// <param name="killer">倒した側の名前。</param>
        /// <param name="victim">倒された側の名前。</param>
        /// <param name="weaponName">武器の名前。武器によらない死か、名前が分からなければ <c>default</c>。</param>
        /// <param name="localAssisted">この画面のプレイヤーがアシストした行か。killer か victim がこの画面のプレイヤーなら無視して false にする。</param>
        public KillFeedLine(KillFeedEntry entry, KillFeedName killer, KillFeedName victim, LocalizedText weaponName, bool localAssisted = false)
        {
            Entry = entry;
            Killer = killer;
            Victim = victim;
            WeaponName = weaponName;
            LocalAssisted = localAssisted && !killer.IsLocal && !victim.IsLocal;
        }

        /// <summary>この画面のプレイヤーが倒したか倒された行か。</summary>
        public bool Highlighted => Killer.IsLocal || Victim.IsLocal;

        /// <summary>倒した側の名前を出すか。自滅では倒された側だけを出す。</summary>
        public bool ShowsKiller => !Entry.IsSuicide;

        /// <summary>倒した側と倒された側の間に出す文 (<see cref="KillFeedText.Weapon"/>)。</summary>
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

        /// <summary>
        /// ログ用。言語によらない (例: <c>killer=1 victim=2 weapon=rifle headshot=True friendly=False killerName=P1 victimName=P2</c>)。
        /// この画面のプレイヤーがアシストした行だけ、後ろに <c> localAssist=True</c> を付ける。
        /// </summary>
        public override string ToString()
        {
            // アシストしていない行は前と同じ文字列のままにして、それを読むログやテストを変えずに済ませる。
            var assist = LocalAssisted ? " localAssist=True" : string.Empty;
            return Entry + " killerName=" + Killer.Text + " victimName=" + Victim.Text + assist;
        }
    }
}
