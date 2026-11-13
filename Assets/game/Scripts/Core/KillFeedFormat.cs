using System.Text;

namespace Fps.Core
{
    /// <summary>キルフィードの文字列の書式。</summary>
    public static class KillFeedFormat
    {
        /// <summary>止めの弾が頭に当たった行に、武器の後ろへ付ける印。</summary>
        public const string HeadshotMark = "頭";

        /// <summary>味方撃ちの行の先頭に出す印。</summary>
        public const string FriendlyMark = "味方撃ち";

        /// <summary>武器によらない自滅 (落下・跳ね返った味方撃ちなど) の武器の欄。</summary>
        public const string Suicide = "自滅";

        /// <summary>自滅でなく武器も分からない死 (出血など) の武器の欄。</summary>
        public const string NoWeapon = "-";

        /// <summary>オンラインのプレイヤーの名前 (<c>P&lt;id&gt;</c>)。名前を同期していないので Id で呼ぶ。</summary>
        public static string Player(int playerId)
        {
            return "P" + playerId;
        }

        /// <summary>名前の分からない相手 (<c>?&lt;id&gt;</c>)。</summary>
        public static string Unknown(int id)
        {
            return "?" + id;
        }

        /// <summary>
        /// 倒した側と倒された側の間に出す武器の欄 (<c>[ライフル]</c>、頭なら <c>[ライフル] 頭</c>)。
        /// weaponName が空なら、自滅は <see cref="Suicide"/>、それ以外は <see cref="NoWeapon"/> を入れる。
        /// </summary>
        public static string Weapon(string weaponName, bool headshot, bool suicide)
        {
            var name = !string.IsNullOrEmpty(weaponName) ? weaponName : suicide ? Suicide : NoWeapon;
            var text = "[" + name + "]";
            return headshot ? text + " " + HeadshotMark : text;
        }

        /// <summary>1 行を 1 つの文字列にする (ログ用)。例: <c>味方撃ち P1 [ライフル] 頭 P2</c>、自滅は <c>[自滅] P1</c>。</summary>
        public static string Line(KillFeedLine line)
        {
            var builder = new StringBuilder();
            if (line.Entry.Friendly)
            {
                builder.Append(FriendlyMark).Append(' ');
            }

            if (line.ShowsKiller)
            {
                builder.Append(line.Killer.Text).Append(' ');
            }

            builder.Append(line.WeaponText).Append(' ').Append(line.Victim.Text);
            return builder.ToString();
        }
    }
}
