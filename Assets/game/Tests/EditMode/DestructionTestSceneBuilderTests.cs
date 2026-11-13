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
    public sealed class DestructionTestSceneBuilderTests
    {
        private const float Epsilon = 1e-4f;

        private Scene _scene;
        private readonly List<UnityEngine.Object> _assets = new List<UnityEngine.Object>();

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

            foreach (var asset in _assets)
            {
                if (asset != null)
                {
                    UnityEngine.Object.DestroyImmediate(asset);
                }
            }

            _assets.Clear();
        }

        [Test]
        public void LevelHasAFloorWithANavMeshSurface()
        {
            var surface = DestructionTestSceneBuilder.CreateLevel(_scene);

            var floor = Find(DestructionTestSceneBuilder.FloorName);
            Assert.That(surface.gameObject, Is.SameAs(floor));
            Assert.That(floor.transform.localScale, Is.EqualTo(DestructionTestSceneBuilder.FloorScale));
            Assert.That(floor.GetComponent<MeshCollider>(), Is.Not.Null, "the surface bakes from physics colliders");
            Assert.That(surface.collectObjects, Is.EqualTo(CollectObjects.All));
            Assert.That(surface.useGeometry, Is.EqualTo(NavMeshCollectGeometry.PhysicsColliders));
            Assert.That(_scene.rootCount, Is.EqualTo(1), "the hut is placed after baking so that it does not cut the NavMesh");
        }

        [Test]
        public void LevelRefusesANonEmptyScene()
        {
            var stray = new GameObject("Stray");
            SceneManager.MoveGameObjectToScene(stray, _scene);

            Assert.Throws<InvalidOperationException>(() => DestructionTestSceneBuilder.CreateLevel(_scene));
        }

        [Test]
        public void HutHasWallsAndARoofOfOneMetrePieces()
        {
            var hut = DestructionTestSceneBuilder.CreateHut(_scene, true);

            Assert.That(hut.name, Is.EqualTo(DestructionTestSceneBuilder.HutName));
            Assert.That(hut.gameObject.scene, Is.EqualTo(_scene));
            Assert.That(hut.transform.position, Is.EqualTo(DestructionTestSceneBuilder.HutCenter));
            Assert.That(hut.DestructionEnabled, Is.True);

            var pieces = hut.Pieces;
            Assert.That(pieces, Has.Count.EqualTo(DestructionTestSceneBuilder.PieceCount));
            for (var i = 0; i < pieces.Count; i++)
            {
                Assert.That(pieces[i].Index, Is.EqualTo(i));
                Assert.That(pieces[i].MaxHealth, Is.EqualTo(DestructionTestSceneBuilder.PieceMaxHealth));
                Assert.That(pieces[i].Collider, Is.InstanceOf<BoxCollider>(), pieces[i].name);
                Assert.That(pieces[i].GetComponent<NavMeshObstacle>(), Is.Not.Null, pieces[i].name + " carves the NavMesh");
                Assert.That(pieces[i].transform.lossyScale, Is.EqualTo(Vector3.one * DestructionTestSceneBuilder.PieceSize), pieces[i].name);
            }

            foreach (var wall in new[]
            {
                DestructionTestSceneBuilder.FrontWallName,
                DestructionTestSceneBuilder.BackWallName,
                DestructionTestSceneBuilder.LeftWallName,
                DestructionTestSceneBuilder.RightWallName,
                DestructionTestSceneBuilder.RoofName,
            })
            {
                var group = hut.transform.Find(wall);
                Assert.That(group, Is.Not.Null, wall);
                Assert.That(group.GetComponentsInChildren<DestructiblePiece>(), Is.Not.Empty, wall);
            }

            Assert.That(
                hut.transform.Find(DestructionTestSceneBuilder.RoofName).GetComponentsInChildren<DestructiblePiece>(),
                Has.Length.EqualTo(DestructionTestSceneBuilder.RoofPieceCount));
        }

        [Test]
        public void OnlyTheBottomRowTouchesTheGround()
        {
            var hut = DestructionTestSceneBuilder.CreateHut(_scene, true);
            Physics.SyncTransforms();

            var grounded = 0;
            foreach (var piece in hut.Pieces)
            {
                var bottom = piece.Collider.bounds.min.y;
                if (piece.Grounded)
                {
                    grounded++;
                    Assert.That(bottom, Is.EqualTo(0f).Within(Epsilon), piece.name + " rests on the floor");
                }
                else
                {
                    Assert.That(bottom, Is.GreaterThan(Epsilon), piece.name + " is above the floor");
                }
            }

            Assert.That(grounded, Is.EqualTo(DestructionTestSceneBuilder.GroundedPieceCount));
            Assert.That(hut.Integrity.Layout.PieceCount, Is.EqualTo(DestructionTestSceneBuilder.PieceCount));
        }

        [Test]
        public void TheWallsAndRoofHoldTogether()
        {
            var hut = DestructionTestSceneBuilder.CreateHut(_scene, true);

            // 下段の部品を 1 つ壊しても、角と屋根でつながっているので何も崩れない。
            var integrity = hut.Integrity;
            var bottom = FirstGrounded(hut);
            var result = integrity.ApplyDamage(bottom.Index, bottom.MaxHealth);
            Assert.That(result.Destroyed, Is.EqualTo(new[] { bottom.Index }));
            Assert.That(result.Collapsed, Is.Empty);

            // 下段をすべて壊すと、残りはすべて崩れる。
            var collapsed = 0;
            foreach (var piece in hut.Pieces)
            {
                if (piece.Grounded && !integrity.IsDestroyed(piece.Index))
                {
                    collapsed += integrity.ApplyDamage(piece.Index, piece.MaxHealth).Collapsed.Count;
                }
            }

            Assert.That(collapsed, Is.EqualTo(DestructionTestSceneBuilder.PieceCount - DestructionTestSceneBuilder.GroundedPieceCount));
        }

        [Test]
        public void ActorsAreWiredInCode()
        {
            var launcher = NewWeapon("Launcher", WeaponAssetBuilder.LauncherSpec());
            var rifle = NewWeapon("Rifle", WeaponAssetBuilder.RifleSpec());
            var rules = NewRules(GameRulesAssetBuilder.RangeRules());

            var hut = DestructionTestSceneBuilder.CreateActors(_scene, new[] { launcher, rifle }, rules);

            var bootstrap = Find(DestructionTestSceneBuilder.BootstrapObjectName).GetComponent<GameBootstrap>();
            Assert.That(bootstrap, Is.Not.Null);
            Assert.That(bootstrap.CreatesFloor, Is.False, "the scene's own floor carries the baked NavMesh");
            Assert.That(bootstrap.WeaponDefinitions, Is.EqualTo(new[] { launcher, rifle }), "the player starts with the launcher");
            Assert.That(bootstrap.RulesDefinition, Is.SameAs(rules));

            var pool = Find(DestructionTestSceneBuilder.DebrisPoolName).GetComponent<DebrisPool>();
            Assert.That(pool, Is.Not.Null);
            Assert.That(hut.DebrisPool, Is.SameAs(pool));
            Assert.That(hut.DestructionEnabled, Is.True, "the range rules turn destruction on");
            Assert.That(hut.HasAuthority, Is.True);
        }

        [Test]
        public void TheRulesDecideWhetherTheHutBreaks()
        {
            var launcher = NewWeapon("Launcher", WeaponAssetBuilder.LauncherSpec());
            var rules = NewRules(GameRulesAssetBuilder.CampaignNormalRules());
            Assume.That(rules.ToRules().Destruction, Is.EqualTo(Destruction.Off));

            var hut = DestructionTestSceneBuilder.CreateActors(_scene, new[] { launcher }, rules);

            Assert.That(hut.DestructionEnabled, Is.False);
        }

        [Test]
        public void ActorsRefuseMissingOrInvalidArguments()
        {
            var launcher = NewWeapon("Launcher", WeaponAssetBuilder.LauncherSpec());
            var rules = NewRules(GameRulesAssetBuilder.RangeRules());
            var broken = NewRules(GameRulesAssetBuilder.RangeRules());
            broken.ReserveAmmoMultiplier = -1f;

            Assert.Throws<ArgumentNullException>(() => DestructionTestSceneBuilder.CreateActors(_scene, null, rules));
            Assert.Throws<ArgumentException>(() => DestructionTestSceneBuilder.CreateActors(_scene, Array.Empty<WeaponDefinition>(), rules));
            Assert.Throws<ArgumentNullException>(() => DestructionTestSceneBuilder.CreateActors(_scene, new[] { launcher }, null));
            Assert.Throws<GameRulesDefinitionException>(() => DestructionTestSceneBuilder.CreateActors(_scene, new[] { launcher }, broken));
            Assert.That(_scene.rootCount, Is.Zero, "nothing is placed");
        }

        [Test]
        public void TheLauncherBreaksAPieceInOneShot()
        {
            Assert.That(WeaponAssetBuilder.LauncherSpec().Damage, Is.GreaterThanOrEqualTo(DestructionTestSceneBuilder.PieceMaxHealth));
            Assert.That(
                DestructionTestSceneBuilder.WallHeight,
                Is.GreaterThanOrEqualTo(WeaponAssetBuilder.LauncherExplosionRadius),
                "a shot at the bottom row does not reach the roof with full damage");
        }

        [Test]
        public void CommittedSceneHasTheHutAndABakedNavMesh()
        {
            var path = DestructionTestSceneBuilder.ScenePath;
            Assume.That(AssetDatabase.LoadAssetAtPath<SceneAsset>(path), Is.Not.Null, $"run {nameof(DestructionTestSceneBuilder)}.Build first");

            // テストの前に開いていた追加シーンは使わず、保存済みのシーンを開いて確かめる。
            EditorSceneManager.CloseScene(_scene, true);
            _scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);

            NavMeshSurface surface = null;
            DestructibleStructure hut = null;
            GameBootstrap bootstrap = null;
            foreach (var root in _scene.GetRootGameObjects())
            {
                surface = surface != null ? surface : root.GetComponent<NavMeshSurface>();
                hut = hut != null ? hut : root.GetComponent<DestructibleStructure>();
                bootstrap = bootstrap != null ? bootstrap : root.GetComponent<GameBootstrap>();
            }

            Assert.That(surface, Is.Not.Null);
            Assert.That(surface.navMeshData, Is.Not.Null, "the NavMesh must be baked and committed");
            Assert.That(AssetDatabase.GetAssetPath(surface.navMeshData), Is.EqualTo(DestructionTestSceneBuilder.NavMeshDataPath));

            Assert.That(hut, Is.Not.Null);
            Assert.That(hut.Pieces, Has.Count.EqualTo(DestructionTestSceneBuilder.PieceCount));
            Assert.That(FirstGrounded(hut), Is.Not.Null, "the hut stands on grounded pieces");
            Assert.That(hut.DebrisPool, Is.Not.Null);
            Assert.That(hut.DestructionEnabled, Is.True, $"the range rules turn destruction on; rerun {nameof(GameRulesAssetBuilder)}.Build and {nameof(DestructionTestSceneBuilder)}.Build");

            Assert.That(bootstrap, Is.Not.Null);
            Assert.That(bootstrap.WeaponDefinitions, Is.Not.Empty);
            Assert.That(
                bootstrap.WeaponDefinitions[0],
                Is.SameAs(AssetDatabase.LoadAssetAtPath<WeaponDefinition>(WeaponAssetBuilder.LauncherPath)),
                $"the player starts with the launcher; rerun {nameof(DestructionTestSceneBuilder)}.Build");
            Assert.That(
                bootstrap.RulesDefinition,
                Is.SameAs(AssetDatabase.LoadAssetAtPath<GameRulesDefinition>(GameRulesAssetBuilder.RangePath)));
        }

        private static DestructiblePiece FirstGrounded(DestructibleStructure hut)
        {
            foreach (var piece in hut.Pieces)
            {
                if (piece.Grounded)
                {
                    return piece;
                }
            }

            return null;
        }

        private WeaponDefinition NewWeapon(string name, WeaponSpec spec)
        {
            var weapon = ScriptableObject.CreateInstance<WeaponDefinition>();
            weapon.name = name;
            _assets.Add(weapon);
            WeaponAssetBuilder.Configure(weapon, spec);
            return weapon;
        }

        private GameRulesDefinition NewRules(GameRules rules)
        {
            var definition = ScriptableObject.CreateInstance<GameRulesDefinition>();
            definition.name = "Rules";
            _assets.Add(definition);
            GameRulesAssetBuilder.Configure(definition, rules);
            return definition;
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
    }
}
