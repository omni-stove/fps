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
    /// Bootstrap シーン (射撃場) を生成する。シーンには <see cref="GameBootstrap"/> と試合を進める <see cref="MatchRunner"/> を
    /// 置いた GameObject と、カバーを試す遮蔽物 (<see cref="Covers"/>) を入れ、残りは実行時に GameBootstrap がコードで作る。
    /// 保存したシーンは Build Settings に登録する (先頭はメインメニューなので、既にあれば位置は変えない)。
    /// Editor を閉じた状態で次のように呼べる (保存先は -bootstrapScenePath で変えられる):
    /// <code>unity -batchmode -quit -executeMethod Fps.Editor.BootstrapSceneBuilder.Build</code>
    /// </summary>
    public static class BootstrapSceneBuilder
    {
        public const string DefaultScenePath = BuildSceneList.BootstrapScene;
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
            var weapons = WeaponAssetBuilder.LoadPlayerLoadout();
            var rules = GameRulesAssetBuilder.LoadRules(GameRulesAssetBuilder.RangePath);

            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            PopulateScene(scene, weapons, rules);

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
        /// 空のシーンに GameBootstrap と MatchRunner を置いた GameObject と、遮蔽物 (<see cref="Covers"/>) をまとめた <see cref="CoversName"/> を入れ、
        /// プレイヤーの武器の定義の並び weapons (通常は <see cref="WeaponAssetBuilder.LoadPlayerLoadout"/> の rifle, handgun) とルール rules
        /// (通常は射撃場の <see cref="GameRulesAssetBuilder.RangePath"/>) を GameBootstrap に配線して、GameBootstrap の GameObject を返す。
        /// 残りの参照は実行時にコードで解決する。
        /// </summary>
        public static GameObject PopulateScene(Scene scene, IReadOnlyList<WeaponDefinition> weapons, GameRulesDefinition rules)
        {
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

            // MatchRunner は Start で同じ GameObject の GameBootstrap が Awake で作ったものを拾うので、ここでの配線は要らない。
            var bootstrap = new GameObject(BootstrapObjectName, typeof(GameBootstrap), typeof(MatchRunner));
            var component = bootstrap.GetComponent<GameBootstrap>();
            component.WeaponDefinitions = weapons;
            component.RulesDefinition = rules;
            SceneManager.MoveGameObjectToScene(bootstrap, scene);
            CreateCovers(scene);
            EditorSceneManager.MarkSceneDirty(scene);
            return bootstrap;
        }

        /// <summary>
        /// <see cref="Covers"/> の遮蔽物を、シーン直下の <see cref="CoversName"/> の子として置いて親を返す。
        /// 床は GameBootstrap が実行時に作るので、遮蔽物は床の上 (y = 0 から) に立つ大きさと位置にしてある。
        /// </summary>
        public static GameObject CreateCovers(Scene scene)
        {
            SceneBuildHelpers.RequireLoaded(scene);

            var root = new GameObject(CoversName);
            SceneManager.MoveGameObjectToScene(root, scene);
            foreach (var cover in Covers)
            {
                SceneBuildHelpers.CreateStaticBox(scene, root.transform, cover.Name, cover.Center, cover.Size);
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
