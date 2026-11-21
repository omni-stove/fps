using System;
using System.Collections.Generic;
using System.Linq;
using Shooter.Core;
using Shooter.Editor;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using LadderComponent = Shooter.Runtime.Traversal.Ladder;
using ZiplineComponent = Shooter.Runtime.Traversal.Zipline;

namespace Shooter.Tests.EditMode
{
    public sealed class FeatureSceneBuilderTests
    {
        private const float Epsilon = 1e-4f;

        // 出現位置の周り (プレイヤーの直径の倍ほど) には何も置かない。
        private const float SpawnClearance = 1.5f;

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
        public void ThereAreOnlyThePlaygroundAndTheHighDropScenes()
        {
            Assert.That(FeatureSceneBuilder.All().Select(f => f.Name), Is.EqualTo(new[] { "Playground", "HighDrop" }));
            Assert.That(FeatureSceneBuilder.MenuPath, Is.EqualTo("Shooter/Build All Scenes"));
        }

        [Test]
        public void SceneNamesAreShortEnoughNotToBeCutOff()
        {
            foreach (var feature in FeatureSceneBuilder.All())
            {
                Assert.That(feature.Name.Length, Is.LessThanOrEqualTo(10), feature.Name);
            }
        }

        [Test]
        public void EveryOnOffRuleIsTriedInOneOfTheScenesExceptTheOnesThatNeedAMode()
        {
            var tried = FeatureSceneBuilder.All()
                .SelectMany(f => GameRulesDebugDraft.Fields.Where(GameRulesDebugDraft.IsToggle).Where(field => GameRulesDebugDraft.Read(f.Rules, field) == 1))
                .Distinct()
                .ToArray();
            var untried = GameRulesDebugDraft.Fields.Where(GameRulesDebugDraft.IsToggle).Except(tried).ToArray();

            Assert.That(untried, Is.EquivalentTo(new[] { GameRulesDebugField.Downed, GameRulesDebugField.AimToFire }));
        }

        [Test]
        public void OnlyThePlaygroundCarriesTheWallRunAndDoubleJumpTuning()
        {
            Assert.That(FeatureSceneBuilder.Playground().UsesTraversalTuning, Is.True);
            Assert.That(FeatureSceneBuilder.HighDrop().UsesTraversalTuning, Is.False);
        }

        [Test]
        public void PlaygroundPlacesEveryStationOfTheOldFeatureScenes()
        {
            var names = FeatureSceneBuilder.PlaygroundStations().Select(s => s.Fixture.Name).ToArray();

            Assert.That(names, Is.EquivalentTo(new[]
            {
                BootstrapSceneBuilder.CoversName,
                TpsRangeSceneBuilder.VaultWallName,
                "HutYard",
                LadderRangeFixture.FixtureName,
                new MeleeRangeFixture().Name,
                new StaggerRangeFixture().Name,
                new SlideSlopeFixture().Name,
                new MantleLedgesFixture().Name,
                new ProneCrawlwayFixture().Name,
                ZiplineRangeFixture.FixtureName,
                StealthRangeFixture.AreaName,
                new SuppressionRangeFixture().Name,
                new PingRangeFixture().Name,
                new GrappleWallFixture().Name,
                TraversalFeatureSceneBuilder.LeanCornerName,
                TraversalFeatureSceneBuilder.SwimPoolsName,
                TraversalFeatureSceneBuilder.WallRunCorridorName,
                TraversalFeatureSceneBuilder.DoubleJumpStepsName,
            }));
        }

        [Test]
        public void PlaygroundStationsDoNotOverlapEachOtherTheSpawnOrTheDummies()
        {
            var stations = FeatureSceneBuilder.PlaygroundStations();
            var spawn = new Bounds(Vector3.zero, new Vector3(SpawnClearance * 2f, 1f, SpawnClearance * 2f));
            var dummies = GameBootstrap.TargetDummyPositions(Vector3.zero, Quaternion.identity);

            for (var i = 0; i < stations.Count; i++)
            {
                var name = stations[i].Fixture.Name;
                var footprint = stations[i].Footprint;
                Assert.That(Flat(footprint).Intersects(spawn), Is.False, name + " is on the spawn");
                foreach (var dummy in dummies)
                {
                    Assert.That(OverlapsOnTheFloor(footprint, new Bounds(dummy, Vector3.one)), Is.False, $"{name} is on the dummy at {dummy}");
                }

                for (var j = i + 1; j < stations.Count; j++)
                {
                    Assert.That(footprint.Intersects(stations[j].Footprint), Is.False, $"{name} overlaps {stations[j].Fixture.Name}");
                }
            }
        }

