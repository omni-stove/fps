using System.Collections.Generic;
using Shooter.Core;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    public sealed class HelicopterControlSchemeTests
    {
        private const float Epsilon = 1e-4f;

        private readonly List<Object> _created = new List<Object>();
        private HelicopterControlScheme _scheme;

        [SetUp]
        public void SetUp()
        {
            var helicopter = new GameObject("Helicopter");
            _created.Add(helicopter);
            _scheme = helicopter.AddComponent<HelicopterControlScheme>();
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
        public void TheVehicleUsesItWhenItIsOnTheSameObject()
        {
            Assert.That(VehicleControlSchemes.For(_scheme.gameObject), Is.SameAs(_scheme));
        }

        [Test]
        public void VerticalIsTheCollectiveAndMoveTiltsTheHelicopter()
        {
            var input = _scheme.ToMotorInput(null, Seat(new Vector2(-0.5f, 0.75f), 1f, Vector3.forward), 0.02f);

            Assert.That(input[HelicopterFlight.CollectiveAxis], Is.EqualTo(1f));
            Assert.That(input[HelicopterFlight.PitchAxis], Is.EqualTo(0.75f));
            Assert.That(input[HelicopterFlight.RollAxis], Is.EqualTo(-0.5f));
            Assert.That(input[HelicopterFlight.YawAxis], Is.EqualTo(0f).Within(Epsilon));
            Assert.That(input[HelicopterFlight.EngineAxis], Is.EqualTo(1f), "a pilot keeps the rotor turning");
        }

        [Test]
        public void ANeutralSeatStillRunsTheRotor()
        {
            var input = _scheme.ToMotorInput(null, VehicleSeatInput.Idle, 0.02f);

            Assert.That(input, Is.EqualTo(new VehicleMotorInput(0f, 0f, 0f, 0f, 1f)));
        }

        [Test]
        public void TheYawTurnsTowardTheAimWithTheDefaultAngle()
        {
            var half = Quaternion.Euler(0f, HelicopterSpec.DefaultYawAimAngle * 0.5f, 0f) * Vector3.forward;

            var right = _scheme.ToMotorInput(null, Seat(Vector2.zero, 0f, Vector3.right), 0.02f);
            var left = _scheme.ToMotorInput(null, Seat(Vector2.zero, 0f, -half), 0.02f);
            var partway = _scheme.ToMotorInput(null, Seat(Vector2.zero, 0f, half), 0.02f);

            Assert.That(right[HelicopterFlight.YawAxis], Is.EqualTo(1f));
            Assert.That(partway[HelicopterFlight.YawAxis], Is.EqualTo(0.5f).Within(Epsilon));
            Assert.That(left[HelicopterFlight.YawAxis], Is.EqualTo(-1f), "aiming behind and to the left turns left");
        }

        [Test]
        public void TheAimIsReadFromTheHelicoptersOwnHeading()
        {
            _scheme.transform.rotation = Quaternion.Euler(10f, 90f, 0f);

            var ahead = _scheme.ToMotorInput(null, Seat(Vector2.zero, 0f, Vector3.right), 0.02f);
            var north = _scheme.ToMotorInput(null, Seat(Vector2.zero, 0f, Vector3.forward), 0.02f);

            Assert.That(ahead[HelicopterFlight.YawAxis], Is.EqualTo(0f).Within(Epsilon));
            Assert.That(north[HelicopterFlight.YawAxis], Is.EqualTo(-1f), "facing east, north is to the left");
        }

        [Test]
        public void TheYawAngleIsReadFromTheMotor()
        {
            var motor = _scheme.gameObject.AddComponent<SimpleHelicopterMotor>();
            motor.Configure(new HelicopterSpec(yawAimAngle: 90f));
            var aim = Quaternion.Euler(0f, 45f, 0f) * Vector3.forward;

            var input = _scheme.ToMotorInput(null, Seat(Vector2.zero, 0f, aim), 0.02f);

            Assert.That(input[HelicopterFlight.YawAxis], Is.EqualTo(0.5f).Within(Epsilon));
        }

        [Test]
        public void WithoutAnAimItDoesNotTurn()
        {
            var input = _scheme.ToMotorInput(null, Seat(new Vector2(1f, 0f), 0f, Vector3.zero), 0.02f);

            Assert.That(input[HelicopterFlight.YawAxis], Is.EqualTo(0f));
            Assert.That(input[HelicopterFlight.RollAxis], Is.EqualTo(1f));
        }

        private static VehicleSeatInput Seat(Vector2 move, float vertical, Vector3 aim)
        {
            return new VehicleSeatInput(move, vertical, false, false, false, false, Vector3.zero, aim);
        }
    }
}
