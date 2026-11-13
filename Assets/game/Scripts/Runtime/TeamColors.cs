using Fps.Core;
using UnityEngine;

namespace Fps.Runtime
{
    /// <summary>
    /// 敵味方の色を、今効いている設定の色の組 (<see cref="ControlSettings.Palette"/>) から Unity の <see cref="Color"/> で引く。
    /// 組が変わったときは <see cref="AppliedControlSettings.Changed"/> で知り、塗った側が塗り直す。
    /// </summary>
    public static class TeamColors
    {
        /// <summary>
        /// オフラインのモードで画面のプレイヤーが入るチーム。オフラインの Runner はプレイヤーを Alpha に入れ、敵を Bravo にする。
        /// </summary>
        public const Team OfflineViewer = Team.Alpha;

        /// <summary>今効いている設定の色の組。</summary>
        public static TeamPalette Current => TeamPalette.For(AppliedControlSettings.Current.Palette);

        /// <summary>今の組で、viewer のチームから見た team の色。team が null なら中立、viewer が null なら Alpha を味方とみなす。</summary>
        public static Color Of(Team? team, Team? viewer)
        {
            return Of(Current, team, viewer);
        }

        /// <summary>palette で、viewer のチームから見た team の色。</summary>
        public static Color Of(TeamPalette palette, Team? team, Team? viewer)
        {
            return ToColor(palette.ColorOf(team, viewer));
        }

        /// <summary>今の組の、頭への命中など注意を引く印の色。</summary>
        public static Color Highlight => ToColor(Current.Highlight);

        /// <summary>今の組の中立の色。</summary>
        public static Color Neutral => ToColor(Current.Neutral);

        /// <summary>Core の色を不透明の <see cref="Color"/> にする。</summary>
        public static Color ToColor(PaletteColor color)
        {
            return new Color(color.R, color.G, color.B, 1f);
        }
    }
}
