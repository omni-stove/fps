using System.Linq;
using Shooter.Core;
using Shooter.Editor;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Shooter.Tests.EditMode
{
    public sealed class CoopDoorRangeFixtureTests
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
        public void TheRangeFindsTheFixtureAndTheSmokeFindsTheStep()
        {
            Assert.That(TpsRangeSceneBuilder.Fixtures().OfType<CoopDoorRangeFixture>().Count(), Is.EqualTo(1));
            Assert.That(TpsRangeSmokeRun.ExtraSteps().OfType<CoopDoorSmokeStep>().Count(), Is.EqualTo(1));
        }

        [Test]
        public void ItPlacesADoorWiredToACoopHoldInsideItsFootprint()
        {
            var fixture = new CoopDoorRangeFixture();

            var placed = fixture.Place(_scene, GameRules.Default);

            Assert.That(placed.name, Is.EqualTo(fixture.Name));
            Assert.That(placed.scene, Is.EqualTo(_scene));
            Assert.That(placed.transform.parent, Is.Null);
            var door = placed.GetComponentInChildren<CoopDoor>();
            Assert.That(door, Is.Not.Null);
            Assert.That(door.Interactable, Is.Not.Null);
            Assert.That(door.Interactable.RequiredCount, Is.EqualTo(CoopHoldSpec.DefaultRequiredCount));
            Assert.That(door.Panel, Is.Not.Null);
            Assert.That(door.Panel.gameObject.isStatic, Is.False, "the panel moves when the door opens");
            Assert.That(door.Panel.GetComponent<Collider>().isTrigger, Is.False, "the closed door blocks the way");
            Assert.That(door.Interactable.GetComponents<Collider>().Any(c => c.isTrigger), Is.True);

            var footprint = fixture.Footprint;
            foreach (var renderer in placed.GetComponentsInChildren<Renderer>())
            {
                Assert.That(footprint.Contains(renderer.bounds.center), Is.True, $"{renderer.name} is inside the footprint");
            }

            Assert.That(footprint.Contains(CoopDoorRangeFixture.BodyAt(CoopDoorRangeFixture.OutPoint)), Is.True);
        }

        [Test]
        public void TheHoldersStandInsideTheRangeAndLeaveOutsideIt()
        {
            var interactable = new CoopDoorRangeFixture().Place(_scene, GameRules.Default).GetComponentInChildren<CoopInteractable>();
            Physics.SyncTransforms();

            Assert.That(interactable.IsInRange(CoopDoorRangeFixture.BodyAt(CoopDoorRangeFixture.StandPoint)), Is.True);
            Assert.That(interactable.IsInRange(CoopDoorRangeFixture.BodyAt(CoopDoorRangeFixture.PartnerPoint)), Is.True);
            Assert.That(interactable.IsInRange(CoopDoorRangeFixture.BodyAt(CoopDoorRangeFixture.OutPoint)), Is.False);
        }
    }
}
