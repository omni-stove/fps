using System;
using System.Collections.Generic;
using System.Linq;
using Fps.Core;
using Fps.Editor;
using Fps.Runtime;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using LadderComponent = Fps.Runtime.Traversal.Ladder;
using ZiplineComponent = Fps.Runtime.Traversal.Zipline;

namespace Fps.Tests.EditMode
{
    public sealed class FeatureSceneBuilderTests
    {
        private const float Epsilon = 1e-4f;

        // 出現位置の周り (プレイヤーの直径の倍ほど) には何も置かない。
        private const float SpawnClearance = 1.5f;

        private static readonly GameRulesDebugField[] _openFloorFields =
        {
            GameRulesDebugField.InfiniteReserve,
            GameRulesDebugField.ActiveReload,
            GameRulesDebugField.Dodge,
            GameRulesDebugField.Grenade,
            GameRulesDebugField.SlowMotion,
            GameRulesDebugField.AimToFire,
            GameRulesDebugField.Jump,
        };

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
        public void ThereIsOneSceneForEachOnOffRuleInPanelOrder()
        {
            var expected = GameRulesDebugDraft.Fields.Where(GameRulesDebugDraft.IsToggle).ToArray();

            Assert.That(FeatureSceneBuilder.All().Select(f => f.Field), Is.EqualTo(expected));
            Assert.That(FeatureSceneBuilder.MenuPath, Is.EqualTo("Fps/Build Feature Scenes"));
        }

        [Test]
        public void StationsRejectFieldsThatAreNotOnOff()
        {
            Assert.Throws<ArgumentException>(() => FeatureSceneBuilder.Stations(GameRulesDebugField.HudDensity));
            Assert.Throws<ArgumentException>(() => FeatureSceneBuilder.Stations(GameRulesDebugField.ViewMode));
        }

        [Test]
        public void FeaturesTriedOnTheOpenFloorHaveNoStation()
        {
            foreach (var field in _openFloorFields)
            {
                Assert.That(FeatureSceneBuilder.Stations(field), Is.Empty, field.ToString());
            }

            foreach (var feature in FeatureSceneBuilder.All().Where(f => !_openFloorFields.Contains(f.Field)))
            {
                Assert.That(feature.Stations, Has.Count.EqualTo(1), feature.Field.ToString());
            }
        }

        [Test]
        public void EachFeatureUsesItsOwnPlace()
        {
            Assert.That(FixtureOf(GameRulesDebugField.Zipline), Is.TypeOf<ZiplineRangeFixture>());
            Assert.That(FixtureOf(GameRulesDebugField.Mantle), Is.TypeOf<MantleLedgesFixture>());
            Assert.That(FixtureOf(GameRulesDebugField.Prone), Is.TypeOf<ProneCrawlwayFixture>());
            Assert.That(FixtureOf(GameRulesDebugField.Slide), Is.TypeOf<SlideSlopeFixture>());
            Assert.That(FixtureOf(GameRulesDebugField.Stealth), Is.TypeOf<StealthRangeFixture>());
            Assert.That(FixtureOf(GameRulesDebugField.Stagger), Is.TypeOf<StaggerRangeFixture>());
            Assert.That(FixtureOf(GameRulesDebugField.Suppression), Is.TypeOf<SuppressionRangeFixture>());
            Assert.That(FixtureOf(GameRulesDebugField.Ping), Is.TypeOf<PingRangeFixture>());
            Assert.That(FixtureOf(GameRulesDebugField.Melee), Is.TypeOf<MeleeRangeFixture>());
            Assert.That(FixtureOf(GameRulesDebugField.Abilities), Is.TypeOf<GrappleWallFixture>());
            Assert.That(FixtureOf(GameRulesDebugField.Cover).Name, Is.EqualTo(BootstrapSceneBuilder.CoversName));
            Assert.That(FixtureOf(GameRulesDebugField.BlindFire).Name, Is.EqualTo(BootstrapSceneBuilder.CoversName));
            Assert.That(FixtureOf(GameRulesDebugField.Vault).Name, Is.EqualTo(TpsRangeSceneBuilder.VaultWallName));
            Assert.That(FixtureOf(GameRulesDebugField.Destruction).Name, Is.EqualTo("HutYard"));
            Assert.That(FixtureOf(GameRulesDebugField.Ladder).Name, Is.EqualTo("LadderTower"));
            Assert.That(FixtureOf(GameRulesDebugField.Parachute).Name, Is.EqualTo("LadderTower"));
            Assert.That(FixtureOf(GameRulesDebugField.FallDamage).Name, Is.EqualTo("LadderTower"));
            Assert.That(FixtureOf(GameRulesDebugField.Downed).Name, Is.EqualTo("LadderTower"));
        }