        [Test]
        public void TpsRangeStationsStayWhereTheTpsRangePutsThem()
        {
            var stations = FeatureSceneBuilder.PlaygroundStations();
            var hut = stations.Single(s => s.Fixture.Name == "HutYard");

            Assert.That(hut.Offset, Is.EqualTo(TpsRangeSceneBuilder.HutCenter));
            foreach (var station in stations.Take(14).Where(s => s.Fixture.Name != "HutYard"))
            {
                Assert.That(station.Offset, Is.EqualTo(Vector3.zero), station.Fixture.Name);
            }

            Assert.That(stations.Single(s => s.Fixture.Name == TpsRangeSceneBuilder.VaultWallName).Footprint.center, Is.EqualTo(TpsRangeSceneBuilder.VaultWall.Center));
        }

        [Test]
        public void TraversalStationsLineUpBehindTheSpawnOutOfTheEnemiesSight()
        {
            var stations = FeatureSceneBuilder.PlaygroundStations();
            var names = new[]
            {
                TraversalFeatureSceneBuilder.LeanCornerName,
                TraversalFeatureSceneBuilder.SwimPoolsName,
                TraversalFeatureSceneBuilder.WallRunCorridorName,
                TraversalFeatureSceneBuilder.DoubleJumpStepsName,
            };
            var rest = stations.Where(s => !names.Contains(s.Fixture.Name)).ToArray();
            var row = names.Select(n => stations.Single(s => s.Fixture.Name == n)).ToArray();
            var backZ = rest.Min(s => s.Footprint.min.z) - FeatureSceneBuilder.StationGap;

            Assert.That(row[0].Footprint.min.x, Is.EqualTo(FeatureSceneBuilder.TraversalRowStartX).Within(Epsilon));
            for (var i = 0; i < row.Length; i++)
            {
                Assert.That(row[i].Footprint.max.z, Is.EqualTo(backZ).Within(Epsilon), names[i]);
                Assert.That(row[i].Footprint.min.y, Is.EqualTo(0f).Within(Epsilon), names[i] + " stands on the floor");
                if (i > 0)
                {
                    Assert.That(row[i].Footprint.min.x - row[i - 1].Footprint.max.x, Is.EqualTo(FeatureSceneBuilder.StationGap).Within(Epsilon), names[i]);
                }
            }
        }

        [Test]
        public void RowPutsTheBackEdgesOnOneLineFromTheStart()
        {
            var fixtures = new ITpsRangeFixture[] { new ProneCrawlwayFixture(), new SuppressionRangeFixture() };
            var row = FeatureSceneBuilder.Row(fixtures, 10f, -20f);

            Assert.That(row[0].Footprint.min.x, Is.EqualTo(10f).Within(Epsilon));
            Assert.That(row[0].Footprint.max.z, Is.EqualTo(-20f).Within(Epsilon));
            Assert.That(row[1].Footprint.min.x, Is.EqualTo(row[0].Footprint.max.x + 4f).Within(Epsilon));
            Assert.That(row[1].Footprint.max.z, Is.EqualTo(-20f).Within(Epsilon));
            Assert.That(row[1].Footprint.size.x, Is.EqualTo(1f).Within(Epsilon));
        }

        [Test]
        public void FloorAreaCoversEveryStationWithAMarginInStepsOfFive()
        {
            var stations = FeatureSceneBuilder.PlaygroundStations();
            var area = FeatureSceneBuilder.FloorArea(stations, FeatureSceneBuilder.MinFloorHalfExtent);

            foreach (var value in new[] { area.xMin, area.xMax, area.yMin, area.yMax })
            {
                Assert.That(value % FeatureSceneBuilder.FloorStep, Is.EqualTo(0f).Within(Epsilon));
            }

            foreach (var station in stations)
            {
                var footprint = station.Footprint;
                Assert.That(footprint.min.x - FeatureSceneBuilder.FloorMargin, Is.GreaterThanOrEqualTo(area.xMin), station.Fixture.Name);
                Assert.That(footprint.max.x + FeatureSceneBuilder.FloorMargin, Is.LessThanOrEqualTo(area.xMax), station.Fixture.Name);
                Assert.That(footprint.min.z - FeatureSceneBuilder.FloorMargin, Is.GreaterThanOrEqualTo(area.yMin), station.Fixture.Name);
                Assert.That(footprint.max.z + FeatureSceneBuilder.FloorMargin, Is.LessThanOrEqualTo(area.yMax), station.Fixture.Name);
            }
        }

