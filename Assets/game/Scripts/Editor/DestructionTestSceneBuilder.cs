using System;
using System.Collections.Generic;
using System.IO;
using Fps.Core;
using Fps.Runtime;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Fps.Editor
{
    /// <summary>
    /// 建造物破壊の動作確認用シーン DestructionTest を生成する。床に <see cref="NavMeshSurface"/> を置いて NavMesh を焼き、
    /// NavMeshData をシーンの隣にアセットとして保存してから、壊せる小屋 (<see cref="DestructibleStructure"/>)・破片の置き場
    /// (<see cref="DebrisPool"/>)・<see cref="GameBootstrap"/> (床は生成しない設定) を置く。
    /// プレイヤーは launcher を持って始まり、小屋が壊れるかは射撃場のルール (<see cref="GameRulesAssetBuilder.RangePath"/>) の
    /// <see cref="Destruction"/> で決める。プレイヤーとライトは実行時に GameBootstrap が作る。
    /// 何度実行しても同じ結果になる。Editor を閉じた状態で次のように呼べる:
    /// <code>unity -batchmode -quit -projectPath . -executeMethod Fps.Editor.DestructionTestSceneBuilder.Build</code>
    /// </summary>
    /// <remarks>
    /// 配置 (上から見て、プレイヤーは原点で +Z を向く):
    /// GameBootstrap が z = 10 に標的ダミーを並べるので、小屋はその奥の <see cref="HutCenter"/> に置き、正面の壁は z = 18 になる。
    /// 小屋は <see cref="HutWidth"/> × <see cref="HutDepth"/> の床面に高さ <see cref="WallHeight"/> の壁 4 面と屋根を 1m の Cube で組む。
    /// 壁の角の部品は正面と背面の壁に含め、左右の壁は角を除いた列にする。
    /// </remarks>
    public static class DestructionTestSceneBuilder
    {
        public const string ScenePath = BuildSceneList.DestructionTestScene;

        /// <summary>焼いた NavMesh。Unity の慣習どおりシーン名のフォルダに置く。</summary>
        public const string NavMeshDataPath = "Assets/game/Scenes/DestructionTest/NavMesh-Floor.asset";

        public const string MenuPath = "Fps/Build Destruction Test Scene";
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

        /// <summary>Plane は 10m 四方なので、6 倍で 60m 四方 (x, z とも ±30m)。小屋の奥にも歩ける余白を残す。</summary>
        public static readonly Vector3 FloorScale = new Vector3(6f, 1f, 6f);

        /// <summary>小屋の床面の中心 (地面の高さ)。</summary>
        public static readonly Vector3 HutCenter = new Vector3(0f, 0f, 20f);

        /// <summary>小屋の x 方向の幅 (部品の数 = m)。</summary>
        public const int HutWidth = 4;

        /// <summary>小屋の z 方向の奥行き (部品の数 = m)。</summary>
        public const int HutDepth = 4;

        /// <summary>
        /// 壁の高さ (部品の数 = m)。launcher の被害の半径 (<see cref="WeaponAssetBuilder.LauncherExplosionRadius"/>) より低いと、
        /// 下段を撃つたびに屋根まで直接壊れてしまい、下段を失って支えの無くなった部品の崩落が起きにくくなる。
        /// </summary>
        public const int WallHeight = 4;

        /// <summary>部品 1 つの一辺 (m)。</summary>
        public const float PieceSize = 1f;

        public const int PieceMaxHealth = DestructiblePiece.DefaultMaxHealth;

        /// <summary>壁の部品の数。角は正面と背面に含める。</summary>
        public const int WallPieceCount = ((HutWidth * 2) + ((HutDepth - 2) * 2)) * WallHeight;

        /// <summary>屋根の部品の数。</summary>
        public const int RoofPieceCount = HutWidth * HutDepth;

        public const int PieceCount = WallPieceCount + RoofPieceCount;

        /// <summary>地面に接する (壁の下段の) 部品の数。</summary>
        public const int GroundedPieceCount = WallPieceCount / WallHeight;

        /// <summary>NavMesh に乗っていることを確かめる点を探す半径 (メートル)。</summary>
        public const float NavMeshCheckRadius = 1f;

        /// <summary>小屋の外周から、NavMesh に乗っていることを確かめる点までの距離 (m)。</summary>
        public const float AroundHutDistance = 2f;

        [MenuItem(MenuPath)]
        public static void Build()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                Debug.Log(LogTag + " cancelled");
                return;
            }

            BuildAt(ScenePath, NavMeshDataPath);
        }

        /// <summary>
        /// シーンを生成して NavMesh を焼き、両方を保存して Build Settings に加える (既存の並びは変えない)。
        /// 武器・ルールのアセットが無いか不正なら何も保存せずに例外を投げる (batchmode では終了コード 1 になる)。
        /// </summary>
        public static void BuildAt(string scenePath, string navMeshDataPath)
        {
            scenePath = BootstrapSceneBuilder.ValidateScenePath(scenePath);
            navMeshDataPath = SceneBuildHelpers.ValidateAssetPath(navMeshDataPath);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // NewScene(Single) は参照されていないアセットをアンロードし、先に読んだ定義が null になるので、シーンを作ってから読む。
            var playerWeapons = LoadPlayerWeapons();
            var rules = GameRulesAssetBuilder.LoadRules(GameRulesAssetBuilder.RangePath);

            SceneBuildHelpers.EnsureFolder(Path.GetDirectoryName(scenePath));
            SceneBuildHelpers.EnsureFolder(Path.GetDirectoryName(navMeshDataPath));

            // 小屋の部品は NavMeshObstacle で NavMesh を削るので、壊れたところを通れるよう床だけを置いた状態で焼く。
            var surface = CreateLevel(scene);
            SceneBuildHelpers.BakeNavMesh(surface);
            SceneBuildHelpers.SaveNavMeshData(surface, navMeshDataPath);
            SceneBuildHelpers.RequireOnNavMesh(NavMeshCheckPoints(), NavMeshCheckRadius, "point");
            CreateActors(scene, playerWeapons, rules);

            if (!EditorSceneManager.SaveScene(scene, scenePath))
            {
                throw new IOException($"Failed to save the destruction test scene to '{scenePath}'");
            }

            BuildSceneList.Register(scenePath);
            AssetDatabase.SaveAssets();

            Debug.Log($"{LogTag} saved '{scenePath}' and '{navMeshDataPath}'");
        }

        /// <summary>
        /// プレイヤーに持たせる武器。launcher (<see cref="WeaponAssetBuilder.LoadLauncher"/>) を先頭にし、
        /// 続けて既定のロードアウト (<see cref="WeaponAssetBuilder.LoadPlayerLoadout"/>) を並べる。
        /// どれかのアセットが無いか不正なら例外を投げる。
        /// </summary>
        public static WeaponDefinition[] LoadPlayerWeapons()
        {
            var launcher = WeaponAssetBuilder.LoadLauncher();
            var loadout = WeaponAssetBuilder.LoadPlayerLoadout();
            var weapons = new WeaponDefinition[loadout.Length + 1];
            weapons[0] = launcher;
            Array.Copy(loadout, 0, weapons, 1, loadout.Length);
            return weapons;
        }

        /// <summary>プレイヤーの立つ位置と、小屋の四方の外側。焼いた NavMesh に乗っているべき点 (足元)。</summary>
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

        /// <summary>床 (NavMeshSurface 付き) を空のシーンに置き、床の NavMeshSurface を返す。焼くのは呼び出し側。</summary>
        public static NavMeshSurface CreateLevel(Scene scene)
        {
            SceneBuildHelpers.RequireEmpty(scene, "the level");

            var floor = SceneBuildHelpers.CreateStaticPlane(scene, null, FloorName, Vector3.zero, FloorScale);
            var surface = SceneBuildHelpers.AddNavMeshSurface(floor, CollectObjects.All);

            EditorSceneManager.MarkSceneDirty(scene);
            return surface;
        }

        /// <summary>
        /// GameBootstrap (床は作らない)・破片の置き場・小屋を置き、小屋を返す。参照はすべてここで設定する。
        /// プレイヤーは playerWeapons (通常は <see cref="LoadPlayerWeapons"/>) を持ち、rules を効かせる。
        /// 小屋が弾で壊れるかは rules の <see cref="GameRules.Destruction"/> で決める。
        /// 引数が不正なら何も置かずに例外を投げる。
        /// </summary>
        public static DestructibleStructure CreateActors(Scene scene, IReadOnlyList<WeaponDefinition> playerWeapons, GameRulesDefinition rules)
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

            var hut = CreateHut(scene, destructionEnabled);
            hut.DebrisPool = pool;

            EditorSceneManager.MarkSceneDirty(scene);
            return hut;
        }

        /// <summary>
        /// <see cref="HutCenter"/> に小屋を置いて返す。壁 4 面 (下段は地面に接する) と屋根を 1m の Cube の部品で組み、
        /// 部品の添字は作った順に 0 から振る。部品の隣接は <see cref="DestructibleStructure"/> がコライダーの重なりから作る。
        /// </summary>
        public static DestructibleStructure CreateHut(Scene scene, bool destructionEnabled)
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
                        CreatePiece(front, x, y, new Vector3(px, height, minZ), ref index, grounded);
                        CreatePiece(back, x, y, new Vector3(px, height, maxZ), ref index, grounded);
                    }

                    for (var z = 1; z < HutDepth - 1; z++)
                    {
                        var pz = minZ + (z * PieceSize);
                        CreatePiece(left, z, y, new Vector3(minX, height, pz), ref index, grounded);
                        CreatePiece(right, z, y, new Vector3(maxX, height, pz), ref index, grounded);
                    }
                }

                var roofHeight = (WallHeight + 0.5f) * PieceSize;
                for (var x = 0; x < HutWidth; x++)
                {
                    for (var z = 0; z < HutDepth; z++)
                    {
                        CreatePiece(roof, x, z, new Vector3(minX + (x * PieceSize), roofHeight, minZ + (z * PieceSize)), ref index, false);
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

        private static void CreatePiece(Transform group, int column, int row, Vector3 hutLocalPosition, ref int index, bool grounded)
        {
            // グループは小屋の原点に置いたままなので、小屋から見た位置をそのまま使える。
            DestructiblePiece.Create(
                group,
                $"{group.name}-{column}-{row}",
                hutLocalPosition,
                Vector3.one * PieceSize,
                index,
                PieceMaxHealth,
                grounded);
            index++;
        }
    }
}
