using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Shooter.Core
{
    // 色を見分けにくい人向けの組 (ColorPalette.RedGreen・ColorPalette.BlueYellow) は、
    // 色だけに頼らないよう MarkOf で味方と敵に違う形の印を付ける。
    public sealed class TeamPalette
    {
        public const string FriendlyMark = "◆";

        public const string EnemyMark = "▲";

        private static readonly PaletteColor _neutral = new PaletteColor(0.85f, 0.85f, 0.85f);

        // 今までの見た目と同じ色にする。
        public static TeamPalette Standard { get; } = new TeamPalette(
            ColorPalette.Standard,
            new PaletteColor(0.25f, 0.5f, 1f),
            new PaletteColor(1f, 0.3f, 0.25f),
            _neutral,
            PaletteColor.FromBytes(255, 70, 60),
            false);

        // 1 型・2 型色覚では赤と緑の軸が潰れ、青と黄の軸は残るので、味方を空色・敵を橙にして明るさも離す (Okabe-Ito の配色)。
        // 強調は橙と明るさの違う黄にする。

        public static TeamPalette RedGreen { get; } = new TeamPalette(
            ColorPalette.RedGreen,
            PaletteColor.FromBytes(86, 180, 233),
            PaletteColor.FromBytes(230, 159, 0),
            _neutral,
            PaletteColor.FromBytes(240, 228, 66),
            true);

        // 3 型色覚では青と黄の軸が潰れ、赤と緑の軸は残るので、味方を青緑・敵を赤にする。強調は敵の赤と明るさの違う桃色にする。

        public static TeamPalette BlueYellow { get; } = new TeamPalette(
            ColorPalette.BlueYellow,
            PaletteColor.FromBytes(40, 200, 180),
            PaletteColor.FromBytes(235, 60, 50),
            _neutral,
            PaletteColor.FromBytes(255, 140, 210),
            true);

        private static readonly TeamPalette[] _all = { Standard, RedGreen, BlueYellow };

        private TeamPalette(
            ColorPalette kind,
            PaletteColor friendly,
            PaletteColor enemy,
            PaletteColor neutral,
            PaletteColor highlight,
            bool usesMarks)
        {
            Kind = kind;
            Friendly = friendly;
            Enemy = enemy;
            Neutral = neutral;
            Highlight = highlight;
            UsesMarks = usesMarks;
        }

        public static IReadOnlyList<TeamPalette> All { get; } = new ReadOnlyCollection<TeamPalette>(_all);

        public ColorPalette Kind { get; }

        public PaletteColor Friendly { get; }

        public PaletteColor Enemy { get; }

        public PaletteColor Neutral { get; }

        public PaletteColor Highlight { get; }

        public bool UsesMarks { get; }

        public static TeamPalette For(ColorPalette kind)
        {
            switch (kind)
            {
                case ColorPalette.Standard:
                    return Standard;
                case ColorPalette.RedGreen:
                    return RedGreen;
                case ColorPalette.BlueYellow:
                    return BlueYellow;
                default:
                    throw new ArgumentOutOfRangeException(nameof(kind), kind, "unknown color palette");
            }
        }

        // viewer が null (観戦だけで自分のプレイヤーがいないなど) でも 2 チームを見分けられるよう、Alpha を味方・Bravo を敵とみなす。
        public static TeamRelation RelationOf(Team? team, Team? viewer)
        {
            if (!team.HasValue)
            {
                return TeamRelation.Neutral;
            }

            var reference = viewer ?? Team.Alpha;
            return team.Value == reference ? TeamRelation.Friendly : TeamRelation.Enemy;
        }

        public PaletteColor ColorOf(TeamRelation relation)
        {
            switch (relation)
            {
                case TeamRelation.Friendly:
                    return Friendly;
                case TeamRelation.Enemy:
                    return Enemy;
                case TeamRelation.Neutral:
                    return Neutral;
                default:
                    throw new ArgumentOutOfRangeException(nameof(relation), relation, "unknown team relation");
            }
        }

        public PaletteColor ColorOf(Team? team, Team? viewer)
        {
            return ColorOf(RelationOf(team, viewer));
        }

        public string MarkOf(TeamRelation relation)
        {
            switch (relation)
            {
                case TeamRelation.Friendly:
                    return UsesMarks ? FriendlyMark : string.Empty;
                case TeamRelation.Enemy:
                    return UsesMarks ? EnemyMark : string.Empty;
                case TeamRelation.Neutral:
                    return string.Empty;
                default:
                    throw new ArgumentOutOfRangeException(nameof(relation), relation, "unknown team relation");
            }
        }

        public string NameplateText(TeamRelation relation, string name)
        {
            var mark = MarkOf(relation);
            name = name ?? string.Empty;
            return mark.Length > 0 ? mark + " " + name : name;
        }

        public override string ToString()
        {
            return Kind.ToString();
        }
    }
}
