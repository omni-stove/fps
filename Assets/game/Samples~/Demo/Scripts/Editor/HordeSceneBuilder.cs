using System;
using System.Collections.Generic;
using Shooter.Core;
using Shooter.Runtime;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Shooter.Editor
{
    // 防衛物は NavMesh を焼いた後に置き、部品の NavMeshObstacle で実行時に道を塞ぐことで、壊れたら通れるようにする。
    public sealed class HordeSceneBuilder : ModeSceneBuilder
    {
        public static string ScenePathValue => BuildSceneList.HordeScene;
        public static string RulesPathValue => GameRulesAssetBuilder.HordePath;
        public static string DefinitionPath => ShooterKitRoot.OutputPath("Modes/Horde.asset");
        public const string MenuPath = SceneBuildSteps.MenuPath;

        public const string BaseRootName = "Base";
        public const string BaseMarkerName = "Base-Marker";
        public const string SpawnerObjectName = "WaveSpawner";
        public const string TemplateName = "WaveEnemyTemplate";
        public const string TemplateId = "horde-template";
        public const string DebrisPoolName = DebrisPool.DefaultObjectName;

        public const int AllyBotCount = 2;

        public static readonly Side DefenderSide = Side.Alpha;
        public static readonly Side EnemySide = Side.Bravo;

        public static readonly Vector3 BaseCenter = PlayerFeet;

        // 拠点の目印は壁の内側に収まる大きさにする。
        public const float BaseMarkerRadius = 5f;

        // Plane は 10m 四方なので、6 倍で x・z とも ±30m にする。
        public static readonly Vector3 HordeFloorScale = new Vector3(6f, 1f, 6f);

        // 2 段で胸の高さになり、陰から撃ち返せる大きさにする。
        public static readonly Vector3 PieceSize = new Vector3(1f, 0.6f, 0.5f);

        public const int PieceRows = 2;
        public const int PieceMaxHealth = DestructiblePiece.DefaultMaxHealth;

        public const float InteractReach = 1.5f;

        public const int WallBuildCost = HordeFortification.DefaultBuildCost;
        public const int WallRepairCostPerPiece = HordeFortification.DefaultRepairCostPerPiece;
        public const int BarricadeBuildCost = 30;

        public static float DefinitionInteractRadius
        {
            get
            {
                var radius = 0f;
                foreach (var placement in Fortifications)
                {
                    radius = Mathf.Max(radius, FortificationInteractRadius(placement));
                }

                return radius;
            }
        }

        public static float FortificationInteractRadius(HordeFortificationPlacement placement) =>
            (placement.Length * PieceSize.x / 2f) + InteractReach;

        // 人が出た途端に壁に埋まらないよう、出現点・拠点の中心と防衛物の部品の間を空けておく。
        public const float SpawnClearance = 1f;

        public const int FirstEnemyCount = 4;
        public const float EnemiesPerWave = 2f;

        public const float FirstStrength = 1f;
        public const float StrengthPerWave = 0.2f;
        public const float MaxStrength = 2.5f;

        public const int MaxAlive = 6;

        // 倒れた味方が戻り、壁を直す時間を取る。
        public const float RestSeconds = 20f;

        public const int WaveCount = 5;

        // 敵も味方も通れるよう、四隅のバリケードと壁の端の間は 3m ほど開けておく。
        public static readonly IReadOnlyList<HordeFortificationPlacement> Fortifications = new[]
        {
            new HordeFortificationPlacement("Wall-North", new Vector3(0f, 0f, 7f), 0f, 6, true),
            new HordeFortificationPlacement("Wall-South", new Vector3(0f, 0f, -7f), 0f, 6, true),
            new HordeFortificationPlacement("Wall-East", new Vector3(7f, 0f, 0f), 90f, 6, true),
            new HordeFortificationPlacement("Wall-West", new Vector3(-7f, 0f, 0f), 90f, 6, true),
            new HordeFortificationPlacement("Barricade-NE", new Vector3(7f, 0f, 7f), 45f, 2, false),
            new HordeFortificationPlacement("Barricade-NW", new Vector3(-7f, 0f, 7f), -45f, 2, false),
            new HordeFortificationPlacement("Barricade-SE", new Vector3(7f, 0f, -7f), -45f, 2, false),
            new HordeFortificationPlacement("Barricade-SW", new Vector3(-7f, 0f, -7f), 45f, 2, false),
        };

        public static readonly IReadOnlyList<ModeObstacle> HordeObstacles = new[]
        {
            new ModeObstacle("Cover-North", new Vector3(0f, 0.525f, 15f), new Vector3(4f, 1.05f, 0.5f)),
            new ModeObstacle("Cover-South", new Vector3(0f, 0.525f, -15f), new Vector3(4f, 1.05f, 0.5f)),
            new ModeObstacle("Cover-East", new Vector3(15f, 0.525f, 0f), new Vector3(0.5f, 1.05f, 4f)),
            new ModeObstacle("Cover-West", new Vector3(-15f, 0.525f, 0f), new Vector3(0.5f, 1.05f, 4f)),
            new ModeObstacle("Pillar-NE", new Vector3(11f, 1.5f, 11f), new Vector3(1.5f, 3f, 1.5f)),
            new ModeObstacle("Pillar-NW", new Vector3(-11f, 1.5f, 11f), new Vector3(1.5f, 3f, 1.5f)),
            new ModeObstacle("Pillar-SE", new Vector3(11f, 1.5f, -11f), new Vector3(1.5f, 3f, 1.5f)),
            new ModeObstacle("Pillar-SW", new Vector3(-11f, 1.5f, -11f), new Vector3(1.5f, 3f, 1.5f)),
        };

        public static readonly IReadOnlyList<Vector3> DefenderSpawnFeet = new[]
        {
            PlayerFeet,
            PlayerFeet + new Vector3(-2.5f, 0f, 0f),
            PlayerFeet + new Vector3(2.5f, 0f, 0f),
            PlayerFeet + new Vector3(0f, 0f, -2.5f),
        };

        public static readonly IReadOnlyList<Vector3> EnemySpawnFeet = new[]
        {
            new Vector3(0f, 0f, 25f),
            new Vector3(17.5f, 0f, 17.5f),
            new Vector3(25f, 0f, 0f),
            new Vector3(17.5f, 0f, -17.5f),
            new Vector3(0f, 0f, -25f),
            new Vector3(-17.5f, 0f, -17.5f),
            new Vector3(-25f, 0f, 0f),
            new Vector3(-17.5f, 0f, 17.5f),
        };

        public static readonly Vector3 TemplateFeet = new Vector3(0f, 0f, -27f);

        public const float AllyPatrolLength = 3f;

        public HordeSceneBuilder()
            : base(ScenePathValue, RulesPathValue)
        {
        }

        protected override Vector3 FloorCenter => Vector3.zero;

        protected override Vector3 FloorScale => HordeFloorScale;

        protected override IReadOnlyList<ModeObstacle> Obstacles => HordeObstacles;

        protected override IReadOnlyList<ModeSideLayout> Sides => Layout();

        public static void Build()
        {
            new HordeSceneBuilder().BuildSceneFromMenu();
        }

        public static IReadOnlyList<ModeSideLayout> Layout()
        {
            var defenders = new ModeSpawnPlacement[DefenderSpawnFeet.Count];
            for (var i = 0; i < defenders.Length; i++)
            {
                defenders[i] = new ModeSpawnPlacement(DefenderSpawnFeet[i], Vector3.forward);
            }

            var enemies = new ModeSpawnPlacement[EnemySpawnFeet.Count];
            for (var i = 0; i < enemies.Length; i++)
            {
                enemies[i] = new ModeSpawnPlacement(EnemySpawnFeet[i], BaseCenter - EnemySpawnFeet[i]);
            }

            return new[]
            {
                new ModeSideLayout(DefenderSide, defenders, AllyBotCount),
                new ModeSideLayout(EnemySide, enemies, 0),
            };
        }

        public static void RequireFortificationsClear(IReadOnlyList<HordeFortificationPlacement> fortifications, IReadOnlyList<ModeSideLayout> sides)
        {
            var points = new List<Vector3> { BaseCenter };
            foreach (var side in sides)
            {
                foreach (var spawn in side.Spawns)
                {
                    points.Add(spawn.Feet);
                }
            }

            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var fortification in fortifications)
            {
                if (!names.Add(fortification.Name))
                {
                    throw new InvalidOperationException($"fortification name '{fortification.Name}' is used twice");
                }

                foreach (var point in points)
                {
                    if (fortification.HorizontalDistanceTo(point) < SpawnClearance)
                    {
                        throw new InvalidOperationException(
                            $"fortification '{fortification.Name}' is within {SpawnClearance}m of the spawn point or base at {point}");
                    }
                }
            }
        }

        // ゲームがアセットで変えた値をシーンを組み直しても消さないよう、既にあれば値を変えずに返す。
        public static HordeDefinition BuildDefinition(string path)
        {
            path = SceneBuildHelpers.ValidateAssetPath(path);

            if (AssetDatabase.LoadMainAssetAtPath(path) is HordeDefinition existing)
            {
                existing.Validate(DefenderSide, EnemySide);
                Debug.Log($"[{nameof(HordeSceneBuilder)}] kept '{path}' ({existing.WaveCount} wave(s), rest {existing.RestSeconds}s)");
                return existing;
            }

            var definition = SceneBuildHelpers.CreateOrUpdateAsset<HordeDefinition>(path, Configure);
            Debug.Log($"[{nameof(HordeSceneBuilder)}] saved '{path}' ({definition.WaveCount} wave(s), rest {definition.RestSeconds}s)");
            return definition;
        }

        public static void Configure(HordeDefinition definition)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            definition.StartingCurrency = HordeSpec.DefaultStartingCurrency;
            definition.KillReward = HordeSpec.DefaultKillReward;
            definition.KillRewardPerWave = HordeSpec.DefaultKillRewardPerWave;
            definition.PurchasesOnlyBetweenWaves = true;
            definition.KillScore = HordeSpec.DefaultKillScore;

            // Definition の費用と距離はすべての防衛物に同じに効く。置くのは始めは無いバリケードだけなのでその値、距離はいちばん長い壁の端まで届く値にする。
            definition.BuildCost = BarricadeBuildCost;
            definition.RepairCostPerPiece = WallRepairCostPerPiece;
            definition.InteractRadius = DefinitionInteractRadius;
            definition.EnemySide = EnemySide;
            definition.AlertInterval = HordeSpec.DefaultAlertInterval;
            definition.MinSpawnDistance = HordeSpec.DefaultMinSpawnDistance;
            definition.CorpseSeconds = HordeSpec.DefaultCorpseSeconds;
            definition.WaveCount = WaveCount;
            definition.RestSeconds = RestSeconds;
            definition.FirstEnemyCount = FirstEnemyCount;
            definition.EnemiesPerWave = EnemiesPerWave;
            definition.FirstStrength = FirstStrength;
            definition.StrengthPerWave = StrengthPerWave;
            definition.MaxStrength = MaxStrength;
            definition.MaxAlive = MaxAlive;
            definition.Validate(DefenderSide, EnemySide);
        }

        protected override ModeRunnerBase AddRunner(GameObject host, ModeSceneContext context)
        {
            var runner = host.AddComponent<HordeRunner>();
            runner.EnemySide = EnemySide;

            // NewScene(Single) は参照されていないアセットをアンロードするので、シーンを作った後のここで読む。
            runner.Definition = BuildDefinition(DefinitionPath);
            host.AddComponent<HordeInteractInput>().Runner = runner;
            return runner;
        }

        // 波の敵の側に bot はいないが、出現点から拠点へ歩けることを確かめるために道を返す。
        protected override IReadOnlyList<Vector3> BotPatrolRoute(IReadOnlyList<ModeSideLayout> sides, int sideIndex, int spawnIndex)
        {
            var feet = sides[sideIndex].Spawns[spawnIndex].Feet;
            if (sides[sideIndex].Side == DefenderSide)
            {
                return new[] { feet, feet + (Vector3.forward * AllyPatrolLength) };
            }

            return new[] { feet, BaseCenter };
        }

        protected override void AddModeObjects(ModeSceneContext context)
        {
            RequireFortificationsClear(Fortifications, Layout());

            var centers = new List<Vector3>(Fortifications.Count);
            foreach (var fortification in Fortifications)
            {
                centers.Add(fortification.Center);
            }

            SceneBuildHelpers.RequireOnNavMesh(centers, NavMeshCheckRadius, "fortification");
            SceneBuildHelpers.RequireReachable(PlayerFeet, centers, NavMeshCheckRadius, "fortification");

            var runner = context.Runner as HordeRunner
                ?? throw new InvalidOperationException($"{nameof(HordeSceneBuilder)} expects a {nameof(HordeRunner)}");

            var destructionEnabled = context.Rules.ToRules().Destruction == Destruction.On;
            if (!destructionEnabled)
            {
                Debug.LogWarning($"{LogTag} ルール '{RulesPath}' では建造物が壊れないため、防衛物は直す必要がありません (置くことはできます)。");
            }

            var poolObject = new GameObject(DebrisPoolName);
            SceneManager.MoveGameObjectToScene(poolObject, context.Scene);
            var pool = poolObject.AddComponent<DebrisPool>();

            var baseRoot = new GameObject(BaseRootName);
            SceneManager.MoveGameObjectToScene(baseRoot, context.Scene);
            CreateBaseMarker(baseRoot.transform);

            var pieceVisualPrefab = DestructiblePieceVisual.Load();
            var built = new HordeFortification[Fortifications.Count];
            for (var i = 0; i < built.Length; i++)
            {
                built[i] = CreateFortification(baseRoot.transform, Fortifications[i], destructionEnabled, pool, pieceVisualPrefab);
            }

            runner.SetFortifications(built);
            runner.Spawner = CreateSpawner(context);
        }

        private static void CreateBaseMarker(Transform parent)
        {
            var marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            marker.name = BaseMarkerName;
            marker.transform.SetParent(parent, false);
            marker.transform.localPosition = BaseCenter + new Vector3(0f, 0.01f, 0f);
            marker.transform.localScale = new Vector3(BaseMarkerRadius * 2f, 0.01f, BaseMarkerRadius * 2f);

            // 目印は見せるだけ。弾や足を止めないよう当たり判定を外す。
            UnityEngine.Object.DestroyImmediate(marker.GetComponent<Collider>());
        }

        public static HordeFortification CreateFortification(
            Transform parent, HordeFortificationPlacement placement, bool destructionEnabled, DebrisPool pool, GameObject pieceVisualPrefab = null)
        {
            var root = new GameObject(placement.Name);
            root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation(placement.Center, placement.Rotation);

            var structure = root.AddComponent<DestructibleStructure>();
            structure.DestructionEnabled = destructionEnabled;
            structure.DebrisPool = pool;

            var index = 0;
            for (var row = 0; row < PieceRows; row++)
            {
                for (var column = 0; column < placement.Length; column++)
                {
                    var local = new Vector3(
                        (column - ((placement.Length - 1) / 2f)) * PieceSize.x,
                        (row + 0.5f) * PieceSize.y,
                        0f);
                    DestructiblePiece.Create(
                        root.transform,
                        $"{placement.Name}-{column}-{row}",
                        local,
                        PieceSize,
                        index,
                        PieceMaxHealth,
                        grounded: row == 0,
                        visualPrefab: pieceVisualPrefab);
                    index++;
                }
            }

            var fortification = root.AddComponent<HordeFortification>();
            fortification.StartsBuilt = placement.StartsBuilt;
            fortification.BuildCost = placement.StartsBuilt ? WallBuildCost : BarricadeBuildCost;
            fortification.RepairCostPerPiece = WallRepairCostPerPiece;
            fortification.InteractRadius = FortificationInteractRadius(placement);
            return fortification;
        }

        private WaveSpawner CreateSpawner(ModeSceneContext context)
        {
            var weapon = WeaponAssetBuilder.FindInCatalog(context.Catalog, TemplateName, BotWeaponId);
            var template = SceneBuildHelpers.CreateEnemy(
                context.Scene,
                null,
                TemplateName,
                TemplateId,
                TemplateFeet,
                SceneBuildHelpers.FacingTowards(TemplateFeet, BaseCenter),
                Array.Empty<Vector3>(),
                BotBody,
                weapon);
            template.gameObject.AddComponent<SideMember>().Side = EnemySide;

            // 有効なまま置くと、雛形そのものが 1 体の敵として動き出し、モードの参加者にもなってしまう。
            template.gameObject.SetActive(false);

            var points = new List<SideSpawnPoint>();
            foreach (var point in context.SpawnPoints)
            {
                if (point != null && point.Side == EnemySide)
                {
                    points.Add(point);
                }
            }

            var host = new GameObject(SpawnerObjectName);
            SceneManager.MoveGameObjectToScene(host, context.Scene);
            var spawner = host.AddComponent<WaveSpawner>();
            spawner.EnemyTemplate = template;
            spawner.SpawnPoints = points;
            spawner.EnemySide = EnemySide;
            spawner.PlayerSide = DefenderSide;
            spawner.BeginOnStart = false;
            spawner.SetFormula(FirstEnemyCount, EnemiesPerWave, FirstStrength, StrengthPerWave, MaxStrength, MaxAlive, RestSeconds, WaveCount);
            return spawner;
        }
    }

    public readonly struct HordeFortificationPlacement
    {
        public string Name { get; }

        public Vector3 Center { get; }

        public float YawDegrees { get; }

        public int Length { get; }

        public bool StartsBuilt { get; }

        public HordeFortificationPlacement(string name, Vector3 center, float yawDegrees, int length, bool startsBuilt)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("a fortification needs a name", nameof(name));
            }

            if (length < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(length), length, $"fortification '{name}' needs at least one piece");
            }

            Name = name;
            Center = center;
            YawDegrees = yawDegrees;
            Length = length;
            StartsBuilt = startsBuilt;
        }

        public Quaternion Rotation => Quaternion.Euler(0f, YawDegrees, 0f);

        public float HorizontalDistanceTo(Vector3 point)
        {
            var local = Quaternion.Inverse(Rotation) * (point - Center);
            var halfLength = Length * HordeSceneBuilder.PieceSize.x / 2f;
            var halfThickness = HordeSceneBuilder.PieceSize.z / 2f;
            var dx = Mathf.Max(0f, Mathf.Abs(local.x) - halfLength);
            var dz = Mathf.Max(0f, Mathf.Abs(local.z) - halfThickness);
            return Mathf.Sqrt((dx * dx) + (dz * dz));
        }
    }
}
