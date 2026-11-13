using System;
using System.Collections.Generic;
using System.IO;
using Fps.Core;
using Fps.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Fps.Editor
{
    /// <summary>
    /// 三人称で TPS のアクションを試す射撃場 TpsRange を生成する。シーンには肩越し視点 (<see cref="ViewMode.ThirdPerson"/>) の
    /// <see cref="GameBootstrap"/> と <see cref="MatchRunner"/> を置いた GameObject、高低の遮蔽物 (<see cref="Covers"/>)、
    /// 乗り越え用の低い壁 (<see cref="VaultWall"/>)、回避と手榴弾を試す開けた場所の目印 (<see cref="OpenAreaName"/>)、
    /// 壊せる小屋 (<see cref="HutName"/>) とその破片の置き場を入れる。床・プレイヤー・標的ダミー・HUD は実行時に GameBootstrap が作る。
    /// ルールは <see cref="GameRulesAssetBuilder.TpsRangePath"/> を配線する。保存したシーンは Build Settings に (並びを変えずに) 加える。
    /// 何度実行しても同じ結果になる。Editor を閉じた状態で次のように呼べる:
    /// <code>unity -batchmode -quit -projectPath . -executeMethod Fps.Editor.TpsRangeSceneBuilder.Build</code>
    /// </summary>
    /// <remarks>
    /// 配置 (上から見て、プレイヤーは原点で +Z を向き、GameBootstrap が z = 10 に標的ダミーを x = -4..4 で並べる):
    /// 遮蔽物は出現位置と標的の間とその周り、乗り越えの壁は左 (x = -10)、開けた場所は右手前 (x = 8..20, z = -12..0)、
    /// 小屋は右奥 (<see cref="HutCenter"/>) に置き、どれも GameBootstrap の床 (x, z とも ±25m) に収める。
    /// </remarks>
    public static class TpsRangeSceneBuilder
    {
        public const string ScenePath = BuildSceneList.TpsRangeScene;
        public const string MenuPath = "Fps/Build TPS Range Scene";
        public const string LogTag = "[TpsRangeSceneBuilder]";

        public const string BootstrapObjectName = "GameBootstrap";
        public const string CoversName = BootstrapSceneBuilder.CoversName;
        public const string LowCoverName = BootstrapSceneBuilder.LowCoverName;
        public const string HighCoverName = BootstrapSceneBuilder.HighCoverName;
        public const string LowCoverBackName = "Cover_Low_Back";
        public const string HighPillarName = "Cover_High_Pillar";
        public const string VaultWallName = "VaultWall";
        public const string OpenAreaName = "OpenArea";
        public const string HutName = DestructionTestSceneBuilder.HutName;
        public const string DebrisPoolName = DestructionTestSceneBuilder.DebrisPoolName;

        /// <summary>
        /// 乗り越え用の壁の高さ (m)。<see cref="MotorSpec.DefaultVaultMaxHeight"/> より低く、プレイヤーのジャンプ
        /// (<see cref="PlayerController.DefaultJumpHeight"/>) でも越えられる。<see cref="CoverProbe.LowWallHeight"/> 未満なので低い遮蔽物にもなる。
        /// </summary>
        public const float VaultWallHeight = 0.8f;

        /// <summary>乗り越え用の壁の厚さ (m)。乗り越えの軌道が短く済むよう遮蔽物より薄くする。</summary>
        public const float VaultWallThickness = 0.3f;

        /// <summary>開けた場所の中心 (床の高さ)。この周り <see cref="OpenAreaHalfExtent"/> には何も置かない。</summary>
        public static readonly Vector3 OpenAreaCenter = new Vector3(14f, 0f, -6f);

        /// <summary>開けた場所の x, z 方向の半分の広さ (m)。回避 (<see cref="MotorSpec.DefaultDodgeDistance"/>) を何度か続けられる広さ。</summary>
        public const float OpenAreaHalfExtent = 6f;

        /// <summary>壊せる小屋の床面の中心 (地面の高さ)。大きさは <see cref="DestructionTestSceneBuilder"/> の小屋と同じ。</summary>
        public static readonly Vector3 HutCenter = new Vector3(14f, 0f, 14f);

        /// <summary>GameBootstrap が作る床の x, z 方向の半分の広さ (m)。Plane は 10m 四方。</summary>
        public static readonly float FloorHalfExtent = GameBootstrap.FloorScale.x * 10f / 2f;

        /// <summary>
        /// 遮蔽物。0, 1 番は Bootstrap の射撃場と同じ低い壁と高い壁 (<see cref="BootstrapSceneBuilder.Covers"/>) で、
        /// 出現位置の後ろに低い壁、右前に高い柱を足す。どれも +Z (標的) 側へ撃てる向きに置く。
        /// </summary>
        public static readonly IReadOnlyList<LevelBox> Covers = new[]
        {
            BootstrapSceneBuilder.LowCover,
            BootstrapSceneBuilder.HighCover,
            new LevelBox(
                LowCoverBackName,
                new Vector3(0f, BootstrapSceneBuilder.LowCoverHeight / 2f, -4f),
                new Vector3(2f, BootstrapSceneBuilder.LowCoverHeight, BootstrapSceneBuilder.CoverThickness)),
            new LevelBox(
                HighPillarName,
                new Vector3(7f, BootstrapSceneBuilder.HighCoverHeight / 2f, 2f),
                new Vector3(1f, BootstrapSceneBuilder.HighCoverHeight, 1f)),
        };

        /// <summary>乗り越え用の低い壁。+Z へ歩いて越える向きに置く。</summary>
        public static readonly LevelBox VaultWall = new LevelBox(
            VaultWallName, new Vector3(-10f, VaultWallHeight / 2f, 2f), new Vector3(4f, VaultWallHeight, VaultWallThickness));

        /// <summary>低い遮蔽物 (<see cref="Covers"/> の 0 番)。</summary>
        public static LevelBox LowCover => Covers[0];

        [MenuItem(MenuPath)]
        public static void Build()
        {
            // メニューから呼ばれたとき、開いているシーンの未保存の変更を黙って捨てない。
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                Debug.Log(LogTag + " cancelled");
                return;
            }

            BuildAt(ScenePath);
        }

        /// <summary>
        /// path にシーンを生成・保存し、Build Settings に加える (既存の並びは変えない)。
        /// 武器・ルールのアセットが無いか不正なら何も保存せずに例外を投げる (batchmode では終了コード 1 になる)。
        /// </summary>
        public static void BuildAt(string path)
        {
            path = BootstrapSceneBuilder.ValidateScenePath(path);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // NewScene(Single) は参照されていないアセットをアンロードし、先に読んだ定義が null になるので、シーンを作ってから読む。
            var weapons = LoadPlayerWeapons();
            var rules = GameRulesAssetBuilder.LoadRules(GameRulesAssetBuilder.TpsRangePath);

            SceneBuildHelpers.EnsureFolder(Path.GetDirectoryName(path));
            PopulateScene(scene, weapons, rules);

            if (!EditorSceneManager.SaveScene(scene, path))
            {
                throw new IOException($"Failed to save the TPS range scene to '{path}'");
            }

            BuildSceneList.Register(path);
            AssetDatabase.SaveAssets();

            Debug.Log($"{LogTag} saved '{path}' and registered it in Build Settings");
        }

        /// <summary>
        /// プレイヤーに持たせる武器。既定のロードアウト (<see cref="WeaponAssetBuilder.LoadPlayerLoadout"/>) で始め、
        /// 小屋を壊しやすいよう launcher (<see cref="WeaponAssetBuilder.LoadLauncher"/>) を最後に足す。
        /// どれかのアセットが無いか不正なら例外を投げる。
        /// </summary>
        public static WeaponDefinition[] LoadPlayerWeapons()
        {
            var loadout = WeaponAssetBuilder.LoadPlayerLoadout();
            var launcher = WeaponAssetBuilder.LoadLauncher();
            var weapons = new WeaponDefinition[loadout.Length + 1];
            Array.Copy(loadout, weapons, loadout.Length);
            weapons[loadout.Length] = launcher;
            return weapons;
        }

        /// <summary>
        /// 空のシーンに GameBootstrap (三人称、weapons と rules を配線) と MatchRunner を置いた GameObject、遮蔽物、乗り越えの壁、
        /// 開けた場所の目印、壊せる小屋と破片の置き場を入れ、GameBootstrap を返す。小屋が壊れるかは rules の
        /// <see cref="GameRules.Destruction"/> で決める。引数が不正なら何も置かずに例外を投げる。
        /// </summary>
        public static GameBootstrap PopulateScene(Scene scene, IReadOnlyList<WeaponDefinition> weapons, GameRulesDefinition rules)
        {
            SceneBuildHelpers.RequireEmpty(scene, "the TPS range");
            SceneBuildHelpers.RequirePlayerWeapons(weapons, nameof(weapons));
            if (rules == null)
            {
                throw new ArgumentNullException(nameof(rules));
            }

            // 不正なルールを配線しないよう、何か置く前に検査する。
            var destructionEnabled = rules.ToRules().Destruction == Destruction.On;

            var created = new List<GameObject>();
            try
            {
                // MatchRunner は Start で同じ GameObject の GameBootstrap が Awake で作ったものを拾うので、ここでの配線は要らない。
                var bootstrapObject = new GameObject(BootstrapObjectName, typeof(GameBootstrap), typeof(MatchRunner));
                created.Add(bootstrapObject);
                SceneManager.MoveGameObjectToScene(bootstrapObject, scene);
                var bootstrap = bootstrapObject.GetComponent<GameBootstrap>();
                bootstrap.ViewMode = ViewMode.ThirdPerson;
                bootstrap.WeaponDefinitions = weapons;
                bootstrap.RulesDefinition = rules;

                created.Add(CreateBoxes(scene, CoversName, Covers));
                created.Add(SceneBuildHelpers.CreateStaticBox(scene, null, VaultWall.Name, VaultWall.Center, VaultWall.Size));

                var openArea = new GameObject(OpenAreaName);
                created.Add(openArea);
                SceneManager.MoveGameObjectToScene(openArea, scene);
                openArea.transform.SetPositionAndRotation(OpenAreaCenter, Quaternion.identity);

                var poolObject = new GameObject(DebrisPoolName);
                created.Add(poolObject);
                SceneManager.MoveGameObjectToScene(poolObject, scene);
                var pool = poolObject.AddComponent<DebrisPool>();

                // 小屋の部品は小屋の子として親から見た位置で置かれるので、親を動かせば形を保ったまま移せる。
                var hut = DestructionTestSceneBuilder.CreateHut(scene, destructionEnabled);
                created.Add(hut.gameObject);
                hut.transform.SetPositionAndRotation(HutCenter, Quaternion.identity);
                hut.DebrisPool = pool;

                EditorSceneManager.MarkSceneDirty(scene);
                return bootstrap;
            }
            catch
            {
                foreach (var gameObject in created)
                {
                    if (gameObject != null)
                    {
                        UnityEngine.Object.DestroyImmediate(gameObject);
                    }
                }

                throw;
            }
        }

        /// <summary>
        /// 床に置くもの (遮蔽物・乗り越えの壁・小屋) の x, z の範囲。開けた場所や床からはみ出していないかを確かめるのに使う。
        /// 小屋は <see cref="DestructionTestSceneBuilder"/> の大きさから求める。
        /// </summary>
        public static IReadOnlyList<(string Name, Bounds Bounds)> Footprints()
        {
            var footprints = new List<(string, Bounds)>();
            foreach (var cover in Covers)
            {
                footprints.Add((cover.Name, new Bounds(cover.Center, cover.Size)));
            }

            footprints.Add((VaultWall.Name, new Bounds(VaultWall.Center, VaultWall.Size)));

            var hutSize = new Vector3(
                DestructionTestSceneBuilder.HutWidth * DestructionTestSceneBuilder.PieceSize,
                (DestructionTestSceneBuilder.WallHeight + 1) * DestructionTestSceneBuilder.PieceSize,
                DestructionTestSceneBuilder.HutDepth * DestructionTestSceneBuilder.PieceSize);
            footprints.Add((HutName, new Bounds(HutCenter + new Vector3(0f, hutSize.y / 2f, 0f), hutSize)));
            return footprints;
        }

        private static GameObject CreateBoxes(Scene scene, string rootName, IReadOnlyList<LevelBox> boxes)
        {
            var root = new GameObject(rootName);
            SceneManager.MoveGameObjectToScene(root, scene);
            foreach (var box in boxes)
            {
                SceneBuildHelpers.CreateStaticBox(scene, root.transform, box.Name, box.Center, box.Size);
            }

            return root;
        }
    }
}
