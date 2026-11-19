using System;
using System.Collections.Generic;
using Shooter.Core;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    public sealed class AircraftStickControlSchemeTests
    {
        private const float Epsilon = 1e-4f;

        private readonly List<Object> _created = new List<Object>();
        private AircraftStickControlScheme _scheme;

        [SetUp]
        public void SetUp()
        {
            var aircraft = new GameObject("Aircraft");
            _created.Add(aircraft);
            _scheme = aircraft.AddComponent<AircraftStickControlScheme>();
            _scheme.Configure(new AircraftStickSpec(deadzone: 0.2f, exponent: 2f, throttleRate: 0.5f, forwardPitchesDown: false));
        }

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
        public void TheThrottleRisesOnlyWhileBoostIsHeld()
        {
            var held = _scheme.ToMotorInput(null, Seat(Vector2.zero, 0f, boost: true), 0.5f);

            Assert.That(_scheme.Throttle, Is.EqualTo(0.25f).Within(Epsilon));
            Assert.That(held[AircraftFlight.ThrottleAxis], Is.EqualTo(0.25f).Within(Epsilon));

            _scheme.ToMotorInput(null, Seat(Vector2.zero, 0f, boost: true), 0.5f);
            Assert.That(_scheme.Throttle, Is.EqualTo(0.5f).Within(Epsilon));

            var released = _scheme.ToMotorInput(null, Seat(Vector2.zero, 0f, boost: false), 0.2f);
            Assert.That(_scheme.Throttle, Is.EqualTo(0.4f).Within(Epsilon), "letting go closes it at the same rate");
            Assert.That(released[AircraftFlight.ThrottleAxis], Is.EqualTo(0.4f).Within(Epsilon));
        }

        [Test]
        public void WithoutBoostTheThrottleStaysClosed()
        {
            var input = _scheme.ToMotorInput(null, Seat(new Vector2(0f, 1f), 0f, boost: false), 1f);

            Assert.That(_scheme.Throttle, Is.EqualTo(0f));
            Assert.That(input[AircraftFlight.ThrottleAxis], Is.EqualTo(0f));
        }

        [Test]
        public void ResettingTheStateClosesTheThrottle()
        {
            _scheme.ToMotorInput(null, Seat(Vector2.zero, 0f, boost: true), 1f);
            Assert.That(_scheme.Throttle, Is.GreaterThan(0f));

            ((IVehicleControlSchemeReset)_scheme).ResetState();
            var input = _scheme.ToMotorInput(null, Seat(Vector2.zero, 0f, boost: false), 0.02f);

            Assert.That(_scheme.Throttle, Is.EqualTo(0f));
            Assert.That(input[AircraftFlight.ThrottleAxis], Is.EqualTo(0f));
        }

        [Test]
        public void MoveIsPitchAndRollAndVerticalIsTheRudder()
        {
            var input = _scheme.ToMotorInput(null, Seat(new Vector2(-1f, 0.6f), 1f, boost: false), 0.02f);

            Assert.That(input[AircraftFlight.PitchAxis], Is.EqualTo(0.25f).Within(Epsilon));
            Assert.That(input[AircraftFlight.RollAxis], Is.EqualTo(-1f).Within(Epsilon));
            Assert.That(input[AircraftFlight.YawAxis], Is.EqualTo(1f).Within(Epsilon));
        }

        [Test]
        public void TheDeadzoneKeepsASmallPushStill()
        {
            var input = _scheme.ToMotorInput(null, Seat(new Vector2(0.1f, -0.15f), 0.2f, boost: false), 0.02f);

            Assert.That(input[AircraftFlight.PitchAxis], Is.EqualTo(0f));
            Assert.That(input[AircraftFlight.RollAxis], Is.EqualTo(0f));
            Assert.That(input[AircraftFlight.YawAxis], Is.EqualTo(0f));
        }

        [Test]
        public void TheSpecIsReadFromTheDefinitionsStickBlock()
        {
            var aircraft = new GameObject("Plain");
            _created.Add(aircraft);
            var scheme = aircraft.AddComponent<AircraftStickControlScheme>();
            var definition = ScriptableObject.CreateInstance<AircraftDefinition>();
            definition.name = "Trainer";
            _created.Add(definition);
            definition.Stick.CopyFrom(new AircraftStickSpec(deadzone: 0.3f, exponent: 3f, throttleRate: 2f, forwardPitchesDown: false));

            Assert.That(scheme.Spec.Deadzone, Is.EqualTo(AircraftStickSpec.DefaultDeadzone), "no asset uses the defaults");
            scheme.Definition = definition;

            Assert.That(scheme.Spec.Deadzone, Is.EqualTo(0.3f));
            Assert.That(scheme.Spec.Exponent, Is.EqualTo(3f));
            Assert.That(scheme.Spec.ThrottleRate, Is.EqualTo(2f));
            Assert.That(scheme.Spec.ForwardPitchesDown, Is.False);
        }

        [Test]
        public void TheVehicleUsesItWhenItIsOnTheSameObject()
        {
            Assert.That(VehicleControlSchemes.For(_scheme.gameObject), Is.SameAs(_scheme));
        }

        [Test]
        public void ConfigureRejectsAMissingSpec()
        {
            Assert.Throws<ArgumentNullException>(() => _scheme.Configure(null));
        }

        private static VehicleSeatInput Seat(Vector2 move, float vertical, bool boost)
        {
            return new VehicleSeatInput(move, vertical, boost, false, false, false, Vector3.zero, Vector3.forward);
        }
    }
}
