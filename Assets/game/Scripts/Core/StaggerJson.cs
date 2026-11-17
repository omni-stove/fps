using System;
using System.Collections.Generic;

namespace Fps.Core
{
    /// <summary>
    /// よろめきの調整値 (<see cref="StaggerTuning"/>) を JSON から読む。最上位は <see cref="PlayerItem"/> と <see cref="EnemyItem"/> のオブジェクトで、
    /// 中の項目の名前は <see cref="StaggerSpec"/> のコンストラクターの引数名 (<see cref="PlayerItemNames"/>・<see cref="EnemyItemNames"/>)。
    /// 省略した項目・オブジェクトは <see cref="StaggerSpec.PlayerDefault"/>・<see cref="StaggerSpec.EnemyDefault"/> の値になる。
    /// 文法は <see cref="TuningJson"/> と同じで、値の範囲の検査は <see cref="StaggerSpec"/> に任せ、
    /// どれかに引っかかればどのファイルのどの項目 (player.flinchSeconds など) かを添えた <see cref="TuningJsonException"/> を投げる。
    /// </summary>
    public static class StaggerJson
    {
        public const string PlayerItem = "player";
        public const string EnemyItem = "enemy";

        public const string FlinchSecondsItem = "flinchSeconds";
        public const string KneelSecondsItem = "kneelSeconds";
        public const string StumbleSecondsItem = "stumbleSeconds";
        public const string BlastSecondsItem = "blastSeconds";
        public const string HeadDamageItem = "headDamage";
        public const string LimbDamageItem = "limbDamage";
        public const string MeleeDamageItem = "meleeDamage";
        public const string ExplosionDamageItem = "explosionDamage";
        public const string WindowSecondsItem = "windowSeconds";
        public const string ImmunitySecondsItem = "immunitySeconds";
        public const string MoveMultiplierItem = "moveMultiplier";
        public const string TurnMultiplierItem = "turnMultiplier";

        private const string _kind = "stagger JSON";

        public static IReadOnlyList<string> ItemNames { get; } = new[] { PlayerItem, EnemyItem };

        // 敵は怯みの間 EnemyAgent が止まるので、歩きと視点の倍率は使わない。書けても効かない項目は受け付けない。
        public static IReadOnlyList<string> EnemyItemNames { get; } = new[]
        {
            FlinchSecondsItem,
            KneelSecondsItem,
            StumbleSecondsItem,
            BlastSecondsItem,
            HeadDamageItem,
            LimbDamageItem,
            MeleeDamageItem,
            ExplosionDamageItem,
            WindowSecondsItem,
            ImmunitySecondsItem,
        };

        public static IReadOnlyList<string> PlayerItemNames { get; } = new[]
        {
            FlinchSecondsItem,
            KneelSecondsItem,
            StumbleSecondsItem,
            BlastSecondsItem,
            HeadDamageItem,
            LimbDamageItem,
            MeleeDamageItem,
            ExplosionDamageItem,
            WindowSecondsItem,
            ImmunitySecondsItem,
            MoveMultiplierItem,
            TurnMultiplierItem,
        };

        /// <summary>text をプレイヤーと敵のよろめきの調整値として読む。</summary>
        /// <param name="text">JSON の文字列。先頭の BOM は読み飛ばす。</param>
        /// <param name="sourceName">例外の文に出す読み込み元の名前 (ファイルのパスなど)。</param>
        /// <exception cref="ArgumentNullException">text が null。</exception>
        /// <exception cref="TuningJsonException">
        /// JSON の文法に合わない、知らない項目がある、型が合わない、または値が <see cref="StaggerSpec"/> の検査に通らない。
        /// </exception>
        public static StaggerTuning Parse(string text, string sourceName)
        {
            var source = ServerDataJson.SourceOf(sourceName, _kind);
            try
            {
                var root = new ServerDataJson.Fields(source, _kind, ServerDataJson.ParseDocument(text, source, _kind), string.Empty, ItemNames);
                return new StaggerTuning(
                    Read(root.Object(PlayerItem, PlayerItemNames), StaggerSpec.PlayerDefault, true),
                    Read(root.Object(EnemyItem, EnemyItemNames), StaggerSpec.EnemyDefault, false));
            }
            catch (ServerDataJsonException e)
            {
                var inner = e.InnerException is ArgumentException rejected ? (Exception)rejected : e;
                throw new TuningJsonException(e.SourceName, e.Item, e.Message, inner);
            }
        }

        private static StaggerSpec Read(ServerDataJson.Fields fields, StaggerSpec defaults, bool withMultipliers)
        {
            if (fields == null)
            {
                return defaults;
            }

            var flinch = fields.ReadFloat(FlinchSecondsItem, defaults.FlinchSeconds);
            var kneel = fields.ReadFloat(KneelSecondsItem, defaults.KneelSeconds);
            var stumble = fields.ReadFloat(StumbleSecondsItem, defaults.StumbleSeconds);
            var blast = fields.ReadFloat(BlastSecondsItem, defaults.BlastSeconds);
            var head = fields.ReadFloat(HeadDamageItem, defaults.HeadDamage);
            var limb = fields.ReadFloat(LimbDamageItem, defaults.LimbDamage);
            var melee = fields.ReadFloat(MeleeDamageItem, defaults.MeleeDamage);
            var explosion = fields.ReadFloat(ExplosionDamageItem, defaults.ExplosionDamage);
            var window = fields.ReadFloat(WindowSecondsItem, defaults.WindowSeconds);
            var immunity = fields.ReadFloat(ImmunitySecondsItem, defaults.ImmunitySeconds);
            var move = withMultipliers ? fields.ReadFloat(MoveMultiplierItem, defaults.MoveMultiplier) : defaults.MoveMultiplier;
            var turn = withMultipliers ? fields.ReadFloat(TurnMultiplierItem, defaults.TurnMultiplier) : defaults.TurnMultiplier;
            try
            {
                return new StaggerSpec(flinch, kneel, stumble, blast, head, limb, melee, explosion, window, immunity, move, turn);
            }
            catch (ArgumentException e)
            {
                // StaggerSpec は引数名を ParamName に入れて投げ、引数名は項目名と同じなので、そのまま項目の場所にできる。
                var item = e.ParamName != null && Contains(withMultipliers ? PlayerItemNames : EnemyItemNames, e.ParamName)
                    ? fields.ItemPath(e.ParamName)
                    : fields.Path;
                throw fields.Rejected(item, e.Message, e);
            }
        }

        private static bool Contains(IReadOnlyList<string> names, string name)
        {
            foreach (var candidate in names)
            {
                if (string.Equals(candidate, name, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
