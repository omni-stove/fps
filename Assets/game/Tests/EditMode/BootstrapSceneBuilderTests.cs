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
    public sealed class BootstrapSceneBuilderTests
    {
        private const string Other = "Assets/Scenes/SampleScene.unity";

        [Test]
        public void UsesDefaultPathWithoutArgument()
        {
            var args = new[] { "Unity", "-batchmode", "-executeMethod", "Fps.Editor.BootstrapSceneBuilder.Build" };

            Assert.That(BootstrapSceneBuilder.ResolveScenePath(args), Is.EqualTo(BootstrapSceneBuilder.DefaultScenePath));
            Assert.That(BootstrapSceneBuilder.ResolveScenePath(null), Is.EqualTo(BootstrapSceneBuilder.DefaultScenePath));
        }

        [Test]
        public void ReadsPathArgument()
        {
            var args = new[] { "Unity", BootstrapSceneBuilder.ScenePathArgument, @"Assets\game\Scenes\Other.unity", "-quit" };

            Assert.That(BootstrapSceneBuilder.ResolveScenePath(args), Is.EqualTo("Assets/game/Scenes/Other.unity"));
        }

        // string[] を TestCase に直接渡すと params object[] として展開されるので、ソースから渡す。
        private static readonly object[] _malformedArgs =
        {
            new object[] { new[] { "-bootstrapScenePath" } },
            new object[] { new[] { "-bootstrapScenePath", "-quit" } },
            new object[] { new[] { "-bootstrapScenePath", " " } },
            new object[] { new[] { "-bootstrapScenePath", "Assets/A.unity", "-bootstrapScenePath", "Assets/B.unity" } },
            new object[] { new[] { "-bootstrapScenePath", "Bootstrap.unity" } },
        };

        [TestCaseSource(nameof(_malformedArgs))]
        public void RejectsMalformedPathArgument(string[] args)
        {
            Assert.Throws<ArgumentException>(() => BootstrapSceneBuilder.ResolveScenePath(args));
        }

        [TestCase("")]
        [TestCase("Bootstrap.unity")]
        [TestCase("/Assets/Bootstrap.unity")]
        [TestCase("Assets/Bootstrap.prefab")]
        [TestCase("Assets/../Bootstrap.unity")]
        [TestCase("Assets//Bootstrap.unity")]
        public void RejectsScenePathOutsideAssets(string path)
        {
            Assert.Throws<ArgumentException>(() => BootstrapSceneBuilder.ValidateScenePath(path));
        }

        [Test]
        public void PutsSceneFirstAndKeepsTheRest()
        {
            var current = new[]
            {
                new EditorBuildSettingsScene(Other, false),
                new EditorBuildSettingsScene(BootstrapSceneBuilder.DefaultScenePath, false),
            };

            var scenes = BootstrapSceneBuilder.WithSceneFirst(current, BootstrapSceneBuilder.DefaultScenePath);

            Assert.That(scenes, Has.Length.EqualTo(2));
            Assert.That(scenes[0].path, Is.EqualTo(BootstrapSceneBuilder.DefaultScenePath));
            Assert.That(scenes[0].enabled, Is.True);
            Assert.That(scenes[1].path, Is.EqualTo(Other));
            Assert.That(scenes[1].enabled, Is.False, "other scenes keep their enabled flag");
        }

        [Test]
        public void HandlesEmptyBuildSettings()
        {
            var scenes = BootstrapSceneBuilder.WithSceneFirst(null, BootstrapSceneBuilder.DefaultScenePath);

            Assert.That(scenes, Has.Length.EqualTo(1));
            Assert.That(scenes[0].path, Is.EqualTo(BootstrapSceneBuilder.DefaultScenePath));
        }

        [Test]
        public void PopulatedSceneHoldsOnlyTheBootstrap()
        {
            var scene = TestScenes.NewAdditive();
            var rifle = ScriptableObject.CreateInstance<WeaponDefinition>();
            try
            {
                var bootstrap = BootstrapSceneBuilder.PopulateScene(scene, rifle);

                var roots = scene.GetRootGameObjects();
                Assert.That(roots, Has.Length.EqualTo(1));
                Assert.That(roots[0], Is.SameAs(bootstrap));
                Assert.That(bootstrap.name, Is.EqualTo(BootstrapSceneBuilder.BootstrapObjectName));
                Assert.That(bootstrap.GetComponent<GameBootstrap>(), Is.Not.Null);
                Assert.That(bootstrap.GetComponent<GameBootstrap>().WeaponDefinition, Is.SameAs(rifle), "the player's weapon is wired");
                Assert.That(bootstrap.GetComponent<MatchRunner>(), Is.Not.Null, "the bootstrap scene runs a solo match");
                Assert.That(bootstrap.transform.childCount, Is.Zero, "everything else is built at runtime");
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
                UnityEngine.Object.DestroyImmediate(rifle);
            }
        }

        [Test]
        public void RefusesToPopulateANonEmptySceneOrWithoutAWeapon()
        {
            var scene = TestScenes.NewAdditive();
            var rifle = ScriptableObject.CreateInstance<WeaponDefinition>();
            try
            {
                Assert.Throws<ArgumentNullException>(() => BootstrapSceneBuilder.PopulateScene(scene, null));
                Assert.That(scene.rootCount, Is.Zero);

                var stray = new GameObject("Stray");
                SceneManager.MoveGameObjectToScene(stray, scene);

                Assert.Throws<InvalidOperationException>(() => BootstrapSceneBuilder.PopulateScene(scene, rifle));
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
                UnityEngine.Object.DestroyImmediate(rifle);
            }
        }

        [Test]
        public void CommittedSceneMatchesTheBuilder()
        {
            var path = BootstrapSceneBuilder.DefaultScenePath;
            Assume.That(AssetDatabase.LoadAssetAtPath<SceneAsset>(path), Is.Not.Null, $"run {nameof(BootstrapSceneBuilder)}.Build first");

            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                var roots = scene.GetRootGameObjects();
                Assert.That(roots, Has.Length.EqualTo(1));
                Assert.That(roots[0].GetComponent<GameBootstrap>(), Is.Not.Null);
                Assert.That(roots[0].GetComponent<MatchRunner>(), Is.Not.Null, $"rerun {nameof(BootstrapSceneBuilder)}.Build");
                Assert.That(
                    roots[0].GetComponent<GameBootstrap>().WeaponDefinition,
                    Is.SameAs(AssetDatabase.LoadAssetAtPath<WeaponDefinition>(WeaponAssetBuilder.RiflePath)).And.Not.Null,
                    $"rerun {nameof(BootstrapSceneBuilder)}.Build");
                Assert.That(EditorBuildSettings.scenes, Is.Not.Empty);
                Assert.That(EditorBuildSettings.scenes[0].path, Is.EqualTo(path));
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }
    }
}
