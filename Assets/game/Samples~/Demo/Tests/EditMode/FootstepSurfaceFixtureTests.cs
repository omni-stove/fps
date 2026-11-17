using System.Collections.Generic;
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
    public sealed class FootstepSurfaceFixtureTests
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
            Assert.That(TpsRangeSceneBuilder.Fixtures().OfType<FootstepSurfaceFixture>().Count(), Is.EqualTo(1));
            Assert.That(TpsRangeSmokeRun.ExtraSteps().OfType<FootstepSurfaceSmokeStep>().Count(), Is.EqualTo(1));
        }

        [Test]
        public void ItPlacesPlatesOfDifferentSurfacesInsideItsFootprint()
        {
            var fixture = new FootstepSurfaceFixture();

            var placed = fixture.Place(_scene, GameRules.Default);

            Assert.That(placed.name, Is.EqualTo(fixture.Name));
            Assert.That(placed.scene, Is.EqualTo(_scene));
            Assert.That(placed.transform.parent, Is.Null);
            Assert.That(FootstepSurfaceFixture.SurfaceIds.Distinct().Count(), Is.EqualTo(FootstepSurfaceFixture.SurfaceIds.Count));
            Assert.That(FootstepSurfaceFixture.SurfaceIds, Does.Not.Contain(SurfaceMaterial.DefaultId), "the range floor is the default surface");
            Assert.That(FootstepSurfaceAssetStep.Surfaces(), Is.SupersetOf(FootstepSurfaceFixture.SurfaceIds), "every plate has its own footstep sounds");

            var footprint = fixture.Footprint;
            for (var i = 0; i < FootstepSurfaceFixture.SurfaceIds.Count; i++)
            {
                var plate = placed.transform.Find(FootstepSurfaceFixture.PlateName(i));
                Assert.That(plate, Is.Not.Null, FootstepSurfaceFixture.PlateName(i));
                var collider = plate.GetComponent<Collider>();
                Assert.That(SurfaceMaterial.IdOf(collider), Is.EqualTo(FootstepSurfaceFixture.SurfaceIds[i]));
                var center = plate.position;
                Assert.That(center.y + (plate.localScale.y / 2f), Is.EqualTo(FootstepSurfaceFixture.PlateTop).Within(0.001f), "plates sit just above the range floor");
                Assert.That(footprint.Contains(new Vector3(center.x, footprint.center.y, center.z)), Is.True, "plate inside the footprint");
            }

            Assert.That(footprint.Contains(new Vector3(FootstepSurfaceFixture.Start.x, footprint.center.y, FootstepSurfaceFixture.Start.z)), Is.True);
            Assert.That(footprint.Contains(new Vector3(FootstepSurfaceFixture.Finish.x, footprint.center.y, FootstepSurfaceFixture.Finish.z)), Is.True);
        }

        [Test]
        public void ThePlatesAreLongerThanAStrideSoEachGetsAFootstep()
        {
            Assert.That(FootstepSurfaceFixture.PlateLength, Is.GreaterThan(FootstepCadence.DefaultSprintStride));
        }

        [Test]
        public void TheStepReadsSurfaceSwitchesFromTheFootstepLog()
        {
            var log = $"{CharacterAudio.FootstepLogTag} '{GameBootstrap.PlayerName}' surface default -> metal (Footstep, clip 'Footstep_metal_2' from Surface)";

            Assert.That(FootstepSurfaceSmokeStep.TryParseSwitch(log, out var to, out var fromSurface), Is.True);
            Assert.That(to, Is.EqualTo("metal"));
            Assert.That(fromSurface, Is.True);

            var fallback = $"{CharacterAudio.FootstepLogTag} '{GameBootstrap.PlayerName}' surface metal -> wood (Footstep, clip 'Footstep' from PresentationCatalog)";
            Assert.That(FootstepSurfaceSmokeStep.TryParseSwitch(fallback, out to, out fromSurface), Is.True);
            Assert.That(to, Is.EqualTo("wood"));
            Assert.That(fromSurface, Is.False);

            var enemy = $"{CharacterAudio.FootstepLogTag} 'Enemy' surface default -> metal (Footstep, clip 'Footstep_metal_1' from Surface)";
            Assert.That(FootstepSurfaceSmokeStep.TryParseSwitch(enemy, out _, out _), Is.False);
        }

        [Test]
        public void TheStepNeedsEverySurfaceInOrderFromItsOwnSounds()
        {
            var expected = new[] { "metal", "wood", "concrete" };

            Assert.That(
                FootstepSurfaceSmokeStep.CheckSwitches(
                    new List<(string, bool)> { ("default", true), ("metal", true), ("wood", true), ("concrete", true), ("default", true) }, expected),
                Is.Null);
            Assert.That(
                FootstepSurfaceSmokeStep.CheckSwitches(new List<(string, bool)> { ("metal", true), ("concrete", true) }, expected),
                Is.Not.Null, "a skipped plate fails");
            Assert.That(
                FootstepSurfaceSmokeStep.CheckSwitches(new List<(string, bool)> { ("metal", true), ("wood", false), ("concrete", true) }, expected),
                Is.Not.Null, "a plate that fell back to other sounds fails");
        }
    }
}
