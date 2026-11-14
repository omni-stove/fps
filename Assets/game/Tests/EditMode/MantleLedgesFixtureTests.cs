using Fps.Core;
using Fps.Editor;
using Fps.Runtime;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Fps.Tests.EditMode
{
    public sealed class MantleLedgesFixtureTests
    {
        private const float Epsilon = 1e-4f;

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
        public void TheRangeFindsTheFixtureAndTheSmokeStep()
        {
            Assert.That(ExtensionPoints.IsCandidate(typeof(MantleLedgesFixture)), Is.True);
            Assert.That(ExtensionPoints.IsCandidate(typeof(MantleSmokeStep)), Is.True);
            Assert.That(TpsRangeSceneBuilder.Fixtures(), Has.Some.InstanceOf<MantleLedgesFixture>());
            Assert.That(TpsRangeSmokeRun.ExtraSteps(), Has.Some.InstanceOf<MantleSmokeStep>());
        }

        [Test]
        public void TheLedgesAreAboveTheVaultAndWithinTheMantleReach()
        {
            Assert.That(MantleLedgesFixture.LedgeHeight, Is.GreaterThan(MotorSpec.DefaultVaultMaxHeight));
            Assert.That(MantleLedgesFixture.LedgeHeight, Is.GreaterThan(PlayerController.DefaultJumpHeight), "a plain jump cannot get on top");
            Assert.That(MantleLedgesFixture.LedgeHeight, Is.LessThan(MotorSpec.DefaultMantleMaxHeight));

            var footing = VaultProbe.MantleFootingDepth(GameBootstrap.PlayerRadius);
            Assert.That(MantleLedgesFixture.DeepLedgeDepth, Is.GreaterThanOrEqualTo(footing), "the player can stand on the deep ledges");
            Assert.That(MantleLedgesFixture.NarrowLedgeDepth, Is.LessThan(footing), "the player cannot stand on the narrow ledge");
            Assert.That(MantleLedgesFixture.CeilingClearance, Is.LessThan(GameBootstrap.PlayerHeight), "the ceiling leaves no room to stand");
            Assert.That(
                GameBootstrap.PlayerRadius + MantleSmokeStep.StandOff,
                Is.LessThanOrEqualTo(VaultProbe.MaxReach),
                "the smoke stands within reach of the ledges");
        }

        [Test]
        public void PlacesEveryLedgeAndTheCeilingInsideTheFootprint()
        {
            var fixture = new MantleLedgesFixture();
            var root = fixture.Place(_scene, GameRules.Default);
            Physics.SyncTransforms();

            Assert.That(root.name, Is.EqualTo(MantleLedgesFixture.RootName));
            Assert.That(root.scene, Is.EqualTo(_scene));
            Assert.That(root.transform.parent, Is.Null);

            var footprint = fixture.Footprint;
            var boxes = MantleLedgesFixture.Boxes();
            Assert.That(root.transform.childCount, Is.EqualTo(boxes.Count));
            foreach (var box in boxes)
            {
                var child = root.transform.Find(box.Name);
                Assert.That(child, Is.Not.Null, box.Name);
                var bounds = child.GetComponent<Collider>().bounds;
                Assert.That(Vector3.Distance(bounds.center, box.Center), Is.LessThan(Epsilon), box.Name);
                Assert.That(footprint.Contains(bounds.min + (Vector3.one * Epsilon)), Is.True, box.Name);
                Assert.That(footprint.Contains(bounds.max - (Vector3.one * Epsilon)), Is.True, box.Name);
            }

            foreach (var name in new[] { MantleLedgesFixture.HighLedgeName, MantleLedgesFixture.NarrowLedgeName, MantleLedgesFixture.CoveredLedgeName })
            {
                var bounds = root.transform.Find(name).GetComponent<Collider>().bounds;
                Assert.That(bounds.min.y, Is.EqualTo(0f).Within(Epsilon), name + " stands on the floor");
                Assert.That(bounds.max.y, Is.EqualTo(MantleLedgesFixture.LedgeHeight).Within(Epsilon), name);
                Assert.That(bounds.min.z, Is.EqualTo(MantleLedgesFixture.FrontZ).Within(Epsilon), name);
            }

            var ceiling = root.transform.Find(MantleLedgesFixture.CeilingName).GetComponent<Collider>().bounds;
            var covered = root.transform.Find(MantleLedgesFixture.CoveredLedgeName).GetComponent<Collider>().bounds;
            Assert.That(ceiling.min.y - covered.max.y, Is.EqualTo(MantleLedgesFixture.CeilingClearance).Within(Epsilon));
            Assert.That(ceiling.min.z, Is.EqualTo(covered.min.z).Within(Epsilon), "the ceiling starts at the edge, leaving the climb in front of it open");
        }
    }
}
