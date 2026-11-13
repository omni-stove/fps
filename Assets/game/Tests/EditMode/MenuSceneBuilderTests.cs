using System;
using Fps.Editor;
using Fps.Runtime;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Fps.Tests.EditMode
{
    public sealed class MenuSceneBuilderTests
    {
        [Test]
        public void PopulatedSceneHoldsTheCameraAndTheMenu()
        {
            var scene = TestScenes.NewAdditive();
            try
            {
                var menu = MenuSceneBuilder.PopulateScene(scene);

                var roots = scene.GetRootGameObjects();
                Assert.That(roots, Has.Length.EqualTo(2));
                AssertCamera(roots[0]);
                Assert.That(roots[1], Is.SameAs(menu));
                Assert.That(menu.name, Is.EqualTo(MenuSceneBuilder.MenuObjectName));
                Assert.That(menu.GetComponent<MainMenuScreen>(), Is.Not.Null);
                Assert.That(menu.transform.childCount, Is.Zero, "the UI document is built at runtime");

                // ロードアウトの選択はオンラインの試合と同じアセットから並べる
                var screen = menu.GetComponent<MainMenuScreen>();
                Assert.That(AssetDatabase.GetAssetPath(screen.Loadout), Is.EqualTo(WeaponAssetBuilder.LoadoutPath));
                Assert.That(AssetDatabase.GetAssetPath(screen.Catalog), Is.EqualTo(WeaponAssetBuilder.CatalogPath));

                // キャンペーンの選択はミッションの一覧から並べる (ミッションのシーンがまだ無ければ一覧も作れず、配線しない)
                var missions = AssetDatabase.LoadAssetAtPath<MissionCatalog>(MissionCatalogBuilder.CatalogPath);
                Assert.That(screen.Missions, Is.SameAs(missions));
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        [Test]
        public void RefusesANonEmptyOrUnloadedScene()
        {
            Assert.Throws<ArgumentException>(() => MenuSceneBuilder.PopulateScene(default));

            var scene = TestScenes.NewAdditive();
            try
            {
                var stray = new GameObject("Stray");
                SceneManager.MoveGameObjectToScene(stray, scene);

                Assert.Throws<InvalidOperationException>(() => MenuSceneBuilder.PopulateScene(scene));
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        [Test]
        public void TheMenuIsTheStartScene()
        {
            var scenes = BuildSceneList.WithStartScene(
                new[]
                {
                    new EditorBuildSettingsScene(BuildSceneList.BootstrapScene, true),
                    new EditorBuildSettingsScene(BuildSceneList.NetTestScene, true),
                },
                MenuSceneBuilder.ScenePath);

            Assert.That(scenes[0].path, Is.EqualTo(MenuSceneBuilder.ScenePath));
            Assert.That(scenes[0].enabled, Is.True);
            Assert.That(scenes, Has.Length.EqualTo(3));
        }

        [Test]
        public void CommittedSceneMatchesTheBuilder()
        {
            var path = MenuSceneBuilder.ScenePath;
            Assume.That(AssetDatabase.LoadAssetAtPath<SceneAsset>(path), Is.Not.Null, $"run {nameof(MenuSceneBuilder)}.Build first");

            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                var roots = scene.GetRootGameObjects();
                Assert.That(roots, Has.Length.EqualTo(2), $"rerun {MenuSceneBuilder.MenuPath}");
                AssertCamera(roots[0]);
                Assert.That(roots[1].GetComponent<MainMenuScreen>(), Is.Not.Null, $"rerun {MenuSceneBuilder.MenuPath}");
                var missions = AssetDatabase.LoadAssetAtPath<MissionCatalog>(MissionCatalogBuilder.CatalogPath);
                if (missions != null)
                {
                    Assert.That(roots[1].GetComponent<MainMenuScreen>().Missions, Is.SameAs(missions),
                        $"rerun {MenuSceneBuilder.MenuPath} to wire the mission catalog to the campaign menu");
                }
                Assert.That(EditorBuildSettings.scenes, Is.Not.Empty);
                Assert.That(EditorBuildSettings.scenes[0].path, Is.EqualTo(path), $"rerun {MenuSceneBuilder.MenuPath} to start the game at the menu");
                Assert.That(EditorBuildSettings.scenes[0].enabled, Is.True);
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static void AssertCamera(GameObject cameraObject)
        {
            Assert.That(cameraObject.name, Is.EqualTo(MenuSceneBuilder.CameraName));
            var camera = cameraObject.GetComponent<Camera>();
            Assert.That(camera, Is.Not.Null);
            Assert.That(camera.clearFlags, Is.EqualTo(CameraClearFlags.SolidColor));
            Assert.That(camera.backgroundColor, Is.EqualTo(MenuSceneBuilder.BackgroundColor));
            Assert.That(camera.cullingMask, Is.Zero, "the menu draws nothing but the UI");
        }
    }
}
