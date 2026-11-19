using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Shooter.Core;
using Shooter.Runtime;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Shooter.Editor
{
    // キットの loot.json は空の表なので、このシーンで箱から出る物は Demo 用に作った JSON を Runner に配線して決める。
    // 箱は NavMesh を焼く前に置いて敵が避けるようにし、脱出地点は当たり判定の無い床の印なので焼いた後に置く。
    public sealed class ExtractionSceneBuilder : ModeSceneBuilder
    {
        public const string ModeName = GameRulesAssetBuilder.ExtractionName;

        public static string ScenePathValue => BuildSceneList.ExtractionScene;
        public static string RulesPathValue => GameRulesAssetBuilder.ExtractionPath;
        public static string DefinitionPath => ModeDefinitionAssets.PathOf(ModeName);
        public static string LootTuningPath => ShooterKitRoot.OutputPath("Modes/" + ModeName + "Loot.json");

        public const string MenuPath = SceneBuildSteps.MenuPath;

        public const string LootRootName = "Loot";
        public const string ExitsRootName = "Exits";

        public const int HostileBots = 3;

        public static readonly Side HostileSide = Side.Bravo;

        public const float RaidSeconds = 600f;
        public const float ExtractHoldSeconds = 5f;
        public const int OpenPoints = 2;
        public const float OpenAfterSeconds = 30f;

        public const string SuppliesTable = "demo-supplies";
        public const string ValuablesTable = "demo-valuables";

        // キットの inventory.json の Id。ゲームが持ち物を差し替えたら、作られた JSON の Id を書き換える。
        public const string BandageId = "31d5979e-6067-49d9-b70c-fdcf44dc77d1";
        public const string MedkitId = "61dd226a-156c-4060-ab8f-5dbed8cb7f4f";
        public const string ArmorPlateId = "249ee778-6b2b-47a2-ba53-4515b8aab625";
        public const string KeycardId = "6879cab0-d2cd-4ace-b51a-cc882b7c8aa9";
        public const string ToolboxId = "542515db-5705-497c-8fe6-076a6709131b";
        public const string Ammo9mmId = "02e07a84-f063-4b41-8f63-17a410a3d069";
        public const string Ammo556Id = "212170da-0998-4fa9-9457-761c75a2a797";

        // 遮蔽物 (ModeSceneBuilder.DefaultObstacles) と出現点の列から離し、陰に回って開ける位置にする。
        public static readonly IReadOnlyList<LootPlacement> Containers = new[]
        {
            new LootPlacement("Crate-1", new Vector3(-15f, 0f, 9f), SuppliesTable),
            new LootPlacement("Crate-2", new Vector3(15f, 0f, 9f), SuppliesTable),
            new LootPlacement("Crate-3", new Vector3(0f, 0f, 23f), ValuablesTable),
            new LootPlacement("Crate-4", new Vector3(-15f, 0f, 31f), SuppliesTable),
            new LootPlacement("Crate-5", new Vector3(15f, 0f, 31f), ValuablesTable),
        };

        // 2 つを開けるので、どれが開いても出現点から遠い所まで歩かせる。
        public static readonly IReadOnlyList<ExitPlacement> Exits = new[]
        {
            new ExitPlacement("west", new Vector3(-16f, 0f, 20f)),
            new ExitPlacement("east", new Vector3(16f, 0f, 20f)),
            new ExitPlacement("far", new Vector3(0f, 0f, 46f)),
        };

        private readonly List<LootContainer> _containers = new List<LootContainer>();

        public ExtractionSceneBuilder()
            : base(ScenePathValue, RulesPathValue)
        {
        }

        protected override IReadOnlyList<ModeSideLayout> Sides => TwoSides(0, HostileBots);

        public static void Build()
        {
            GameRulesAssetBuilder.LoadOrBuildModeRules(ModeName);
            new ExtractionSceneBuilder().BuildSceneFromMenu();
        }

        public static ExtractionSpec DemoSpec()
        {
            return new ExtractionSpec(RaidSeconds, ExtractHoldSeconds, OpenPoints, OpenAfterSeconds);
        }

        public static ExtractionDefinition LoadOrCreateDefinition(string path)
        {
            return ModeDefinitionAssets.LoadOrCreate<ExtractionDefinition>(path, d => d.CopyFrom(DemoSpec()));
        }

        public static string LootJsonText()
        {
            var text = new StringBuilder();
            text.Append("{\n  \"").Append(LootJson.TablesItem).Append("\": [\n");
            AppendTable(text, SuppliesTable, 2, new[]
            {
                (BandageId, 4f, 1, 3),
                (Ammo9mmId, 3f, 15, 30),
                (Ammo556Id, 3f, 15, 30),
                (MedkitId, 1f, 1, 1),
            });
            text.Append(",\n");
            AppendTable(text, ValuablesTable, 1, new[]
            {
                (KeycardId, 1f, 1, 1),
                (ToolboxId, 2f, 1, 1),
                (ArmorPlateId, 2f, 1, 1),
                (MedkitId, 2f, 1, 1),
            });
            text.Append("\n  ]\n}\n");
            return text.ToString();
        }

        // ゲーム側で書き換えた表を残すよう、既にあれば中身は変えない (ModeDefinitionAssets と同じ)。
        public static TextAsset LoadOrBuildLootTuning(string path)
        {
            path = SceneBuildHelpers.ValidateAssetPath(path, ".json");
            var existing = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
            if (existing != null)
            {
                // 読めない JSON をシーンに配線すると、遊ぶときに黙ってゲーム全体の表へ戻るので、作るときに止める。
                LootJson.Parse(existing.text, path);
                return existing;
            }

            var json = LootJsonText();
            LootJson.Parse(json, path);
            SceneBuildHelpers.EnsureFolder(Path.GetDirectoryName(path));
            File.WriteAllText(FullPath(path), json);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var built = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
            if (built == null)
            {
                throw new IOException($"Failed to import the loot JSON '{path}'");
            }

            Debug.Log($"[{nameof(ExtractionSceneBuilder)}] saved '{path}'");
            return built;
        }

        protected override void AddLevel(Scene scene, Transform levelRoot)
        {
            _containers.Clear();
            var root = new GameObject(LootRootName);
            SceneManager.MoveGameObjectToScene(root, scene);
            foreach (var placement in Containers)
            {
                var go = new GameObject(placement.Name);
                go.transform.SetParent(root.transform, false);
                go.transform.position = placement.Position;
                var container = go.AddComponent<LootContainer>();
                container.TableId = placement.TableId;
                container.Build();
                _containers.Add(container);
            }
        }

        protected override ModeRunnerBase AddRunner(GameObject host, ModeSceneContext context)
        {
            // NewScene(Single) は参照されていないアセットをアンロードするので、シーンを作った後のここで読む。
            var definition = LoadOrCreateDefinition(DefinitionPath);
            var loot = LoadOrBuildLootTuning(LootTuningPath);

            var runner = host.AddComponent<ExtractionRunner>();
            runner.Definition = definition;
            runner.HostileSide = HostileSide;
            runner.LootTuningJson = loot;
            host.AddComponent<ExtractionInteractInput>().Runner = runner;
            return runner;
        }

        protected override void AddModeObjects(ModeSceneContext context)
        {
            var runner = (ExtractionRunner)context.Runner;
            var root = new GameObject(ExitsRootName);
            SceneManager.MoveGameObjectToScene(root, context.Scene);

            var zones = new List<ExtractionZone>(Exits.Count);
            foreach (var placement in Exits)
            {
                var go = new GameObject("Exit-" + placement.PointId);
                go.transform.SetParent(root.transform, false);
                go.transform.position = placement.Position;
                var zone = go.AddComponent<ExtractionZone>();
                zone.PointId = placement.PointId;
                zone.Build();
                zones.Add(zone);
            }

            runner.SetZones(zones);
            runner.SetContainers(_containers);
        }

        private static void AppendTable(StringBuilder text, string id, int rolls, IReadOnlyList<(string ItemId, float Weight, int Min, int Max)> entries)
        {
            text.Append("    {\n")
                .Append("      \"").Append(LootJson.IdItem).Append("\": \"").Append(id).Append("\",\n")
                .Append("      \"").Append(LootJson.RollsItem).Append("\": ").Append(rolls.ToString(CultureInfo.InvariantCulture)).Append(",\n")
                .Append("      \"").Append(LootJson.EntriesItem).Append("\": [\n");
            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                text.Append("        { \"").Append(LootJson.ItemIdItem).Append("\": \"").Append(entry.ItemId)
                    .Append("\", \"").Append(LootJson.WeightItem).Append("\": ").Append(entry.Weight.ToString(CultureInfo.InvariantCulture))
                    .Append(", \"").Append(LootJson.MinCountItem).Append("\": ").Append(entry.Min.ToString(CultureInfo.InvariantCulture))
                    .Append(", \"").Append(LootJson.MaxCountItem).Append("\": ").Append(entry.Max.ToString(CultureInfo.InvariantCulture))
                    .Append(" }").Append(i < entries.Count - 1 ? ",\n" : "\n");
            }

            text.Append("      ]\n    }");
        }

        private static string FullPath(string assetPath)
        {
            var project = Path.GetDirectoryName(Application.dataPath);
            return Path.Combine(project ?? string.Empty, assetPath);
        }
    }

    public readonly struct LootPlacement
    {
        public LootPlacement(string name, Vector3 position, string tableId)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("a loot container needs a name", nameof(name));
            }

            if (string.IsNullOrWhiteSpace(tableId))
            {
                throw new ArgumentException($"loot container '{name}' needs a table id", nameof(tableId));
            }

            Name = name;
            Position = position;
            TableId = tableId;
        }

        public string Name { get; }

        public Vector3 Position { get; }

        public string TableId { get; }
    }

    public readonly struct ExitPlacement
    {
        public ExitPlacement(string pointId, Vector3 position)
        {
            if (string.IsNullOrWhiteSpace(pointId))
            {
                throw new ArgumentException("an exit needs a point id", nameof(pointId));
            }

            PointId = pointId;
            Position = position;
        }

        public string PointId { get; }

        public Vector3 Position { get; }
    }
}
