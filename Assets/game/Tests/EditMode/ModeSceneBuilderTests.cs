using System;
using System.Collections.Generic;
using Fps.Core;
using Fps.Editor;
using NUnit.Framework;
using UnityEngine;

namespace Fps.Tests.EditMode
{
    /// <summary>
    /// <see cref="ModeSceneBuilder"/> の配置の検査と既定の配置を確かめる。シーンを組んで NavMesh を焼く処理は、
    /// 開いているシーンを置き換える (NewScene Single) ので <c>-executeMethod Fps.Editor.ModeTestSceneBuilder.Build</c> で確かめる。
    /// </summary>
    public sealed class ModeSceneBuilderTests
    {
        private static readonly Side Player = new Side(0);

        [Test]
        public void TheDefaultTwoSidesFaceEachOtherWithThePlayerAtTheOrigin()
        {
            var sides = ModeSceneBuilder.TwoSides(2, 3);

            Assert.That(sides, Has.Count.EqualTo(2));
            Assert.That(sides[0].Side, Is.EqualTo(new Side(0)));
            Assert.That(sides[1].Side, Is.EqualTo(new Side(1)));
            Assert.That(sides[0].BotCount, Is.EqualTo(2));
            Assert.That(sides[1].BotCount, Is.EqualTo(3));
            Assert.That(sides[0].Spawns[0].Feet, Is.EqualTo(ModeSceneBuilder.PlayerFeet));
            Assert.That(sides[0].Spawns[0].Facing, Is.EqualTo(Vector3.forward));
            Assert.That(sides[1].Spawns[0].Facing, Is.EqualTo(Vector3.back));
            foreach (var spawn in sides[1].Spawns)
            {
                Assert.That(spawn.Feet.z, Is.EqualTo(ModeSceneBuilder.DefaultFarSpawnZ));
            }

            Assert.That(ModeSceneBuilder.ValidateLayout(sides, Player), Is.SameAs(sides));
        }

        [Test]
        public void ThePlayerStartsWhereGameBootstrapPutsIt()
        {
            Assert.That(ModeSceneBuilder.PlayerFeet, Is.EqualTo(Vector3.zero));
        }

