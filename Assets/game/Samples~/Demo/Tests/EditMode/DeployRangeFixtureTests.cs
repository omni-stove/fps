using System.Linq;
using Fps.Core;
using Fps.Editor;
using Fps.Runtime;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Fps.Tests.EditMode
{
    public sealed class DeployRangeFixtureTests
    {
        private Scene _scene;

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
        }

        [Test]
        public void TheTpsRangeFindsTheYardAndItsSmokeStep()
        {
            Assert.That(TpsRangeSceneBuilder.Fixtures().OfType<DeployRangeFixture>().Count(), Is.EqualTo(1));
            Assert.That(TpsRangeSmokeRun.ExtraSteps().OfType<DeploySmokeStep>().Count(), Is.EqualTo(1));
        }

        [Test]
        public void PlacesASteepRampAndAShooterThatNothingTargets()
        {
            var root = new DeployRangeFixture().Place(_scene, GameRules.Default);

            Assert.That(root.name, Is.EqualTo(DeployRangeFixture.RootName));
            Assert.That(root.scene, Is.EqualTo(_scene));

            var ramp = root.transform.Find(DeployRangeFixture.RampName);
            Assert.That(ramp, Is.Not.Null);
            Assert.That(ramp.GetComponent<Collider>(), Is.Not.Null);
            var slope = DeployPlacement.SlopeDegrees(ramp.up.ToNumerics());
            Assert.That(slope, Is.GreaterThan(DeploySpec.DefaultMaxSlopeDegrees), "too steep for the default deploy spec");

            var shooter = root.transform.Find(DeployRangeFixture.ShooterName);
            Assert.That(shooter, Is.Not.Null);
            Assert.That(Vector3.Distance(shooter.position, DeployRangeFixture.ShooterPosition), Is.LessThan(1e-4f));
            Assert.That(shooter.GetComponentsInChildren<Collider>(true), Is.Empty, "bullets aimed at the shield must not stop at the shooter");
            Assert.That(shooter.GetComponentInParent<SideMember>(), Is.Null, "a side would make it a mode member and an enemy target");
        }

        [Test]
        public void TheLanesFitTheYardAndLeaveRoomForEveryShield()
        {
            var footprint = new DeployRangeFixture().Footprint;
            var halfWidth = DeploySpec.DefaultWidth / 2f;
            var lanes = DeployRangeFixture.FlatLaneXs;

            Assert.That(lanes.Count, Is.GreaterThan(DeploySpec.DefaultMaxActive), "the smoke places one over the limit");
            for (var i = 0; i < lanes.Count; i++)
            {
                Assert.That(lanes[i] - halfWidth, Is.GreaterThanOrEqualTo(footprint.min.x));
                Assert.That(lanes[i] + halfWidth, Is.LessThanOrEqualTo(footprint.max.x));
                if (i > 0)
                {
                    Assert.That(lanes[i] - lanes[i - 1], Is.GreaterThan(DeploySpec.DefaultWidth), "shields in next lanes must not overlap");
                }
            }

            Assert.That(DeployRangeFixture.PlayerZ, Is.GreaterThan(footprint.min.z));
            Assert.That(DeployRangeFixture.PlayerZ + DeploySpec.DefaultMaxRange, Is.LessThan(DeployRangeFixture.ShooterZ));
        }
    }
}