        [Test]
        public void TheFeatureFixturesAreNotPickedUpByTheTpsRange()
        {
            var names = TpsRangeSceneBuilder.Fixtures().Select(f => f.GetType().Name).ToArray();

            Assert.That(names, Has.No.Member("FeatureLadderTowerFixture"));
            Assert.That(names, Has.No.Member("FeatureHutFixture"));
            Assert.That(names, Has.No.Member("FeatureCoversFixture"));
            Assert.That(names, Has.No.Member("FeatureVaultWallFixture"));
        }

        [Test]
        public void SceneHasOnlyTheBootstrapTheFloorAndTheFeatureStation()
        {
            var stations = FeatureSceneBuilder.Stations(GameRulesDebugField.Zipline);
            var rules = NewRules(GameRulesAssetBuilder.FeatureRules(GameRulesDebugField.Zipline));

            var bootstrap = FeatureSceneBuilder.PopulateScene(_scene, NewWeapons(), rules, stations);

            var names = _scene.GetRootGameObjects().Select(o => o.name).ToArray();
            Assert.That(names, Is.EquivalentTo(new[] { FeatureSceneBuilder.BootstrapObjectName, FeatureSceneBuilder.FloorName, ZiplineRangeFixture.FixtureName }));
            Assert.That(bootstrap.CreatesFloor, Is.False);
            Assert.That(bootstrap.RulesDefinition, Is.SameAs(rules));
            Assert.That(bootstrap.GetComponent<MatchRunner>(), Is.Not.Null);
        }

        [Test]
        public void OpenFloorSceneHasOnlyTheBootstrapAndTheSmallestFloor()
        {
            FeatureSceneBuilder.PopulateScene(
                _scene, NewWeapons(), NewRules(GameRulesAssetBuilder.FeatureRules(GameRulesDebugField.Dodge)), Array.Empty<FeatureStation>());

            Assert.That(_scene.rootCount, Is.EqualTo(2));
            Assert.That(Find(FeatureSceneBuilder.FloorName).transform.localScale, Is.EqualTo(new Vector3(3f, 1f, 3f)));
        }

        [Test]
        public void FloorIsTheMinimumOrCoversTheStationWithAMarginInStepsOfFive()
        {
            Assert.That(FeatureSceneBuilder.FloorHalfExtent(Array.Empty<FeatureStation>()), Is.EqualTo(15f));

            foreach (var feature in FeatureSceneBuilder.All())
            {
                var half = FeatureSceneBuilder.FloorHalfExtent(feature.Stations);
                Assert.That(half % FeatureSceneBuilder.FloorStep, Is.EqualTo(0f), feature.Field.ToString());
                foreach (var station in feature.Stations)
                {
                    var footprint = station.Footprint;
                    Assert.That(Mathf.Max(Mathf.Abs(footprint.min.x), Mathf.Abs(footprint.max.x)) + FeatureSceneBuilder.FloorMargin, Is.LessThanOrEqualTo(half), feature.Field.ToString());
                    Assert.That(Mathf.Max(Mathf.Abs(footprint.min.z), Mathf.Abs(footprint.max.z)) + FeatureSceneBuilder.FloorMargin, Is.LessThanOrEqualTo(half), feature.Field.ToString());
                }
            }

            // ジップラインの線は 22m ほどあり、手前の端 (z = 3) から 25m あまりまで伸びる。
            Assert.That(FeatureSceneBuilder.FloorHalfExtent(FeatureSceneBuilder.Stations(GameRulesDebugField.Zipline)), Is.EqualTo(35f));
        }

