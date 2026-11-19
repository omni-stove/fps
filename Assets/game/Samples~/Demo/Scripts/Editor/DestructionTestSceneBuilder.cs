using System;
using System.Collections.Generic;
using System.IO;
using Shooter.Core;
using Shooter.Runtime;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Shooter.Editor
{
    // GameBootstrap が z = 10 に標的ダミーを並べるので、小屋はその奥に置く。
    // 壁の角の部品は正面と背面の壁に含め、左右の壁は角を除いた列にする (DestructionSmokeRun が角を正面・背面として撃つ)。
    public static class DestructionTestSceneBuilder
    {
        public static string ScenePath => BuildSceneList.DestructionTestScene;

        // Unity の慣習どおり、焼いた NavMesh はシーン名のフォルダに置く。
        public static string NavMeshDataPath => ModeSceneBuilder.NavMeshDataPathFor(ScenePath);

        public const string MenuPath = SceneBuildSteps.MenuPath;
        public const string LogTag = "[DestructionTestSceneBuilder]";

        public const string FloorName = "Floor";
        public const string HutName = "Hut";
        public const string DebrisPoolName = DebrisPool.DefaultObjectName;
        public const string BootstrapObjectName = "GameBootstrap";

        public const string FrontWallName = "Front";
        public const string BackWallName = "Back";
        public const string LeftWallName = "Left";
        public const string RightWallName = "Right";
        public const string RoofName = "Roof";

        // Plane は 10m 四方なので、6 倍で 60m 四方にして小屋の奥にも歩ける余白を残す。
        public static readonly Vector3 FloorScale = new Vector3(6f, 1f, 6f);

        public static readonly Vector3 HutCenter = new Vector3(0f, 0f, 20f);

        public const int HutWidth = 4;

        public const int HutDepth = 4;

        // launcher の被害の半径 (WeaponAssetBuilder.LauncherExplosionRadius) より低いと、
        // 下段を撃つたびに屋根まで直接壊れてしまい、下段を失って支えの無くなった部品の崩落が起きにくくなる。
        public const int WallHeight = 4;

        public const float PieceSize = 1f;

        public const int PieceMaxHealth = DestructiblePiece.DefaultMaxHealth;

        public const int WallPieceCount = ((HutWidth * 2) + ((HutDepth - 2) * 2)) * WallHeight;

        public const int RoofPieceCount = HutWidth * HutDepth;

        public const int PieceCount = WallPieceCount + RoofPieceCount;

        public const int GroundedPieceCount = WallPieceCount / WallHeight;

        public const float NavMeshCheckRadius = 1f;

        public const float AroundHutDistance = 2f;

        public static void Build()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                Debug.Log(LogTag + " cancelled");
                return;
            }

            BuildAt(ScenePath, NavMeshDataPath);
        }

        public static void BuildAt(string scenePath, string navMeshDataPath)
        {
            scenePath = BootstrapSceneBuilder.ValidateScenePath(scenePath);
            navMeshDataPath = SceneBuildHelpers.ValidateAssetPath(navMeshDataPath);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // NewScene(Single) は参照されていないアセットをアンロードし、先に読んだ定義が null になるので、シーンを作ってから読む。
            var playerWeapons = LoadPlayerWeapons();
            var rules = GameRulesAssetBuilder.LoadRules(GameRulesAssetBuilder.RangePath);
            var pieceVisualPrefab = DestructiblePieceVisual.Load();

            SceneBuildHelpers.EnsureFolder(Path.GetDirectoryName(scenePath));
            SceneBuildHelpers.EnsureFolder(Path.GetDirectoryName(navMeshDataPath));

            // 小屋の部品は NavMeshObstacle で NavMesh を削るので、壊れたところを通れるよう床だけを置いた状態で焼く。
            var surface = CreateLevel(scene);
            SceneBuildHelpers.BakeNavMesh(surface);
            SceneBuildHelpers.SaveNavMeshData(surface, navMeshDataPath);
            SceneBuildHelpers.RequireOnNavMesh(NavMeshCheckPoints(), NavMeshCheckRadius, "point");
            CreateActors(scene, playerWeapons, rules, pieceVisualPrefab);

            if (!EditorSceneManager.SaveScene(scene, scenePath))
            {
                throw new IOException($"Failed to save the destruction test scene to '{scenePath}'");
            }

            BuildSceneList.Register(scenePath);
            AssetDatabase.SaveAssets();

            Debug.Log($"{LogTag} saved '{scenePath}' and '{navMeshDataPath}'");
        }

        public static WeaponDefinition[] LoadPlayerWeapons()
        {
            var launcher = WeaponAssetBuilder.LoadLauncher();
            var loadout = WeaponAssetBuilder.LoadPlayerLoadout();
            var weapons = new WeaponDefinition[loadout.Length + 1];
            weapons[0] = launcher;
            Array.Copy(loadout, 0, weapons, 1, loadout.Length);
            return weapons;
        }

        public static Vector3[] NavMeshCheckPoints()
        {
            var halfX = (HutWidth * PieceSize / 2f) + AroundHutDistance;
            var halfZ = (HutDepth * PieceSize / 2f) + AroundHutDistance;
            return new[]
            {
                Vector3.zero,
                HutCenter + new Vector3(0f, 0f, -halfZ),
                HutCenter + new Vector3(0f, 0f, halfZ),
                HutCenter + new Vector3(-halfX, 0f, 0f),
                HutCenter + new Vector3(halfX, 0f, 0f),
            };
        }

        public static NavMeshSurface CreateLevel(Scene scene)
        {
            SceneBuildHelpers.RequireEmpty(scene, "the level");

            var floor = SceneBuildHelpers.CreateStaticPlane(scene, null, FloorName, Vector3.zero, FloorScale);
            var surface = SceneBuildHelpers.AddNavMeshSurface(floor, CollectObjects.All);
            SceneBuildHelpers.CreatePerimeterWalls(scene, null, Vector3.zero, SceneBuildHelpers.PlaneHalfExtents(FloorScale));

            EditorSceneManager.MarkSceneDirty(scene);
            return surface;
        }

        public static DestructibleStructure CreateActors(
            Scene scene, IReadOnlyList<WeaponDefinition> playerWeapons, GameRulesDefinition rules, GameObject pieceVisualPrefab = null)
        {
            SceneBuildHelpers.RequireLoaded(scene);
            SceneBuildHelpers.RequirePlayerWeapons(playerWeapons, nameof(playerWeapons));

            if (rules == null)
            {
                throw new ArgumentNullException(nameof(rules));
            }

            // 不正なルールを配線しないよう、何か置く前に検査する。
            var destructionEnabled = rules.ToRules().Destruction == Destruction.On;

            var bootstrapObject = new GameObject(BootstrapObjectName);
            SceneManager.MoveGameObjectToScene(bootstrapObject, scene);
            var bootstrap = bootstrapObject.AddComponent<GameBootstrap>();
            bootstrap.CreatesFloor = false;
            bootstrap.WeaponDefinitions = playerWeapons;
            bootstrap.RulesDefinition = rules;

            var poolObject = new GameObject(DebrisPoolName);
            SceneManager.MoveGameObjectToScene(poolObject, scene);
            var pool = poolObject.AddComponent<DebrisPool>();

            var hut = CreateHut(scene, destructionEnabled, pieceVisualPrefab);
            hut.DebrisPool = pool;

            EditorSceneManager.MarkSceneDirty(scene);
            return hut;
        }

        // 部品の隣接は DestructibleStructure がコライダーの重なりから作るので、ここでは並べるだけにする。
        public static DestructibleStructure CreateHut(Scene scene, bool destructionEnabled, GameObject pieceVisualPrefab = null)
        {
            SceneBuildHelpers.RequireLoaded(scene);

            var root = new GameObject(HutName);
            SceneManager.MoveGameObjectToScene(root, scene);
            root.transform.SetPositionAndRotation(HutCenter, Quaternion.identity);

            try
            {
                var structure = root.AddComponent<DestructibleStructure>();
                structure.DestructionEnabled = destructionEnabled;

                var index = 0;
                var minX = -((HutWidth - 1) * PieceSize / 2f);
                var minZ = -((HutDepth - 1) * PieceSize / 2f);
                var maxX = -minX;
                var maxZ = -minZ;
                var front = CreateGroup(root.transform, FrontWallName);
                var back = CreateGroup(root.transform, BackWallName);
                var left = CreateGroup(root.transform, LeftWallName);
                var right = CreateGroup(root.transform, RightWallName);
                var roof = CreateGroup(root.transform, RoofName);

                for (var y = 0; y < WallHeight; y++)
                {
                    var height = (y + 0.5f) * PieceSize;
                    var grounded = y == 0;
                    for (var x = 0; x < HutWidth; x++)
                    {
                        var px = minX + (x * PieceSize);
                        CreatePiece(front, x, y, new Vector3(px, height, minZ), ref index, grounded, pieceVisualPrefab);
                        CreatePiece(back, x, y, new Vector3(px, height, maxZ), ref index, grounded, pieceVisualPrefab);
                    }

                    for (var z = 1; z < HutDepth - 1; z++)
                    {
                        var pz = minZ + (z * PieceSize);
                        CreatePiece(left, z, y, new Vector3(minX, height, pz), ref index, grounded, pieceVisualPrefab);
                        CreatePiece(right, z, y, new Vector3(maxX, height, pz), ref index, grounded, pieceVisualPrefab);
                    }
                }

                var roofHeight = (WallHeight + 0.5f) * PieceSize;
                for (var x = 0; x < HutWidth; x++)
                {
                    for (var z = 0; z < HutDepth; z++)
                    {
                        CreatePiece(roof, x, z, new Vector3(minX + (x * PieceSize), roofHeight, minZ + (z * PieceSize)), ref index, false, pieceVisualPrefab);
                    }
                }

                return structure;
            }
            catch
            {
                UnityEngine.Object.DestroyImmediate(root);
                throw;
            }
        }

        private static Transform CreateGroup(Transform parent, string name)
        {
            var group = new GameObject(name).transform;
            group.SetParent(parent, false);
            return group;
        }

        private static void CreatePiece(
            Transform group, int column, int row, Vector3 hutLocalPosition, ref int index, bool grounded, GameObject visualPrefab)
        {
            // グループは小屋の原点に置いたままなので、小屋から見た位置をそのまま使える。
            DestructiblePiece.Create(
                group,
                $"{group.name}-{column}-{row}",
                hutLocalPosition,
                Vector3.one * PieceSize,
                index,
                PieceMaxHealth,
                grounded,
                visualPrefab: visualPrefab);
            index++;
        }
    }
}
