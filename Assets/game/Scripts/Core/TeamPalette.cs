using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Fps.Core
{
    /// <summary>
    /// 敵味方の色の組。<see cref="ColorPalette"/> ごとに味方・敵・中立・強調の色と、ネームプレートに付ける形の印を持つ。不変。
    /// キャラクター・ネームプレート・キルログ・拠点・HUD の点数は <see cref="ColorOf"/> で、頭への命中のような注意を引く印は <see cref="Highlight"/> で塗る。
    /// 色を見分けにくい人向けの組 (<see cref="ColorPalette.RedGreen"/>・<see cref="ColorPalette.BlueYellow"/>) は、
    /// 色だけに頼らないよう <see cref="MarkOf"/> で味方と敵に違う形の印を付ける。
    /// </summary>
    public sealed class TeamPalette
    {
        /// <summary>色を見分けにくい人向けの組で、味方のネームプレートに付ける印。</summary>
        public const string FriendlyMark = "◆";

        /// <summary>色を見分けにくい人向けの組で、敵のネームプレートに付ける印。</summary>
        public const string EnemyMark = "▲";

        private static readonly PaletteColor _neutral = new PaletteColor(0.85f, 0.85f, 0.85f);

        /// <summary>味方は青・敵は赤・強調は赤。今までの見た目と同じ色。印は付けない。</summary>
        public static TeamPalette Standard { get; } = new TeamPalette(
            ColorPalette.Standard,
            new PaletteColor(0.25f, 0.5f, 1f),
            new PaletteColor(1f, 0.3f, 0.25f),
            _neutral,
            PaletteColor.FromBytes(255, 70, 60),
            false);

        // 1 型・2 型色覚では赤と緑の軸が潰れ、青と黄の軸は残るので、味方を空色・敵を橙にして明るさも離す (Okabe-Ito の配色)。
        // 強調は橙と明るさの違う黄にする。

        /// <summary>赤と緑を見分けにくい人向け。味方は空色・敵は橙・強調は黄。</summary>
        public static TeamPalette RedGreen { get; } = new TeamPalette(
            ColorPalette.RedGreen,
            PaletteColor.FromBytes(86, 180, 233),
            PaletteColor.FromBytes(230, 159, 0),
            _neutral,
            PaletteColor.FromBytes(240, 228, 66),
            true);

        // 3 型色覚では青と黄の軸が潰れ、赤と緑の軸は残るので、味方を青緑・敵を赤にする。強調は敵の赤と明るさの違う桃色にする。

        /// <summary>青と黄を見分けにくい人向け。味方は青緑・敵は赤・強調は桃色。</summary>
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

        /// <summary>すべての組を、設定画面で並べる順に返す。</summary>
        public static IReadOnlyList<TeamPalette> All { get; } = new ReadOnlyCollection<TeamPalette>(_all);

        /// <summary>この組の種類。</summary>
        public ColorPalette Kind { get; }

        /// <summary>味方の色。</summary>
        public PaletteColor Friendly { get; }

        /// <summary>敵の色。</summary>
        public PaletteColor Enemy { get; }

        /// <summary>どのチームのものでもないものの色。</summary>
        public PaletteColor Neutral { get; }

        /// <summary>頭への命中など、注意を引く印の色。</summary>
        public PaletteColor Highlight { get; }

        /// <summary>ネームプレートに形の印 (<see cref="MarkOf"/>) を付けるか。</summary>
        public bool UsesMarks { get; }

        /// <summary>kind の組。</summary>
        /// <exception cref="ArgumentOutOfRangeException">kind が定義済みの値でない。</exception>
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

        /// <summary>
        /// viewer のチームから見た team の立場。team が null (持ち主のいない拠点など) なら <see cref="TeamRelation.Neutral"/>。
        /// viewer が null (観戦だけで自分のプレイヤーがいないなど) なら、2 チームを見分けられるよう Alpha を味方・Bravo を敵とみなす。
        /// </summary>
        public static TeamRelation RelationOf(Team? team, Team? viewer)
        {
            if (!team.HasValue)
            {
                return TeamRelation.Neutral;
            }

            var reference = viewer ?? Team.Alpha;
            return team.Value == reference ? TeamRelation.Friendly : TeamRelation.Enemy;
        }

        /// <summary>relation の色。</summary>
        /// <exception cref="ArgumentOutOfRangeException">relation が定義済みの値でない。</exception>
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

        /// <summary>viewer から見た team の色 (<see cref="RelationOf"/> と <see cref="ColorOf(TeamRelation)"/>)。</summary>
        public PaletteColor ColorOf(Team? team, Team? viewer)
        {
            return ColorOf(RelationOf(team, viewer));
        }

        /// <summary>
        /// ネームプレートで名前の前に付ける relation の印。<see cref="UsesMarks"/> が false の組と中立には付けず、空文字を返す。
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">relation が定義済みの値でない。</exception>
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

        /// <summary>ネームプレートに出す文字。印があれば「印 名前」、無ければ名前だけ。name が null なら空文字として扱う。</summary>
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
