using System;
using System.Collections.Generic;
using Fps.Runtime;
using UnityEditor;
using UnityEngine;

namespace Fps.Editor
{
    /// <summary>
    /// キャンペーンのミッションの一覧 (<see cref="CatalogPath"/> の <see cref="MissionCatalog"/>) を作るか上書きする。
    /// <see cref="CampaignSceneBuilder.Missions"/> のミッション (Campaign01, Campaign02) をこの順に入れる。既存のアセットは GUID を保って中身だけ更新する。
    /// ミッションの定義とシーンは先に <see cref="CampaignSceneBuilder.Build"/> で作っておく (無ければ何も保存せずに止める)。
    /// 何度実行しても同じ結果になる。不正なら保存せずに例外を投げる (batchmode では Unity が終了コード 1 で終わる)。
    /// Editor を閉じた状態で次のように呼べる:
    /// <code>unity -batchmode -quit -projectPath . -executeMethod Fps.Editor.MissionCatalogBuilder.Build</code>
    /// </summary>
    public static class MissionCatalogBuilder
    {
        public static string CatalogPath => FpsKitRoot.OutputPath("Missions/MissionCatalog.asset");
        public const string MenuPath = "Fps/Build Mission Catalog";
        public const string LogTag = "[MissionCatalogBuilder]";

        /// <summary>Campaign01 のミッション Id。定義のアセット名 (<see cref="CampaignSceneBuilder.DefinitionPath"/>) と同じ。</summary>
        public const string Campaign01Id = CampaignSceneBuilder.MissionId;

        /// <summary>Campaign02 のミッション Id。定義のアセット名 (<see cref="Campaign02Mission.DefinitionPath"/>) と同じ。</summary>
        public const string Campaign02Id = Campaign02Mission.MissionId;

        /// <summary>カタログに入れるミッションの、定義とシーンの場所。</summary>
        public readonly struct MissionSource
        {
            public MissionSource(string id, string displayName, string scenePath, string definitionPath)
            {
                Id = id;
                DisplayName = displayName;
                ScenePath = scenePath;
                DefinitionPath = definitionPath;
            }

            public string Id { get; }
            public string DisplayName { get; }
            public string ScenePath { get; }
            public string DefinitionPath { get; }
        }

        /// <summary>カタログに入れるミッション (並び順がメニューの順)。</summary>
        public static IReadOnlyList<MissionSource> DefaultMissions()
        {
            var missions = new List<MissionSource>();
            foreach (var spec in CampaignSceneBuilder.Missions())
            {
                missions.Add(new MissionSource(spec.Id, spec.DisplayName, spec.ScenePath, spec.DefinitionPath));
            }

            return missions;
        }

        [MenuItem(MenuPath)]
        public static void Build()
        {
            BuildAt(CatalogPath, DefaultMissions());
        }

        /// <summary>
        /// catalogPath に missions のカタログを作るか上書きして保存し、返す。
        /// 各ミッションの定義が無い・シーンが無い・カタログとして不正 (<see cref="MissionCatalog.Validate"/>) なら何も保存せずに例外を投げる。
        /// </summary>
        public static MissionCatalog BuildAt(string catalogPath, IReadOnlyList<MissionSource> missions)
        {
            catalogPath = SceneBuildHelpers.ValidateAssetPath(catalogPath);
            if (missions == null)
            {
                throw new ArgumentNullException(nameof(missions));
            }

            var entries = new List<MissionCatalogEntry>(missions.Count);
            foreach (var mission in missions)
            {
                entries.Add(LoadEntry(mission));
            }

            var catalog = SceneBuildHelpers.CreateOrUpdateAsset<MissionCatalog>(catalogPath, c => Configure(c, entries));

            Debug.Log($"{LogTag} saved '{catalogPath}' with {string.Join(", ", entries.ConvertAll(e => e.Id))}");
            return catalog;
        }

        /// <summary>catalog の中身を entries にし、<see cref="MissionCatalog.Validate"/> に通ることを確かめる。不正なら元の中身に戻して投げる。</summary>
        public static void Configure(MissionCatalog catalog, IReadOnlyList<MissionCatalogEntry> entries)
        {
            if (catalog == null)
            {
                throw new ArgumentNullException(nameof(catalog));
            }

            if (entries == null)
            {
                throw new ArgumentNullException(nameof(entries));
            }

            var previous = new List<MissionCatalogEntry>(catalog.Missions);
            catalog.SetMissions(entries);
            try
            {
                catalog.Validate();
            }
            catch
            {
                // 既存のアセットのメモリ上の中身を、保存しなかった不正な一覧で汚さない。
                catalog.SetMissions(previous);
                throw;
            }
        }

        private static MissionCatalogEntry LoadEntry(MissionSource mission)
        {
            var definitionPath = SceneBuildHelpers.ValidateAssetPath(mission.DefinitionPath);
            var definition = AssetDatabase.LoadAssetAtPath<MissionDefinition>(definitionPath);
            if (definition == null)
            {
                throw new InvalidOperationException(
                    $"mission definition '{definitionPath}' of mission '{mission.Id}' was not found; run {CampaignSceneBuilder.MenuPath} first");
            }

            var scenePath = BootstrapSceneBuilder.ValidateScenePath(mission.ScenePath);
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) == null)
            {
                throw new InvalidOperationException(
                    $"scene '{scenePath}' of mission '{mission.Id}' was not found; run {CampaignSceneBuilder.MenuPath} first");
            }

            return new MissionCatalogEntry(mission.Id, mission.DisplayName, scenePath, definition);
        }
    }
}
