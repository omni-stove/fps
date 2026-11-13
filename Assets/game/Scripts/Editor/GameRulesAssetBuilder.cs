using System;
using System.Collections.Generic;
using Fps.Core;
using Fps.Runtime;
using UnityEditor;
using UnityEngine;

namespace Fps.Editor
{
    /// <summary>
    /// モード別のルールのアセット (<see cref="Folder"/> の <see cref="GameRulesDefinition"/> 7 つ) を作るか上書きする。
    /// 既存のアセットは GUID を保って中身だけ更新するので、参照しているシーンやプレハブは壊れない。
    /// どのモードもリスポーン直後の無敵なし・落下ダメージなし・HUD はすべて出す (<see cref="GameRules"/> の既定)。
    /// アクションは、チームデスマッチとドミネーションがダウン (蘇生あり) とアクティブリロード、キャンペーンと射撃場がカバー・ブラインドファイア・アクティブリロード、
    /// TPS 射撃場がそれに回避・乗り越え・グレネードを足したもの。
    /// 出血・ダウン中の体力・蘇生の秒数はどのモードも <see cref="ActionRules.Default"/> の値。
    /// 何度実行しても同じ結果になる。値が不正なら保存せずに例外を投げる (batchmode では Unity が終了コード 1 で終わる)。
    /// Editor を閉じた状態で次のように呼べる:
    /// <code>unity -batchmode -quit -projectPath . -executeMethod Fps.Editor.GameRulesAssetBuilder.Build</code>
    /// </summary>
    public static class GameRulesAssetBuilder
    {
        public const string Folder = "Assets/game/Rules";
        public const string TeamDeathmatchPath = Folder + "/TeamDeathmatch.asset";
        public const string CampaignEasyPath = Folder + "/CampaignEasy.asset";
        public const string CampaignNormalPath = Folder + "/CampaignNormal.asset";
        public const string CampaignHardPath = Folder + "/CampaignHard.asset";
        public const string RangePath = Folder + "/Range.asset";
        public const string TpsRangePath = Folder + "/TpsRange.asset";
        public const string DominationPath = Folder + "/Domination.asset";

        /// <summary><see cref="TpsRangeActions"/> で出撃ごとに持つグレネードの数。</summary>
        public const int TpsRangeGrenadeCount = 3;

        public const string MenuPath = "Fps/Build Game Rules Assets";
        public const string LogTag = "[GameRulesAssetBuilder]";

        /// <summary>
        /// チームデスマッチ。味方撃ちなし、個別リスポーン、4 秒後から毎秒 20 回復、体は倒れる、倒した相手を映す、予備弾は有限で 1 倍、
        /// 建造物は弾で壊れる。アクションは <see cref="TeamDeathmatchActions"/>。
        /// </summary>
        public static GameRules TeamDeathmatchRules()
        {
            return new GameRules(
                FriendlyFire.Off,
                RespawnMode.Individual,
                GameRules.DefaultWaveIntervalSeconds,
                4f,
                20f,
                DeathBody.Collapse,
                DeathCamera.Killer,
                false,
                1f,
                Destruction.On,
                TeamDeathmatchActions());
        }

        /// <summary>
        /// キャンペーン (易)。味方撃ちなし、個別リスポーン、3 秒後から毎秒 25 回復、体は倒れる、死亡カメラなし、予備弾は有限で 1.5 倍、
        /// 建造物は壊れない。アクションは <see cref="CampaignActions"/>。
        /// </summary>
        public static GameRules CampaignEasyRules()
        {
            return new GameRules(
                FriendlyFire.Off,
                RespawnMode.Individual,
                GameRules.DefaultWaveIntervalSeconds,
                3f,
                25f,
                DeathBody.Collapse,
                DeathCamera.None,
                false,
                1.5f,
                Destruction.Off,
                CampaignActions());
        }

        /// <summary>
        /// キャンペーン (普通)。<see cref="GameRules.Default"/> のうち、5 秒後から毎秒 15 回復、体は倒れる、予備弾は有限で 1 倍にしたもの。建造物は壊れない。
        /// アクションは <see cref="CampaignActions"/>。
        /// </summary>
        public static GameRules CampaignNormalRules()
        {
            return WithOverrides(GameRules.Default, DeathBody.Collapse, 5f, 15f, 1f, CampaignActions());
        }

