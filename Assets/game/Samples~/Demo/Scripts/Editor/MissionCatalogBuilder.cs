using System;
using System.Collections.Generic;
using Shooter.Runtime;
using UnityEditor;
using UnityEngine;

namespace Shooter.Editor
{
    /// <summary>
    /// キャンペーンのミッションの一覧 (<see cref="CatalogPath"/> の <see cref="MissionCatalog"/>) を作るか上書きする。
    /// <see cref="CampaignSceneBuilder.Missions"/> のミッション (Campaign01, Campaign02) をこの順に入れる。既存のアセットは GUID を保って中身だけ更新する。
    /// ミッションの定義とシーンは先に <see cref="CampaignSceneBuilder.Build"/> で作っておく (無ければ何も保存せずに止める)。
    /// 何度実行しても同じ結果になる。不正なら保存せずに例外を投げる (batchmode では Unity が終了コード 1 で終わる)。
    /// Editor を閉じた状態で次のように呼べる:
    /// <code>unity -batchmode -quit -projectPath . -executeMethod Shooter.Editor.MissionCatalogBuilder.Build</code>
    /// </summary>
    public static class MissionCatalogBuilder
    {
        public static string CatalogPath => ShooterKitRoot.OutputPath("Missions/MissionCatalog.asset");
        public const string MenuPath = "Shooter/Build Mission Catalog";
        public const string LogTag = "[MissionCatalogBuilder]";

        /// <summary>Campaign01 のミッション Id。定義 (<see cref="CampaignSceneBuilder.DefinitionPath"/>) の <see cref="MissionDefinition.Id"/> に入れる。</summary>
        public const string Campaign01Id = CampaignSceneBuilder.MissionId;

        /// <summary>Campaign02 のミッション Id。定義 (<see cref="Campaign02Mission.DefinitionPath"/>) の <see cref="MissionDefinition.Id"/> に入れる。</summary>
        public const string Campaign02Id = Campaign02Mission.MissionId;

        /// <summary>カタログに入れるミッションの、定義とシーンの場所。</summary>
        public readonly struct MissionSource
        {
            public MissionSource(string id, string nameKey, string scenePath, string definitionPath)
            {
                Id = id;
                NameKey = nameKey;
                ScenePath = scenePath;
                DefinitionPath = definitionPath;
            }

            public string Id { get; }

            /// <summary>メニューに出すミッション名を引く言語の表のキー。</summary>
            public string NameKey { get; }
            public string ScenePath { get; }
            public string DefinitionPath { get; }
        }

        /// <summary>カタログに入れるミッション (並び順がメニューの順)。</summary>
        public static IReadOnlyList<MissionSource> DefaultMissions()
        {
            var missions = new List<MissionSource>();
            foreach (var spec in CampaignSceneBuilder.Missions())
            {
                missions.Add(new MissionSource(spec.Id, spec.NameKey, spec.ScenePath, spec.DefinitionPath));
            }

            return missions;
        }

        [MenuItem(MenuPath)]
        public static void Build()
        {
            BuildAt(CatalogPath, DefaultMissions());
        }

        /// <summary>
        /// catalogPath に missions のカタログを作るか上書きして保存し、返す。各ミッションの定義の <see cref="MissionDefinition.Id"/> は
        /// <see cref="MissionSource.Id"/> にそろえて一緒に保存する (定義のアセット名は変えない)。
        /// 各ミッションの定義が無い・シーンが無い・カタログとして不正 (<see cref="MissionCatalog.Validate"/>) なら、定義の Id も元に戻し、何も保存せずに例外を投げる。
        /// </summary>
        public static MissionCatalog BuildAt(string catalogPath, IReadOnlyList<MissionSource> missions)
        {
            catalogPath = SceneBuildHelpers.ValidateAssetPath(catalogPath);
            if (missions == null)
            {
                throw new ArgumentNullException(nameof(missions));
            }

            var entries = new List<MissionCatalogEntry>(missions.Count);
            var previousIds = new List<(MissionDefinition Definition, string Id)>(missions.Count);
            MissionCatalog catalog;
            try
            {
                foreach (var mission in missions)
                {
                    var entry = LoadEntry(mission);
                    previousIds.Add((entry.Definition, entry.Definition.Id));
                    AssignId(entry.Definition, mission.Id);
                    entries.Add(entry);
                }

                catalog = SceneBuildHelpers.CreateOrUpdateAsset<MissionCatalog>(catalogPath, c => Configure(c, entries));
            }
            catch
            {
                // 保存しなかった Id がメモリ上の定義に残ると、後で別のビルダーが SaveAssets したときに書き出されてしまう。
                for (var i = previousIds.Count - 1; i >= 0; i--)
                {
                    previousIds[i].Definition.Id = previousIds[i].Id;
                }

                throw;
            }

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

            return new MissionCatalogEntry(mission.Id, mission.NameKey, scenePath, definition);
        }

        /// <summary>
        /// definition の <see cref="MissionDefinition.Id"/> を id にする。同じなら何もしない。変えたら dirty にし、
        /// <see cref="BuildAt"/> がカタログと一緒に保存する (Id を持つ前に作った定義もこれで Id を持つ)。
        /// </summary>
        public static void AssignId(MissionDefinition definition, string id)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            if (string.Equals(definition.Id, id, StringComparison.Ordinal))
            {
                return;
            }

            definition.Id = id;
            EditorUtility.SetDirty(definition);
        }
    }
}
