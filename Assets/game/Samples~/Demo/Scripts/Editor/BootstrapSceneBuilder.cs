using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Fps.Core;
using Fps.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Fps.Editor
{
    /// <summary>
    /// Bootstrap シーン (射撃場) を生成する。シーンには <see cref="GameBootstrap"/> と試合を進める <see cref="MatchRunner"/>、
    /// 初めて入ったとき (とメニューのチュートリアルから来たとき) に案内を出す <see cref="TutorialPresenter"/> を置いた GameObject と、カバーを試す遮蔽物 (<see cref="Covers"/>) を入れ、残りは実行時に GameBootstrap がコードで作る。
    /// 保存したシーンは Build Settings に登録する (先頭はメインメニューなので、既にあれば位置は変えない)。
    /// Editor を閉じた状態で次のように呼べる (保存先は -bootstrapScenePath で変えられる):
    /// <code>unity -batchmode -quit -executeMethod Fps.Editor.BootstrapSceneBuilder.Build</code>
    /// </summary>
    public static class BootstrapSceneBuilder
    {
        public static string DefaultScenePath => BuildSceneList.BootstrapScene;
        public const string ScenePathArgument = "-bootstrapScenePath";
        public const string BootstrapObjectName = "GameBootstrap";
        public const string MenuPath = "Fps/Build Bootstrap Scene";

        /// <summary>遮蔽物をまとめる親の名前。</summary>
        public const string CoversName = "Covers";

        public const string LowCoverName = "Cover_Low";
        public const string HighCoverName = "Cover_High";

        /// <summary>低い遮蔽物の高さ (m)。<see cref="CoverProbe.LowWallHeight"/> 未満なので、しゃがんで隠れ、立って覗き、上からブラインドファイアする。</summary>
        public const float LowCoverHeight = 1f;

        /// <summary>高い遮蔽物の高さ (m)。<see cref="CoverProbe.LowWallHeight"/> 以上で目 (1.6m) より高いので、立ったまま隠れ、端から覗く。</summary>
        public const float HighCoverHeight = 2f;

        /// <summary>遮蔽物の厚さ (m)。</summary>
        public const float CoverThickness = 0.4f;

        /// <summary>プレイヤーの足元から遮蔽物の中心までの前方の距離 (m)。標的ダミーの列との中間。</summary>
        public const float CoverDistance = GameBootstrap.TargetDummyDistance / 2f;

        /// <summary>
        /// 射撃場の遮蔽物。プレイヤーの出現位置 (<see cref="GameBootstrap.PlayerSpawn"/>、+Z 向き) と標的ダミーの列の間に、
        /// 真ん中の標的への射線を空けて左右に 1 つずつ置く。出現位置からの射線にかかるのは、左の低い遮蔽物が左端の標的への射線の低い所だけ。
        /// </summary>
        public static readonly IReadOnlyList<LevelBox> Covers = new[]
        {
            new LevelBox(LowCoverName, new Vector3(-3f, LowCoverHeight / 2f, CoverDistance), new Vector3(3f, LowCoverHeight, CoverThickness)),
            new LevelBox(HighCoverName, new Vector3(3f, HighCoverHeight / 2f, CoverDistance), new Vector3(1.5f, HighCoverHeight, CoverThickness)),
        };

        /// <summary>低い遮蔽物 (<see cref="Covers"/> の 0 番)。</summary>
        public static LevelBox LowCover => Covers[0];

        /// <summary>高い遮蔽物 (<see cref="Covers"/> の 1 番)。</summary>
        public static LevelBox HighCover => Covers[1];

        /// <summary>
        /// 遮蔽物の名前ごとの面の材質 (<see cref="SurfaceMaterial"/> の Id)。着弾の演出を材質ごとに見比べられるよう、低い壁と高い壁で変える。
        /// 載っていない遮蔽物は <see cref="SurfaceMaterial.DefaultId"/>。
        /// </summary>
        public static readonly IReadOnlyDictionary<string, string> CoverSurfaceIds = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [LowCoverName] = SurfaceImpactAssetStep.ConcreteId,
            [HighCoverName] = SurfaceImpactAssetStep.MetalId,
        };

        public const string WeaponTableName = "WeaponTable";

        // サンプルを全部 (Reference と Realistic で 64) 並べると出現位置の前が埋まるので、それぞれ武器種ごとに 1 つだけにし、同じ武器種の順にして前後の列で撃ち比べられるようにする。
        public static readonly IReadOnlyList<string> FeaturedReferenceWeaponIds = new[]
        {
            "25974cbc-c194-4f98-b22d-c9d34c3ba6e4",
            "6b1ce288-952f-4ead-b23b-337df82d3a02",
            "7f8258fd-a401-403b-93d2-60a22a69cb0b",
            "e250274d-82be-401c-a098-2ee06295b582",
            "07895118-2fb7-4b50-aa16-8f8afef0fc79",
            "7875b62b-86b5-4af7-bc20-27f643d3c4fe",
            "343b6024-75f2-4d68-8ac6-d31fad86aa47",
            "b9daae5b-9015-46cf-b104-6f9a9ba9c623",
        };

        public static readonly IReadOnlyList<string> FeaturedRealisticWeaponIds = new[]
        {
            "43c9c224-dfea-463d-a411-fd0f6c37824d",
            "2f3825b8-dac2-4744-8025-d2909cbb57d4",
            "05e08bbf-1757-464a-a142-8dcdd0c86ba9",
            "aa7e7cf5-6840-4b20-b0ec-ae5d1dfb9c5c",
            "c1f6ce08-79a8-4d20-a16a-6eb6cee0256d",
            "a21e791d-cf45-4f05-981d-17480c5a791b",
            "c5171e80-7b9c-44f0-bdd5-3e4fdd67f585",
            "148acbfc-9d35-4c65-96fc-36bea468eaa4",
        };

        public static IReadOnlyList<string> FeaturedWeaponIds => FeaturedReferenceWeaponIds.Concat(FeaturedRealisticWeaponIds).ToArray();

        public static readonly IReadOnlyList<string> PlayerWeaponIds = new[]
        {
            "25974cbc-c194-4f98-b22d-c9d34c3ba6e4",
            "343b6024-75f2-4d68-8ac6-d31fad86aa47",
        };

        // 出現位置で拾える範囲 (RangeWeaponRackFixture.PickupRadius) に入らず、遮蔽物より手前に置く。
        public const float WeaponTableDistance = 2.5f;

        public const float WeaponTableSpacing = 2f;

        public const int WeaponTableRowLength = 8;

        // 後ろの列も遮蔽物 (CoverDistance) より手前に収めるため、横の間隔より詰める。
        public const float WeaponTableRowSpacing = 1.5f;

        /// <summary>
        /// コマンドライン引数で決まる場所に Bootstrap シーンを生成し、Build Settings に登録する。
        /// 失敗したら例外を投げる (batchmode では Unity が終了コード 1 で終わる)。
        /// </summary>
        [MenuItem(MenuPath)]
        public static void Build()
        {
            // メニューから呼ばれたとき、開いているシーンの未保存の変更を黙って捨てない。
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                Debug.Log("[BootstrapSceneBuilder] cancelled");
                return;
            }

            var path = ResolveScenePath(Environment.GetCommandLineArgs());
            BuildAt(path);
        }

        /// <summary>path に Bootstrap シーンを生成・保存し、Build Settings に登録する。</summary>
        public static void BuildAt(string path)
        {
            path = ValidateScenePath(path);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // NewScene(Single) は参照されていないアセットをアンロードし、先に読んだ定義が null になるので、シーンを作ってから読む。
            // 武器のアセットが無ければ何も保存せずに止める (先に WeaponAssetBuilder.Build を走らせる)。
            // 射撃場のルールのアセットも同じく、無ければ止める (先に GameRulesAssetBuilder.Build を走らせる)。
            var reference = WeaponAssetBuilder.LoadReferenceWeapons();
            var realistic = WeaponAssetBuilder.LoadRealisticWeapons();
            var weapons = LoadPlayerWeapons(reference);
            var rules = GameRulesAssetBuilder.LoadRules(GameRulesAssetBuilder.RangePath);
            var featured = LoadFeaturedWeapons(reference, realistic);

            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            PopulateScene(scene, weapons, rules, featured);

            if (!EditorSceneManager.SaveScene(scene, path))
            {
                throw new IOException($"Failed to save the bootstrap scene to '{path}'");
            }

            // 起動時のシーンはメインメニュー (MenuSceneBuilder が先頭に置く) なので、射撃場は並びを変えずに加える
            BuildSceneList.Register(path);
            AssetDatabase.SaveAssets();

            Debug.Log($"[BootstrapSceneBuilder] saved '{path}' and registered it in Build Settings");
        }

        /// <summary>
        /// 空のシーンに GameBootstrap と MatchRunner と TutorialPresenter を置いた GameObject と、遮蔽物 (<see cref="Covers"/>) をまとめた <see cref="CoversName"/> を入れ、
        /// プレイヤーの武器の定義の並び weapons (通常は <see cref="LoadPlayerWeapons"/> の Reference の主武器・副武器) とルール rules
        /// (通常は射撃場の <see cref="GameRulesAssetBuilder.RangePath"/>) を GameBootstrap に配線して、GameBootstrap の GameObject を返す。
        /// 残りの参照は実行時にコードで解決する。
        /// </summary>
        public static GameObject PopulateScene(Scene scene, IReadOnlyList<WeaponDefinition> weapons, GameRulesDefinition rules)
        {
            return PopulateScene(scene, weapons, rules, Array.Empty<WeaponDefinition>());
        }

        public static GameObject PopulateScene(
            Scene scene, IReadOnlyList<WeaponDefinition> weapons, GameRulesDefinition rules, IReadOnlyList<WeaponDefinition> featured)
        {
            if (featured == null)
            {
                throw new ArgumentNullException(nameof(featured));
            }

            if (!scene.IsValid() || !scene.isLoaded)
            {
                throw new ArgumentException("scene must be a valid, loaded scene", nameof(scene));
            }

            SceneBuildHelpers.RequirePlayerWeapons(weapons, nameof(weapons));
            if (rules == null)
            {
                throw new ArgumentNullException(nameof(rules));
            }

            if (scene.rootCount != 0)
            {
                throw new InvalidOperationException(
                    $"scene '{scene.name}' already has {scene.rootCount} root object(s); the bootstrap scene must start empty");
            }

            // MatchRunner と TutorialPresenter は Start で同じ GameObject の GameBootstrap が Awake で作ったものを拾うので、ここでの配線は要らない。
            var bootstrap = new GameObject(BootstrapObjectName, typeof(GameBootstrap), typeof(MatchRunner), typeof(TutorialPresenter));
            var component = bootstrap.GetComponent<GameBootstrap>();
            component.WeaponDefinitions = weapons;
            component.RulesDefinition = rules;
            SceneManager.MoveGameObjectToScene(bootstrap, scene);
            CreateCovers(scene);
            if (featured.Count > 0)
            {
                CreateWeaponTable(scene, featured);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            return bootstrap;
        }

        /// <summary>
        /// <see cref="Covers"/> の遮蔽物を、シーン直下の <see cref="CoversName"/> の子として置いて親を返す。
        /// 床は GameBootstrap が実行時に作るので、遮蔽物は床の上 (y = 0 から) に立つ大きさと位置にしてある。
        /// 遮蔽物には <see cref="CoverSurfaceIds"/> の材質を付ける。
        /// </summary>
        public static GameObject CreateCovers(Scene scene)
        {
            SceneBuildHelpers.RequireLoaded(scene);

            var root = new GameObject(CoversName);
            SceneManager.MoveGameObjectToScene(root, scene);
            foreach (var cover in Covers)
            {
                var box = SceneBuildHelpers.CreateStaticBox(scene, root.transform, cover.Name, cover.Center, cover.Size);
                SceneBuildHelpers.SetSurfaceMaterial(box, SceneBuildHelpers.SurfaceIdOf(CoverSurfaceIds, cover.Name));
            }

            return root;
        }

        public static WeaponDefinition[] LoadPlayerWeapons(WeaponCatalogAsset reference)
        {
            return FindAll(reference, PlayerWeaponIds);
        }

        public static WeaponDefinition[] LoadFeaturedWeapons(WeaponCatalogAsset reference, WeaponCatalogAsset realistic)
        {
            return FindAll(reference, FeaturedReferenceWeaponIds).Concat(FindAll(realistic, FeaturedRealisticWeaponIds)).ToArray();
        }

        private static WeaponDefinition[] FindAll(WeaponCatalogAsset catalog, IReadOnlyList<string> ids)
        {
            var weapons = new WeaponDefinition[ids.Count];
            for (var i = 0; i < weapons.Length; i++)
            {
                weapons[i] = WeaponAssetBuilder.FindInCatalog(catalog, WeaponTableName, ids[i]);
            }

            return weapons;
        }

        public static Vector3[] WeaponTablePositions(int count)
        {
            var feet = GameBootstrap.PlayerSpawn + (Vector3.down * (GameBootstrap.PlayerHeight / 2f));
            var positions = new Vector3[count];
            for (var start = 0; start < count; start += WeaponTableRowLength)
            {
                var row = start / WeaponTableRowLength;
                var offsets = LineLayout.CenteredOffsets(Math.Min(WeaponTableRowLength, count - start), WeaponTableSpacing);
                for (var i = 0; i < offsets.Length; i++)
                {
                    positions[start + i] = feet + new Vector3(offsets[i], 0f, WeaponTableDistance + (row * WeaponTableRowSpacing));
                }
            }

            return positions;
        }

        public static GameObject CreateWeaponTable(Scene scene, IReadOnlyList<WeaponDefinition> weapons)
        {
            SceneBuildHelpers.RequireLoaded(scene);
            if (weapons == null)
            {
                throw new ArgumentNullException(nameof(weapons));
            }

            // 中身を先に作って検査し、不正なら何も置かない。
            var contents = new PickupContents[weapons.Count];
            for (var i = 0; i < weapons.Count; i++)
            {
                if (weapons[i] == null)
                {
                    throw new ArgumentException($"featured weapon #{i} is empty", nameof(weapons));
                }

                contents[i] = PickupContents.ForWeapon(weapons[i], weapons[i].ToSpec().MagazineSize);
            }

            var root = new GameObject(WeaponTableName);
            SceneManager.MoveGameObjectToScene(root, scene);
            var positions = WeaponTablePositions(weapons.Count);
            for (var i = 0; i < weapons.Count; i++)
            {
                RangeWeaponRackFixture.CreatePickup(root.transform, weapons[i].name, contents[i], positions[i]);
            }

            return root;
        }

        /// <summary>
        /// Build Settings のシーン一覧の先頭に path を有効な状態で置く (<see cref="BuildSceneList.WithStartScene"/>)。
        /// </summary>
        public static EditorBuildSettingsScene[] WithSceneFirst(IReadOnlyList<EditorBuildSettingsScene> current, string path)
        {
            return BuildSceneList.WithStartScene(current, path);
        }

        /// <summary>
        /// コマンドライン引数から保存先を決める。<see cref="ScenePathArgument"/> があればその次の値、無ければ既定の場所。
        /// </summary>
        public static string ResolveScenePath(IReadOnlyList<string> args)
        {
            if (args == null)
            {
                return DefaultScenePath;
            }

            string found = null;
            for (var i = 0; i < args.Count; i++)
            {
                if (!string.Equals(args[i], ScenePathArgument, StringComparison.Ordinal))
                {
                    continue;
                }

                if (i + 1 >= args.Count || string.IsNullOrWhiteSpace(args[i + 1]) || args[i + 1].StartsWith("-", StringComparison.Ordinal))
                {
                    throw new ArgumentException($"{ScenePathArgument} needs a path such as {DefaultScenePath}", nameof(args));
                }

                if (found != null)
                {
                    throw new ArgumentException($"{ScenePathArgument} is given more than once", nameof(args));
                }

                found = args[i + 1];
                i++;
            }

            return found == null ? DefaultScenePath : ValidateScenePath(found);
        }

        /// <summary>Assets/ 以下の .unity であることを確かめ、区切りを / に揃えて返す。</summary>
        public static string ValidateScenePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("scene path must not be empty", nameof(path));
            }

            var normalized = path.Trim().Replace('\\', '/');
            if (!normalized.StartsWith("Assets/", StringComparison.Ordinal))
            {
                throw new ArgumentException($"scene path '{path}' must be under Assets/", nameof(path));
            }

            if (!normalized.EndsWith(".unity", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException($"scene path '{path}' must end with .unity", nameof(path));
            }

            foreach (var segment in normalized.Split('/'))
            {
                if (segment.Length == 0 || segment == "." || segment == "..")
                {
                    throw new ArgumentException($"scene path '{path}' must not contain empty, '.' or '..' segments", nameof(path));
                }
            }

            return normalized;
        }
    }
}
