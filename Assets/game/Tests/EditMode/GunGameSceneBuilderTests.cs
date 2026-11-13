using System;
using System.Collections.Generic;
using Fps.Core;
using Fps.Editor;
using NUnit.Framework;
using UnityEngine;

namespace Fps.Tests.EditMode
{
    /// <summary>
    /// <see cref="GunGameSceneBuilder"/> の配置と配線先を確かめる。シーンを組んで NavMesh を焼く処理は、開いているシーンを置き換える
    /// (NewScene Single) ので <c>-executeMethod Fps.Editor.GunGameSceneBuilder.Build</c> で確かめる。
    /// </summary>
    public sealed class GunGameSceneBuilderTests
    {
        [Test]
        public void TheSceneUsesTheGunGameSceneAndRules()
        {
            var builder = new GunGameSceneBuilder();

            Assert.That(builder.ScenePath, Is.EqualTo(BuildSceneList.GunGameScene));
            Assert.That(builder.ScenePath, Is.EqualTo(BuildSceneList.ModeScene(GameRulesAssetBuilder.GunGameName)));
            Assert.That(builder.RulesPath, Is.EqualTo(GameRulesAssetBuilder.GunGamePath));
            Assert.That(builder.NavMeshDataPath, Is.EqualTo("Assets/game/Scenes/GunGame/NavMesh-Floor.asset"));
            Assert.That(builder.LogTag, Is.EqualTo("[GunGameSceneBuilder]"));
            Assert.That(SceneBuildHelpers.ValidateAssetPath(GunGameSceneBuilder.LadderPath), Is.EqualTo(GunGameSceneBuilder.LadderPath));
        }

        [Test]
        public void TheLadderStartsWithTheBotWeaponAndHasSeveralTiers()
        {
            Assert.That(GunGameSceneBuilder.LadderWeaponPaths, Has.Count.GreaterThan(1));
            Assert.That(GunGameSceneBuilder.LadderWeaponPaths[0], Is.EqualTo(WeaponAssetBuilder.RiflePath));
            Assert.That(GunGameSceneBuilder.LadderWeaponPaths, Is.Unique);
        }

        [Test]
        public void EverySideIsAloneAndStartsAtItsOwnSpawnPoint()
        {
            var sides = GunGameSceneBuilder.FreeForAllSides(GunGameSceneBuilder.BotCount);

            Assert.That(sides, Has.Count.EqualTo(GunGameSceneBuilder.BotCount + 1));
            Assert.That(ModeSceneBuilder.ValidateLayout(sides, new Side(0)), Is.SameAs(sides));

            var starts = new HashSet<Vector3>();
            for (var s = 0; s < sides.Count; s++)
            {
                Assert.That(sides[s].Side, Is.EqualTo(new Side(s)));
                Assert.That(sides[s].BotCount, Is.EqualTo(s == 0 ? 0 : 1), "the player's side has no bot and each bot is alone");
                Assert.That(sides[s].Spawns, Has.Count.EqualTo(GunGameSceneBuilder.SpawnPositions.Count), "every side can respawn anywhere");
                Assert.That(starts.Add(sides[s].Spawns[0].Feet), Is.True, $"side {s} starts apart from the others");
            }
        }

        [Test]
        public void TheSpawnsAreOnTheFloorAndClearOfTheObstacles()
        {
            var halfX = ModeSceneBuilder.DefaultFloorScale.x * 5f;
            var halfZ = ModeSceneBuilder.DefaultFloorScale.z * 5f;
            var inner = ModeSceneBuilder.BoundaryWallThickness + EnemyTestSceneBuilder.EnemyRadius;
            foreach (var spawn in GunGameSceneBuilder.SpawnPositions)
            {
                var local = spawn.Feet - ModeSceneBuilder.DefaultFloorCenter;
                Assert.That(Mathf.Abs(local.x), Is.LessThan(halfX - inner), $"{spawn.Feet} is inside the boundary walls");
                Assert.That(Mathf.Abs(local.z), Is.LessThan(halfZ - inner), $"{spawn.Feet} is inside the boundary walls");

                foreach (var obstacle in ModeSceneBuilder.DefaultObstacles)
                {
                    var bounds = new Bounds(obstacle.Center, obstacle.Size);
                    bounds.Expand(EnemyTestSceneBuilder.EnemyRadius * 4f);
                    Assert.That(bounds.Contains(spawn.Feet), Is.False, $"{spawn.Feet} is clear of {obstacle.Name}");
                }
            }
        }

        [Test]
        public void TheBotCountMustLeaveEachSideAStartingPoint()
        {
            Assert.DoesNotThrow(() => GunGameSceneBuilder.FreeForAllSides(0));
            Assert.DoesNotThrow(() => GunGameSceneBuilder.FreeForAllSides(GunGameSceneBuilder.SpawnPositions.Count - 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => GunGameSceneBuilder.FreeForAllSides(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => GunGameSceneBuilder.FreeForAllSides(GunGameSceneBuilder.SpawnPositions.Count));
        }
    }
}
