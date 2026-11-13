using System;
using Fps.Core;
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
        public void PopulatedSceneHoldsTheBootstrapAndTheCovers()
        {
            var scene = TestScenes.NewAdditive();
            var rifle = ScriptableObject.CreateInstance<WeaponDefinition>();
            var handgun = ScriptableObject.CreateInstance<WeaponDefinition>();
            var range = ScriptableObject.CreateInstance<GameRulesDefinition>();
            try
            {
                var bootstrap = BootstrapSceneBuilder.PopulateScene(scene, new[] { rifle, handgun }, range);

                var roots = scene.GetRootGameObjects();
                Assert.That(roots, Has.Length.EqualTo(2));
                Assert.That(roots[0], Is.SameAs(bootstrap));
                Assert.That(roots[1].name, Is.EqualTo(BootstrapSceneBuilder.CoversName));
                AssertCovers(roots[1]);
                Assert.That(bootstrap.name, Is.EqualTo(BootstrapSceneBuilder.BootstrapObjectName));
                Assert.That(bootstrap.GetComponent<GameBootstrap>(), Is.Not.Null);
                Assert.That(
                    bootstrap.GetComponent<GameBootstrap>().WeaponDefinitions,
                    Is.EqualTo(new[] { rifle, handgun }),
                    "the player's loadout is wired");
                Assert.That(bootstrap.GetComponent<GameBootstrap>().RulesDefinition, Is.SameAs(range), "the range rules are wired");
                Assert.That(bootstrap.GetComponent<MatchRunner>(), Is.Not.Null, "the bootstrap scene runs a solo match");
                Assert.That(bootstrap.transform.childCount, Is.Zero, "everything else is built at runtime");
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
                UnityEngine.Object.DestroyImmediate(rifle);
                UnityEngine.Object.DestroyImmediate(handgun);
                UnityEngine.Object.DestroyImmediate(range);
            }
        }

        [Test]
        public void RefusesToPopulateANonEmptySceneOrWithoutAWeaponOrRules()
        {
            var scene = TestScenes.NewAdditive();
            var rifle = ScriptableObject.CreateInstance<WeaponDefinition>();
            var range = ScriptableObject.CreateInstance<GameRulesDefinition>();
            try
            {
                Assert.Throws<ArgumentNullException>(() => BootstrapSceneBuilder.PopulateScene(scene, null, range));
                Assert.Throws<ArgumentException>(() => BootstrapSceneBuilder.PopulateScene(scene, Array.Empty<WeaponDefinition>(), range));
                Assert.Throws<ArgumentException>(() => BootstrapSceneBuilder.PopulateScene(scene, new[] { rifle, null }, range));
                Assert.Throws<ArgumentNullException>(() => BootstrapSceneBuilder.PopulateScene(scene, new[] { rifle }, null));
                Assert.That(scene.rootCount, Is.Zero);

                var stray = new GameObject("Stray");
                SceneManager.MoveGameObjectToScene(stray, scene);

                Assert.Throws<InvalidOperationException>(() => BootstrapSceneBuilder.PopulateScene(scene, new[] { rifle }, range));
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
                UnityEngine.Object.DestroyImmediate(rifle);
                UnityEngine.Object.DestroyImmediate(range);
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
                Assert.That(roots, Has.Length.EqualTo(2), $"rerun {nameof(BootstrapSceneBuilder)}.Build to add the covers");
                Assert.That(roots[0].GetComponent<GameBootstrap>(), Is.Not.Null);
                Assert.That(roots[1].name, Is.EqualTo(BootstrapSceneBuilder.CoversName));
                AssertCovers(roots[1]);
                Assert.That(roots[0].GetComponent<MatchRunner>(), Is.Not.Null, $"rerun {nameof(BootstrapSceneBuilder)}.Build");
                Assert.That(
                    roots[0].GetComponent<GameBootstrap>().WeaponDefinitions,
                    Is.EqualTo(TestScenes.ExpectedPlayerWeapons()),
                    $"rerun {nameof(BootstrapSceneBuilder)}.Build");
                var range = AssetDatabase.LoadAssetAtPath<GameRulesDefinition>(GameRulesAssetBuilder.RangePath);
                Assert.That(range, Is.Not.Null, $"run {nameof(GameRulesAssetBuilder)}.Build");
                Assert.That(
                    roots[0].GetComponent<GameBootstrap>().RulesDefinition,
                    Is.SameAs(range),
                    $"rerun {nameof(BootstrapSceneBuilder)}.Build to wire the range rules");
                Assert.That(
                    Array.Exists(EditorBuildSettings.scenes, s => s.path == path && s.enabled),
                    Is.True,
                    $"the range is in the build; rerun {BuildSceneList.MenuPath}");
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        [Test]
        public void TheCoversAreOneLowAndOneHigh()
        {
            Assert.That(BootstrapSceneBuilder.Covers, Has.Count.EqualTo(2));
            Assert.That(BootstrapSceneBuilder.LowCover.Size.y, Is.EqualTo(1f));
            Assert.That(BootstrapSceneBuilder.HighCover.Size.y, Is.EqualTo(2f));
            Assert.That(BootstrapSceneBuilder.LowCover.Size.y, Is.LessThan(CoverProbe.LowWallHeight), "the player crouches behind the low cover");
            Assert.That(BootstrapSceneBuilder.HighCover.Size.y, Is.GreaterThanOrEqualTo(CoverProbe.LowWallHeight), "the player stands behind the high cover");

            foreach (var cover in BootstrapSceneBuilder.Covers)
            {
                Assert.That(cover.Center.y - (cover.Size.y / 2f), Is.EqualTo(0f).Within(1e-5f), $"{cover.Name} stands on the floor");
            }
        }

        [Test]
        public void TheCoversStandBetweenThePlayerAndTheTargetsAndLeaveALineOfFireOpen()
        {
            var feet = GameBootstrap.PlayerSpawn + (Vector3.down * (GameBootstrap.PlayerHeight / 2f));
            var eye = GameBootstrap.PlayerSpawn + GameBootstrap.EyeOffset;
            var targets = GameBootstrap.TargetDummyPositions(feet, Quaternion.identity);

            var open = 0;
            foreach (var target in targets)
            {
                // 標的の胴の高さ (足元から 1m) を狙う。
                var aim = target + Vector3.up;
                var ray = new Ray(eye, aim - eye);
                var length = Vector3.Distance(eye, aim);
                var blocked = false;
                foreach (var cover in BootstrapSceneBuilder.Covers)
                {
                    var bounds = new Bounds(cover.Center, cover.Size);
                    if (bounds.IntersectRay(ray, out var distance) && distance <= length)
                    {
                        blocked = true;
                    }
                }

                if (!blocked)
                {
                    open++;
                }
            }

            Assert.That(open, Is.GreaterThan(0), "at least one target can be shot from the spawn");

            foreach (var cover in BootstrapSceneBuilder.Covers)
            {
                Assert.That(cover.Center.z, Is.GreaterThan(feet.z + GameBootstrap.PlayerRadius + CoverProbe.MaxEnterDistance), $"{cover.Name} is ahead of the spawn");
                Assert.That(cover.Center.z, Is.LessThan(targets[0].z), $"{cover.Name} is in front of the targets");
            }

            var center = targets[targets.Length / 2];
            var toCenter = new Ray(eye, center + Vector3.up - eye);
            foreach (var cover in BootstrapSceneBuilder.Covers)
            {
                Assert.That(new Bounds(cover.Center, cover.Size).IntersectRay(toCenter), Is.False, $"{cover.Name} keeps the middle target open");
            }
        }

        [Test]
        public void CreateCoversRejectsAnUnloadedScene()
        {
            Assert.Throws<ArgumentException>(() => BootstrapSceneBuilder.CreateCovers(default));
        }

        private static void AssertCovers(GameObject root)
        {
            Assert.That(root.transform.childCount, Is.EqualTo(2), "one low and one high cover");
            var low = root.transform.Find(BootstrapSceneBuilder.LowCoverName);
            var high = root.transform.Find(BootstrapSceneBuilder.HighCoverName);
            Assert.That(low, Is.Not.Null);
            Assert.That(high, Is.Not.Null);

            foreach (var (box, expected) in new[] { (low, BootstrapSceneBuilder.LowCover), (high, BootstrapSceneBuilder.HighCover) })
            {
                Assert.That(Vector3.Distance(box.position, expected.Center), Is.LessThan(1e-4f), expected.Name);
                Assert.That(Vector3.Distance(box.lossyScale, expected.Size), Is.LessThan(1e-4f), expected.Name);
                var collider = box.GetComponent<BoxCollider>();
                Assert.That(collider, Is.Not.Null, $"{expected.Name} blocks the player and the cover probe");
                Assert.That(collider.isTrigger, Is.False, expected.Name);
                Assert.That(box.gameObject.isStatic, Is.True, expected.Name);
            }
        }
    }
}
