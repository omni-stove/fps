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
        public void ResettingTheStateClosesTheThrottle()
        {
            _scheme.Throttle = 1f;

            ((IVehicleControlSchemeReset)_scheme).ResetState();
            var input = _scheme.ToMotorInput(null, Seat(Vector2.zero, Vector3.forward), 0.02f);

            Assert.That(_scheme.Throttle, Is.EqualTo(0f));
            Assert.That(input[AircraftFlight.ThrottleAxis], Is.EqualTo(0f));
        }

        [Test]
        public void TheThrottleRejectsLevelsOutsideZeroAndOne()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => _scheme.Throttle = 1.5f);
            Assert.Throws<ArgumentOutOfRangeException>(() => _scheme.Throttle = -0.1f);
            Assert.Throws<ArgumentNullException>(() => _scheme.Configure(null));
        }

        [Test]
        public void TheMotorsAngleOfAttackEasesOffThePitch()
        {
            var motor = AddMotor();
            _scheme.transform.position = new Vector3(0f, 5000f, 0f);
            motor.Body.linearVelocity = new Vector3(0f, -20f, 40f);

            var before = _scheme.ToMotorInput(null, Seat(Vector2.zero, Vector3.up), 0.02f);
            motor.Step();
            var after = _scheme.ToMotorInput(null, Seat(Vector2.zero, Vector3.up), 0.02f);

            Assert.That(motor.State.AngleOfAttackRadians * Mathf.Rad2Deg, Is.GreaterThan(AircraftAimSpec.DefaultCriticalAngleOfAttack));
            Assert.That(before[AircraftFlight.PitchAxis], Is.EqualTo(1f), "with no state yet the stick is untouched");
            Assert.That(after[AircraftFlight.PitchAxis], Is.EqualTo(0f), "past the stall angle the nose is not raised");
            Assert.That(
                after[AircraftFlight.PitchAxis],
                Is.EqualTo(AircraftAim.LimitPitch(_scheme.Spec, 1f, motor.State)),
                "the scheme passes the motor's state to the limiter");
        }

        [Test]
        public void DivingTowardTheGroundPullsUp()
        {
            var motor = AddMotor();
            var noseDown = Quaternion.Euler(45f, 0f, 0f);
            _scheme.transform.rotation = noseDown;
            motor.Body.rotation = noseDown;
            motor.Body.linearVelocity = (noseDown * Vector3.forward) * 40f;
            var ownBelly = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ownBelly.transform.SetParent(_scheme.transform, false);
            ownBelly.transform.localPosition = new Vector3(0f, -1f, 0f);
            ownBelly.transform.localScale = new Vector3(4f, 1f, 4f);
            Physics.SyncTransforms();
            var atTheGround = noseDown * Vector3.forward;

            var clear = _scheme.ToMotorInput(null, Seat(Vector2.zero, atTheGround), 0.02f);

            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _created.Add(ground);
            ground.transform.position = new Vector3(0f, -50.5f, 0f);
            ground.transform.localScale = new Vector3(200f, 1f, 200f);
            Physics.SyncTransforms();
            var pulled = _scheme.ToMotorInput(null, Seat(new Vector2(1f, 0f), atTheGround), 0.02f);

            _scheme.GroundMask = 0;
            var masked = _scheme.ToMotorInput(null, Seat(Vector2.zero, atTheGround), 0.02f);

            Assert.That(clear[AircraftFlight.PitchAxis], Is.EqualTo(0f).Within(Epsilon), "its own colliders are not the ground");
            Assert.That(pulled[AircraftFlight.PitchAxis], Is.EqualTo(1f), "50 m below at 28 m/s down is under 2 seconds");
            Assert.That(pulled[AircraftFlight.RollAxis], Is.EqualTo(0f).Within(Epsilon), "the roll keys wait until it has pulled up");
            Assert.That(masked[AircraftFlight.PitchAxis], Is.EqualTo(0f).Within(Epsilon), "layers outside the mask are not the ground");
        }

        [Test]
        public void AGentleDescentIsNotPulledUp()
        {
            var motor = AddMotor();
            motor.Body.linearVelocity = new Vector3(0f, -2f, 30f);
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _created.Add(ground);
            ground.transform.position = new Vector3(0f, -3.5f, 0f);
            ground.transform.localScale = new Vector3(200f, 1f, 200f);
            Physics.SyncTransforms();

            var input = _scheme.ToMotorInput(null, Seat(Vector2.zero, Vector3.forward), 0.02f);

            Assert.That(input[AircraftFlight.PitchAxis], Is.EqualTo(0f).Within(Epsilon));
        }

        private SimpleAircraftMotor AddMotor()
        {
            var motor = _scheme.gameObject.AddComponent<SimpleAircraftMotor>();
            motor.Body.useGravity = false;
            return motor;
        }

        private static VehicleSeatInput Seat(Vector2 move, Vector3 aim)
        {
            return new VehicleSeatInput(move, 0f, false, false, false, false, Vector3.zero, aim);
        }
    }
}
