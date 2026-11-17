using System;
using System.Collections.Generic;
using Shooter.Runtime;
using UnityEditor;
using UnityEngine;

namespace Shooter.Editor
{
    public static class MissionCatalogBuilder
    {
        public static string CatalogPath => ShooterKitRoot.OutputPath("Missions/MissionCatalog.asset");
        public const string MenuPath = "Shooter/Build Mission Catalog";
        public const string LogTag = "[MissionCatalogBuilder]";

        public const string Campaign01Id = CampaignSceneBuilder.MissionId;

        public const string Campaign02Id = Campaign02Mission.MissionId;

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

            public string NameKey { get; }
            public string ScenePath { get; }
            public string DefinitionPath { get; }
        }

        // 並び順がメニューの順になる。
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

        // Id を持つ前に作った定義にも Id を持たせるため、変えたら dirty にして BuildAt がカタログと一緒に保存する。
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
