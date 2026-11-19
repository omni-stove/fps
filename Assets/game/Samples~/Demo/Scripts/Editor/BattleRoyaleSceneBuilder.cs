using System;
using System.Collections.Generic;
using Shooter.Core;
using Shooter.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Shooter.Editor
{
    // 丘は Mesh を作って MeshCollider を付け、斜面を歩けるよう NavMesh を焼く前に置く。Mesh はアセットにせずシーンに埋め込む。
    public sealed class BattleRoyaleSceneBuilder : ModeSceneBuilder
    {
        public static string ScenePathValue => BuildSceneList.BattleRoyaleScene;
        public static string RulesPathValue => GameRulesAssetBuilder.BattleRoyalePath;
        public const string MenuPath = SceneBuildSteps.MenuPath;

        public const int SquadCount = 6;
        public const int SquadSize = 3;

        public const string ZoneObjectName = "Zone";
        public const string PickupsObjectName = "Pickups";
        public const string SupplyDropsObjectName = "SupplyDrops";
        public const string HillMeshName = "BattleRoyaleHill";

        public const int WeaponPickupAmmoIfOwned = 30;

        public const int AmmoPickupAmount = 30;
        public const int HealthPickupAmount = 50;

        // プレイヤーの出現点 (原点) が南の縁から 20m 内側に来るよう、中心を +Z へずらす。
        public static readonly Vector3 MapCenter = new Vector3(0f, 0f, 130f);

        // Plane は 10m 四方なので、30 倍で 300m 四方にする。
        public static readonly Vector3 MapFloorScale = new Vector3(30f, 1f, 30f);

        // 分隊 0 の最初の点がプレイヤーの出現点 (原点) に来るよう、原点から中心までの距離にする。
        public const float SquadSpawnRadius = 130f;

        public const float SquadMemberSpacing = 3f;

        public const float SquadLootRadius = 120f;

        public const float RallyRadius = 95f;

        public const float LandmarkRadius = 75f;

        public const float CoverClusterRadius = 40f;

        public static readonly Vector3 ZoneCenter = MapCenter;

        // どの出現点 (中心から約 130m) も中に入り、床の四隅 (約 212m) は外に出る大きさにする。
        public const float ZoneInitialRadius = 150f;

        // 最初の待ちで武器を拾えるようにし、外のダメージは段階ごとに強める。
        // 最後の円の外は 15/秒で、体力 100 の人が 7 秒ほどで倒れるので、最後は円の中の撃ち合いで決まる。
        public static readonly IReadOnlyList<ShrinkingZone.PhaseSettings> ZonePhases = new[]
        {
            new ShrinkingZone.PhaseSettings(60f, 40f, 100f, 1f),
            new ShrinkingZone.PhaseSettings(45f, 35f, 60f, 2f),
            new ShrinkingZone.PhaseSettings(35f, 30f, 32f, 4f),
            new ShrinkingZone.PhaseSettings(30f, 25f, 15f, 8f),
            new ShrinkingZone.PhaseSettings(20f, 20f, 6f, 15f),
        };

        public static readonly IReadOnlyList<TerrainHill> Hills = new[]
        {
            new TerrainHill("Hill-East", OnRing(LandmarkRadius, 0f), 24f, 5f),
            new TerrainHill("Hill-North", OnRing(LandmarkRadius, 120f), 22f, 4.5f),
            new TerrainHill("Hill-West", OnRing(LandmarkRadius, 240f), 24f, 5f),
        };

        public static readonly IReadOnlyList<BattleRoyaleBuilding> Buildings = CreateBuildings();

        public static readonly IReadOnlyList<ModeObstacle> MapObstacles = CreateObstacles();

        private const int HillRings = 16;
        private const int HillSegments = 48;

        public BattleRoyaleSceneBuilder()
            : base(ScenePathValue, RulesPathValue)
        {
        }

        protected override IReadOnlyList<ModeSideLayout> Sides => SquadLayout();

        protected override Vector3 FloorCenter => MapCenter;

        protected override Vector3 FloorScale => MapFloorScale;

        protected override IReadOnlyList<ModeObstacle> Obstacles => MapObstacles;

        public static void Build()
        {
            new BattleRoyaleSceneBuilder().BuildSceneFromMenu();
        }

        public static float SquadBearing(int squad)
        {
            if (squad < 0 || squad >= SquadCount)
            {
                throw new ArgumentOutOfRangeException(nameof(squad), squad, $"squad must be in [0, {SquadCount})");
            }

            return -90f + (360f / SquadCount * squad);
        }

        // 三角関数の誤差でずれないよう mm に丸める。
        public static Vector3 OnRing(float radius, float bearingDegrees)
        {
            var radians = bearingDegrees * Mathf.Deg2Rad;
            var point = MapCenter + new Vector3(Mathf.Cos(radians) * radius, 0f, Mathf.Sin(radians) * radius);
            return new Vector3(RoundMillimeters(point.x), 0f, RoundMillimeters(point.z));
        }

        public static float GroundHeight(float x, float z)
        {
            var height = 0f;
            foreach (var hill in Hills)
            {
                height = Mathf.Max(height, hill.HeightAt(x, z));
            }

            return height;
        }

        public static IReadOnlyList<ModeSideLayout> SquadLayout()
        {
            var sides = new ModeSideLayout[SquadCount];
            for (var squad = 0; squad < SquadCount; squad++)
            {
                var bearing = SquadBearing(squad);
                var anchor = OnRing(SquadSpawnRadius, bearing);
                var facing = Inward(bearing);
                var spawns = new ModeSpawnPlacement[SquadSize];
                for (var i = 0; i < SquadSize; i++)
                {
                    spawns[i] = new ModeSpawnPlacement(anchor + (Tangent(bearing) * (SquadMemberSpacing * MemberSlot(i))), facing);
                }

                var bots = squad == 0 ? SquadSize - 1 : SquadSize;
                sides[squad] = new ModeSideLayout(new Side(squad), spawns, bots);
            }

            return sides;
        }

        public static Vector3 RallyPoint(int squad)
        {
            return OnRing(RallyRadius, SquadBearing(squad));
        }

        // 建物の中の物は壁から離し、どれも出現点から拾える範囲 (Pickup.DefaultRadius) の外で、遮蔽物と重ならない所に置く。
        public static IReadOnlyList<(string Name, Vector3 Position, PickupContents Contents)> PickupPlacements(
            WeaponDefinition rifle, WeaponDefinition handgun)
        {
            if (rifle == null)
            {
                throw new ArgumentNullException(nameof(rifle));
            }

            if (handgun == null)
            {
                throw new ArgumentNullException(nameof(handgun));
            }

            var spots = LootSpots();
            var placements = new (string, Vector3, PickupContents)[spots.Count];
            for (var i = 0; i < spots.Count; i++)
            {
                var (name, position, kind) = spots[i];
                placements[i] = (name, position, ContentsFor(kind, rifle, handgun));
            }

            return placements;
        }

        public static Mesh BuildHillMesh(int rings, int segments)
        {
            if (rings < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(rings), rings, "a hill needs at least one ring");
            }

            if (segments < 3)
            {
                throw new ArgumentOutOfRangeException(nameof(segments), segments, "a hill needs at least three segments");
            }

            var vertices = new Vector3[1 + (rings * segments)];
            var uv = new Vector2[vertices.Length];
            vertices[0] = new Vector3(0f, TerrainHill.Profile(0f), 0f);
            uv[0] = new Vector2(0.5f, 0.5f);
            for (var ring = 1; ring <= rings; ring++)
            {
                var r = (float)ring / rings;
                for (var s = 0; s < segments; s++)
                {
                    var angle = 2f * Mathf.PI * s / segments;
                    var x = Mathf.Cos(angle) * r;
                    var z = Mathf.Sin(angle) * r;
                    var index = VertexIndex(ring, s, segments);
                    vertices[index] = new Vector3(x, TerrainHill.Profile(r), z);
                    uv[index] = new Vector2((x * 0.5f) + 0.5f, (z * 0.5f) + 0.5f);
                }
            }

            // Unity は上から見て時計回りの面を表にするので、角度の大きい頂点を先に並べる。
            var triangles = new List<int>(segments * ((rings * 2) - 1) * 3);
            for (var s = 0; s < segments; s++)
            {
                var next = (s + 1) % segments;
                triangles.Add(0);
                triangles.Add(VertexIndex(1, next, segments));
                triangles.Add(VertexIndex(1, s, segments));
            }

            for (var ring = 1; ring < rings; ring++)
            {
                for (var s = 0; s < segments; s++)
                {
                    var next = (s + 1) % segments;
                    var inner = VertexIndex(ring, s, segments);
                    var innerNext = VertexIndex(ring, next, segments);
                    var outer = VertexIndex(ring + 1, s, segments);
                    var outerNext = VertexIndex(ring + 1, next, segments);
                    triangles.Add(outer);
                    triangles.Add(inner);
                    triangles.Add(innerNext);
                    triangles.Add(outer);
                    triangles.Add(innerNext);
                    triangles.Add(outerNext);
                }
            }

            var mesh = new Mesh { name = HillMeshName };
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uv);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        // 武器を拾う・エリアへ移るのは Runner が用事として渡すので、巡回は出現点と分隊の集合点の往復だけにする。
        protected override IReadOnlyList<Vector3> BotPatrolRoute(IReadOnlyList<ModeSideLayout> sides, int sideIndex, int spawnIndex)
        {
            return new[] { sides[sideIndex].Spawns[spawnIndex].Feet, RallyPoint(sides[sideIndex].Side.Id) };
        }

        protected override void AddLevel(Scene scene, Transform levelRoot)
        {
            // どの丘も同じ形を拡大して使うので、Mesh は 1 つだけ作って共有する。
            var mesh = BuildHillMesh(HillRings, HillSegments);
            foreach (var hill in Hills)
            {
                CreateHill(levelRoot, hill, mesh);
            }
        }

        protected override ModeRunnerBase AddRunner(GameObject host, ModeSceneContext context)
        {
            var runner = host.AddComponent<BattleRoyaleRunner>();
            runner.SquadCount = SquadCount;
            runner.SquadSize = SquadSize;
            runner.TimeLimitSeconds = BattleRoyaleRunner.DefaultTimeLimitSeconds;
            runner.StartsUnarmed = true;
            runner.BotPlanIntervalSeconds = BattleRoyaleRunner.DefaultBotPlanIntervalSeconds;
            runner.BotSearchRadius = BattleRoyaleBotPlanner.DefaultSearchRadius;
            return runner;
        }

        protected override void AddModeObjects(ModeSceneContext context)
        {
            var rifle = WeaponAssetBuilder.FindInCatalog(context.Catalog, PickupsObjectName, WeaponAssetBuilder.RifleId);
            var handgun = WeaponAssetBuilder.FindInCatalog(context.Catalog, PickupsObjectName, WeaponAssetBuilder.HandgunId);

            // 中身を先に作って検査し、不正なら何も置かない。建物の中の物は戸口から入れることも確かめる。
            var placements = PickupPlacements(rifle, handgun);
            var positions = PositionsOf(placements);
            SceneBuildHelpers.RequireOnNavMesh(positions, NavMeshCheckRadius, "pickup");
            SceneBuildHelpers.RequireReachable(PlayerFeet, positions, NavMeshCheckRadius, "pickup");

            var root = new GameObject(PickupsObjectName);
            SceneManager.MoveGameObjectToScene(root, context.Scene);
            foreach (var placement in placements)
            {
                var pointObject = new GameObject(placement.Name);
                pointObject.transform.SetParent(root.transform, false);
                pointObject.transform.position = placement.Position;
                var point = pointObject.AddComponent<PickupSpawnPoint>();
                point.Contents = placement.Contents;
                point.CollectMode = PickupCollectMode.Touch;

                // バトルロイヤルの物は取り合いなので、拾われたらもう出さない。
                point.RespawnSeconds = 0f;
            }

            var zoneObject = new GameObject(ZoneObjectName);
            SceneManager.MoveGameObjectToScene(zoneObject, context.Scene);
            zoneObject.transform.position = ZoneCenter;
            var zone = zoneObject.AddComponent<ShrinkingZone>();
            zone.InitialRadius = ZoneInitialRadius;
            zone.SetPhases(ZonePhases);
            zone.UseRandomSeed = true;

            // Runner が参加者だけを相手にして始めるので、自分では始めない。
            zone.BeginsOnStart = false;

            ((BattleRoyaleRunner)context.Runner).Zone = zone;

            // キットの loot.json には表が無く、表から引く物が無いので、どの箱にも決まった武器と回復と弾を入れる。
            var dropsObject = new GameObject(SupplyDropsObjectName);
            SceneManager.MoveGameObjectToScene(dropsObject, context.Scene);
            dropsObject.transform.position = ZoneCenter;
            var dropper = dropsObject.AddComponent<SupplyDropper>();
            dropper.SetFixedContents(SupplyCrateContents(rifle));
            ((BattleRoyaleRunner)context.Runner).SupplyDropper = dropper;
            EditorSceneManager.MarkSceneDirty(context.Scene);
        }

        public static IReadOnlyList<PickupContents> SupplyCrateContents(WeaponDefinition rifle)
        {
            if (rifle == null)
            {
                throw new ArgumentNullException(nameof(rifle));
            }

            return new[]
            {
                PickupContents.ForWeapon(rifle, WeaponPickupAmmoIfOwned),
                PickupContents.ForHealth(HealthPickupAmount),
                PickupContents.ForReserveAmmo(AmmoPickupAmount),
            };
        }

        private static void CreateHill(Transform parent, TerrainHill hill, Mesh mesh)
        {
            // 既定の材質を付けるため Cube から作り、形と当たりだけ丘に差し替える。
            var hillObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            hillObject.name = hill.Name;
            UnityEngine.Object.DestroyImmediate(hillObject.GetComponent<BoxCollider>());
            hillObject.GetComponent<MeshFilter>().sharedMesh = mesh;
            hillObject.AddComponent<MeshCollider>().sharedMesh = mesh;
            hillObject.transform.SetParent(parent, false);
            hillObject.transform.SetPositionAndRotation(
                new Vector3(hill.Center.x, -TerrainHill.Sink, hill.Center.z), Quaternion.identity);
            hillObject.transform.localScale = new Vector3(hill.Radius, hill.Height, hill.Radius);
            hillObject.isStatic = true;
        }

        private static IReadOnlyList<BattleRoyaleBuilding> CreateBuildings()
        {
            var farm = OnRing(LandmarkRadius, -60f);
            var depot = OnRing(LandmarkRadius, 60f);
            var outpost = OnRing(LandmarkRadius, 180f);
            return new[]
            {
                new BattleRoyaleBuilding("Town-NE", MapCenter + new Vector3(14f, 0f, 14f), 9f, 7f, BuildingDoors.South | BuildingDoors.West),
                new BattleRoyaleBuilding("Town-NW", MapCenter + new Vector3(-14f, 0f, 14f), 9f, 7f, BuildingDoors.South | BuildingDoors.East),
                new BattleRoyaleBuilding("Town-SE", MapCenter + new Vector3(14f, 0f, -14f), 9f, 7f, BuildingDoors.North | BuildingDoors.West),
                new BattleRoyaleBuilding("Town-SW", MapCenter + new Vector3(-14f, 0f, -14f), 9f, 7f, BuildingDoors.North | BuildingDoors.East),
                new BattleRoyaleBuilding("Farm-House", farm + new Vector3(-6f, 0f, 0f), 8f, 6f, BuildingDoors.North | BuildingDoors.South),
                new BattleRoyaleBuilding("Farm-Barn", farm + new Vector3(9f, 0f, 3f), 10f, 8f, BuildingDoors.East | BuildingDoors.West),
                new BattleRoyaleBuilding(
                    "Depot-Warehouse", depot, 16f, 10f, BuildingDoors.North | BuildingDoors.South | BuildingDoors.East | BuildingDoors.West),
                new BattleRoyaleBuilding("Outpost-Tower", outpost + new Vector3(0f, 0f, 6f), 6f, 6f, BuildingDoors.East),
                new BattleRoyaleBuilding("Outpost-Barracks", outpost + new Vector3(0f, 0f, -8f), 12f, 6f, BuildingDoors.East | BuildingDoors.West),
            };
        }

        private static IReadOnlyList<ModeObstacle> CreateObstacles()
        {
            var obstacles = new List<ModeObstacle>();
            foreach (var building in Buildings)
            {
                obstacles.AddRange(building.Obstacles());
            }

            obstacles.Add(Box("Town-Well", MapCenter, 0f, 0f, new Vector3(2f, 1f, 2f)));
            obstacles.Add(Box("Town-WallNorth", MapCenter, 0f, 6f, new Vector3(4f, 1.05f, 0.5f)));
            obstacles.Add(Box("Town-WallSouth", MapCenter, 0f, -6f, new Vector3(4f, 1.05f, 0.5f)));

            var farm = OnRing(LandmarkRadius, -60f);
            obstacles.Add(Box("Farm-Fence", farm, 0f, -9f, new Vector3(14f, 1.05f, 0.3f)));

            var depot = OnRing(LandmarkRadius, 60f);
            obstacles.Add(Box("Depot-CrateA", depot, -12f, 3f, new Vector3(1.5f, 1.5f, 1.5f)));
            obstacles.Add(Box("Depot-CrateB", depot, -12f, -1f, new Vector3(1.5f, 1.5f, 1.5f)));
            obstacles.Add(Box("Depot-Container", depot, 14f, 8f, new Vector3(6f, 2.6f, 2.5f)));

            var outpost = OnRing(LandmarkRadius, 180f);
            obstacles.Add(Box("Outpost-Sandbags", outpost, 9f, 0f, new Vector3(0.6f, 1.05f, 6f)));

            for (var squad = 0; squad < SquadCount; squad++)
            {
                var cluster = CoverCluster(squad);
                var name = ClusterName(squad);
                obstacles.Add(Box(name + "-Rock", cluster, 0f, 0f, new Vector3(3f, 2.2f, 2.5f)));
                obstacles.Add(Box(name + "-WallA", cluster, -5f, 2f, new Vector3(3.5f, 1.05f, 0.5f)));
                obstacles.Add(Box(name + "-WallB", cluster, 5f, -2f, new Vector3(3.5f, 1.05f, 0.5f)));
                obstacles.Add(Box(name + "-Crate", cluster, 2.5f, 3.5f, new Vector3(1.2f, 1.2f, 1.2f)));
            }

            return obstacles;
        }

        private static List<(string Name, Vector3 Position, LootKind Kind)> LootSpots()
        {
            var spots = new List<(string, Vector3, LootKind)>();

            for (var squad = 0; squad < SquadCount; squad++)
            {
                var bearing = SquadBearing(squad);
                var center = OnRing(SquadLootRadius, bearing);
                var side = Tangent(bearing) * SquadMemberSpacing;
                spots.Add(($"PickupSpawn-Squad{squad}-Rifle", center + side, LootKind.Rifle));
                spots.Add(($"PickupSpawn-Squad{squad}-Handgun", center - side, LootKind.Handgun));
            }

            // 建物の中は、戸口を結ぶ通り道を空けて左右 (奥行きの長い建物は前後) に 1 つずつ置く。
            var buildingLoot = new[]
            {
                (LootKind.Rifle, LootKind.Ammo),
                (LootKind.Handgun, LootKind.Health),
                (LootKind.Handgun, LootKind.Ammo),
                (LootKind.Rifle, LootKind.Health),
                (LootKind.Rifle, LootKind.Ammo),
                (LootKind.Handgun, LootKind.Health),
                (LootKind.Rifle, LootKind.Health),
                (LootKind.Rifle, LootKind.Ammo),
                (LootKind.Handgun, LootKind.Ammo),
            };
            for (var i = 0; i < Buildings.Count; i++)
            {
                var building = Buildings[i];
                var (first, second) = buildingLoot[i % buildingLoot.Length];
                var offset = new Vector3(building.Width / 4f, 0f, 0f);
                spots.Add(($"PickupSpawn-{building.Name}-{first}", building.Center - offset, first));
                spots.Add(($"PickupSpawn-{building.Name}-{second}", building.Center + offset, second));
            }

            spots.Add(("PickupSpawn-Town-Plaza-Health", MapCenter + new Vector3(5f, 0f, 0f), LootKind.Health));
            spots.Add(("PickupSpawn-Town-Plaza-Ammo", MapCenter + new Vector3(-5f, 0f, 0f), LootKind.Ammo));
            spots.Add(("PickupSpawn-Farm-Fence-Ammo", OnRing(LandmarkRadius, -60f) + new Vector3(0f, 0f, -11f), LootKind.Ammo));
            spots.Add(("PickupSpawn-Depot-Crates-Handgun", OnRing(LandmarkRadius, 60f) + new Vector3(-14.5f, 0f, 1f), LootKind.Handgun));
            spots.Add(("PickupSpawn-Depot-Warehouse-Rifle2", OnRing(LandmarkRadius, 60f) + new Vector3(0f, 0f, 2.5f), LootKind.Rifle));
            spots.Add(("PickupSpawn-Outpost-Sandbags-Health", OnRing(LandmarkRadius, 180f) + new Vector3(11f, 0f, 0f), LootKind.Health));

            foreach (var hill in Hills)
            {
                var top = hill.Center;
                spots.Add(($"PickupSpawn-{hill.Name}-Top-Rifle", OnGround(top), LootKind.Rifle));

                var inward = MapCenter - hill.Center;
                inward.y = 0f;
                var flank = hill.Center + (inward.normalized * (hill.Radius / 2f));
                spots.Add(($"PickupSpawn-{hill.Name}-Slope-Ammo", OnGround(flank), LootKind.Ammo));
            }

            for (var squad = 0; squad < SquadCount; squad++)
            {
                var cluster = CoverCluster(squad);
                var name = ClusterName(squad);
                var weapon = squad % 2 == 0 ? LootKind.Rifle : LootKind.Handgun;
                var supply = squad % 2 == 0 ? LootKind.Ammo : LootKind.Health;
                spots.Add(($"PickupSpawn-{name}-{weapon}", cluster + new Vector3(0f, 0f, -3.5f), weapon));
                spots.Add(($"PickupSpawn-{name}-{supply}", cluster + new Vector3(-5f, 0f, -1f), supply));
            }

            return spots;
        }

        private static PickupContents ContentsFor(LootKind kind, WeaponDefinition rifle, WeaponDefinition handgun)
        {
            switch (kind)
            {
                case LootKind.Rifle:
                    return PickupContents.ForWeapon(rifle, WeaponPickupAmmoIfOwned);
                case LootKind.Handgun:
                    return PickupContents.ForWeapon(handgun, WeaponPickupAmmoIfOwned);
                case LootKind.Ammo:
                    return PickupContents.ForReserveAmmo(AmmoPickupAmount);
                case LootKind.Health:
                    return PickupContents.ForHealth(HealthPickupAmount);
                default:
                    throw new ArgumentOutOfRangeException(nameof(kind), kind, "unknown loot kind");
            }
        }

        private static Vector3 CoverCluster(int squad)
        {
            return OnRing(CoverClusterRadius, SquadBearing(squad));
        }

        private static string ClusterName(int squad)
        {
            return $"Rocks-{squad}";
        }

        private static ModeObstacle Box(string name, Vector3 origin, float dx, float dz, Vector3 size)
        {
            return new ModeObstacle(name, new Vector3(origin.x + dx, size.y / 2f, origin.z + dz), size);
        }

        private static Vector3 OnGround(Vector3 point)
        {
            return new Vector3(point.x, GroundHeight(point.x, point.z), point.z);
        }

        private static Vector3 Inward(float bearingDegrees)
        {
            var radians = bearingDegrees * Mathf.Deg2Rad;
            return new Vector3(-Mathf.Cos(radians), 0f, -Mathf.Sin(radians));
        }

        private static Vector3 Tangent(float bearingDegrees)
        {
            var radians = bearingDegrees * Mathf.Deg2Rad;
            var tangent = new Vector3(-Mathf.Sin(radians), 0f, Mathf.Cos(radians));
            return new Vector3(RoundMillimeters(tangent.x), 0f, RoundMillimeters(tangent.z));
        }

        private static int MemberSlot(int index)
        {
            var step = (index + 1) / 2;
            return index % 2 == 1 ? step : -step;
        }

        private static float RoundMillimeters(float value)
        {
            return Mathf.Round(value * 1000f) / 1000f;
        }

        private static int VertexIndex(int ring, int segment, int segments)
        {
            return 1 + ((ring - 1) * segments) + segment;
        }

        private static List<Vector3> PositionsOf(IReadOnlyList<(string Name, Vector3 Position, PickupContents Contents)> placements)
        {
            var positions = new List<Vector3>(placements.Count);
            foreach (var placement in placements)
            {
                positions.Add(placement.Position);
            }

            return positions;
        }

        private enum LootKind
        {
            Rifle,
            Handgun,
            Ammo,
            Health,
        }
    }

    [Flags]
    public enum BuildingDoors
    {
        None = 0,
        North = 1,
        South = 2,
        East = 4,
        West = 8,
    }

    // 屋根の上にも NavMesh はできるが、地面から登る道は無い。
    public readonly struct BattleRoyaleBuilding
    {
        // 中を bot が立って歩ける天井の高さにする。
        public const float WallHeight = 3f;
        public const float WallThickness = 0.3f;
        public const float DoorWidth = 2.4f;
        public const float RoofThickness = 0.2f;

        public const float MinimumWallBeside = 0.5f;

        public BattleRoyaleBuilding(string name, Vector3 center, float width, float depth, BuildingDoors doors)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("a building needs a name", nameof(name));
            }

            var minimum = DoorWidth + (2f * (MinimumWallBeside + WallThickness));
            if (!(width >= minimum) || float.IsInfinity(width))
            {
                throw new ArgumentOutOfRangeException(nameof(width), width, $"building '{name}' must be at least {minimum}m wide");
            }

            if (!(depth >= minimum) || float.IsInfinity(depth))
            {
                throw new ArgumentOutOfRangeException(nameof(depth), depth, $"building '{name}' must be at least {minimum}m deep");
            }

            Name = name;
            Center = new Vector3(center.x, 0f, center.z);
            Width = width;
            Depth = depth;
            Doors = doors;
        }

        public string Name { get; }

        public Vector3 Center { get; }

        public float Width { get; }

        public float Depth { get; }

        public BuildingDoors Doors { get; }

        public bool ContainsInside(float x, float z)
        {
            var halfX = (Width / 2f) - WallThickness;
            var halfZ = (Depth / 2f) - WallThickness;
            return Mathf.Abs(x - Center.x) < halfX && Mathf.Abs(z - Center.z) < halfZ;
        }

        public IReadOnlyList<ModeObstacle> Obstacles()
        {
            var obstacles = new List<ModeObstacle>();
            var halfX = Width / 2f;
            var halfZ = Depth / 2f;
            var y = WallHeight / 2f;

            // 南北の壁は幅いっぱい、東西の壁はその間に収めて角で重ねない。
            AddWall(obstacles, "North", new Vector3(0f, y, halfZ - (WallThickness / 2f)), Width, alongX: true, (Doors & BuildingDoors.North) != 0);
            AddWall(obstacles, "South", new Vector3(0f, y, -halfZ + (WallThickness / 2f)), Width, alongX: true, (Doors & BuildingDoors.South) != 0);
            var inner = Depth - (2f * WallThickness);
            AddWall(obstacles, "East", new Vector3(halfX - (WallThickness / 2f), y, 0f), inner, alongX: false, (Doors & BuildingDoors.East) != 0);
            AddWall(obstacles, "West", new Vector3(-halfX + (WallThickness / 2f), y, 0f), inner, alongX: false, (Doors & BuildingDoors.West) != 0);

            obstacles.Add(new ModeObstacle(
                Name + "-Roof",
                Center + new Vector3(0f, WallHeight + (RoofThickness / 2f), 0f),
                new Vector3(Width, RoofThickness, Depth)));
            return obstacles;
        }

        private void AddWall(List<ModeObstacle> obstacles, string side, Vector3 offset, float length, bool alongX, bool hasDoor)
        {
            var baseName = Name + "-Wall" + side;
            if (!hasDoor)
            {
                obstacles.Add(new ModeObstacle(baseName, Center + offset, WallSize(length, alongX)));
                return;
            }

            var piece = (length - DoorWidth) / 2f;
            var shift = (DoorWidth / 2f) + (piece / 2f);
            var along = alongX ? Vector3.right : Vector3.forward;
            obstacles.Add(new ModeObstacle(baseName + "-A", Center + offset - (along * shift), WallSize(piece, alongX)));
            obstacles.Add(new ModeObstacle(baseName + "-B", Center + offset + (along * shift), WallSize(piece, alongX)));
        }

        private static Vector3 WallSize(float length, bool alongX)
        {
            return alongX
                ? new Vector3(length, WallHeight, WallThickness)
                : new Vector3(WallThickness, WallHeight, length);
        }
    }

    public readonly struct TerrainHill
    {
        // 縁を床と重ねるとちらつくので、床より少し沈める。
        public const float Sink = 0.05f;

        // NavMesh (45°) とプレイヤーの CharacterController の登れる坂より十分に緩くする。
        public const float MaxSlope = 0.45f;

        public TerrainHill(string name, Vector3 center, float radius, float height)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("a hill needs a name", nameof(name));
            }

            if (!(radius > 0f) || float.IsInfinity(radius))
            {
                throw new ArgumentOutOfRangeException(nameof(radius), radius, $"hill '{name}' needs a positive finite radius");
            }

            if (!(height > Sink) || float.IsInfinity(height))
            {
                throw new ArgumentOutOfRangeException(nameof(height), height, $"hill '{name}' must rise above the floor");
            }

            var slope = SteepestSlope(radius, height);
            if (slope > MaxSlope)
            {
                throw new ArgumentOutOfRangeException(nameof(height), height, $"hill '{name}' is too steep ({slope} > {MaxSlope})");
            }

            Name = name;
            Center = new Vector3(center.x, 0f, center.z);
            Radius = radius;
            Height = height;
        }

        public string Name { get; }

        public Vector3 Center { get; }

        public float Radius { get; }

        public float Height { get; }

        public float Slope => SteepestSlope(Radius, Height);

        public static float Profile(float t)
        {
            if (!(t < 1f))
            {
                return 0f;
            }

            return (1f + Mathf.Cos(Mathf.PI * Mathf.Max(0f, t))) / 2f;
        }

        public float HeightAt(float x, float z)
        {
            var distance = new Vector2(x - Center.x, z - Center.z).magnitude;
            return Mathf.Max(0f, (Height * Profile(distance / Radius)) - Sink);
        }

        private static float SteepestSlope(float radius, float height)
        {
            // Profile の傾きの最大は π/2 (t = 0.5) なので、高さと半径で割り戻す。
            return height * Mathf.PI / (2f * radius);
        }
    }
}
