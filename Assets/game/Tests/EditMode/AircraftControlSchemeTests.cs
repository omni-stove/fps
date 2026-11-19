using System;
using System.Collections.Generic;
using Shooter.Core;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    public sealed class AircraftControlSchemeTests
    {
        private const float Epsilon = 1e-4f;

        private readonly List<Object> _created = new List<Object>();
        private AircraftControlScheme _scheme;

        [SetUp]
        public void SetUp()
        {
            var aircraft = new GameObject("Aircraft");
            _created.Add(aircraft);
            _scheme = aircraft.AddComponent<AircraftControlScheme>();
            _scheme.Configure(new AircraftAimSpec(pitchGain: 2f, yawGain: 1f, rollGain: 3f, levelGain: 1f, throttleRate: 0.5f));
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
        public void ForwardOpensTheThrottleOverTime()
        {
            var input = _scheme.ToMotorInput(null, Seat(new Vector2(0f, 1f), Vector3.forward), 0.5f);

            Assert.That(_scheme.Throttle, Is.EqualTo(0.25f).Within(Epsilon));
            Assert.That(input[AircraftFlight.ThrottleAxis], Is.EqualTo(0.25f).Within(Epsilon));

            input = _scheme.ToMotorInput(null, Seat(Vector2.zero, Vector3.forward), 0.5f);
            Assert.That(input[AircraftFlight.ThrottleAxis], Is.EqualTo(0.25f).Within(Epsilon), "the throttle stays where it was left");
        }

        [Test]
        public void BackOnAClosedThrottleIsTheAirBrake()
        {
            _scheme.Throttle = 0.1f;

            var input = _scheme.ToMotorInput(null, Seat(new Vector2(0f, -1f), Vector3.forward), 1f);

            Assert.That(_scheme.Throttle, Is.EqualTo(0f));
            Assert.That(input[AircraftFlight.ThrottleAxis], Is.EqualTo(-1f));
        }

        [Test]
        public void TheAimIsReadFromTheAircraftsOwnFacing()
        {
            _scheme.transform.rotation = Quaternion.Euler(0f, 90f, 0f);

            var ahead = _scheme.ToMotorInput(null, Seat(Vector2.zero, Vector3.right), 0.02f);
            var up = _scheme.ToMotorInput(null, Seat(Vector2.zero, Vector3.up), 0.02f);

            Assert.That(ahead[AircraftFlight.PitchAxis], Is.EqualTo(0f).Within(Epsilon));
            Assert.That(ahead[AircraftFlight.YawAxis], Is.EqualTo(0f).Within(Epsilon));
            Assert.That(up[AircraftFlight.PitchAxis], Is.EqualTo(1f).Within(Epsilon));
        }

        [Test]
        public void ABankedAircraftLevelsAndSideKeysRoll()
        {
            _scheme.transform.rotation = Quaternion.Euler(0f, 0f, 30f);

            var level = _scheme.ToMotorInput(null, Seat(Vector2.zero, Vector3.zero), 0.02f);
            var pushed = _scheme.ToMotorInput(null, Seat(new Vector2(-1f, 0f), Vector3.zero), 0.02f);

            Assert.That(level[AircraftFlight.RollAxis], Is.EqualTo(0.5f).Within(Epsilon), "the right wing is up by sin 30");
            Assert.That(pushed[AircraftFlight.RollAxis], Is.EqualTo(-0.5f).Within(Epsilon));
        }

        [Test]
        public void TheVehicleUsesItWhenItIsOnTheSameObject()
        {
            Assert.That(VehicleControlSchemes.For(_scheme.gameObject), Is.SameAs(_scheme));
        }

        [Test]
        public void TheThrottleRejectsLevelsOutsideZeroAndOne()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => _scheme.Throttle = 1.5f);
            Assert.Throws<ArgumentOutOfRangeException>(() => _scheme.Throttle = -0.1f);
            Assert.Throws<ArgumentNullException>(() => _scheme.Configure(null));
        }

        private static VehicleSeatInput Seat(Vector2 move, Vector3 aim)
        {
            return new VehicleSeatInput(move, 0f, false, false, false, false, Vector3.zero, aim);
        }
    }
}
