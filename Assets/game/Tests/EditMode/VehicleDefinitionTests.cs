using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Shooter.Core;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    public sealed class VehicleDefinitionTests
    {
        private static readonly VehicleSeatSpec[] _twoSeats =
        {
            new VehicleSeatSpec(VehicleSeatRole.Driver),
            new VehicleSeatSpec(VehicleSeatRole.Passenger),
        };

        private readonly List<Object> _created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            for (var i = _created.Count - 1; i >= 0; i--)
            {
                if (_created[i] != null)
                {
                    Object.DestroyImmediate(_created[i]);
                }
            }

            _created.Clear();
        }

        [Test]
        public void ANewDefinitionStartsFromTheSpecDefaults()
        {
            var definition = NewDefinition("Fresh");

            Assert.That(definition.Id, Is.EqualTo(VehicleSpec.DefaultId));
            Assert.That(definition.MaxHealth, Is.EqualTo(VehicleSpec.DefaultMaxHealth));
            Assert.That(definition.EnterRange, Is.EqualTo(VehicleSpec.DefaultEnterRange));
            Assert.That(definition.EnterSeconds, Is.EqualTo(VehicleSpec.DefaultEnterSeconds));
            Assert.That(definition.ExitSeconds, Is.EqualTo(VehicleSpec.DefaultExitSeconds));
            Assert.That(definition.ChaseDistance, Is.EqualTo(VehicleSpec.DefaultChaseDistance));
            Assert.That(definition.ChaseHeight, Is.EqualTo(VehicleSpec.DefaultChaseHeight));
        }

        [Test]
        public void TheGettingInAndOutSecondsGoIntoTheSpecAndBack()
        {
            var definition = Jeep();
            definition.EnterSeconds = 1.25f;
            definition.ExitSeconds = 0.5f;

            var spec = definition.ToSpec(_twoSeats);
            Assert.That(spec.EnterSeconds, Is.EqualTo(1.25f));
            Assert.That(spec.ExitSeconds, Is.EqualTo(0.5f));

            var copy = NewDefinition("Copy");
            copy.CopyFrom(spec);
            Assert.That(copy.EnterSeconds, Is.EqualTo(1.25f));
            Assert.That(copy.ExitSeconds, Is.EqualTo(0.5f));

            definition.ExitSeconds = -1f;
            Assert.Throws<VehicleDefinitionException>(() => definition.ToSpec(_twoSeats));
        }

        [Test]
        public void ToSpecPutsTheDefinitionsValuesIntoTheSpec()
        {
            var definition = Jeep();

            var spec = definition.ToSpec(_twoSeats);

            Assert.That(spec.Id, Is.EqualTo("jeep"));
            Assert.That(spec.MaxHealth, Is.EqualTo(50));
            Assert.That(spec.EnterRange, Is.EqualTo(3f));
            Assert.That(spec.ChaseDistance, Is.EqualTo(8f));
            Assert.That(spec.ChaseHeight, Is.EqualTo(1.5f));
            Assert.That(spec.Seats, Is.EqualTo(_twoSeats));
            Assert.That(spec.DriverSeatIndex, Is.EqualTo(0));
            Assert.That(spec.Weapons, Is.Empty);
            Assert.That(spec.Parts, Is.Empty);
        }

        [Test]
        public void ToSpecNamesTheAssetWhenAValueIsInvalid()
        {
            var definition = Jeep();
            definition.MaxHealth = 0;

            var error = Assert.Throws<VehicleDefinitionException>(() => definition.ToSpec(_twoSeats));

            Assert.That(error.Message, Does.StartWith("vehicle 'Jeep' is invalid: "));
            Assert.That(error.InnerException, Is.InstanceOf<ArgumentOutOfRangeException>());
            Assert.Throws<ArgumentNullException>(() => definition.ToSpec(null));
        }

        [Test]
        public void CopyFromTakesTheSpecsValues()
        {
            var definition = NewDefinition("Copy");

            definition.CopyFrom(new VehicleSpec("tank", 4000, _twoSeats, null, null, 4f, 10f, 3f));

            Assert.That(definition.Id, Is.EqualTo("tank"));
            Assert.That(definition.MaxHealth, Is.EqualTo(4000));
            Assert.That(definition.EnterRange, Is.EqualTo(4f));
            Assert.That(definition.ChaseDistance, Is.EqualTo(10f));
            Assert.That(definition.ChaseHeight, Is.EqualTo(3f));
            Assert.Throws<ArgumentNullException>(() => definition.CopyFrom(null));
        }

        [Test]
        public void ANewDefinitionDoesNotWidenTheFieldOfView()
        {
            var definition = NewDefinition("Fresh");

            Assert.That(definition.SpeedFieldOfViewStartSpeed, Is.EqualTo(VehicleSpeedFieldOfViewSpec.DefaultStartSpeed));
            Assert.That(definition.SpeedFieldOfViewFullSpeed, Is.EqualTo(VehicleSpeedFieldOfViewSpec.DefaultFullSpeed));
            Assert.That(definition.SpeedFieldOfViewExtraDegrees, Is.EqualTo(0f));
            Assert.That(definition.SpeedFieldOfViewBlendSeconds, Is.EqualTo(VehicleSpeedFieldOfViewSpec.DefaultBlendSeconds));
            Assert.That(definition.ToSpec(_twoSeats).SpeedFieldOfView.IsEnabled, Is.False);
        }

        [Test]
        public void TheSpeedFieldOfViewSurvivesToSpecAndCopyFrom()
        {
            var definition = Jeep();
            definition.SpeedFieldOfViewStartSpeed = 8f;
            definition.SpeedFieldOfViewFullSpeed = 32f;
            definition.SpeedFieldOfViewExtraDegrees = 15f;
            definition.SpeedFieldOfViewBlendSeconds = 0.25f;

            var spec = definition.ToSpec(_twoSeats);

            Assert.That(spec.SpeedFieldOfView.StartSpeed, Is.EqualTo(8f));
            Assert.That(spec.SpeedFieldOfView.FullSpeed, Is.EqualTo(32f));
            Assert.That(spec.SpeedFieldOfView.ExtraDegrees, Is.EqualTo(15f));
            Assert.That(spec.SpeedFieldOfView.BlendSeconds, Is.EqualTo(0.25f));

            var copy = NewDefinition("Copy");
            copy.CopyFrom(spec);

            Assert.That(copy.SpeedFieldOfViewStartSpeed, Is.EqualTo(8f));
            Assert.That(copy.SpeedFieldOfViewFullSpeed, Is.EqualTo(32f));
            Assert.That(copy.SpeedFieldOfViewExtraDegrees, Is.EqualTo(15f));
            Assert.That(copy.SpeedFieldOfViewBlendSeconds, Is.EqualTo(0.25f));
            var again = copy.ToSpec(_twoSeats).SpeedFieldOfView;
            Assert.That(again.ExtraDegreesAt(20f), Is.EqualTo(spec.SpeedFieldOfView.ExtraDegreesAt(20f)));
        }

        [Test]
        public void AnInvalidSpeedFieldOfViewNamesTheAsset()
        {
            var definition = Jeep();
            definition.SpeedFieldOfViewStartSpeed = 30f;
            definition.SpeedFieldOfViewFullSpeed = 20f;

            var error = Assert.Throws<VehicleDefinitionException>(() => definition.ToSpec(_twoSeats));

            Assert.That(error.Message, Does.StartWith("vehicle 'Jeep' is invalid: "));
            Assert.That(error.InnerException, Is.InstanceOf<ArgumentOutOfRangeException>());
        }

        [Test]
        public void ANewDefinitionHasNoPartsAndTheDefaultDurability()
        {
            var definition = NewDefinition("Fresh");

            Assert.That(definition.Parts, Is.Empty);
            Assert.That(definition.SmokeHealthFraction, Is.EqualTo(VehicleDurabilitySpec.DefaultSmokeHealthFraction));
            Assert.That(definition.BurnHealthFraction, Is.EqualTo(VehicleDurabilitySpec.DefaultBurnHealthFraction));
            Assert.That(definition.BurnDamagePerSecond, Is.EqualTo(VehicleDurabilitySpec.DefaultBurnDamagePerSecond));
            Assert.That(definition.ExplosionMaxDamage, Is.EqualTo(VehicleDurabilitySpec.DefaultExplosionMaxDamage));
            Assert.That(definition.ExplosionRadius, Is.EqualTo(VehicleDurabilitySpec.DefaultExplosionRadius));
            Assert.That(definition.ExplosionFullDamageRadius, Is.EqualTo(VehicleDurabilitySpec.DefaultExplosionFullDamageRadius));
            Assert.That(definition.ExplosionStructureDamage, Is.EqualTo(VehicleDurabilitySpec.DefaultExplosionStructureDamage));
        }

        [Test]
        public void ToSpecCarriesThePartsAndTheDurability()
        {
            var definition = Armored();

            var spec = definition.ToSpec(_twoSeats);

            Assert.That(spec.Parts.Count, Is.EqualTo(2));
            Assert.That(spec.FindPart("engine").DamageMultiplier, Is.EqualTo(1.5f));
            Assert.That(spec.FindPart("fuel").DamageMultiplier, Is.EqualTo(2.5f));
            Assert.That(spec.Durability.SmokeHealthFraction, Is.EqualTo(0.6f));
            Assert.That(spec.Durability.BurnHealthFraction, Is.EqualTo(0.3f));
            Assert.That(spec.Durability.BurnDamagePerSecond, Is.EqualTo(40f));
            Assert.That(spec.Durability.Explosion.Damage.MaxDamage, Is.EqualTo(300));
            Assert.That(spec.Durability.Explosion.Damage.Radius, Is.EqualTo(10f));
            Assert.That(spec.Durability.Explosion.Damage.FullDamageRadius, Is.EqualTo(3f));
            Assert.That(spec.Durability.Explosion.StructureDamageAtCenter, Is.EqualTo(80));
        }

        [Test]
        public void InvalidPartsAndDurabilityNameTheAsset()
        {
            var burnsBeforeSmoking = Armored();
            burnsBeforeSmoking.BurnHealthFraction = 0.9f;
            var sameBlastRadii = Armored();
            sameBlastRadii.ExplosionFullDamageRadius = 10f;
            var twoEngines = Armored();
            twoEngines.Parts = new[] { new VehicleDefinition.Part("engine", 1f), new VehicleDefinition.Part("engine", 2f) };
            var emptyPart = Armored();
            emptyPart.Parts = new VehicleDefinition.Part[] { null };

            foreach (var definition in new[] { burnsBeforeSmoking, sameBlastRadii, twoEngines, emptyPart })
            {
                var error = Assert.Throws<VehicleDefinitionException>(() => definition.ToSpec(_twoSeats));
                Assert.That(error.Message, Does.StartWith("vehicle 'Armored' is invalid: "));
            }
        }

        [Test]
        public void CopyFromTakesThePartsAndTheDurability()
        {
            var source = Armored().ToSpec(_twoSeats);
            var definition = NewDefinition("Copy");

            definition.CopyFrom(source);

            Assert.That(definition.Parts.Length, Is.EqualTo(2));
            Assert.That(definition.Parts[1].Id, Is.EqualTo("fuel"));
            Assert.That(definition.Parts[1].DamageMultiplier, Is.EqualTo(2.5f));
            Assert.That(definition.BurnDamagePerSecond, Is.EqualTo(40f));
            Assert.That(definition.ExplosionRadius, Is.EqualTo(10f));
            Assert.That(definition.ToSpec(_twoSeats).Durability.Explosion.StructureDamageAtCenter, Is.EqualTo(80));
        }

        [Test]
        public void AVehicleWithoutADefinitionUsesItsInspectorValues()
        {
            var vehicle = NewVehicle("Car", out _);

            Assert.That(vehicle.Definition, Is.Null);
            Assert.That(vehicle.Spec.Id, Is.EqualTo(VehicleSpec.DefaultId));
            Assert.That(vehicle.Spec.MaxHealth, Is.EqualTo(VehicleSpec.DefaultMaxHealth));
            Assert.That(vehicle.Spec.EnterRange, Is.EqualTo(VehicleSpec.DefaultEnterRange));
        }

        [Test]
        public void ConfiguringWithADefinitionUsesItsValuesAndKeepsTheSeats()
        {
            var vehicle = NewVehicle("Car", out var damageable);
            var definition = Jeep();

            vehicle.Configure(definition);

            Assert.That(vehicle.Definition, Is.SameAs(definition));
            Assert.That(vehicle.VehicleId, Is.EqualTo("jeep"));
            Assert.That(vehicle.Spec.MaxHealth, Is.EqualTo(50));
            Assert.That(vehicle.Spec.EnterRange, Is.EqualTo(3f));
            Assert.That(vehicle.Spec.ChaseDistance, Is.EqualTo(8f));
            Assert.That(vehicle.Spec.ChaseHeight, Is.EqualTo(1.5f));
            Assert.That(vehicle.Spec.Seats.Count, Is.EqualTo(2));
            Assert.That(damageable.MaxHealth, Is.EqualTo(50));
            Assert.That(damageable.Health.Current, Is.EqualTo(50));
        }

        [Test]
        public void TheDefinitionsHealthWrecksTheVehicleAtFiftyDamage()
        {
            var vehicle = NewVehicle("Car", out var damageable);
            vehicle.Configure(Jeep());

            damageable.TakeDamage(49);
            Assert.That(vehicle.IsWrecked, Is.False);

            LogAssert.Expect(LogType.Log, "[Combat] target died id=car");
            damageable.TakeDamage(1);

            Assert.That(vehicle.IsWrecked, Is.True);
        }

        [Test]
        public void TheSpecIsRebuiltFromTheDefinitionAfterRefreshingTheSeats()
        {
            var vehicle = NewVehicle("Car", out _);
            vehicle.Configure(Jeep());

            vehicle.RefreshSeats();

            Assert.That(vehicle.Spec.Id, Is.EqualTo("jeep"));
            Assert.That(vehicle.Spec.MaxHealth, Is.EqualTo(50));
        }

        [Test]
        public void AnInvalidDefinitionIsRejectedAndChangesNothing()
        {
            var vehicle = NewVehicle("Car", out var damageable);
            var definition = Jeep();
            definition.EnterRange = 0f;

            Assert.Throws<VehicleDefinitionException>(() => vehicle.Configure(definition));
            Assert.Throws<ArgumentNullException>(() => vehicle.Configure((VehicleDefinition)null));

            Assert.That(vehicle.Definition, Is.Null);
            Assert.That(vehicle.Spec.Id, Is.EqualTo(VehicleSpec.DefaultId));
            Assert.That(damageable.MaxHealth, Is.EqualTo(200));
        }

        [Test]
        public void ADefinitionThatBecomesInvalidFallsBackToTheInspectorValuesWithAnError()
        {
            var vehicle = NewVehicle("Car", out _);
            var definition = Jeep();
            vehicle.Configure(definition);
            definition.MaxHealth = 0;

            vehicle.RefreshSeats();

            LogAssert.Expect(LogType.Error, new Regex(@"^\[Vehicle\] 'Car' の定義を使えないので Inspector の値を使います: vehicle 'Jeep' is invalid: "));
            Assert.That(vehicle.Spec.MaxHealth, Is.EqualTo(50), "the Inspector values were overwritten by the last Configure");
            Assert.That(vehicle.Spec.Id, Is.EqualTo("jeep"));
        }

        private VehicleDefinition Jeep()
        {
            var definition = NewDefinition("Jeep");
            definition.Id = "jeep";
            definition.MaxHealth = 50;
            definition.EnterRange = 3f;
            definition.ChaseDistance = 8f;
            definition.ChaseHeight = 1.5f;
            return definition;
        }

        private VehicleDefinition Armored()
        {
            var definition = NewDefinition("Armored");
            definition.Id = "armored";
            definition.Parts = new[] { new VehicleDefinition.Part("engine", 1.5f), new VehicleDefinition.Part("fuel", 2.5f) };
            definition.SmokeHealthFraction = 0.6f;
            definition.BurnHealthFraction = 0.3f;
            definition.BurnDamagePerSecond = 40f;
            definition.ExplosionMaxDamage = 300;
            definition.ExplosionRadius = 10f;
            definition.ExplosionFullDamageRadius = 3f;
            definition.ExplosionStructureDamage = 80;
            return definition;
        }

        private VehicleDefinition NewDefinition(string name)
        {
            var definition = ScriptableObject.CreateInstance<VehicleDefinition>();
            definition.name = name;
            _created.Add(definition);
            return definition;
        }

        private Vehicle NewVehicle(string name, out Damageable damageable)
        {
            var root = new GameObject(name);
            _created.Add(root);
            new GameObject("Driver").AddComponent<VehicleSeat>().transform.SetParent(root.transform, false);
            new GameObject("Passenger").AddComponent<VehicleSeat>().transform.SetParent(root.transform, false);
            damageable = root.AddComponent<Damageable>();
            damageable.Initialize(name.ToLowerInvariant(), 200);
            return root.AddComponent<Vehicle>();
        }
    }
}
