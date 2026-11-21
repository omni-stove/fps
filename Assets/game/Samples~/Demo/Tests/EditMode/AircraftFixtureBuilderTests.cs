using System;
using System.Linq;
using Shooter.Core;
using Shooter.Editor;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    public sealed class AircraftFixtureBuilderTests
    {
        private AircraftDefinition _definition;

        [SetUp]
        public void SetUp()
        {
            _definition = ScriptableObject.CreateInstance<AircraftDefinition>();
            _definition.name = "Aircraft";
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_definition);
        }

        [Test]
        public void TheVehicleSpecListsEveryDamagePartAtTheSameMultiplier()
        {
            var spec = AircraftFixtureBuilder.Spec();

            Assert.That(spec.Id, Is.EqualTo(AircraftFixtureBuilder.VehicleId));
            Assert.That(
                spec.Parts.Select(p => p.Id),
                Is.EqualTo(new[]
                {
                    AircraftDamageSpec.DefaultLeftWingId, AircraftDamageSpec.DefaultRightWingId, AircraftDamageSpec.DefaultTailId, AircraftDamageSpec.DefaultEngineId,
                }));
            Assert.That(spec.Parts.Select(p => p.DamageMultiplier), Is.All.EqualTo(AircraftFixtureBuilder.PartDamageMultiplier));

            var custom = AircraftFixtureBuilder.Spec(new AircraftDamageSpec(new[] { new AircraftDamagePartSpec("nose", AircraftDamageEffect.Thrust, 50) }));
            Assert.That(custom.Parts.Select(p => p.Id), Is.EqualTo(new[] { "nose" }));
            Assert.That(AircraftFixtureBuilder.Spec(new AircraftDamageSpec(Array.Empty<AircraftDamagePartSpec>())).Parts, Is.Empty);
        }

        [Test]
        public void PartsAreFoundByWhatTheyAffectNotByTheirId()
        {
            var damage = new AircraftDamageSpec(new[]
            {
                new AircraftDamagePartSpec("port", AircraftDamageEffect.LeftLift, 100),
                new AircraftDamagePartSpec("starboard", AircraftDamageEffect.RightLift, 100),
            });

            Assert.That(AircraftFixtureBuilder.PartFor(damage, AircraftDamageEffect.LeftLift).Id, Is.EqualTo("port"));
            Assert.That(AircraftFixtureBuilder.PartFor(damage, AircraftDamageEffect.RightLift).Id, Is.EqualTo("starboard"));
            Assert.That(AircraftFixtureBuilder.PartFor(damage, AircraftDamageEffect.Thrust), Is.Null);
            Assert.Throws<ArgumentNullException>(() => AircraftFixtureBuilder.PartFor(null, AircraftDamageEffect.Thrust));
        }

        [Test]
        public void ConfiguringPutsBackTheKitDefaults()
        {
            _definition.MaxThrust = 3f;
            _definition.Damage.Parts = Array.Empty<AircraftDamageSettings.Part>();

            AircraftFixtureBuilder.Configure(_definition);

            Assert.That(_definition.MaxThrust, Is.EqualTo(AircraftSpec.DefaultMaxThrust));
            Assert.That(_definition.Mass, Is.EqualTo(AircraftSpec.DefaultMass));
            Assert.That(_definition.ToDamageSpec().Parts.Select(p => p.Id), Is.EqualTo(AircraftDamageSpec.DefaultParts().Select(p => p.Id)));
            Assert.Throws<ArgumentNullException>(() => AircraftFixtureBuilder.Configure(null));
        }

        [Test]
        public void ValidatingReadsEverySectionAndNamesTheAsset()
        {
            Assert.DoesNotThrow(() => AircraftFixtureBuilder.Validate(_definition));

            _definition.Mass = 0f;
            var error = Assert.Throws<ArgumentException>(() => AircraftFixtureBuilder.Validate(_definition));
            Assert.That(error.Message, Does.Contain("'Aircraft'"));
            Assert.Throws<ArgumentNullException>(() => AircraftFixtureBuilder.Validate(null));
        }

        [Test]
        public void TheEngineSoundLoopsWholeCyclesOfItsTones()
        {
            var spec = AircraftFixtureBuilder.EngineSoundSpec();

            Assert.That(spec.Name, Is.EqualTo(AircraftFixtureBuilder.EngineSound));
            Assert.That(AircraftFixtureBuilder.EngineSoundPath, Does.EndWith("/" + AircraftFixtureBuilder.EngineSound + ".wav"));
            foreach (var layer in spec.Layers.Where(l => l.ToneGain > 0f))
            {
                Assert.That(layer.ToneStartHz, Is.EqualTo(layer.ToneEndHz), "a steady tone");
                var cycles = layer.ToneStartHz * spec.LengthSeconds;
                Assert.That(cycles, Is.EqualTo(Mathf.Round(cycles)).Within(1e-4f), "whole cycles meet at the loop seam");
            }
        }

        [Test]
        public void CreatingNeedsALoadedSceneAndAName()
        {
            var scene = TestScenes.NewAdditive();

            // NewAdditive が Single でシーンを開き直すと SetUp で作った _definition が捨てられるので、シーンの後に作る。
            var definition = ScriptableObject.CreateInstance<AircraftDefinition>();
            definition.name = "Aircraft";
            try
            {
                Assert.Throws<ArgumentException>(() => AircraftFixtureBuilder.Create(default, null, "Aircraft", Vector3.zero, Quaternion.identity));
                Assert.Throws<ArgumentException>(() => AircraftFixtureBuilder.Create(scene, null, " ", Vector3.zero, Quaternion.identity));

                definition.MaxThrust = -1f;
                Assert.Throws<ArgumentException>(() => AircraftFixtureBuilder.Create(scene, null, "Aircraft", Vector3.zero, Quaternion.identity, definition));
                Assert.That(scene.rootCount, Is.Zero, "an invalid asset places nothing");
            }
            finally
            {
                Object.DestroyImmediate(definition);
                UnityEditor.SceneManagement.EditorSceneManager.CloseScene(scene, true);
            }
        }

        [Test]
        public void WithoutAnAssetTheAircraftFliesOnTheDefaults()
        {
            var scene = TestScenes.NewAdditive();
            try
            {
                var vehicle = AircraftFixtureBuilder.Create(scene, null, "Aircraft", Vector3.zero, Quaternion.identity);

                Assert.That(vehicle.GetComponent<SimpleAircraftMotor>().Definition, Is.Null);
                Assert.That(vehicle.GetComponent<SimpleAircraftMotor>().Body.mass, Is.EqualTo(AircraftSpec.DefaultMass));
                Assert.That(vehicle.GetComponent<AircraftPartDamage>().Spec.Parts.Count, Is.EqualTo(AircraftDamageSpec.DefaultParts().Length));
                Assert.That(vehicle.GetComponentsInChildren<VehiclePart>().Length, Is.EqualTo(4));
                Assert.That(vehicle.GetComponent<AircraftEffects>().EngineClip, Is.Null, "no clip, no engine sound");
            }
            finally
            {
                UnityEditor.SceneManagement.EditorSceneManager.CloseScene(scene, true);
            }
        }
    }
}