        /// <summary>
        /// キャンペーン (難)。<see cref="GameRules.Default"/> のうち、7 秒後から毎秒 10 回復、体は倒れる、予備弾は有限で 0.6 倍にしたもの。建造物は壊れない。
        /// アクションは <see cref="CampaignActions"/>。
        /// </summary>
        public static GameRules CampaignHardRules()
        {
            return WithOverrides(GameRules.Default, DeathBody.Collapse, 7f, 10f, 0.6f, CampaignActions());
        }

        /// <summary>
        /// 射撃場。味方撃ちなし、個別リスポーン、4 秒後から毎秒 20 回復、体は消える、死亡カメラなし、予備弾は無限、建造物は弾で壊れる。
        /// アクションは <see cref="RangeActions"/>。
        /// </summary>
        public static GameRules RangeRules()
        {
            return new GameRules(
                FriendlyFire.Off,
                RespawnMode.Individual,
                GameRules.DefaultWaveIntervalSeconds,
                4f,
                20f,
                DeathBody.Vanish,
                DeathCamera.None,
                true,
                1f,
                Destruction.On,
                RangeActions());
        }

        /// <summary>
        /// TPS 射撃場。<see cref="RangeRules"/> (予備弾は無限) のアクションを <see cref="TpsRangeActions"/> にしたもの。
        /// </summary>
        public static GameRules TpsRangeRules()
        {
            return RangeRules().WithActions(TpsRangeActions());
        }

        /// <summary>
        /// ドミネーション。今は <see cref="TeamDeathmatchRules"/> と同じ値 (アクションも <see cref="TeamDeathmatchActions"/>)。
        /// チームデスマッチの値を後で変えてもドミネーションが付いて変わるよう、値を写さずに呼び出して作る。
        /// </summary>
        public static GameRules DominationRules()
        {
            return TeamDeathmatchRules();
        }

        /// <summary>
        /// TPS 射撃場のアクション。<see cref="RangeActions"/> に回避・乗り越え・グレネード (<see cref="TpsRangeGrenadeCount"/> 個) を足したもの。
        /// </summary>
        public static ActionRules TpsRangeActions()
        {
            return RangeActions()
                .WithDodge(Dodge.On)
                .WithVault(Vault.On)
                .WithGrenade(Grenade.On)
                .WithGrenadeCount(TpsRangeGrenadeCount);
        }

        /// <summary>
        /// チームデスマッチのアクション。倒れたらダウンして味方が起こせ、アクティブリロードを使う。
        /// カバーとブラインドファイアはオンラインのプレイヤーが対応していないので使わない。
        /// </summary>
        public static ActionRules TeamDeathmatchActions()
        {
            return WithDefaultDownedTimings(Downed.Revive, Cover.Off, BlindFire.Off, ActiveReload.On);
        }

        /// <summary>キャンペーン (どの難易度も) のアクション。カバー・ブラインドファイア・アクティブリロードを使い、ダウンはしない。</summary>
        public static ActionRules CampaignActions()
        {
            return WithDefaultDownedTimings(Downed.Off, Cover.On, BlindFire.On, ActiveReload.On);
        }

        /// <summary>射撃場のアクション。カバー・ブラインドファイア・アクティブリロードを使い、ダウンはしない。</summary>
        public static ActionRules RangeActions()
        {
            return WithDefaultDownedTimings(Downed.Off, Cover.On, BlindFire.On, ActiveReload.On);
        }

        /// <summary>path の <see cref="GameRulesDefinition"/> を読み、<see cref="GameRulesDefinition.ToRules"/> に通ることを確かめて返す。</summary>
        public static GameRulesDefinition LoadRules(string path)
        {
            path = SceneBuildHelpers.ValidateAssetPath(path);

            var definition = AssetDatabase.LoadAssetAtPath<GameRulesDefinition>(path);
            if (definition == null)
            {
                throw new InvalidOperationException(
                    $"game rules '{path}' were not found; run {MenuPath} (Fps.Editor.GameRulesAssetBuilder.Build) first");
            }

            definition.ToRules();
            return definition;
        }

