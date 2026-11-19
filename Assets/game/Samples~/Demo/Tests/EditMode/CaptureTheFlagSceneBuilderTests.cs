using System;
using Shooter.Core;
using Shooter.Editor;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Shooter.Tests.EditMode
{
    // シーンを組んで NavMesh を焼く処理は開いているシーンを置き換える (NewScene Single) ので、
    // -executeMethod Shooter.Editor.CaptureTheFlagSceneBuilder.Build で確かめる。
    public sealed class CaptureTheFlagSceneBuilderTests
    {
        private string _folder;

        [SetUp]
        public void SetUp()
        {
            _folder = "Assets/__CaptureTheFlagSceneBuilderTests_" + Guid.NewGuid().ToString("N");
        }

        [TearDown]
        public void TearDown()
        {
            if (AssetDatabase.IsValidFolder(_folder))
            {
                AssetDatabase.DeleteAsset(_folder);
            }
        }

        [Test]
        public void TheSceneUsesTheCaptureTheFlagRules()
        {
            var builder = new CaptureTheFlagSceneBuilder();

            Assert.That(builder.ScenePath, Is.EqualTo(BuildSceneList.CaptureTheFlagScene));
            Assert.That(builder.ScenePath, Is.EqualTo(DemoSample.Root + "/Scenes/CaptureTheFlag.unity"));
            Assert.That(builder.RulesPath, Is.EqualTo(GameRulesAssetBuilder.CaptureTheFlagPath));
            Assert.That(builder.NavMeshDataPath, Is.EqualTo(DemoSample.Root + "/Scenes/CaptureTheFlag/NavMesh-Floor.asset"));
            Assert.That(BuildSceneList.Scenes, Has.Member(BuildSceneList.CaptureTheFlagScene));
            Assert.That(CaptureTheFlagSceneBuilder.DefinitionPath, Is.EqualTo(DemoSample.Root + "/Modes/CaptureTheFlag.asset"));
            Assert.That(CaptureTheFlagSceneBuilder.MenuPath, Is.EqualTo("Shooter/Build All Scenes"));
        }

        [Test]
        public void ThereIsOneBaseForEachFlagSide()
        {
            Assert.That(CaptureTheFlagSceneBuilder.BaseCenters.Count, Is.EqualTo(CaptureTheFlag.FlagSides.Count));
            Assert.That(CaptureTheFlagSceneBuilder.BaseCenters[0], Is.EqualTo(new Vector3(0f, 0f, -5f)));
            Assert.That(CaptureTheFlagSceneBuilder.BaseCenters[1], Is.EqualTo(new Vector3(0f, 0f, 45f)));
        }

        [Test]
        public void TheBasesAreOnTheFloorAndClearOfTheObstacles()
        {
            var halfX = ModeSceneBuilder.DefaultFloorScale.x * 5f;
            var halfZ = ModeSceneBuilder.DefaultFloorScale.z * 5f;
            var radius = CaptureTheFlagSpec.DefaultCaptureRadius;
            var inner = ModeSceneBuilder.BoundaryWallThickness + radius;

            foreach (var center in CaptureTheFlagSceneBuilder.BaseCenters)
            {
                var local = center - ModeSceneBuilder.DefaultFloorCenter;
                Assert.That(Mathf.Abs(local.x), Is.LessThan(halfX - inner), $"{center} is inside the boundary walls");
                Assert.That(Mathf.Abs(local.z), Is.LessThan(halfZ - inner), $"{center} is inside the boundary walls");

                foreach (var obstacle in ModeSceneBuilder.DefaultObstacles)
                {
                    var bounds = new Bounds(obstacle.Center, obstacle.Size);
                    Assert.That(bounds.SqrDistance(center), Is.GreaterThan(radius * radius), $"{center} is clear of {obstacle.Name}");
                }
            }
        }

        [Test]
        public void EachBaseSitsBehindItsOwnSpawnsAndIsTheSameDistanceFromBothSides()
        {
            var sides = ModeSceneBuilder.TwoSides(0, 0);
            var alpha = CaptureTheFlagSceneBuilder.BaseCenters[0];
            var bravo = CaptureTheFlagSceneBuilder.BaseCenters[1];

            Assert.That(sides[0].Side, Is.EqualTo(Side.Alpha));
            Assert.That(sides[1].Side, Is.EqualTo(Side.Bravo));
            foreach (var spawn in sides[0].Spawns)
            {
                Assert.That(alpha.z, Is.LessThan(spawn.Feet.z), "the Alpha base is behind the Alpha spawns");
                Assert.That(Vector3.Distance(spawn.Feet, bravo), Is.GreaterThan(Vector3.Distance(spawn.Feet, alpha)));
            }

            foreach (var spawn in sides[1].Spawns)
            {
                Assert.That(bravo.z, Is.GreaterThan(spawn.Feet.z), "the Bravo base is behind the Bravo spawns");
                Assert.That(Vector3.Distance(spawn.Feet, alpha), Is.GreaterThan(Vector3.Distance(spawn.Feet, bravo)));
            }

            Assert.That(
                Vector3.Distance(ModeSceneBuilder.PlayerFeet, bravo),
                Is.EqualTo(Vector3.Distance(new Vector3(0f, 0f, ModeSceneBuilder.DefaultFarSpawnZ), alpha)).Within(1e-4f),
                "neither side has a shorter run to the other flag");
        }

        [Test]
        public void ThePlayerDoesNotSpawnOnAFlag()
        {
            foreach (var center in CaptureTheFlagSceneBuilder.BaseCenters)
            {
                Assert.That(
                    CaptureTheFlagRunner.IsWithin(ModeSceneBuilder.PlayerFeet, center, CaptureTheFlagSpec.DefaultCaptureRadius),
                    Is.False,
                    $"the player spawn is outside the base at {center}");
            }
        }

        [Test]
        public void ObjectsAreNamedAfterTheirSide()
        {
            Assert.That(CaptureTheFlagSceneBuilder.BaseObjectName(Side.Alpha), Is.EqualTo("FlagBase-0"));
            Assert.That(CaptureTheFlagSceneBuilder.BaseObjectName(Side.Bravo), Is.EqualTo("FlagBase-1"));
            Assert.That(CaptureTheFlagSceneBuilder.FlagObjectName(Side.Alpha), Is.EqualTo("Flag-0"));
            Assert.That(CaptureTheFlagSceneBuilder.FlagObjectName(Side.Bravo), Is.EqualTo("Flag-1"));
        }

        [Test]
        public void LoadOrCreateDefinitionCreatesTheDefaultsAndKeepsAnEditedAsset()
        {
            var path = _folder + "/CaptureTheFlag.asset";

            var created = CaptureTheFlagSceneBuilder.LoadOrCreateDefinition(path);

            Assert.That(AssetDatabase.LoadAssetAtPath<CaptureTheFlagDefinition>(path), Is.SameAs(created));
            Assert.That(created.ScoreLimit, Is.EqualTo(CaptureTheFlagSpec.DefaultScoreLimit));
            Assert.That(created.CaptureRadius, Is.EqualTo(CaptureTheFlagSpec.DefaultCaptureRadius));
            Assert.That(created.BotAttackShare, Is.EqualTo(CaptureTheFlagBotPlanner.DefaultAttackShare));

            var guid = AssetDatabase.AssetPathToGUID(path);
            created.ScoreLimit = 5;
            EditorUtility.SetDirty(created);
            AssetDatabase.SaveAssets();

            var again = CaptureTheFlagSceneBuilder.LoadOrCreateDefinition(path);

            Assert.That(again, Is.SameAs(created));
            Assert.That(AssetDatabase.AssetPathToGUID(path), Is.EqualTo(guid));
            Assert.That(again.ScoreLimit, Is.EqualTo(5));
        }

        [Test]
        public void LoadOrCreateDefinitionRefusesAnInvalidAsset()
        {
            var path = _folder + "/CaptureTheFlag.asset";
            var created = CaptureTheFlagSceneBuilder.LoadOrCreateDefinition(path);
            created.ScoreLimit = 0;
            EditorUtility.SetDirty(created);
            AssetDatabase.SaveAssets();

            Assert.Throws<InvalidOperationException>(() => CaptureTheFlagSceneBuilder.LoadOrCreateDefinition(path));
        }

        [Test]
        public void LoadOrCreateDefinitionRefusesAPathOutsideAssets()
        {
            Assert.Throws<ArgumentException>(() => CaptureTheFlagSceneBuilder.LoadOrCreateDefinition("Packages/CaptureTheFlag.asset"));
        }
    }
}