        [Test]
        public void FloorAreaWithoutStationsIsTheMinimumSquare()
        {
            var area = FeatureSceneBuilder.FloorArea(Array.Empty<FeatureStation>(), 15f);

            Assert.That(area, Is.EqualTo(Rect.MinMaxRect(-15f, -15f, 15f, 15f)));
        }

        [Test]
        public void PlaygroundIsWalledInAlongTheFloorEdges()
        {
            var feature = FeatureSceneBuilder.Playground();
            FeatureSceneBuilder.PopulateScene(_scene, NewWeapons(), NewRules(feature.Rules), feature);

            var area = FeatureSceneBuilder.FloorArea(feature.Stations, FeatureSceneBuilder.MinFloorHalfExtent);
            var floor = Find(FeatureSceneBuilder.FloorName);
            Assert.That(floor.transform.position, Is.EqualTo(new Vector3(area.center.x, 0f, area.center.y)));
            Assert.That(floor.transform.localScale, Is.EqualTo(new Vector3(area.width / 10f, 1f, area.height / 10f)));

            var north = Find(SceneBuildHelpers.PerimeterName).transform.Find("Perimeter_North");
            var west = Find(SceneBuildHelpers.PerimeterName).transform.Find("Perimeter_West");
            Assert.That(north.position.z - (north.localScale.z / 2f), Is.EqualTo(area.yMax).Within(Epsilon));
            Assert.That(west.position.x + (west.localScale.x / 2f), Is.EqualTo(area.xMin).Within(Epsilon));
            Assert.That(north.localScale.y, Is.EqualTo(FeatureSceneBuilder.PerimeterHeight(feature.Stations, 0f)).Within(Epsilon));
            Assert.That(north.localScale.y, Is.GreaterThanOrEqualTo(SceneBuildHelpers.DefaultPerimeterHeight + TraversalFeatureSceneBuilder.StepRise * TraversalFeatureSceneBuilder.StepCount));
        }

        [Test]
        public void PlaygroundHasTheBootstrapTheFloorTheWallsAndEveryStation()
        {
            var feature = FeatureSceneBuilder.Playground();
            var bootstrap = FeatureSceneBuilder.PopulateScene(_scene, NewWeapons(), NewRules(feature.Rules), feature);

            Assert.That(bootstrap.CreatesFloor, Is.False);
            Assert.That(bootstrap.GetComponent<MatchRunner>(), Is.Not.Null);
            Assert.That(_scene.rootCount, Is.EqualTo(3 + feature.Stations.Count));

            // 置く物には自分の場所に根を置くものがあるので、同じ物をずらさずにもう一度置いて比べ、ずらした分だけ動いたかを見る。
            var placed = feature.Stations.Select(s => Find(s.Fixture.Name)).ToArray();
            for (var i = 0; i < feature.Stations.Count; i++)
            {
                var station = feature.Stations[i];
                var own = station.Fixture.Place(_scene, feature.Rules);
                try
                {
                    Assert.That(Vector3.Distance(placed[i].transform.position, own.transform.position + station.Offset), Is.LessThan(Epsilon), station.Fixture.Name);
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(own);
                }
            }
        }