        [MenuItem(MenuPath)]
        public static void Build()
        {
            BuildAt(
                TeamDeathmatchPath, CampaignEasyPath, CampaignNormalPath, CampaignHardPath, RangePath, TpsRangePath, DominationPath);
        }

        /// <summary>
        /// 各パスにモード別のルールを作るか上書きして保存し、
        /// teamDeathmatch・campaignEasy・campaignNormal・campaignHard・range・tpsRange・domination の順に返す。
        /// 7 つのパスは互いに異なる Assets/ 以下の .asset であること。
        /// </summary>
        public static GameRulesDefinition[] BuildAt(
            string teamDeathmatchPath,
            string campaignEasyPath,
            string campaignNormalPath,
            string campaignHardPath,
            string rangePath,
            string tpsRangePath,
            string dominationPath)
        {
            var entries = new[]
            {
                (Path: SceneBuildHelpers.ValidateAssetPath(teamDeathmatchPath), Rules: TeamDeathmatchRules()),
                (Path: SceneBuildHelpers.ValidateAssetPath(campaignEasyPath), Rules: CampaignEasyRules()),
                (Path: SceneBuildHelpers.ValidateAssetPath(campaignNormalPath), Rules: CampaignNormalRules()),
                (Path: SceneBuildHelpers.ValidateAssetPath(campaignHardPath), Rules: CampaignHardRules()),
                (Path: SceneBuildHelpers.ValidateAssetPath(rangePath), Rules: RangeRules()),
                (Path: SceneBuildHelpers.ValidateAssetPath(tpsRangePath), Rules: TpsRangeRules()),
                (Path: SceneBuildHelpers.ValidateAssetPath(dominationPath), Rules: DominationRules()),
            };

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in entries)
            {
                if (!seen.Add(entry.Path))
                {
                    throw new ArgumentException($"game rules asset path '{entry.Path}' is used more than once");
                }
            }

            var built = new GameRulesDefinition[entries.Length];
            for (var i = 0; i < entries.Length; i++)
            {
                var rules = entries[i].Rules;
                built[i] = SceneBuildHelpers.CreateOrUpdateAsset<GameRulesDefinition>(entries[i].Path, d => Configure(d, rules));
            }

            Debug.Log($"{LogTag} saved {string.Join(", ", Array.ConvertAll(entries, e => "'" + e.Path + "'"))}");
            return built;
        }

        /// <summary>definition の中身を rules の値にし、<see cref="GameRulesDefinition.ToRules"/> に通ることを確かめる。</summary>
        public static void Configure(GameRulesDefinition definition, GameRules rules)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            if (rules == null)
            {
                throw new ArgumentNullException(nameof(rules));
            }

            definition.CopyFrom(rules);

            // rules は検査済みだが、アセットの形で読み直せることも確かめる。不正なら保存しない。
            definition.ToRules();
        }

        private static ActionRules WithDefaultDownedTimings(Downed downed, Cover cover, BlindFire blindFire, ActiveReload activeReload)
        {
            var defaults = ActionRules.Default;
            return new ActionRules(
                downed,
                defaults.DownedBleedoutSeconds,
                defaults.DownedHealthFraction,
                defaults.ReviveSeconds,
                cover,
                blindFire,
                activeReload);
        }

        private static GameRules WithOverrides(
            GameRules source,
            DeathBody deathBody,
            float regenDelaySeconds,
            float regenPerSecond,
            float reserveAmmoMultiplier,
            ActionRules actions)
        {
            return new GameRules(
                source.FriendlyFire,
                source.RespawnMode,
                source.WaveIntervalSeconds,
                regenDelaySeconds,
                regenPerSecond,
                deathBody,
                source.DeathCamera,
                source.InfiniteReserve,
                reserveAmmoMultiplier,
                Destruction.Off,
                actions,
                source.SpawnProtectionSeconds,
                source.FallDamage,
                source.HudDensity);
        }
    }
}