        [Test]
        public void BesideSpawnStationsStartRightOfTheDummiesAndAheadOfTheSpawn()
        {
            foreach (var feature in FeatureSceneBuilder.All())
            {
                foreach (var station in feature.Stations.Where(s => s.Placement == FeaturePlacement.BesideSpawn))
                {
                    Assert.That(station.Footprint.min.x, Is.EqualTo(FeatureSceneBuilder.BesideSpawnMinX).Within(Epsilon), feature.Field.ToString());
                    Assert.That(station.Footprint.min.z, Is.EqualTo(FeatureSceneBuilder.BesideSpawnMinZ).Within(Epsilon), feature.Field.ToString());
                    Assert.That(station.Offset.y, Is.EqualTo(0f));
                }
            }
        }

        [Test]
        public void StationsKeepClearOfTheSpawnAndTheTargetDummies()
        {
            var spawn = new Bounds(Vector3.zero, new Vector3(SpawnClearance * 2f, 1f, SpawnClearance * 2f));
            var dummies = GameBootstrap.TargetDummyPositions(Vector3.zero, Quaternion.identity);

            foreach (var feature in FeatureSceneBuilder.All())
            {
                foreach (var station in feature.Stations)
                {
                    var footprint = Flat(station.Footprint);
                    Assert.That(footprint.Intersects(spawn), Is.False, feature.Field + " is on the spawn");
                    foreach (var dummy in dummies)
                    {
                        Assert.That(footprint.Intersects(new Bounds(dummy, new Vector3(1f, 1f, 1f))), Is.False, $"{feature.Field} is on the dummy at {dummy}");
                    }
                }
            }
        }

        [Test]
        public void MovedZiplineKeepsItsShape()
        {
            var station = FeatureSceneBuilder.Stations(GameRulesDebugField.Zipline)[0];
            FeatureSceneBuilder.PopulateScene(
                _scene, NewWeapons(), NewRules(GameRulesAssetBuilder.FeatureRules(GameRulesDebugField.Zipline)), new[] { station });

            var line = Find(ZiplineRangeFixture.FixtureName).GetComponent<ZiplineComponent>();

            Assert.That(Vector3.Distance(line.StartPoint, ZiplineRangeFixture.StartAnchor + station.Offset), Is.LessThan(Epsilon));
            Assert.That(Vector3.Distance(line.EndPoint, ZiplineRangeFixture.EndAnchor + station.Offset), Is.LessThan(Epsilon));
        }

        [Test]
        public void InPlaceStationsStayWhereTheTpsRangePutsThem()
        {
            var station = FeatureSceneBuilder.Stations(GameRulesDebugField.Cover)[0];
            FeatureSceneBuilder.PopulateScene(
                _scene, NewWeapons(), NewRules(GameRulesAssetBuilder.FeatureRules(GameRulesDebugField.Cover)), new[] { station });

            Assert.That(station.Offset, Is.EqualTo(Vector3.zero));
            var low = Find(BootstrapSceneBuilder.CoversName).transform.Find(BootstrapSceneBuilder.LowCoverName);
            Assert.That(low.position, Is.EqualTo(BootstrapSceneBuilder.LowCover.Center));
        }

