using Shooter.Core;

namespace Shooter.Runtime
{
    /// <summary>
    /// キルフィードの行に出す文。言葉の入る印と武器の欄は表のキー <c>hud.killFeed.*</c> の <see cref="LocalizedText"/> で返し、
    /// <see cref="KillFeedView"/> が今の言語で文にする。
    /// </summary>
    public static class KillFeedText
    {
        /// <summary>止めの弾が頭に当たった行に、武器の後ろへ付ける印。</summary>
        public static LocalizedText HeadshotMark => new LocalizedText("hud.killFeed.headshot");

        /// <summary>味方撃ちの行の先頭に出す印。</summary>
        public static LocalizedText FriendlyMark => new LocalizedText("hud.killFeed.friendly");

        /// <summary>武器によらない自滅 (落下・跳ね返った味方撃ちなど) の武器の欄。</summary>
        public static LocalizedText Suicide => new LocalizedText("hud.killFeed.suicide");

        /// <summary>自滅でなく武器も分からない死 (出血など) の武器の欄。</summary>
        public const string NoWeapon = "-";

        /// <summary>名前の分からない相手 (<c>?&lt;id&gt;</c>)。</summary>
        public static string Unknown(int id)
        {
            return "?" + id;
        }

        /// <summary>
        /// 倒した側と倒された側の間に出す武器の欄 (<c>[ライフル]</c>、頭なら <c>[ライフル] 頭</c>)。
        /// 武器の名前 weaponName は文にするときに同じ表で引く。<c>default</c> なら、自滅は <see cref="Suicide"/>、それ以外は <see cref="NoWeapon"/> を入れる。
        /// </summary>
        public static LocalizedText Weapon(LocalizedText weaponName, bool headshot, bool suicide)
        {
            object name = !weaponName.IsEmpty ? weaponName : suicide ? (object)Suicide : NoWeapon;
            return headshot
                ? new LocalizedText("hud.killFeed.weaponHeadshot", name, HeadshotMark)
                : new LocalizedText("hud.killFeed.weapon", name);
        }
    }
}
