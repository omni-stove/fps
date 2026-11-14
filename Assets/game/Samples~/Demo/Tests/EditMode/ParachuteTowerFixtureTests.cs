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
    public sealed class ParachuteTowerFixtureTests
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
        public void TheTpsRangeFindsTheTowerAndTheSmokeStep()
        {
            Assert.That(TpsRangeSceneBuilder.Fixtures().OfType<ParachuteTowerFixture>().Count(), Is.EqualTo(1));
            Assert.That(TpsRangeSmokeRun.ExtraSteps().OfType<ParachuteSmokeStep>().Count(), Is.EqualTo(1));
        }

        [Test]
        public void TheTowerIsHighEnoughToOpenAndToHurtWithoutOpening()
        {
            var tower = new ParachuteTowerFixture();
            var spec = new MotorSpec(
                PlayerController.DefaultWalkSpeed,
                PlayerController.DefaultSprintSpeed,
                PlayerController.DefaultJumpHeight,
                PlayerController.DefaultGravity,
                PlayerController.DefaultAirControl,
                PlayerController.DefaultCrouchSpeed);
            var height = tower.Footprint.size.y;
            var openFrom = height - ParachuteSmokeStep.DropBelowTopToOpen;

            Assert.That(openFrom, Is.GreaterThan(ParachuteMotion.DefaultMinOpenHeight), "it opens after stepping off the top");
            Assert.That(Mathf.Sqrt(2f * spec.Gravity * height), Is.GreaterThan(spec.SafeFallSpeed), "falling from the top without opening hurts");
            Assert.That(tower.Footprint.min.y, Is.EqualTo(0f).Within(1e-4f), "it stands on the floor");
        }

        [Test]
        public void PlacesASolidBoxAtTheRootWithItsName()
        {
            var tower = new ParachuteTowerFixture();

            var placed = tower.Place(_scene, GameRules.Default);
            Physics.SyncTransforms();

            Assert.That(placed.name, Is.EqualTo(ParachuteTowerFixture.TowerName));
            Assert.That(placed.scene, Is.EqualTo(_scene));
            Assert.That(placed.transform.parent, Is.Null);
            var collider = placed.GetComponent<BoxCollider>();
            Assert.That(collider, Is.Not.Null);
            Assert.That(Vector3.Distance(collider.bounds.center, tower.Footprint.center), Is.LessThan(1e-4f));
            Assert.That(collider.bounds.size.y, Is.EqualTo(tower.Footprint.size.y).Within(1e-4f));
        }
    }
}
