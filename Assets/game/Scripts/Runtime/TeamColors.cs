using System.Collections.Generic;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    public static class TeamColors
    {
        // オフラインの Runner はプレイヤーを Alpha に入れ、敵を Bravo にするので、それに合わせる。
        public const Team OfflineViewer = Team.Alpha;

        public static IReadOnlyList<TeamPalette> All => KitTuning.Palettes;

        public static TeamPalette Current => For(AppliedControlSettings.Current.Palette);

        public static TeamPalette For(ColorPalette kind)
        {
            return TeamPalette.For(kind, All);
        }

        public static Color Of(Team? team, Team? viewer)
        {
            return Of(Current, team, viewer);
        }

        public static Color Of(TeamPalette palette, Team? team, Team? viewer)
        {
            return ToColor(palette.ColorOf(team, viewer));
        }

        public static Color Highlight => ToColor(Current.Highlight);

        public static Color Neutral => ToColor(Current.Neutral);

        public static Color ToColor(PaletteColor color)
        {
            return new Color(color.R, color.G, color.B, 1f);
        }
    }
}
