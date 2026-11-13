using System.IO;
using Fps.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Fps.Editor
{
    /// <summary>
    /// メインメニューのシーン MainMenu を生成し、起動時のシーンとして Build Settings の先頭に登録する。
    /// シーンには背景を塗るカメラと <see cref="MainMenuScreen"/> を置いた GameObject だけを入れ、UI Toolkit の UIDocument と
    /// PanelSettings は実行時に MainMenuScreen がコードで作る (Resources の MainMenu.uxml を読む)。
    /// MainMenuScreen にはロードアウトの選択に使うデフォルトロードアウトと武器の一覧のアセット (<see cref="WeaponAssetBuilder"/>) を配線する。
    /// 何度実行しても同じ結果になる。Editor を閉じた状態で次のように呼べる:
    /// <code>unity -batchmode -quit -projectPath . -executeMethod Fps.Editor.MenuSceneBuilder.Build</code>
    /// </summary>
    public static class MenuSceneBuilder
    {
        public const string ScenePath = BuildSceneList.MainMenuScene;
        public const string MenuPath = "Fps/Build Main Menu Scene";
        public const string LogTag = "[MenuSceneBuilder]";

        public const string CameraName = "MenuCamera";
        public const string MenuObjectName = "MainMenu";

        /// <summary>メニューの後ろを塗る色。UI の背景は半透明なので、これが透けて見える。</summary>
        public static readonly Color BackgroundColor = new Color(0.04f, 0.05f, 0.07f, 1f);

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

        /// <summary>path に MainMenu シーンを生成・保存し、Build Settings の先頭に登録する。失敗したら例外を投げる。</summary>
        public static void BuildAt(string path)
        {
            path = BootstrapSceneBuilder.ValidateScenePath(path);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SceneBuildHelpers.EnsureFolder(Path.GetDirectoryName(path));
            PopulateScene(scene);

            if (!EditorSceneManager.SaveScene(scene, path))
            {
                throw new IOException($"Failed to save the main menu scene to '{path}'");
            }

            BuildSceneList.RegisterStartScene(path);
            AssetDatabase.SaveAssets();

            Debug.Log($"{LogTag} saved '{path}' and registered it first in Build Settings");
        }

        /// <summary>
        /// 空のシーンに背景を塗るカメラ (<see cref="CameraName"/>) と <see cref="MainMenuScreen"/> を置いた GameObject
        /// (<see cref="MenuObjectName"/>) を入れ、後者を返す。
        /// </summary>
        public static GameObject PopulateScene(Scene scene)
        {
            SceneBuildHelpers.RequireEmpty(scene, "the main menu");

            // UI Toolkit はカメラが無くても描けるが、カメラが無いと画面が塗られず前のフレームが残る
            var cameraObject = new GameObject(CameraName, typeof(Camera));
            var camera = cameraObject.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = BackgroundColor;
            camera.cullingMask = 0;
            camera.orthographic = true;
            SceneManager.MoveGameObjectToScene(cameraObject, scene);

            var menu = new GameObject(MenuObjectName, typeof(MainMenuScreen));
            var screen = menu.GetComponent<MainMenuScreen>();
            screen.Loadout = LoadOrBuildWeaponAsset(WeaponAssetBuilder.LoadoutPath, WeaponAssetBuilder.LoadDefaultLoadout);
            screen.Catalog = LoadOrBuildWeaponAsset(WeaponAssetBuilder.CatalogPath, WeaponAssetBuilder.LoadCatalog);
            SceneManager.MoveGameObjectToScene(menu, scene);

            EditorSceneManager.MarkSceneDirty(scene);
            return menu;
        }

        // ロードアウトの選択はオンラインの試合と同じアセットから並べるので、無ければ NetSceneBuilder と同じく先に作る
        private static T LoadOrBuildWeaponAsset<T>(string path, System.Func<T> load)
            where T : Object
        {
            if (AssetDatabase.LoadAssetAtPath<T>(path) == null)
            {
                Debug.Log($"{LogTag} {path} is missing; running {WeaponAssetBuilder.MenuPath}");
                WeaponAssetBuilder.Build();
            }

            return load();
        }
    }
}
