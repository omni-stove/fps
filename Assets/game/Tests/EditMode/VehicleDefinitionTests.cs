using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Fps.Core;
using Fps.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Fps.Tests.EditMode
{
    /// <summary>
    /// 乗り物の値の定義 (<see cref="VehicleDefinition"/>) が <see cref="VehicleSpec"/> に入ることと、
    /// <see cref="Vehicle"/> が定義を Inspector の値より先に使い、耐久を <see cref="Damageable"/> に入れること。
    /// </summary>
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
            Assert.That(definition.ChaseDistance, Is.EqualTo(VehicleSpec.DefaultChaseDistance));
            Assert.That(definition.ChaseHeight, Is.EqualTo(VehicleSpec.DefaultChaseHeight));
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
