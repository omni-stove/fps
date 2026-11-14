using System;
using System.Collections.Generic;
using Fps.Core;
using Fps.Runtime;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Fps.Editor
{
    /// <summary>
    /// ホードのシーン Horde を組む。Runner は <see cref="HordeRunner"/>、ルールは <see cref="GameRulesAssetBuilder.HordePath"/>、
    /// 通貨と波の調整値は <see cref="DefinitionPath"/> (<see cref="HordeDefinition"/>。無いときだけ作る)。
    /// Editor を閉じた状態で次のように呼べる:
    /// <code>unity -batchmode -quit -projectPath . -executeMethod Fps.Editor.HordeSceneBuilder.Build</code>
    /// </summary>
    /// <remarks>
    /// 配置 (上から見て、プレイヤーは原点で +Z を向く。床は 60m 四方):
    /// <list type="bullet">
    /// <item>守る拠点は原点。プレイヤーと味方の bot <see cref="AllyBotCount"/> 体 (陣営 0) が中に出る。拠点の目印 (当たり判定なし) を床に置く。</item>
    /// <item>拠点の四方に、通貨で直せる壊れる壁 (<see cref="HordeFortification"/>) を置き、四隅には通貨で置くバリケードの場所を空けておく
    /// (<see cref="Fortifications"/>)。壁とバリケードの間は開けておき、敵も味方も通れる。</item>
    /// <item>拠点の周り (半径 11〜15m) に動かない遮蔽物 (<see cref="Obstacles"/>)、外周 (半径 25m) に波の敵 (陣営 1) の出現点
    /// (<see cref="EnemySpawnFeet"/>) を置く。</item>
    /// <item>波の敵は、無効にした雛形を <see cref="WaveSpawner"/> が複製して出す。波は <see cref="WaveCount"/> 波で、
    /// 波の間の休憩 (<see cref="RestSeconds"/>) に壁を直す。どちらも作った後は <see cref="DefinitionPath"/> の値が効く。</item>
    /// <item>壁が弾で壊れるかはルールの <see cref="Destruction"/> で決める。</item>
    /// </list>
    /// 防衛物は NavMesh を焼いた後に置き、部品の NavMeshObstacle で実行時に道を塞ぐ (壊れたら通れる)。
    /// </remarks>
    public sealed class HordeSceneBuilder : ModeSceneBuilder
    {
        public static string ScenePathValue => BuildSceneList.HordeScene;
        public static string RulesPathValue => GameRulesAssetBuilder.HordePath;
        public static string DefinitionPath => FpsKitRoot.OutputPath("Modes/Horde.asset");
        public const string MenuPath = "Fps/Build Horde Scene";

        public const string BaseRootName = "Base";
        public const string BaseMarkerName = "Base-Marker";
        public const string SpawnerObjectName = "WaveSpawner";
        public const string TemplateName = "WaveEnemyTemplate";
        public const string TemplateId = "horde-template";
        public const string DebrisPoolName = DebrisPool.DefaultObjectName;

        /// <summary>プレイヤーと一緒に守る bot の数。</summary>
        public const int AllyBotCount = 2;

        public static readonly Side DefenderSide = Side.Alpha;
        public static readonly Side EnemySide = Side.Bravo;

        /// <summary>守る拠点の中心。プレイヤーの出現点。</summary>
        public static readonly Vector3 BaseCenter = PlayerFeet;

        /// <summary>拠点の目印の半径 (m)。壁の内側に収める。</summary>
        public const float BaseMarkerRadius = 5f;

        /// <summary>Plane は 10m 四方なので、x・z とも ±30m。</summary>
        public static readonly Vector3 HordeFloorScale = new Vector3(6f, 1f, 6f);

        /// <summary>防衛物の部品 1 つの幅・高さ・厚さ (m)。2 段で胸の高さになり、陰から撃ち返せる。</summary>
        public static readonly Vector3 PieceSize = new Vector3(1f, 0.6f, 0.5f);

        public const int PieceRows = 2;
        public const int PieceMaxHealth = DestructiblePiece.DefaultMaxHealth;

        /// <summary>防衛物の端から、直す・置く操作が届く距離 (m)。</summary>
        public const float InteractReach = 1.5f;

        public const int WallBuildCost = HordeFortification.DefaultBuildCost;
        public const int WallRepairCostPerPiece = HordeFortification.DefaultRepairCostPerPiece;
        public const int BarricadeBuildCost = 30;

        /// <summary>出現点・拠点の中心から、防衛物の部品の箱までに空けておく距離 (m)。人が出た途端に壁に埋まらないように。</summary>
        public const float SpawnClearance = 1f;

        /// <summary>波: 1 波目の敵の数と、波ごとに増える数。</summary>
        public const int FirstEnemyCount = 4;
        public const float EnemiesPerWave = 2f;

        /// <summary>波: 強さの倍率の始まり・増え方・上限。</summary>
        public const float FirstStrength = 1f;
        public const float StrengthPerWave = 0.2f;
        public const float MaxStrength = 2.5f;

        /// <summary>波: 同時に出しておく敵の上限。</summary>
        public const int MaxAlive = 6;

        /// <summary>波の間の休憩 (秒)。倒れた味方が戻り、壁を直す時間。</summary>
        public const float RestSeconds = 20f;

        /// <summary>越えれば勝ちになる波の数。</summary>
        public const int WaveCount = 5;

        /// <summary>
        /// 通貨で直す・置く防衛物。拠点から 7m の四方に長さ 6 の壁 (始めから置かれている)、四隅に長さ 2 のバリケードの場所 (始めは無い)。
        /// バリケードは拠点の中心から見て斜めに向け、壁の端との間は 3m ほど開けてある。
        /// </summary>
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

        /// <summary>拠点の周りの遮蔽物。四方に低い壁、斜めに高い柱。どれも拠点の壁と外周の出現点から離す。</summary>
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

        /// <summary>守る側の出現点の足元。先頭がプレイヤー (原点)、続く 2 つに味方の bot が立つ。どれも +Z を向く。</summary>
        public static readonly IReadOnlyList<Vector3> DefenderSpawnFeet = new[]
        {
            PlayerFeet,
            PlayerFeet + new Vector3(-2.5f, 0f, 0f),
            PlayerFeet + new Vector3(2.5f, 0f, 0f),
            PlayerFeet + new Vector3(0f, 0f, -2.5f),
        };

        /// <summary>波の敵の出現点の足元。拠点から 25m の外周 (四方と斜め)。どれも拠点を向く。</summary>
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

        /// <summary>雛形を置く足元。無効なので動かない。</summary>
        public static readonly Vector3 TemplateFeet = new Vector3(0f, 0f, -27f);

        /// <summary>味方の bot が拠点の中で往復する距離 (m)。出現点から +Z へ。</summary>
        public const float AllyPatrolLength = 3f;

        public HordeSceneBuilder()
            : base(ScenePathValue, RulesPathValue)
        {
        }

        protected override Vector3 FloorCenter => Vector3.zero;

        protected override Vector3 FloorScale => HordeFloorScale;

        protected override IReadOnlyList<ModeObstacle> Obstacles => HordeObstacles;

        protected override IReadOnlyList<ModeSideLayout> Sides => Layout();

        [MenuItem(MenuPath)]
        public static void Build()
        {
            new HordeSceneBuilder().BuildSceneFromMenu();
        }

        /// <summary>守る側 (出現点 <see cref="DefenderSpawnFeet"/>、bot <see cref="AllyBotCount"/> 体) と、bot のいない波の敵の側 (<see cref="EnemySpawnFeet"/>)。</summary>
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

        /// <summary>
        /// 防衛物の部品の箱 (高さを除いた水平の矩形) が、出現点と拠点の中心から <see cref="SpawnClearance"/> 以上離れていることを確かめる。
        /// 満たさなければ <see cref="InvalidOperationException"/>。
        /// </summary>
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

        /// <summary>
        /// path に通貨と波の調整値 (<see cref="HordeDefinition"/>) が無ければ <see cref="Configure"/> の値で作って保存し、あれば値を変えずに返す。
        /// ゲームがアセットで変えた値をシーンを組み直しても消さないため。既存のアセットの値が不正なら <see cref="ArgumentException"/>。
        /// </summary>
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

        /// <summary>definition をこのシーンの見本の値 (通貨は <see cref="HordeSpec"/> の既定値、波は <see cref="WaveCount"/> など) にする。</summary>
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

        /// <summary>味方の bot は拠点の中で出現点から +Z へ往復する。波の敵の側 (bot はいない) は、出現点から拠点へ歩けることを確かめるための道。</summary>
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
            fortification.InteractRadius = (placement.Length * PieceSize.x / 2f) + InteractReach;
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

    /// <summary>ホードの防衛物 1 つの置き方 (<see cref="HordeSceneBuilder.Fortifications"/>)。</summary>
    public readonly struct HordeFortificationPlacement
    {
        public string Name { get; }

        /// <summary>足元の中心。</summary>
        public Vector3 Center { get; }

        /// <summary>上から見た向き (度)。0 なら部品が x 方向に並ぶ。</summary>
        public float YawDegrees { get; }

        /// <summary>横に並べる部品の数。</summary>
        public int Length { get; }

        /// <summary>始めから置かれているか。false なら通貨を払って置く場所。</summary>
        public bool StartsBuilt { get; }

        /// <exception cref="ArgumentException">name が空。</exception>
        /// <exception cref="ArgumentOutOfRangeException">length が 1 未満。</exception>
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

        /// <summary>point から、部品の並ぶ水平の矩形 (長さ × 厚さ) までの距離 (m)。中にあれば 0。</summary>
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
