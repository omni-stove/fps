using System;
using System.Collections.Generic;
using Fps.Core;
using Fps.Editor;
using NUnit.Framework;
using UnityEngine;

namespace Fps.Tests.EditMode
{
    /// <summary>
    /// <see cref="FreeForAllSceneBuilder"/> の配置を確かめる。シーンを組んで NavMesh を焼く処理は開いているシーンを置き換えるので、
    /// <c>-executeMethod Fps.Editor.FreeForAllSceneBuilder.Build</c> で確かめる。
    /// </summary>
    public sealed class FreeForAllSceneBuilderTests
    {
        [Test]
        public void TheSceneAndRulesAreTheRegisteredFreeForAllOnes()
        {
            var builder = new FreeForAllSceneBuilder();

            Assert.That(builder.ScenePath, Is.EqualTo(BuildSceneList.ModeScene(GameRulesAssetBuilder.FreeForAllName)));
            Assert.That(builder.RulesPath, Is.EqualTo(GameRulesAssetBuilder.FreeForAllPath));
        }

        [Test]
        public void EveryBotHasASideOfItsOwn()
        {
            var sides = FreeForAllSceneBuilder.Layout(FreeForAllSceneBuilder.BotCount);

            Assert.That(sides, Has.Count.EqualTo(FreeForAllSceneBuilder.BotCount + 1));
            Assert.That(sides[0].Side, Is.EqualTo(new Side(0)));
            Assert.That(sides[0].BotCount, Is.Zero, "side 0 is the player's alone");

            var ids = new HashSet<Side>();
            foreach (var side in sides)
            {
                Assert.That(ids.Add(side.Side), Is.True, $"{side.Side} is laid out once");
                if (side.Side != new Side(0))
                {
                    Assert.That(side.BotCount, Is.EqualTo(1), $"{side.Side} has one bot");
                }
            }

            Assert.That(ModeSceneBuilder.ValidateLayout(sides, new Side(0)), Is.SameAs(sides));
        }

        [Test]
        public void EverySideStartsAtADifferentSpawnFacingTheCenter()
        {
            var sides = FreeForAllSceneBuilder.Layout(FreeForAllSceneBuilder.BotCount);
            var starts = new List<Vector3>();
            foreach (var side in sides)
            {
                var first = side.Spawns[0];
                foreach (var other in starts)
                {
                    Assert.That((first.Feet - other).magnitude, Is.GreaterThan(10f), $"{side.Side} starts away from the others");
                }

                starts.Add(first.Feet);
                var toCenter = FreeForAllSceneBuilder.ArenaCenter - first.Feet;
                toCenter.y = 0f;
                Assert.That(Vector3.Dot(first.Facing, toCenter.normalized), Is.GreaterThan(0.999f), $"{side.Side} faces the center");
            }

            Assert.That(sides[0].Spawns[0].Feet, Is.EqualTo(ModeSceneBuilder.PlayerFeet));
            Assert.That(sides[0].Spawns[0].Facing, Is.EqualTo(Vector3.forward));
        }

        [Test]
        public void EverySideCanRespawnAtEverySpawnOfThePool()
        {
            foreach (var side in FreeForAllSceneBuilder.Layout(FreeForAllSceneBuilder.BotCount))
            {
                Assert.That(side.Spawns, Has.Count.EqualTo(FreeForAllSceneBuilder.SpawnPool.Count));
                foreach (var point in FreeForAllSceneBuilder.SpawnPool)
                {
                    var found = false;
                    foreach (var spawn in side.Spawns)
                    {
                        found |= spawn.Feet == point;
                    }

                    Assert.That(found, Is.True, $"{side.Side} can respawn at {point}");
                }
            }
        }

        [Test]
        public void ThePoolIsOnTheFloorClearOfObstaclesAndMirroredThroughTheCenter()
        {
            var halfX = ModeSceneBuilder.DefaultFloorScale.x * 5f;
            var halfZ = ModeSceneBuilder.DefaultFloorScale.z * 5f;
            var inner = ModeSceneBuilder.BoundaryWallThickness + EnemyTestSceneBuilder.EnemyRadius;
            foreach (var point in FreeForAllSceneBuilder.SpawnPool)
            {
                var local = point - ModeSceneBuilder.DefaultFloorCenter;
                Assert.That(Mathf.Abs(local.x), Is.LessThan(halfX - inner), $"{point} is inside the boundary walls");
                Assert.That(Mathf.Abs(local.z), Is.LessThan(halfZ - inner), $"{point} is inside the boundary walls");

                foreach (var obstacle in ModeSceneBuilder.DefaultObstacles)
                {
                    var bounds = new Bounds(obstacle.Center, obstacle.Size);
                    bounds.Expand(EnemyTestSceneBuilder.EnemyRadius * 4f);
                    Assert.That(bounds.Contains(point), Is.False, $"{point} is clear of {obstacle.Name}");
                }

                // bot の巡回の折り返し点も出現点なので、NavMesh の上にあることを出現点と一緒に確かめられる。
                Assert.That(FreeForAllSceneBuilder.SpawnPool, Does.Contain(FreeForAllSceneBuilder.Opposite(point)), $"the opposite of {point} is a spawn");
            }
        }

        [Test]
        public void LayoutRejectsMoreBotsThanSpawns()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => FreeForAllSceneBuilder.Layout(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => FreeForAllSceneBuilder.Layout(FreeForAllSceneBuilder.SpawnPool.Count));
            Assert.That(FreeForAllSceneBuilder.Layout(0), Has.Count.EqualTo(1));
        }
    }
}