        [Test]
        public void HighDropSpawnsOnTopOfATowerTwentyMetresAboveTheFloor()
        {
            var feature = FeatureSceneBuilder.HighDrop();
            FeatureSceneBuilder.PopulateScene(_scene, NewWeapons(), NewRules(feature.Rules), feature);

            Assert.That(feature.FloorY, Is.EqualTo(-20f));
            var floor = Find(FeatureSceneBuilder.FloorName);
            Assert.That(floor.transform.position, Is.EqualTo(new Vector3(0f, -20f, 0f)));
            Assert.That(floor.transform.localScale, Is.EqualTo(new Vector3(6f, 1f, 6f)));

            var tower = Find("DropTower").transform.Find("SpawnTower");
            var platform = tower.Find(LadderTowerBuilder.PlatformName);
            Assert.That(platform.position, Is.EqualTo(new Vector3(0f, -10f, 4.5f)));
            Assert.That(platform.localScale, Is.EqualTo(new Vector3(12f, 20f, 17f)));

            // 出現位置とダミーの列が台の上面 (y = 0) に載る。
            var top = new Bounds(new Vector3(0f, 0f, 4.5f), new Vector3(12f, 0.01f, 17f));
            Assert.That(top.Contains(new Vector3(0f, 0f, 0f)), Is.True);
            foreach (var dummy in GameBootstrap.TargetDummyPositions(Vector3.zero, Quaternion.identity))
            {
                Assert.That(top.Contains(dummy), Is.True, dummy.ToString());
            }

            var ladder = tower.GetComponentInChildren<LadderComponent>();
            Assert.That(ladder.Height, Is.EqualTo(20f));
            Assert.That(ladder.transform.position, Is.EqualTo(new Vector3(6f, -20f, 4.5f)));
        }

        [Test]
        public void HighDropLedgesGiveASafeAHurtingAndALethalDrop()
        {
            var feature = FeatureSceneBuilder.HighDrop();
            FeatureSceneBuilder.PopulateScene(_scene, NewWeapons(), NewRules(feature.Rules), feature);
            var root = Find("DropTower").transform;

            var safe = root.Find("Ledge_Safe");
            var hurt = root.Find("Ledge_Hurt");
            Assert.That(safe.position, Is.EqualTo(new Vector3(-8f, -11.5f, -1f)));
            Assert.That(safe.localScale, Is.EqualTo(new Vector3(4f, 17f, 6f)));
            Assert.That(hurt.position, Is.EqualTo(new Vector3(-8f, -14f, 10f)));
            Assert.That(hurt.localScale, Is.EqualTo(new Vector3(4f, 12f, 6f)));

            // 重力 20 で落ちて着く速さ (v = √(2gh)) を、安全・致死の速さと比べる。
            Assert.That(Mathf.Sqrt(2f * MotorSpec.DefaultGravity * 3f), Is.LessThan(MotorSpec.DefaultSafeFallSpeed));
            Assert.That(Mathf.Sqrt(2f * MotorSpec.DefaultGravity * 8f), Is.InRange(MotorSpec.DefaultSafeFallSpeed, MotorSpec.DefaultLethalFallSpeed));
            Assert.That(Mathf.Sqrt(2f * MotorSpec.DefaultGravity * 20f), Is.GreaterThan(MotorSpec.DefaultLethalFallSpeed));
            Assert.That(20f, Is.GreaterThan(ParachuteMotion.DefaultMinOpenHeight * 2f));
        }

        [Test]
        public void HighDropWallsRiseAboveTheTowerTop()
        {
            var feature = FeatureSceneBuilder.HighDrop();
            FeatureSceneBuilder.PopulateScene(_scene, NewWeapons(), NewRules(feature.Rules), feature);

            var north = Find(SceneBuildHelpers.PerimeterName).transform.Find("Perimeter_North");
            Assert.That(north.position, Is.EqualTo(new Vector3(0f, -6f, 30.5f)));
            Assert.That(north.localScale, Is.EqualTo(new Vector3(62f, 28f, 1f)));
        }

        [Test]
        public void HutIsCenteredOnItsYardAndBreaksWithTheRules()
        {
            var feature = FeatureSceneBuilder.Playground();
            FeatureSceneBuilder.PopulateScene(_scene, NewWeapons(), NewRules(feature.Rules), feature);

            var yard = Find("HutYard");
            var hut = yard.GetComponentInChildren<DestructibleStructure>();
            Assert.That(hut, Is.Not.Null);
            Assert.That(hut.transform.position, Is.EqualTo(TpsRangeSceneBuilder.HutCenter));
            Assert.That(hut.DebrisPool, Is.SameAs(yard.GetComponentInChildren<DebrisPool>()));
        }

        [Test]
        public void PlaygroundArmsTheStealthEnemiesWithTheBootstrapWeapon()
        {
            var feature = FeatureSceneBuilder.Playground();
            FeatureSceneBuilder.PopulateScene(_scene, NewWeapons(), NewRules(feature.Rules), feature);

            Assert.That(Find(StealthRangeFixture.AreaName).GetComponentsInChildren<EnemyAgent>(true), Is.Not.Empty);
        }

