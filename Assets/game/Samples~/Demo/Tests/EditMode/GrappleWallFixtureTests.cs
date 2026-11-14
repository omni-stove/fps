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
    public sealed class GrappleWallFixtureTests
    {
        private Scene _scene;
        private GrappleAbilityDefinition _definition;

        [SetUp]
        public void SetUp()
        {
            _scene = TestScenes.NewAdditive();
            _definition = ScriptableObject.CreateInstance<GrappleAbilityDefinition>();
            _definition.GrabbableLayers = AbilityAssetBuilder.GrappleGrabbableLayers;
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_definition);
            if (_scene.IsValid())
            {
                EditorSceneManager.CloseScene(_scene, true);
            }
        }

        [Test]
        public void TheFixtureAndTheSmokeStepAreFound()
        {
            Assert.That(TpsRangeSceneBuilder.Fixtures().OfType<GrappleWallFixture>().Count(), Is.EqualTo(1));
            Assert.That(TpsRangeSmokeRun.ExtraSteps().OfType<GrappleSmokeStep>().Count(), Is.EqualTo(1));
        }

        [Test]
        public void PlacesAGrabbableAndANotGrabbableWallInsideTheFootprint()
        {
            var fixture = new GrappleWallFixture();

            var root = fixture.Place(_scene, GameRules.Default);
            Physics.SyncTransforms();

            Assert.That(root.name, Is.EqualTo(fixture.Name));
            Assert.That(root.scene, Is.EqualTo(_scene));
            Assert.That(root.transform.parent, Is.Null);

            var grabbable = root.transform.Find(GrappleWallFixture.GrabbableWallName).GetComponent<Collider>();
            var notGrabbable = root.transform.Find(GrappleWallFixture.NotGrabbableWallName).GetComponent<Collider>();
            var surfaces = new GrappleSurfaceFilter(_definition.GrabbableLayers, _definition.GrabbableTags);
            Assert.That(surfaces.Allows(grabbable.gameObject), Is.True);
            Assert.That(surfaces.Allows(notGrabbable.gameObject), Is.False);

            // 掴めない壁も Ray に当たらなければ、縄が抜けて「掴めない面」を試せない。
            Assert.That((Physics.DefaultRaycastLayers & (1 << notGrabbable.gameObject.layer)) != 0, Is.True);

            var footprint = fixture.Footprint;
            foreach (var wall in new[] { grabbable, notGrabbable })
            {
                Assert.That(footprint.Contains(wall.bounds.min + (Vector3.one * 0.01f)), Is.True, wall.name);
                Assert.That(footprint.Contains(wall.bounds.max - (Vector3.one * 0.01f)), Is.True, wall.name);
            }

            Assert.That(footprint.Contains(GrappleWallFixture.StandPoint + (Vector3.up * 0.01f)), Is.True);
            Assert.That(footprint.Contains(GrappleWallFixture.NotGrabbableStandPoint + (Vector3.up * 0.01f)), Is.True);
        }

        [Test]
        public void TheWallIsOnlyReachableWithTheGrappleAndCanBeClimbedFromItsEdge()
        {
            var motor = new MotorSpec(5f, 8f, 1.2f, 20f, 0.1f, 2.5f);
            var grapple = GrappleSpec.Default;

            Assert.That(GrappleWallFixture.WallHeight, Is.GreaterThan(motor.MantleMaxHeight), "a climb from the floor cannot reach the top");
            Assert.That(GrappleWallFixture.WallThickness, Is.GreaterThan(VaultProbe.MantleFootingDepth(GameBootstrap.PlayerRadius)));

            // 縁に掴まった体の足元から縁までが、崖のぼりで届く高さに収まる。
            var feetBelowEdge = grapple.HangHeight + (GameBootstrap.PlayerHeight / 2f);
            Assert.That(feetBelowEdge, Is.GreaterThan(motor.VaultMaxHeight));
            Assert.That(feetBelowEdge + grapple.ArriveDistance, Is.LessThanOrEqualTo(motor.MantleMaxHeight));

            var eye = GrappleWallFixture.StandPoint + (Vector3.up * GameBootstrap.PlayerHeight);
            var edge = new Vector3(GrappleWallFixture.FaceX, GrappleWallFixture.WallHeight, GrappleWallFixture.GrabbableWallZ);
            Assert.That(grapple.InRange(Vector3.Distance(eye, edge)), Is.True);
        }
    }
}
