using System;
using System.Linq;
using System.Text.RegularExpressions;
using Fps.Core;
using Fps.Editor;
using Fps.Runtime;
using Fps.Runtime.Traversal;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;
using ZiplineLine = Fps.Runtime.Traversal.Zipline;
using ZiplineRule = Fps.Core.Zipline;

namespace Fps.Tests.EditMode
{
    /// <summary>射撃場のジップライン (<see cref="Fps.Editor.ZiplineRangeFixture"/>) が乗れる線を置くことを確かめる。</summary>
    public sealed class ZiplineRangeFixtureTests
    {
        [Test]
        public void TheRangeFixturePlacesARideableLineFromThePlatform()
        {
            var scene = TestScenes.NewAdditive();
            try
            {
                var fixture = new ZiplineRangeFixture();
                var root = fixture.Place(scene, GameRules.Default);
                var line = root.GetComponent<ZiplineLine>();

                Assert.That(root.name, Is.EqualTo(fixture.Name));
                Assert.That(root.scene, Is.EqualTo(scene));
                Assert.That(line, Is.Not.Null);
                Assert.That(line.StartPoint, Is.EqualTo(ZiplineRangeFixture.StartAnchor));
                Assert.That(line.EndPoint, Is.EqualTo(ZiplineRangeFixture.EndAnchor));
                Assert.That(fixture.Footprint.Contains(ZiplineRangeFixture.StartAnchor), Is.True);
                Assert.That(fixture.Footprint.Contains(ZiplineRangeFixture.EndAnchor), Is.True);

                var standingHand = new Vector3(
                    ZiplineRangeFixture.LineX,
                    ZiplineRangeFixture.PlatformHeight + GameBootstrap.PlayerSpawn.y + ZiplineMovement.DefaultHangOffset,
                    ZiplineRangeFixture.PlatformZ);
                Assert.That(line.DistanceTo(standingHand), Is.LessThan(ZiplineMovement.DefaultGrabRadius), "reachable from the platform");
                Assert.That(root.transform.Find(ZiplineRangeFixture.CableName).GetComponent<Collider>(), Is.Null, "the cable stops nothing");
                Assert.That(TpsRangeSceneBuilder.Fixtures().OfType<ZiplineRangeFixture>().Count(), Is.EqualTo(1));
                Assert.That(TpsRangeSmokeRun.ExtraSteps().OfType<ZiplineSmokeStep>().Count(), Is.EqualTo(1));
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }
    }
}