        [Test]
        public void MovedZiplineKeepsItsShape()
        {
            var station = new FeatureStation(new ZiplineRangeFixture(), new Vector3(5f, 0f, -3f));
            var feature = new FeatureScene("Test", GameRulesAssetBuilder.PlaygroundRules(), new[] { station }, 0f, 15f, false);
            FeatureSceneBuilder.PopulateScene(_scene, NewWeapons(), NewRules(feature.Rules), feature);

            var line = Find(ZiplineRangeFixture.FixtureName).GetComponent<ZiplineComponent>();

            Assert.That(Vector3.Distance(line.StartPoint, ZiplineRangeFixture.StartAnchor + station.Offset), Is.LessThan(Epsilon));
            Assert.That(Vector3.Distance(line.EndPoint, ZiplineRangeFixture.EndAnchor + station.Offset), Is.LessThan(Epsilon));
        }

        [Test]
        public void RejectsADuplicateStationNameAndLeavesTheSceneEmpty()
        {
            var stations = new[]
            {
                FeatureStation.InPlace(new ProneCrawlwayFixture()),
                new FeatureStation(new ProneCrawlwayFixture(), new Vector3(10f, 0f, 0f)),
            };
            var feature = new FeatureScene("Test", GameRulesAssetBuilder.PlaygroundRules(), stations, 0f, 15f, false);

            Assert.Throws<ArgumentException>(() => FeatureSceneBuilder.PopulateScene(_scene, NewWeapons(), NewRules(feature.Rules), feature));
            Assert.That(_scene.rootCount, Is.EqualTo(0));
        }

        [Test]
        public void StationRejectsANullFixtureAndTheDropTowerALowHeight()
        {
            Assert.Throws<ArgumentNullException>(() => FeatureStation.InPlace(null));
            Assert.Throws<ArgumentException>(() => new FeatureScene(" ", GameRulesAssetBuilder.PlaygroundRules(), Array.Empty<FeatureStation>(), 0f, 15f, false));
        }

        [Test]
        public void LegacyAssetsAreEveryOldPerFeatureSceneAndRule()
        {
            var paths = FeatureSceneBuilder.LegacyAssetPaths();

            Assert.That(paths, Has.Member(DemoSample.Root + "/Scenes/Feature_Zipline.unity"));
            Assert.That(paths, Has.Member(DemoSample.Root + "/Scenes/Feature_Downed.unity"));
            Assert.That(paths, Has.Member(DemoSample.Root + "/Scenes/Feature_WallRun.unity"));
            Assert.That(paths, Has.Member(DemoSample.Root + "/Rules/Features/Zipline.asset"));
            Assert.That(paths, Has.Member(DemoSample.Root + "/Rules/Features/Swim.asset"));
            Assert.That(paths, Has.Member(DemoSample.Root + "/Movement/Feature_DoubleJump.asset"));
            Assert.That(paths, Has.Member(DemoSample.Root + "/Tuning/Feature_WallRun.json"));
            Assert.That(paths.Count(p => p.EndsWith(".unity", StringComparison.Ordinal)), Is.EqualTo(25 + 4), "one per on/off rule and per move");
            Assert.That(paths, Has.No.Member(BuildSceneList.PlaygroundScene));
            Assert.That(paths, Has.No.Member(GameRulesAssetBuilder.FeatureRulesPath("Playground")));
        }

        [Test]
        public void TheFeatureFixturesAreNotPickedUpByTheTpsRange()
        {
            var names = TpsRangeSceneBuilder.Fixtures().Select(f => f.GetType().Name).ToArray();

            Assert.That(names, Has.No.Member("FeatureDropTowerFixture"));
            Assert.That(names, Has.No.Member("FeatureHutFixture"));
            Assert.That(names, Has.No.Member("FeatureCoversFixture"));
            Assert.That(names, Has.No.Member("FeatureVaultWallFixture"));
            Assert.That(names, Has.No.Member("TraversalFeatureFixture"));
        }

        // Bounds.Intersects は辺が触れているだけでも重なりと数えるが、ダミーの 1 m 四方に辺が触れているだけの置き場はダミーをふさがないので、内側に入り込んだときだけ重なりとする。
        private static bool OverlapsOnTheFloor(Bounds a, Bounds b)
        {
            return a.min.x < b.max.x && b.min.x < a.max.x && a.min.z < b.max.z && b.min.z < a.max.z;
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
