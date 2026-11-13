using System;
using System.Collections.Generic;
using Fps.Core;
using Fps.Runtime;
using UnityEditor;
using UnityEngine;

namespace Fps.Editor
{
    /// <summary>
    /// モード別のルールのアセット (<see cref="Folder"/> の <see cref="GameRulesDefinition"/> 5 つ) を作るか上書きする。
    /// 既存のアセットは GUID を保って中身だけ更新するので、参照しているシーンやプレハブは壊れない。
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

        public const string MenuPath = "Fps/Build Game Rules Assets";
        public const string LogTag = "[GameRulesAssetBuilder]";

        /// <summary>チームデスマッチ。味方撃ちなし、個別リスポーン、4 秒後から毎秒 20 回復、体は倒れる、倒した相手を映す、予備弾は有限で 1 倍。</summary>
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
                Destruction.Off);
        }

        /// <summary>キャンペーン (易)。味方撃ちなし、個別リスポーン、3 秒後から毎秒 25 回復、体は倒れる、死亡カメラなし、予備弾は有限で 1.5 倍。</summary>
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
                Destruction.Off);
        }

        /// <summary>キャンペーン (普通)。<see cref="GameRules.Default"/> のうち、体だけ倒れるようにし、予備弾の倍率を 1 にしたもの。</summary>
        public static GameRules CampaignNormalRules()
        {
            return WithOverrides(GameRules.Default, DeathBody.Collapse, GameRules.Default.RegenDelaySeconds, GameRules.Default.RegenPerSecond, 1f);
        }

        /// <summary>キャンペーン (難)。<see cref="GameRules.Default"/> のうち、回復なし、体は倒れる、予備弾は有限で 0.6 倍にしたもの。</summary>
        public static GameRules CampaignHardRules()
        {
            return WithOverrides(GameRules.Default, DeathBody.Collapse, 0f, 0f, 0.6f);
        }

        /// <summary>射撃場。味方撃ちなし、個別リスポーン、回復なし、体は消える、死亡カメラなし、予備弾は無限。</summary>
        public static GameRules RangeRules()
        {
            return new GameRules(
                FriendlyFire.Off,
                RespawnMode.Individual,
                GameRules.DefaultWaveIntervalSeconds,
                0f,
                0f,
                DeathBody.Vanish,
                DeathCamera.None,
                true,
                1f,
                Destruction.Off);
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
            BuildAt(TeamDeathmatchPath, CampaignEasyPath, CampaignNormalPath, CampaignHardPath, RangePath);
        }

        /// <summary>
        /// 各パスにモード別のルールを作るか上書きして保存し、teamDeathmatch・campaignEasy・campaignNormal・campaignHard・range の順に返す。
        /// 5 つのパスは互いに異なる Assets/ 以下の .asset であること。
        /// </summary>
        public static GameRulesDefinition[] BuildAt(
            string teamDeathmatchPath,
            string campaignEasyPath,
            string campaignNormalPath,
            string campaignHardPath,
            string rangePath)
        {
            var entries = new[]
            {
                (Path: SceneBuildHelpers.ValidateAssetPath(teamDeathmatchPath), Rules: TeamDeathmatchRules()),
                (Path: SceneBuildHelpers.ValidateAssetPath(campaignEasyPath), Rules: CampaignEasyRules()),
                (Path: SceneBuildHelpers.ValidateAssetPath(campaignNormalPath), Rules: CampaignNormalRules()),
                (Path: SceneBuildHelpers.ValidateAssetPath(campaignHardPath), Rules: CampaignHardRules()),
                (Path: SceneBuildHelpers.ValidateAssetPath(rangePath), Rules: RangeRules()),
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

        private static GameRules WithOverrides(
            GameRules source,
            DeathBody deathBody,
            float regenDelaySeconds,
            float regenPerSecond,
            float reserveAmmoMultiplier)
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
                Destruction.Off);
        }
    }
}
