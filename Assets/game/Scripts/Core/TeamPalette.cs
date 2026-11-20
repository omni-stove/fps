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
            new PaletteColor(1f, 0.65f, 0.1f),
            new PaletteColor(1f, 0.15f, 0.1f),
            new PaletteColor(1f, 0.82f, 0.31f),
            new PaletteColor(0.2f, 0.8f, 0.3f),
            false);

        // 1 型・2 型色覚では赤と緑の軸が潰れ、青と黄の軸は残るので、味方を空色・敵を橙にして明るさも離す (Okabe-Ito の配色)。
        // 強調は橙と明るさの違う黄にする。
        // 目的の物 (明るい黄) と危険 (暗い朱色) は明るさで、競合 (赤紫) と解除済み (青) は青と黄の軸で分ける。

        public static TeamPalette RedGreen { get; } = new TeamPalette(
            ColorPalette.RedGreen,
            PaletteColor.FromBytes(86, 180, 233),
            PaletteColor.FromBytes(230, 159, 0),
            _neutral,
            PaletteColor.FromBytes(240, 228, 66),
            PaletteColor.FromBytes(240, 228, 66),
            PaletteColor.FromBytes(213, 94, 0),
            PaletteColor.FromBytes(204, 121, 167),
            PaletteColor.FromBytes(0, 114, 178),
            true);

        // 3 型色覚では青と黄の軸が潰れ、赤と緑の軸は残るので、味方を青緑・敵を赤にする。強調は敵の赤と明るさの違う桃色にする。
        // 目的の物 (明るい黄緑) と危険 (暗い赤) は明るさで、競合 (淡い赤) と解除済み (暗い緑) は赤と緑の軸と明るさで分ける。

        public static TeamPalette BlueYellow { get; } = new TeamPalette(
            ColorPalette.BlueYellow,
            PaletteColor.FromBytes(40, 200, 180),
            PaletteColor.FromBytes(235, 60, 50),
            _neutral,
            PaletteColor.FromBytes(255, 140, 210),
            PaletteColor.FromBytes(170, 250, 110),
            PaletteColor.FromBytes(220, 30, 30),
            PaletteColor.FromBytes(255, 150, 150),
            PaletteColor.FromBytes(0, 140, 90),
            true);

        private static readonly TeamPalette[] _all = { Standard, RedGreen, BlueYellow };

        public TeamPalette(
            ColorPalette kind,
            PaletteColor friendly,
            PaletteColor enemy,
            PaletteColor neutral,
            PaletteColor highlight,
            PaletteColor objective,
            PaletteColor danger,
            PaletteColor contested,
            PaletteColor safe,
            bool usesMarks)
        {
            if (!Enum.IsDefined(typeof(ColorPalette), kind))
            {
                throw new ArgumentOutOfRangeException(nameof(kind), kind, "unknown color palette");
            }

            Kind = kind;
            Friendly = friendly;
            Enemy = enemy;
            Neutral = neutral;
            Highlight = highlight;
            Objective = objective;
            Danger = danger;
            Contested = contested;
            Safe = safe;
            UsesMarks = usesMarks;
        }

        public static IReadOnlyList<TeamPalette> All { get; } = new ReadOnlyCollection<TeamPalette>(_all);

        public ColorPalette Kind { get; }

        public PaletteColor Friendly { get; }

        public PaletteColor Enemy { get; }

        public PaletteColor Neutral { get; }

        public PaletteColor Highlight { get; }

        // 爆弾 (運ばれている・落ちている) と、まだ爆弾の無い設置場所。
        public PaletteColor Objective { get; }

        // 設置された爆弾・爆弾の置かれた設置場所・縮む円の境界。
        public PaletteColor Danger { get; }

        // 両チームが中にいて取り合いになっている拠点。
        public PaletteColor Contested { get; }

        // 解除された爆弾。
        public PaletteColor Safe { get; }

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

        // Core は Resources を読めないので、palettes.json で差し替えた組は呼ぶ側 (TeamColors) が読んで渡す。For(kind) は既定の組しか返さない。
        public static TeamPalette For(ColorPalette kind, IReadOnlyList<TeamPalette> palettes)
        {
            if (palettes == null)
            {
                throw new ArgumentNullException(nameof(palettes));
            }

            foreach (var palette in palettes)
            {
                if (palette != null && palette.Kind == kind)
                {
                    return palette;
                }
            }

            if (!Enum.IsDefined(typeof(ColorPalette), kind))
            {
                throw new ArgumentOutOfRangeException(nameof(kind), kind, "unknown color palette");
            }

            throw new ArgumentException($"the palettes have no {kind} palette", nameof(palettes));
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
