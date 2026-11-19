using System;
using System.Linq;
using Shooter.Core;
using Shooter.Editor;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace Shooter.Tests.EditMode
{
    // シーンを組んで NavMesh を焼く処理は開いているシーンを置き換える (NewScene Single) ので、
    // -executeMethod Shooter.Editor.PayloadSceneBuilder.Build で確かめる。
    public sealed class PayloadSceneBuilderTests
    {
        private const double ExpectedRouteLength = 28d + 8.94427191d;

        private static float CartHalfDiagonal =>
            new Vector2(PayloadCart.DefaultBodySize.x, PayloadCart.DefaultBodySize.z).magnitude / 2f;

        [Test]
        public void TheSceneUsesThePayloadPathAndThePayloadRules()
        {
            var builder = new PayloadSceneBuilder();

            Assert.That(builder.ScenePath, Is.EqualTo(ShooterKitRoot.OutputScene("Payload")));
            Assert.That(builder.ScenePath, Is.EqualTo(BuildSceneList.PayloadScene));
            Assert.That(builder.RulesPath, Is.EqualTo(GameRulesAssetBuilder.PayloadPath));
            Assert.That(builder.RulesPath, Is.EqualTo(GameRulesAssetBuilder.ModeRulesPath(GameRulesAssetBuilder.PayloadName)));
            Assert.That(PayloadSceneBuilder.MenuPath, Is.EqualTo(SceneBuildSteps.MenuPath));
            Assert.That(SceneBuildSteps.BuiltIn.Select(step => step.Name), Has.Member("Payload"));
            Assert.That(builder.NavMeshDataPath, Is.EqualTo(ModeSceneBuilder.NavMeshDataPathFor(builder.ScenePath)));
            Assert.That(builder.NavMeshDataPath, Does.EndWith("/Payload/NavMesh-Floor.asset"));
            Assert.That(builder.LogTag, Is.EqualTo("[PayloadSceneBuilder]"));
        }

        [Test]
        public void TheRouteIsValidAndHasTwoCheckpointsAtItsTurns()
        {
            var route = PayloadSceneBuilder.BuildRoute();

            Assert.That(route.PointCount, Is.EqualTo(PayloadSceneBuilder.RoutePoints.Count));
            Assert.That(route.CheckpointCount, Is.EqualTo(2));
            Assert.That(route.Length, Is.EqualTo(ExpectedRouteLength).Within(1e-4d));
            Assert.That(route.CheckpointPoint(0), Is.EqualTo(2));
            Assert.That(route.CheckpointPoint(1), Is.EqualTo(4));
            Assert.That(route.CheckpointDistance(0), Is.EqualTo(16d).Within(1e-4d));
            Assert.That(route.CheckpointDistance(1), Is.EqualTo(22d + 8.94427191d).Within(1e-4d));
            foreach (var point in PayloadSceneBuilder.RoutePoints)
            {
                Assert.That(point.y, Is.Zero, $"{point} sits on the floor");
            }
        }

        [Test]
        public void TheAttackersAreThePlayersSideAndTheLayoutIsValid()
        {
            Assert.That(PayloadSceneBuilder.AttackerSide, Is.EqualTo(Side.Alpha));
            Assert.That(PayloadSceneBuilder.AttackerBots, Is.GreaterThan(0));
            Assert.That(PayloadSceneBuilder.DefenderBots, Is.GreaterThan(0));
            Assert.DoesNotThrow(() => ModeSceneBuilder.ValidateLayout(
                ModeSceneBuilder.TwoSides(PayloadSceneBuilder.AttackerBots, PayloadSceneBuilder.DefenderBots), PayloadSceneBuilder.AttackerSide));
        }

        [Test]
        public void TheCartStartsInFrontOfThePlayerAndNoSpawnIsInsideItsZone()
        {
            var start = PayloadSceneBuilder.RoutePoints[0];
            Assert.That(start.x, Is.Zero, "the cart starts straight ahead of the player");
            Assert.That(start.z, Is.GreaterThan(ModeSceneBuilder.PlayerFeet.z));

            var route = PayloadSceneBuilder.BuildRoute();
            foreach (var side in ModeSceneBuilder.TwoSides(0, 0))
            {
                foreach (var spawn in side.Spawns)
                {
                    for (var distance = 0d; distance <= route.Length; distance += 0.5d)
                    {
                        var center = route.PositionAt(distance).ToUnity();
                        Assert.That(
                            Vector3.Distance(center, spawn.Feet),
                            Is.GreaterThan(PayloadSceneBuilder.CartZoneRadius),
                            $"the cart at {distance:0.#}m does not reach the spawn at {spawn.Feet}");
                    }
                }
            }
        }

        [Test]
        public void TheCartNeverRunsIntoAnObstacleOrAWall()
        {
            var route = PayloadSceneBuilder.BuildRoute();
            var halfX = ModeSceneBuilder.DefaultFloorScale.x * 5f;
            var halfZ = ModeSceneBuilder.DefaultFloorScale.z * 5f;
            var inner = ModeSceneBuilder.BoundaryWallThickness + CartHalfDiagonal;
            for (var distance = 0d; distance <= route.Length; distance += 0.25d)
            {
                var center = route.PositionAt(distance).ToUnity();
                var local = center - ModeSceneBuilder.DefaultFloorCenter;
                Assert.That(Mathf.Abs(local.x), Is.LessThan(halfX - inner), $"the cart at {distance:0.##}m is inside the walls");
                Assert.That(Mathf.Abs(local.z), Is.LessThan(halfZ - inner), $"the cart at {distance:0.##}m is inside the walls");

                foreach (var obstacle in PayloadSceneBuilder.PayloadObstacles)
                {
                    var bounds = new Bounds(obstacle.Center, obstacle.Size);
                    bounds.Expand(CartHalfDiagonal * 2f);
                    Assert.That(bounds.Contains(new Vector3(center.x, obstacle.Center.y, center.z)), Is.False, $"the cart at {distance:0.##}m clears {obstacle.Name}");
                }
            }
        }

        [Test]
        public void TheBotsCirclingTheCartStayClearOfTheCartObstaclesAndWalls()
        {
            var botRadius = EnemyTestSceneBuilder.EnemyRadius;
            var halfX = ModeSceneBuilder.DefaultFloorScale.x * 5f;
            var halfZ = ModeSceneBuilder.DefaultFloorScale.z * 5f;
            var inner = ModeSceneBuilder.BoundaryWallThickness + botRadius;
            var bots = PayloadSceneBuilder.AttackerBots + PayloadSceneBuilder.DefenderBots;

            // 台車から bot が回る点までの距離は、台車の箱の角より bot の太さぶん外にある。
            Assert.That(
                PayloadSceneBuilder.CartZoneRadius * HardpointRunner.RouteRadiusFraction,
                Is.GreaterThan(CartHalfDiagonal + botRadius));

            foreach (var point in PayloadSceneBuilder.PatrolPoints(bots))
            {
                var local = point - ModeSceneBuilder.DefaultFloorCenter;
                Assert.That(Mathf.Abs(local.x), Is.LessThan(halfX - inner), $"{point} is inside the walls");
                Assert.That(Mathf.Abs(local.z), Is.LessThan(halfZ - inner), $"{point} is inside the walls");

                foreach (var obstacle in PayloadSceneBuilder.PayloadObstacles)
                {
                    var bounds = new Bounds(obstacle.Center, obstacle.Size);
                    bounds.Expand(botRadius * 2f);
                    Assert.That(bounds.Contains(new Vector3(point.x, obstacle.Center.y, point.z)), Is.False, $"{point} is clear of {obstacle.Name}");
                }
            }
        }

        [Test]
        public void ThePatrolPointsFollowTheWholeRoute()
        {
            var route = PayloadSceneBuilder.BuildRoute();
            var points = PayloadSceneBuilder.PatrolPoints(0);

            Assert.That(Vector3.Distance(points.First(), PayloadSceneBuilder.RoutePoints[0]), Is.LessThan(1e-4f));
            Assert.That(Vector3.Distance(points.Last(), PayloadSceneBuilder.RoutePoints[PayloadSceneBuilder.RoutePoints.Count - 1]), Is.LessThan(1e-4f));
            Assert.That(points.Count, Is.EqualTo((int)Math.Ceiling(route.Length / PayloadSceneBuilder.PatrolCheckStep) + 1));
            Assert.That(PayloadSceneBuilder.PatrolPoints(2).Count, Is.EqualTo(points.Count * 5), "each place adds the center and two ends per bot");
            Assert.Throws<ArgumentOutOfRangeException>(() => PayloadSceneBuilder.PatrolPoints(-1));
        }

        [Test]
        public void TheObstaclesHaveUniqueNamesAndStayClearOfTheSpawns()
        {
            var obstacles = PayloadSceneBuilder.PayloadObstacles;

            Assert.That(obstacles.Select(obstacle => obstacle.Name), Is.Unique);
            foreach (var obstacle in obstacles)
            {
                var bounds = new Bounds(obstacle.Center, obstacle.Size);
                bounds.Expand(EnemyTestSceneBuilder.EnemyRadius * 2f);
                foreach (var side in ModeSceneBuilder.TwoSides(0, 0))
                {
                    foreach (var spawn in side.Spawns)
                    {
                        Assert.That(bounds.Contains(new Vector3(spawn.Feet.x, obstacle.Center.y, spawn.Feet.z)), Is.False, $"{obstacle.Name} is clear of {spawn.Feet}");
                    }
                }
            }
        }

        [Test]
        public void TheRoutePointsAreNamedInOrder()
        {
            Assert.That(PayloadSceneBuilder.RoutePointName(0), Is.EqualTo("RoutePoint-0"));
            Assert.That(PayloadSceneBuilder.RoutePointName(5), Is.EqualTo("RoutePoint-5"));
            Assert.Throws<ArgumentOutOfRangeException>(() => PayloadSceneBuilder.RoutePointName(-1));
        }
    }
}
