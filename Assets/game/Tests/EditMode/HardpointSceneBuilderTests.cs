using System;
using Fps.Editor;
using Fps.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace Fps.Tests.EditMode
{
    /// <summary>
    /// <see cref="HardpointSceneBuilder"/> の拠点の配置と、シーン・ルールのパスを確かめる。シーンを組んで NavMesh を焼く処理は、
    /// 開いているシーンを置き換える (NewScene Single) ので <c>-executeMethod Fps.Editor.HardpointSceneBuilder.Build</c> で確かめる。
    /// </summary>
    public sealed class HardpointSceneBuilderTests
    {
        [Test]
        public void TheSceneUsesTheHardpointPathsAndRules()
        {
            var builder = new HardpointSceneBuilder();

            Assert.That(builder.ScenePath, Is.EqualTo(BuildSceneList.ModeScene(GameRulesAssetBuilder.HardpointName)));
            Assert.That(builder.RulesPath, Is.EqualTo(GameRulesAssetBuilder.ModeRulesPath(GameRulesAssetBuilder.HardpointName)));
            Assert.That(builder.NavMeshDataPath, Is.EqualTo("Assets/game/Scenes/Hardpoint/NavMesh-Floor.asset"));
            Assert.That(builder.LogTag, Is.EqualTo("[HardpointSceneBuilder]"));
            Assert.That(HardpointSceneBuilder.DefinitionPath, Is.EqualTo("Assets/game/Modes/Hardpoint.asset"));
            Assert.That(DominationSceneBuilder.DefinitionPath, Is.EqualTo("Assets/game/Modes/Domination.asset"));
        }

        [Test]
        public void ThereAreFourNamedPointsFarEnoughApartNotToOverlap()
        {
            var points = HardpointSceneBuilder.Points;

            Assert.That(points, Has.Count.EqualTo(4));
            for (var i = 0; i < points.Count; i++)
            {
                Assert.That(points[i].Center.y, Is.Zero, $"{points[i].Name} sits on the floor");
                for (var j = i + 1; j < points.Count; j++)
                {
                    Assert.That(points[i].Name, Is.Not.EqualTo(points[j].Name));
                    Assert.That(
                        Vector3.Distance(points[i].Center, points[j].Center),
                        Is.GreaterThan(HardpointSceneBuilder.PointRadius * 2f),
                        $"{points[i].Name} and {points[j].Name} do not overlap");
                }
            }
        }

        [Test]
        public void ThePointsAreOnTheFloorAndClearOfObstaclesAndSpawns()
        {
            var radius = HardpointSceneBuilder.PointRadius;
            var halfX = ModeSceneBuilder.DefaultFloorScale.x * 5f;
            var halfZ = ModeSceneBuilder.DefaultFloorScale.z * 5f;
            var inner = ModeSceneBuilder.BoundaryWallThickness + radius;
            foreach (var point in HardpointSceneBuilder.Points)
            {
                var local = point.Center - ModeSceneBuilder.DefaultFloorCenter;
                Assert.That(Mathf.Abs(local.x), Is.LessThan(halfX - inner), $"{point.Name} is inside the boundary walls");
                Assert.That(Mathf.Abs(local.z), Is.LessThan(halfZ - inner), $"{point.Name} is inside the boundary walls");

                foreach (var obstacle in ModeSceneBuilder.DefaultObstacles)
                {
                    var bounds = new Bounds(obstacle.Center, obstacle.Size);
                    bounds.Expand(radius * 2f);
                    Assert.That(bounds.Contains(point.Center), Is.False, $"{point.Name} is clear of {obstacle.Name}");
                }

                foreach (var side in ModeSceneBuilder.TwoSides(0, 0))
                {
                    foreach (var spawn in side.Spawns)
                    {
                        Assert.That(
                            Vector3.Distance(spawn.Feet, point.Center), Is.GreaterThan(radius * 2f), $"{point.Name} is away from the spawn at {spawn.Feet}");
                    }
                }
            }
        }

        [Test]
        public void ThePointsNearEachSideAreEquallyFarFromItsSpawns()
        {
            float NearestSpawn(Vector3 center, int sideIndex)
            {
                var nearest = float.PositiveInfinity;
                foreach (var spawn in ModeSceneBuilder.TwoSides(0, 0)[sideIndex].Spawns)
                {
                    nearest = Mathf.Min(nearest, Vector3.Distance(spawn.Feet, center));
                }

                return nearest;
            }

            var alphaTotal = 0f;
            var bravoTotal = 0f;
            foreach (var point in HardpointSceneBuilder.Points)
            {
                alphaTotal += NearestSpawn(point.Center, 0);
                bravoTotal += NearestSpawn(point.Center, 1);
            }

            Assert.That(alphaTotal, Is.EqualTo(bravoTotal).Within(1e-3f), "neither side starts closer to the points overall");
        }

        [Test]
        public void TheBotRoutesStayInsideEachPoint()
        {
            foreach (var point in HardpointSceneBuilder.Points)
            {
                for (var slot = 0; slot < 8; slot++)
                {
                    foreach (var end in HardpointRunner.RouteAround(point.Center, HardpointSceneBuilder.PointRadius, slot))
                    {
                        Assert.That(Vector3.Distance(end, point.Center), Is.LessThan(HardpointSceneBuilder.PointRadius));
                    }
                }
            }
        }

        [Test]
        public void APlacementNeedsAName()
        {
            Assert.Throws<ArgumentException>(() => new HardpointPlacement(" ", Vector3.zero));
        }
    }
}
