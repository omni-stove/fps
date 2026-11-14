using Fps.Editor;
using NUnit.Framework;
using UnityEngine;

namespace Fps.Tests.EditMode
{
    /// <summary>
    /// <see cref="SearchAndDestroySceneBuilder"/> のパスと設置地点の配置を確かめる。シーンを組んで NavMesh を焼く処理は、
    /// 開いているシーンを置き換える (NewScene Single) ので <c>-executeMethod Fps.Editor.SearchAndDestroySceneBuilder.Build</c> で確かめる。
    /// </summary>
    public sealed class SearchAndDestroySceneBuilderTests
    {
        [Test]
        public void TheSceneUsesTheSearchAndDestroyRules()
        {
            var builder = new SearchAndDestroySceneBuilder();

            Assert.That(builder.ScenePath, Is.EqualTo(BuildSceneList.SearchAndDestroyScene));
            Assert.That(builder.RulesPath, Is.EqualTo(GameRulesAssetBuilder.SearchAndDestroyPath));
            Assert.That(builder.NavMeshDataPath, Is.EqualTo(DemoSample.Root + "/Scenes/SearchAndDestroy/NavMesh-Floor.asset"));
            Assert.That(BuildSceneList.Scenes, Has.Member(BuildSceneList.SearchAndDestroyScene));
            Assert.That(SearchAndDestroySceneBuilder.DefinitionPath, Is.EqualTo(DemoSample.Root + "/Modes/SearchAndDestroy.asset"));
        }

        [Test]
        public void TheSitesAreOnTheFloorAndClearOfTheObstaclesAndSpawns()
        {
            var halfX = ModeSceneBuilder.DefaultFloorScale.x * 5f;
            var halfZ = ModeSceneBuilder.DefaultFloorScale.z * 5f;
            var inner = ModeSceneBuilder.BoundaryWallThickness + SearchAndDestroySceneBuilder.SiteRadius;

            Assert.That(SearchAndDestroySceneBuilder.SiteCenters, Has.Count.EqualTo(2));
            foreach (var center in SearchAndDestroySceneBuilder.SiteCenters)
            {
                var local = center - ModeSceneBuilder.DefaultFloorCenter;
                Assert.That(Mathf.Abs(local.x), Is.LessThan(halfX - inner), $"{center} is inside the boundary walls");
                Assert.That(Mathf.Abs(local.z), Is.LessThan(halfZ - inner), $"{center} is inside the boundary walls");

                foreach (var obstacle in ModeSceneBuilder.DefaultObstacles)
                {
                    var bounds = new Bounds(obstacle.Center, obstacle.Size);
                    Assert.That(
                        bounds.SqrDistance(center),
                        Is.GreaterThan(SearchAndDestroySceneBuilder.SiteRadius * SearchAndDestroySceneBuilder.SiteRadius),
                        $"{center} is clear of {obstacle.Name}");
                }

                foreach (var side in ModeSceneBuilder.TwoSides(0, 0))
                {
                    foreach (var spawn in side.Spawns)
                    {
                        Assert.That(Vector3.Distance(spawn.Feet, center), Is.GreaterThan(SearchAndDestroySceneBuilder.SiteRadius * 3f));
                    }
                }
            }
        }

        [Test]
        public void TheSitesAreTheSameDistanceFromBothSides()
        {
            foreach (var center in SearchAndDestroySceneBuilder.SiteCenters)
            {
                Assert.That(
                    center.z - ModeSceneBuilder.DefaultNearSpawnZ,
                    Is.EqualTo(ModeSceneBuilder.DefaultFarSpawnZ - center.z).Within(1e-4f),
                    "the sides swap attack and defense, so neither spawn may be closer to the sites");
            }
        }

        [Test]
        public void SiteObjectsAreNamedAfterTheirLetter()
        {
            Assert.That(SearchAndDestroySceneBuilder.SiteObjectName(0), Is.EqualTo("BombSite-A"));
            Assert.That(SearchAndDestroySceneBuilder.SiteObjectName(1), Is.EqualTo("BombSite-B"));
        }
    }
}
