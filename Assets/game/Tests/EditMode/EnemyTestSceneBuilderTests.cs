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
            _scene = TestScenes.NewAdditive();
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
            var playerWeapon = NewWeapon("PlayerRifle", WeaponAssetBuilder.RifleSpec());
            var rifle = NewWeapon("Rifle", WeaponAssetBuilder.RifleSpec());
            var handgun = NewWeapon("Handgun", WeaponAssetBuilder.HandgunSpec());
            var catalog = NewCatalog("WeaponCatalog", handgun, rifle);
            try
            {
                var agent = EnemyTestSceneBuilder.CreateActors(_scene, new[] { playerWeapon, handgun }, catalog);

                AssertActorsAreWired(agent, new[] { playerWeapon, handgun });
                Assert.That(agent.WeaponDefinition, Is.SameAs(rifle), "the enemy shoots the catalog's weapon with its id");
                Assert.That(agent.WeaponDefinition.Id, Is.EqualTo(EnemyTestSceneBuilder.EnemyWeaponId));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(catalog);
                UnityEngine.Object.DestroyImmediate(handgun);
                UnityEngine.Object.DestroyImmediate(rifle);
                UnityEngine.Object.DestroyImmediate(playerWeapon);
            }
        }

        [Test]
        public void ActorsNeedAPlayerWeaponAndACatalog()
        {
            var rifle = NewWeapon("Rifle", WeaponAssetBuilder.RifleSpec());
            var catalog = NewCatalog("WeaponCatalog", rifle);
            try
            {
                Assert.Throws<ArgumentNullException>(() => EnemyTestSceneBuilder.CreateActors(_scene, null, catalog));
                Assert.Throws<ArgumentException>(() => EnemyTestSceneBuilder.CreateActors(_scene, Array.Empty<WeaponDefinition>(), catalog));
                Assert.Throws<ArgumentNullException>(() => EnemyTestSceneBuilder.CreateActors(_scene, new[] { rifle }, null));
                Assert.That(_scene.rootCount, Is.Zero);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(catalog);
                UnityEngine.Object.DestroyImmediate(rifle);
            }
        }

        [Test]
        public void ActorsRefuseAWeaponIdThatIsNotInTheCatalog()
        {
            var rifle = NewWeapon("Rifle", WeaponAssetBuilder.RifleSpec());
            var handgun = NewWeapon("Handgun", WeaponAssetBuilder.HandgunSpec());
            var catalog = NewCatalog("HandgunOnly", handgun);
            try
            {
                var e = Assert.Throws<KeyNotFoundException>(() => EnemyTestSceneBuilder.CreateActors(_scene, new[] { rifle }, catalog));

                Assert.That(e.Message, Does.Contain($"'{EnemyTestSceneBuilder.EnemyId}'"), "names the placement");
                Assert.That(e.Message, Does.Contain($"'{EnemyTestSceneBuilder.EnemyWeaponId}'"), "names the weapon id");
                Assert.That(e.Message, Does.Contain("'HandgunOnly'"), "names the catalog");
                Assert.That(_scene.rootCount, Is.Zero, "nothing is placed");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(catalog);
                UnityEngine.Object.DestroyImmediate(handgun);
                UnityEngine.Object.DestroyImmediate(rifle);
            }
        }

        [Test]
        public void ActorsRefuseAnInvalidCatalog()
        {
            var rifle = NewWeapon("Rifle", WeaponAssetBuilder.RifleSpec());
            var empty = ScriptableObject.CreateInstance<WeaponCatalogAsset>();
            try
            {
                Assert.Throws<WeaponDefinitionException>(() => EnemyTestSceneBuilder.CreateActors(_scene, new[] { rifle }, empty));
                Assert.That(_scene.rootCount, Is.Zero, "nothing is placed");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(empty);
                UnityEngine.Object.DestroyImmediate(rifle);
            }
        }

        [Test]
        public void PickupSpawnPointsAreWiredInCode()
        {
            var launcher = NewWeapon("Launcher", WeaponAssetBuilder.LauncherSpec());
            try
            {
                var root = EnemyTestSceneBuilder.CreatePickups(_scene, launcher);

                Assert.That(root.name, Is.EqualTo(EnemyTestSceneBuilder.PickupsObjectName));
                Assert.That(root.scene, Is.EqualTo(_scene));
                var points = root.GetComponentsInChildren<PickupSpawnPoint>();
                Assert.That(points, Has.Length.EqualTo(4));

                var weapon = root.transform.Find(EnemyTestSceneBuilder.WeaponPickupName).GetComponent<PickupSpawnPoint>();
                Assert.That(weapon.Contents.Kind, Is.EqualTo(PickupKind.Weapon));
                Assert.That(weapon.Contents.Weapon, Is.SameAs(launcher));
                Assert.That(weapon.transform.position, Is.EqualTo(EnemyTestSceneBuilder.WeaponPickupPosition));

                var ammo = root.transform.Find(EnemyTestSceneBuilder.AmmoPickupName).GetComponent<PickupSpawnPoint>();
                Assert.That(ammo.Contents.Kind, Is.EqualTo(PickupKind.ReserveAmmo));
                Assert.That(ammo.Contents.Weapon, Is.Null, "fills the weapon in hand");

                var health = root.transform.Find(EnemyTestSceneBuilder.HealthPickupName).GetComponent<PickupSpawnPoint>();
                Assert.That(health.Contents.Kind, Is.EqualTo(PickupKind.Health));
                Assert.That(health.CollectMode, Is.EqualTo(PickupCollectMode.Touch));

                var interact = root.transform.Find(EnemyTestSceneBuilder.InteractHealthPickupName).GetComponent<PickupSpawnPoint>();
                Assert.That(interact.CollectMode, Is.EqualTo(PickupCollectMode.Interact));

                foreach (var point in points)
                {
                    Assert.That(point.RespawnSeconds, Is.EqualTo(EnemyTestSceneBuilder.PickupRespawnSeconds));
                    var flat = new Vector2(point.transform.position.x, point.transform.position.z);
                    Assert.That(flat.magnitude, Is.GreaterThan(point.Radius + 1f), "not picked up at the player's spawn");
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(launcher);
            }
        }

        [Test]
        public void PickupsNeedAWeapon()
        {
            Assert.Throws<ArgumentNullException>(() => EnemyTestSceneBuilder.CreatePickups(_scene, null));
            Assert.That(_scene.rootCount, Is.Zero, "nothing is placed");
        }

        private void AssertActorsAreWired(EnemyAgent agent, WeaponDefinition[] playerWeapons)
        {
            var bootstrap = Find(EnemyTestSceneBuilder.BootstrapObjectName).GetComponent<GameBootstrap>();
            Assert.That(bootstrap, Is.Not.Null);
            Assert.That(bootstrap.CreatesFloor, Is.False, "the scene's own floor carries the baked NavMesh");
            Assert.That(bootstrap.WeaponDefinitions, Is.EqualTo(playerWeapons), "the player carries the loadout");

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

            Assert.That(agent.GetComponent<WeaponPresentation>(), Is.Not.Null, "rerun EnemyTestSceneBuilder.Build");
            Assert.That(agent.GetComponent<CharacterAudio>(), Is.Not.Null, "rerun EnemyTestSceneBuilder.Build");
            Assert.That(agent.GetComponent<CharacterAnimator>(), Is.Not.Null, "rerun EnemyTestSceneBuilder.Build");
            Assert.That(agent.GetComponent<GrenadeThrower>(), Is.Null, "enemies do not throw grenades");

            var drop = agent.GetComponent<DropOnDeath>();
            Assert.That(drop, Is.Not.Null, "the enemy drops pickups when it falls");
            Assert.That(drop.Drops, Has.Count.EqualTo(2));
            Assert.That(drop.Drops[0].Kind, Is.EqualTo(PickupKind.ReserveAmmo));
            Assert.That(drop.Drops[0].Amount, Is.EqualTo(EnemyTestSceneBuilder.EnemyDropAmmoAmount));
            Assert.That(drop.Drops[1].Kind, Is.EqualTo(PickupKind.Health));
            Assert.That(drop.Drops[1].Amount, Is.EqualTo(EnemyTestSceneBuilder.EnemyDropHealthAmount));

            var registry = agent.CoverRegistry;
            Assert.That(registry, Is.Not.Null, "the enemy takes cover");
            Assert.That(registry.name, Is.EqualTo(EnemyTestSceneBuilder.CoverPointsObjectName));
            Assert.That(registry.gameObject.scene, Is.EqualTo(_scene));
            Assert.That(registry.Points.Count, Is.EqualTo(EnemyTestSceneBuilder.CoverPoints.Count));
            for (var i = 0; i < registry.Points.Count; i++)
            {
                var placement = EnemyTestSceneBuilder.CoverPoints[i];
                var point = registry.Points[i];
                Assert.That(point.name, Is.EqualTo(placement.Name));
                Assert.That(point.Height, Is.EqualTo(placement.Height));
                Assert.That(Vector3.Distance(point.Position, placement.Position), Is.LessThan(1e-4f));
                Assert.That(Vector3.Distance(point.PeekPosition, placement.PeekPosition), Is.LessThan(1e-4f));
                Assert.That(Vector3.Angle(point.Facing, placement.Facing), Is.LessThan(0.01f));
            }
        }

        [Test]
        public void LowCoverHidesACrouchedEnemyAndWallEndsHideUntilPeeking()
        {
            // 原点のプレイヤーの目から、遮蔽点で隠れた敵は見えず、覗けば見える。
            var eye = new Vector3(0f, GameBootstrap.PlayerSpawn.y + GameBootstrap.EyeOffset.y, 0f);
            EnemyTestSceneBuilder.CreateLevel(_scene);
            Physics.SyncTransforms();

            Assert.That(Find(EnemyTestSceneBuilder.LowCoverName).transform.localScale, Is.EqualTo(EnemyTestSceneBuilder.LowCoverSize));
            Assert.That(EnemyTestSceneBuilder.LowCoverSize.y, Is.LessThan(CoverProbe.LowWallHeight), "it counts as a low wall");

            foreach (var placement in EnemyTestSceneBuilder.CoverPoints)
            {
                if (placement.Height == CoverHeight.Low)
                {
                    // 低い壁の、プレイヤーの側を向いた点だけを見る (反対側の点は回り込まれたとき用)。
                    if (Vector3.Dot(placement.Facing, -placement.Position) <= 0f)
                    {
                        continue;
                    }

                    var crouchedTop = placement.Position + (Vector3.up * (EnemyAgent.DefaultCrouchHeight - 0.05f));
                    Assert.That(IsVisibleAt(eye, crouchedTop), Is.False, $"{placement.Name} hides a crouched enemy");
                    var standingEye = placement.Position + (Vector3.up * EnemyAgent.DefaultEyeHeight);
                    Assert.That(IsVisibleAt(eye, standingEye), Is.True, $"{placement.Name} lets the enemy stand up and peek");
                }
                else
                {
                    var hidden = placement.Position + (Vector3.up * EnemyAgent.DefaultEyeHeight);
                    Assert.That(IsVisibleAt(eye, hidden), Is.False, $"{placement.Name} hides a standing enemy");
                    var peeking = placement.PeekPosition + (Vector3.up * EnemyAgent.DefaultEyeHeight);
                    Assert.That(IsVisibleAt(eye, peeking), Is.True, $"{placement.Name} peeks past the wall end");
                }
            }
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
            return IsVisibleAt(eye, groundPoint + (Vector3.up * 1f));
        }

        private static bool IsVisibleAt(Vector3 eye, Vector3 point)
        {
            var toPoint = point - eye;
            return !Physics.Raycast(eye, toPoint.normalized, toPoint.magnitude, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        }
    }
}
