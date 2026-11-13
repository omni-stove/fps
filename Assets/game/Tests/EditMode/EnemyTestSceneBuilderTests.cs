using System;
using System.Collections.Generic;
using Fps.Core;
using Fps.Editor;
using Fps.Runtime;
using NUnit.Framework;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace Fps.Tests.EditMode
{
    public sealed class EnemyTestSceneBuilderTests
    {
        private const string Other = "Assets/Scenes/SampleScene.unity";

        private Scene _scene;

        [SetUp]
        public void SetUp()
        {
            _scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        }

        [TearDown]
        public void TearDown()
        {
            if (_scene.IsValid())
            {
                EditorSceneManager.CloseScene(_scene, true);
            }
        }

        [Test]
        public void LevelHasAFloorWithANavMeshSurfaceAndAWall()
        {
            var surface = EnemyTestSceneBuilder.CreateLevel(_scene);

            var floor = Find(EnemyTestSceneBuilder.FloorName);
            Assert.That(surface.gameObject, Is.SameAs(floor));
            Assert.That(floor.transform.localScale, Is.EqualTo(EnemyTestSceneBuilder.FloorScale));
            Assert.That(floor.GetComponent<MeshCollider>(), Is.Not.Null, "the surface bakes from physics colliders");
            Assert.That(surface.collectObjects, Is.EqualTo(CollectObjects.All));
            Assert.That(surface.useGeometry, Is.EqualTo(NavMeshCollectGeometry.PhysicsColliders));

            var wall = Find(EnemyTestSceneBuilder.WallName);
            Assert.That(wall.transform.position, Is.EqualTo(EnemyTestSceneBuilder.WallCenter));
            Assert.That(wall.transform.localScale, Is.EqualTo(EnemyTestSceneBuilder.WallSize));
        }

        [Test]
        public void LevelRefusesANonEmptyScene()
        {
            var stray = new GameObject("Stray");
            SceneManager.MoveGameObjectToScene(stray, _scene);

            Assert.Throws<InvalidOperationException>(() => EnemyTestSceneBuilder.CreateLevel(_scene));
        }

        [Test]
        public void ActorsAreWiredInCode()
        {
            var playerLoadout = ScriptableObject.CreateInstance<LoadoutDefinition>();
            var rifle = NewWeapon("Rifle", WeaponAssetBuilder.RifleSpec());
            var handgun = NewWeapon("Handgun", WeaponAssetBuilder.HandgunSpec());
            var catalog = NewCatalog("WeaponCatalog", handgun, rifle);
            try
            {
                var agent = EnemyTestSceneBuilder.CreateActors(_scene, playerLoadout, catalog);

                AssertActorsAreWired(agent, playerLoadout);
                Assert.That(agent.WeaponDefinition, Is.SameAs(rifle), "the enemy shoots the catalog's weapon with its id");
                Assert.That(agent.WeaponDefinition.Id, Is.EqualTo(EnemyTestSceneBuilder.EnemyWeaponId));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(catalog);
                UnityEngine.Object.DestroyImmediate(handgun);
                UnityEngine.Object.DestroyImmediate(rifle);
                UnityEngine.Object.DestroyImmediate(playerLoadout);
            }
        }

        [Test]
        public void ActorsNeedAPlayerLoadoutAndACatalog()
        {
            var playerLoadout = ScriptableObject.CreateInstance<LoadoutDefinition>();
            var rifle = NewWeapon("Rifle", WeaponAssetBuilder.RifleSpec());
            var catalog = NewCatalog("WeaponCatalog", rifle);
            try
            {
                Assert.Throws<ArgumentNullException>(() => EnemyTestSceneBuilder.CreateActors(_scene, null, catalog));
                Assert.Throws<ArgumentNullException>(() => EnemyTestSceneBuilder.CreateActors(_scene, playerLoadout, null));
                Assert.That(_scene.rootCount, Is.Zero);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(catalog);
                UnityEngine.Object.DestroyImmediate(rifle);
                UnityEngine.Object.DestroyImmediate(playerLoadout);
            }
        }

        [Test]
        public void ActorsRefuseAWeaponIdThatIsNotInTheCatalog()
        {
            var playerLoadout = ScriptableObject.CreateInstance<LoadoutDefinition>();
            var handgun = NewWeapon("Handgun", WeaponAssetBuilder.HandgunSpec());
            var catalog = NewCatalog("HandgunOnly", handgun);
            try
            {
                var e = Assert.Throws<KeyNotFoundException>(() => EnemyTestSceneBuilder.CreateActors(_scene, playerLoadout, catalog));

                Assert.That(e.Message, Does.Contain($"'{EnemyTestSceneBuilder.EnemyId}'"), "names the placement");
                Assert.That(e.Message, Does.Contain($"'{EnemyTestSceneBuilder.EnemyWeaponId}'"), "names the weapon id");
                Assert.That(e.Message, Does.Contain("'HandgunOnly'"), "names the catalog");
                Assert.That(_scene.rootCount, Is.Zero, "nothing is placed");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(catalog);
                UnityEngine.Object.DestroyImmediate(handgun);
                UnityEngine.Object.DestroyImmediate(playerLoadout);
            }
        }

        [Test]
        public void ActorsRefuseAnInvalidCatalog()
        {
            var playerLoadout = ScriptableObject.CreateInstance<LoadoutDefinition>();
            var empty = ScriptableObject.CreateInstance<WeaponCatalogAsset>();
            try
            {
                Assert.Throws<WeaponDefinitionException>(() => EnemyTestSceneBuilder.CreateActors(_scene, playerLoadout, empty));
                Assert.That(_scene.rootCount, Is.Zero, "nothing is placed");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(empty);
                UnityEngine.Object.DestroyImmediate(playerLoadout);
            }
        }

        private void AssertActorsAreWired(EnemyAgent agent, LoadoutDefinition playerLoadout)
        {
            var bootstrap = Find(EnemyTestSceneBuilder.BootstrapObjectName).GetComponent<GameBootstrap>();
            Assert.That(bootstrap, Is.Not.Null);
            Assert.That(bootstrap.CreatesFloor, Is.False, "the scene's own floor carries the baked NavMesh");
            Assert.That(bootstrap.LoadoutDefinition, Is.SameAs(playerLoadout), "the player carries the loadout");

            Assert.That(agent.name, Is.EqualTo(EnemyTestSceneBuilder.EnemyName));
            Assert.That(agent.gameObject.scene, Is.EqualTo(_scene));
            Assert.That(agent.Id, Is.EqualTo(EnemyTestSceneBuilder.EnemyId));
            Assert.That(agent.PatrolRoute, Is.EqualTo(EnemyTestSceneBuilder.PatrolRoute));

            var navAgent = agent.GetComponent<NavMeshAgent>();
            Assert.That(navAgent.baseOffset, Is.EqualTo(EnemyTestSceneBuilder.EnemyHeight / 2f));
            Assert.That(navAgent.radius, Is.EqualTo(EnemyTestSceneBuilder.EnemyRadius));

            var feet = agent.transform.position - (Vector3.up * navAgent.baseOffset);
            Assert.That(Vector3.Distance(feet, EnemyTestSceneBuilder.PatrolRoute[0]), Is.LessThan(1e-4f), "starts on the first patrol point");

            var damageable = agent.GetComponent<Damageable>();
            Assert.That(damageable.Id, Is.EqualTo(EnemyTestSceneBuilder.EnemyId));
            Assert.That(damageable.MaxHealth, Is.EqualTo(EnemyTestSceneBuilder.EnemyMaxHealth));

            var hitbox = agent.GetComponent<Hitbox>();
            Assert.That(hitbox.Zone, Is.EqualTo(HitZone.Body));
            Assert.That(hitbox.Owner, Is.SameAs(damageable));
            Assert.That(agent.GetComponent<CapsuleCollider>(), Is.Not.Null);
        }

        [Test]
        public void PatrolRouteIsHiddenFirstAndThenComesIntoView()
        {
            // 巡回路の配置が意図どおりか (プレイヤーは原点の目の高さから見る)。
            var eye = new Vector3(0f, GameBootstrap.PlayerSpawn.y + GameBootstrap.EyeOffset.y, 0f);
            EnemyTestSceneBuilder.CreateLevel(_scene);
            Physics.SyncTransforms();

            var route = EnemyTestSceneBuilder.PatrolRoute;
            Assert.That(IsVisible(eye, route[0]), Is.False, "the enemy starts behind the wall");
            Assert.That(IsVisible(eye, route[2]), Is.True, "the last point is in the open");
            Assert.That(
                Vector3.Distance(Vector3.zero, route[2]),
                Is.LessThan(EnemyAgent.DefaultSightRange),
                "the player is within sight from the last point");
        }

        [Test]
        public void AppendsSceneAndKeepsTheOrder()
        {
            var current = new[]
            {
                new EditorBuildSettingsScene(BootstrapSceneBuilder.DefaultScenePath, true),
                new EditorBuildSettingsScene(Other, false),
            };

            var scenes = EnemyTestSceneBuilder.WithSceneAppended(current, EnemyTestSceneBuilder.ScenePath);

            Assert.That(scenes, Has.Length.EqualTo(3));
            Assert.That(scenes[0].path, Is.EqualTo(BootstrapSceneBuilder.DefaultScenePath));
            Assert.That(scenes[1].path, Is.EqualTo(Other));
            Assert.That(scenes[1].enabled, Is.False);
            Assert.That(scenes[2].path, Is.EqualTo(EnemyTestSceneBuilder.ScenePath));
            Assert.That(scenes[2].enabled, Is.True);
        }

        [Test]
        public void ReenablesAnExistingEntryInPlace()
        {
            var current = new[]
            {
                new EditorBuildSettingsScene(EnemyTestSceneBuilder.ScenePath, false),
                new EditorBuildSettingsScene(Other, true),
            };

            var scenes = EnemyTestSceneBuilder.WithSceneAppended(current, EnemyTestSceneBuilder.ScenePath);

            Assert.That(scenes, Has.Length.EqualTo(2));
            Assert.That(scenes[0].path, Is.EqualTo(EnemyTestSceneBuilder.ScenePath));
            Assert.That(scenes[0].enabled, Is.True);
            Assert.That(EnemyTestSceneBuilder.WithSceneAppended(null, EnemyTestSceneBuilder.ScenePath), Has.Length.EqualTo(1));
        }

        [Test]
        public void CommittedSceneHasABakedNavMesh()
        {
            var path = EnemyTestSceneBuilder.ScenePath;
            Assume.That(AssetDatabase.LoadAssetAtPath<SceneAsset>(path), Is.Not.Null, $"run {nameof(EnemyTestSceneBuilder)}.Build first");

            // テストの前に開いていた追加シーンは使わず、保存済みのシーンを開いて確かめる。
            EditorSceneManager.CloseScene(_scene, true);
            _scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);

            NavMeshSurface surface = null;
            EnemyAgent agent = null;
            foreach (var root in _scene.GetRootGameObjects())
            {
                surface = surface != null ? surface : root.GetComponent<NavMeshSurface>();
                agent = agent != null ? agent : root.GetComponent<EnemyAgent>();
            }

            Assert.That(surface, Is.Not.Null);
            Assert.That(surface.navMeshData, Is.Not.Null, "the NavMesh must be baked and committed");
            Assert.That(AssetDatabase.GetAssetPath(surface.navMeshData), Is.EqualTo(EnemyTestSceneBuilder.NavMeshDataPath));
            Assert.That(agent, Is.Not.Null);
            Assert.That(agent.PatrolRoute, Is.Not.Empty);

            var catalog = AssetDatabase.LoadAssetAtPath<WeaponCatalogAsset>(WeaponAssetBuilder.CatalogPath);
            Assert.That(catalog, Is.Not.Null, $"run {nameof(WeaponAssetBuilder)}.Build");
            Assert.That(catalog.TryFind(EnemyTestSceneBuilder.EnemyWeaponId, out var expected), Is.True);
            Assert.That(agent.WeaponDefinition, Is.SameAs(expected), $"rerun {nameof(EnemyTestSceneBuilder)}.Build");
        }

        private static WeaponDefinition NewWeapon(string name, WeaponSpec spec)
        {
            var weapon = ScriptableObject.CreateInstance<WeaponDefinition>();
            weapon.name = name;
            WeaponAssetBuilder.Configure(weapon, spec);
            return weapon;
        }

        private static WeaponCatalogAsset NewCatalog(string name, params WeaponDefinition[] weapons)
        {
            var catalog = ScriptableObject.CreateInstance<WeaponCatalogAsset>();
            catalog.name = name;
            WeaponAssetBuilder.Configure(catalog, weapons);
            return catalog;
        }

        private GameObject Find(string name)
        {
            foreach (var root in _scene.GetRootGameObjects())
            {
                if (root.name == name)
                {
                    return root;
                }
            }

            Assert.Fail($"'{name}' is not in the scene");
            return null;
        }

        private static bool IsVisible(Vector3 eye, Vector3 groundPoint)
        {
            var point = groundPoint + (Vector3.up * 1f);
            var toPoint = point - eye;
            return !Physics.Raycast(eye, toPoint.normalized, toPoint.magnitude, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        }
    }
}