        [Test]
        public void TheDefaultSpawnsAreOnTheFloorAndClearOfTheObstacles()
        {
            var halfX = ModeSceneBuilder.DefaultFloorScale.x * 5f;
            var halfZ = ModeSceneBuilder.DefaultFloorScale.z * 5f;
            var inner = ModeSceneBuilder.BoundaryWallThickness + EnemyTestSceneBuilder.EnemyRadius;
            foreach (var side in ModeSceneBuilder.TwoSides(0, 0))
            {
                foreach (var spawn in side.Spawns)
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
        }

        [Test]
        public void ValidateLayoutRejectsAMissingOrMisplacedPlayerSide()
        {
            Assert.Throws<InvalidOperationException>(() => ModeSceneBuilder.ValidateLayout(null, Player));
            Assert.Throws<InvalidOperationException>(() => ModeSceneBuilder.ValidateLayout(Array.Empty<ModeSideLayout>(), Player));
            Assert.Throws<InvalidOperationException>(
                () => ModeSceneBuilder.ValidateLayout(ModeSceneBuilder.TwoSides(0, 0), new Side(2)), "the player's side is not laid out");

            var misplaced = new[]
            {
                new ModeSideLayout(Player, new[] { new ModeSpawnPlacement(new Vector3(5f, 0f, 0f), Vector3.forward) }, 0),
            };
            Assert.Throws<InvalidOperationException>(() => ModeSceneBuilder.ValidateLayout(misplaced, Player), "the player starts at the origin");

            var turned = new[]
            {
                new ModeSideLayout(Player, new[] { new ModeSpawnPlacement(Vector3.zero, Vector3.right) }, 0),
            };
            Assert.Throws<InvalidOperationException>(() => ModeSceneBuilder.ValidateLayout(turned, Player), "the player starts facing +Z");
        }

        [Test]
        public void ValidateLayoutRejectsDuplicateSidesAndTooManyBots()
        {
            var twice = new List<ModeSideLayout>(ModeSceneBuilder.TwoSides(0, 0));
            twice.Add(twice[1]);
            Assert.Throws<InvalidOperationException>(() => ModeSceneBuilder.ValidateLayout(twice, Player));

            var spawns = ModeSceneBuilder.DefaultSpawnColumns.Count;

            // プレイヤーの陣営は最初の点をプレイヤーが使うので、bot は 1 つ少なくしか立てない。
            Assert.DoesNotThrow(() => ModeSceneBuilder.ValidateLayout(ModeSceneBuilder.TwoSides(spawns - 1, spawns), Player));
            Assert.Throws<InvalidOperationException>(() => ModeSceneBuilder.ValidateLayout(ModeSceneBuilder.TwoSides(spawns, 0), Player));
            Assert.Throws<InvalidOperationException>(() => ModeSceneBuilder.ValidateLayout(ModeSceneBuilder.TwoSides(0, spawns + 1), Player));

            var empty = new[]
            {
                ModeSceneBuilder.TwoSides(0, 0)[0],
                new ModeSideLayout(new Side(1), Array.Empty<ModeSpawnPlacement>(), 0),
            };
            Assert.Throws<InvalidOperationException>(() => ModeSceneBuilder.ValidateLayout(empty, Player));
        }

        [Test]
        public void PlacementsRejectInvalidValues()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new ModeSpawnPlacement(Vector3.zero, Vector3.up));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ModeSideLayout(Player, Array.Empty<ModeSpawnPlacement>(), -1));
            Assert.Throws<ArgumentNullException>(() => new ModeSideLayout(Player, null, 0));
            Assert.Throws<ArgumentException>(() => new ModeObstacle(" ", Vector3.zero, Vector3.one));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ModeObstacle("Flat", Vector3.zero, new Vector3(1f, 0f, 1f)));

            var tilted = new ModeSpawnPlacement(Vector3.zero, new Vector3(0f, 1f, 2f));
            Assert.That(tilted.Facing, Is.EqualTo(Vector3.forward), "the facing is flattened and normalized");
        }

        [Test]
        public void TheNavMeshGoesInAFolderNamedAfterTheScene()
        {
            Assert.That(
                ModeSceneBuilder.NavMeshDataPathFor("Assets/game/Scenes/FreeForAll.unity"),
                Is.EqualTo("Assets/game/Scenes/FreeForAll/NavMesh-Floor.asset"));
            Assert.Throws<ArgumentException>(() => ModeSceneBuilder.NavMeshDataPathFor("Scenes/FreeForAll.unity"));
            Assert.Throws<ArgumentException>(() => ModeSceneBuilder.NavMeshDataPathFor("Assets/game/Scenes/FreeForAll.asset"));
        }

        [Test]
        public void BotNamesSortBySideThenNumber()
        {
            Assert.That(ModeSceneBuilder.BotName(new Side(1), 2), Is.EqualTo("Bot-1-2"));
            Assert.That(ModeSceneBuilder.BotId(new Side(1), 2), Is.EqualTo("bot-1-2"));
        }

        [Test]
        public void TheModeTestSceneUsesTheTeamDeathmatchRules()
        {
            var builder = new ModeTestSceneBuilder();

            Assert.That(builder.ScenePath, Is.EqualTo(BuildSceneList.ModeTestScene));
            Assert.That(builder.RulesPath, Is.EqualTo(GameRulesAssetBuilder.TeamDeathmatchPath));
            Assert.That(builder.NavMeshDataPath, Is.EqualTo("Assets/game/Scenes/ModeTest/NavMesh-Floor.asset"));
            Assert.That(builder.LogTag, Is.EqualTo("[ModeTestSceneBuilder]"));
            Assert.That(BuildSceneList.Scenes, Has.Member(BuildSceneList.ModeTestScene));
            Assert.That(ModeTestSceneBuilder.DefinitionPath, Is.EqualTo("Assets/game/Modes/TeamDeathmatch.asset"));
        }
    }
}