        [Test]
        public void DropTowerIsTallEnoughToOpenAParachuteAndTheLadderFacesTheSpawn()
        {
            var station = FeatureSceneBuilder.Stations(GameRulesDebugField.Parachute)[0];
            FeatureSceneBuilder.PopulateScene(
                _scene, NewWeapons(), NewRules(GameRulesAssetBuilder.FeatureRules(GameRulesDebugField.Parachute)), new[] { station });

            var tower = Find("LadderTower");
            var ladder = tower.GetComponentInChildren<LadderComponent>();

            Assert.That(ladder.Height, Is.EqualTo(ParachuteTowerFixture.TowerHeight));
            Assert.That(ladder.Height, Is.GreaterThan(ParachuteMotion.DefaultMinOpenHeight));
            Assert.That(ladder.transform.position.x, Is.LessThan(tower.transform.position.x), "the ladder is on the spawn side (-X)");
        }

        [Test]
        public void LadderSceneUsesTheLowTowerOfTheTpsRange()
        {
            var station = FeatureSceneBuilder.Stations(GameRulesDebugField.Ladder)[0];
            FeatureSceneBuilder.PopulateScene(
                _scene, NewWeapons(), NewRules(GameRulesAssetBuilder.FeatureRules(GameRulesDebugField.Ladder)), new[] { station });

            Assert.That(Find("LadderTower").GetComponentInChildren<LadderComponent>().Height, Is.EqualTo(LadderRangeFixture.TowerSize.y));
        }

        [Test]
        public void HutBreaksOnlyWhenTheRulesTurnDestructionOn()
        {
            var station = FeatureSceneBuilder.Stations(GameRulesDebugField.Destruction)[0];
            FeatureSceneBuilder.PopulateScene(
                _scene, NewWeapons(), NewRules(GameRulesAssetBuilder.FeatureRules(GameRulesDebugField.Destruction)), new[] { station });

            var yard = Find("HutYard");
            var hut = yard.GetComponentInChildren<DestructibleStructure>();

            Assert.That(hut, Is.Not.Null);
            Assert.That(hut.DebrisPool, Is.SameAs(yard.GetComponentInChildren<DebrisPool>()));
            Assert.That(yard.transform.position, Is.EqualTo(station.Offset));
        }

        [Test]
        public void StealthSceneArmsItsEnemiesWithTheBootstrapWeapon()
        {
            var weapons = NewWeapons();
            FeatureSceneBuilder.PopulateScene(
                _scene,
                weapons,
                NewRules(GameRulesAssetBuilder.FeatureRules(GameRulesDebugField.Stealth)),
                FeatureSceneBuilder.Stations(GameRulesDebugField.Stealth));

            Assert.That(Find(StealthRangeFixture.AreaName).GetComponentsInChildren<EnemyAgent>(true), Is.Not.Empty);
        }

        [Test]
        public void RejectsADuplicateStationNameAndLeavesTheSceneEmpty()
        {
            var stations = new[]
            {
                new FeatureStation(new ProneCrawlwayFixture(), FeaturePlacement.BesideSpawn),
                new FeatureStation(new ProneCrawlwayFixture(), FeaturePlacement.InPlace),
            };

            Assert.Throws<ArgumentException>(() => FeatureSceneBuilder.PopulateScene(
                _scene, NewWeapons(), NewRules(GameRulesAssetBuilder.FeatureRules(GameRulesDebugField.Prone)), stations));
            Assert.That(_scene.rootCount, Is.EqualTo(0));
        }

        [Test]
        public void StationRejectsANullFixture()
        {
            Assert.Throws<ArgumentNullException>(() => new FeatureStation(null, FeaturePlacement.InPlace));
        }

        private static ITpsRangeFixture FixtureOf(GameRulesDebugField field)
        {
            return FeatureSceneBuilder.Stations(field).Single().Fixture;
        }

        private static Bounds Flat(Bounds bounds)
        {
            return new Bounds(new Vector3(bounds.center.x, 0f, bounds.center.z), new Vector3(bounds.size.x, 1f, bounds.size.z));
        }

        private WeaponDefinition[] NewWeapons()
        {
            return new[] { NewWeapon("Rifle", WeaponAssetBuilder.RifleSpec()), NewWeapon("Launcher", WeaponAssetBuilder.LauncherSpec()) };
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
