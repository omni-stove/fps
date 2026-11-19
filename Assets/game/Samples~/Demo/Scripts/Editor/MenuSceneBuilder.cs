using System.IO;
using Shooter.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Shooter.Editor
{
    // UI Toolkit の UIDocument と PanelSettings は実行時に MainMenuScreen がコードで作る (Resources の MainMenu.uxml を読む) ので、
    // シーンには背景を塗るカメラと MainMenuScreen を置いた GameObject だけを入れる。
    public static class MenuSceneBuilder
    {
        public static string ScenePath => BuildSceneList.MainMenuScene;
        public const string MenuPath = SceneBuildSteps.MenuPath;
        public const string LogTag = "[MenuSceneBuilder]";

        public const string CameraName = "MenuCamera";
        public const string MenuObjectName = "MainMenu";

        // UI の背景は半透明なので、メニューの後ろはこの色が透けて見える。
        public static readonly Color BackgroundColor = new Color(0.04f, 0.05f, 0.07f, 1f);

        public static void Build()
        {
            // メニューから呼ばれたとき、開いているシーンの未保存の変更を黙って捨てない。
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                Debug.Log(LogTag + " cancelled");
                return;
            }

            BuildAt(ScenePath);
            DemoSample.WriteMenuSceneSettings();
        }

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
            screen.Missions = LoadOrBuildMissionCatalog();
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

        // 定義やシーンがまだ無くて作れなくても例外にせず警告と null で返すのは、メニューが「キャンペーンを開けない」案内を出せるため。
        public static MissionCatalog LoadOrBuildMissionCatalog()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<MissionCatalog>(MissionCatalogBuilder.CatalogPath);
            if (catalog != null)
            {
                return catalog;
            }

            Debug.Log($"{LogTag} {MissionCatalogBuilder.CatalogPath} is missing; running {MissionCatalogBuilder.MenuPath}");
            try
            {
                MissionCatalogBuilder.Build();
            }
            catch (System.Exception e) when (e is System.InvalidOperationException || e is MissionCatalogException)
            {
                // ミッションのシーンは NavMesh を焼くので重く、メニューを作るたびに巻き込まない。後で作ってメニューを作り直せばつながる
                Debug.LogWarning(
                    $"{LogTag} the campaign menu has no missions: {e.Message}. Run {SceneBuildSteps.MenuPath}");
                return null;
            }

            return AssetDatabase.LoadAssetAtPath<MissionCatalog>(MissionCatalogBuilder.CatalogPath);
        }
    }
}
