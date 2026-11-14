using System;
using Fps.Core;
using Fps.Editor;
using Fps.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace Fps.Tests.EditMode
{
    /// <summary>
    /// <see cref="HordeSceneBuilder"/> の拠点・防衛物・出現点の配置と、シーン・ルールのパスを確かめる。シーンを組んで NavMesh を焼く処理は、
    /// 開いているシーンを置き換える (NewScene Single) ので <c>-executeMethod Fps.Editor.HordeSceneBuilder.Build</c> で確かめる。
    /// </summary>
    public sealed class HordeSceneBuilderTests
    {
        [Test]
        public void TheSceneUsesTheHordePathsAndRules()
        {
            var builder = new HordeSceneBuilder();

            Assert.That(builder.ScenePath, Is.EqualTo(BuildSceneList.ModeScene(GameRulesAssetBuilder.HordeName)));
            Assert.That(builder.RulesPath, Is.EqualTo(GameRulesAssetBuilder.ModeRulesPath(GameRulesAssetBuilder.HordeName)));
            Assert.That(builder.NavMeshDataPath, Is.EqualTo(DemoSample.Root + "/Scenes/Horde/NavMesh-Floor.asset"));
            Assert.That(builder.LogTag, Is.EqualTo("[HordeSceneBuilder]"));
            Assert.That(BuildSceneList.Scenes, Has.Member(BuildSceneList.HordeScene));
        }

        [Test]
        public void ThePlayerSideDefendsAndTheEnemySideOnlyHasSpawnPoints()
        {
            var sides = HordeSceneBuilder.Layout();

            Assert.That(ModeSceneBuilder.ValidateLayout(sides, HordeSceneBuilder.DefenderSide), Is.SameAs(sides));
            Assert.That(sides[0].Side, Is.EqualTo(HordeSceneBuilder.DefenderSide));
            Assert.That(sides[0].BotCount, Is.EqualTo(HordeSceneBuilder.AllyBotCount));
            Assert.That(sides[1].Side, Is.EqualTo(HordeSceneBuilder.EnemySide));
            Assert.That(sides[1].BotCount, Is.Zero, "the WaveSpawner brings the enemies");
            Assert.That(HordeSceneBuilder.EnemySide, Is.Not.EqualTo(HordeSceneBuilder.DefenderSide));
        }

        [Test]
        public void EnemiesSpawnOnTheOutskirtsFacingTheBase()
        {
            var nearestEnemy = float.PositiveInfinity;
            foreach (var feet in HordeSceneBuilder.EnemySpawnFeet)
            {
                nearestEnemy = Mathf.Min(nearestEnemy, Flat(feet - HordeSceneBuilder.BaseCenter));
                Assert.That(Mathf.Abs(feet.x), Is.LessThan((HordeSceneBuilder.HordeFloorScale.x * 5f) - 1f), $"{feet} is inside the floor");
                Assert.That(Mathf.Abs(feet.z), Is.LessThan((HordeSceneBuilder.HordeFloorScale.z * 5f) - 1f), $"{feet} is inside the floor");
            }

            foreach (var obstacle in HordeSceneBuilder.HordeObstacles)
            {
                Assert.That(Flat(obstacle.Center - HordeSceneBuilder.BaseCenter), Is.LessThan(nearestEnemy), $"{obstacle.Name} stands between the base and the enemies");
            }

            foreach (var fortification in HordeSceneBuilder.Fortifications)
            {
                Assert.That(Flat(fortification.Center - HordeSceneBuilder.BaseCenter), Is.LessThan(nearestEnemy));
            }

            var enemies = HordeSceneBuilder.Layout()[1].Spawns;
            foreach (var spawn in enemies)
            {
                var toBase = (HordeSceneBuilder.BaseCenter - spawn.Feet).normalized;
                Assert.That(Vector3.Dot(spawn.Facing, toBase), Is.GreaterThan(0.999f));
            }
        }

        [Test]
        public void WallsStartBuiltAndBarricadesAreBoughtLater()
        {
            var walls = 0;
            var slots = 0;
            foreach (var fortification in HordeSceneBuilder.Fortifications)
            {
                if (fortification.StartsBuilt)
                {
                    walls++;
                }
                else
                {
                    slots++;
                }
            }

            Assert.That(walls, Is.EqualTo(4));
            Assert.That(slots, Is.EqualTo(4));
        }

        [Test]
        public void FortificationsAreClearOfSpawnsAndObstacles()
        {
            Assert.DoesNotThrow(() => HordeSceneBuilder.RequireFortificationsClear(HordeSceneBuilder.Fortifications, HordeSceneBuilder.Layout()));

            foreach (var fortification in HordeSceneBuilder.Fortifications)
            {
                foreach (var obstacle in HordeSceneBuilder.HordeObstacles)
                {
                    var reach = (fortification.Length * HordeSceneBuilder.PieceSize.x / 2f) + HordeSceneBuilder.InteractReach;
                    Assert.That(
                        Flat(obstacle.Center - fortification.Center),
                        Is.GreaterThan(reach + (Mathf.Max(obstacle.Size.x, obstacle.Size.z) / 2f)),
                        $"{fortification.Name} is clear of {obstacle.Name}");
                }
            }
        }

        [Test]
        public void AFortificationOnASpawnPointIsRejected()
        {
            var onPlayer = new[] { new HordeFortificationPlacement("OnPlayer", ModeSceneBuilder.PlayerFeet, 0f, 2, true) };
            var twice = new[]
            {
                new HordeFortificationPlacement("Twice", new Vector3(0f, 0f, 7f), 0f, 2, true),
                new HordeFortificationPlacement("Twice", new Vector3(0f, 0f, -7f), 0f, 2, true),
            };

            Assert.Throws<InvalidOperationException>(() => HordeSceneBuilder.RequireFortificationsClear(onPlayer, HordeSceneBuilder.Layout()));
            Assert.Throws<InvalidOperationException>(() => HordeSceneBuilder.RequireFortificationsClear(twice, HordeSceneBuilder.Layout()));
        }

        [Test]
        public void ThePlacementMeasuresDistanceToItsPieces()
        {
            var wall = new HordeFortificationPlacement("Wall", Vector3.zero, 90f, 4, true);

            Assert.That(wall.HorizontalDistanceTo(new Vector3(0f, 0f, 1.5f)), Is.Zero.Within(1e-4f), "a 4m wall turned 90 degrees runs along z");
            Assert.That(wall.HorizontalDistanceTo(new Vector3(0f, 0f, 3f)), Is.EqualTo(1f).Within(1e-4f));
            Assert.That(wall.HorizontalDistanceTo(new Vector3(1.25f, 0f, 0f)), Is.EqualTo(1f).Within(1e-4f));

            Assert.Throws<ArgumentException>(() => new HordeFortificationPlacement(" ", Vector3.zero, 0f, 1, true));
            Assert.Throws<ArgumentOutOfRangeException>(() => new HordeFortificationPlacement("Empty", Vector3.zero, 0f, 0, true));
        }

        [Test]
        public void TheWavesEndAfterAFixedNumber()
        {
            Assert.That(HordeSceneBuilder.WaveCount, Is.GreaterThan(0), "a horde with no end could never be won");
            Assert.That(HordeSceneBuilder.RestSeconds, Is.GreaterThan(0f), "the rest is when walls get repaired");
            Assert.That(new HordeSpec(HordeSceneBuilder.DefenderSide, HordeSceneBuilder.EnemySide).StartingCurrency, Is.GreaterThan(0));
        }

        [Test]
        public void TheDefinitionCarriesTheSceneWaves()
        {
            Assert.That(HordeSceneBuilder.DefinitionPath, Is.EqualTo(DemoSample.Root + "/Modes/Horde.asset"));

            var definition = ScriptableObject.CreateInstance<HordeDefinition>();
            try
            {
                HordeSceneBuilder.Configure(definition);

                Assert.That(definition.WaveCount, Is.EqualTo(HordeSceneBuilder.WaveCount));
                Assert.That(definition.RestSeconds, Is.EqualTo(HordeSceneBuilder.RestSeconds));
                Assert.That(definition.FirstEnemyCount, Is.EqualTo(HordeSceneBuilder.FirstEnemyCount));
                Assert.That(definition.MaxAlive, Is.EqualTo(HordeSceneBuilder.MaxAlive));
                Assert.That(definition.StartingCurrency, Is.EqualTo(HordeSpec.DefaultStartingCurrency));
                Assert.That(definition.PurchasesOnlyBetweenWaves, Is.True);
                Assert.That(definition.CreateSchedule().WaveCount, Is.EqualTo(HordeSceneBuilder.WaveCount));
                Assert.Throws<ArgumentNullException>(() => HordeSceneBuilder.Configure(null));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(definition);
            }
        }

        private static float Flat(Vector3 v) => new Vector2(v.x, v.z).magnitude;
    }
}
