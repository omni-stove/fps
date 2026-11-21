using System;
using System.Collections.Generic;
using NUnit.Framework;
using Shooter.Core;
using Shooter.Runtime;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    // 物理は進めず、Step が足もとの地面と入力から Rigidbody へ入れる速さを確かめる。重力も掛からないので、上下の速さは Step が入れた値のまま残る。
    public sealed class SimpleHorseMotorTests
    {
        private const float Epsilon = 1e-3f;
        private const float StepSeconds = 0.02f;

        private static readonly VehicleMotorInput FullThrottle = new VehicleMotorInput(1f);
        private static readonly VehicleMotorInput FullGallop = new VehicleMotorInput(1f, 0f, 1f);
        private static readonly VehicleMotorInput Jump = new VehicleMotorInput(0f, 0f, 0f, 1f);

        private readonly List<Object> _created = new List<Object>();
        private SimpleHorseMotor _motor;

        [SetUp]
        public void SetUp()
        {
            var horse = new GameObject("Horse");
            _created.Add(horse);
            _motor = horse.AddComponent<SimpleHorseMotor>();
            Place(Vector3.zero, Quaternion.identity);
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
        public void TheSpecIsBuiltFromTheDefaultsWhenNotConfigured()
        {
            Assert.That(_motor.Spec.Mass, Is.EqualTo(HorseSpec.DefaultMass));
            Assert.That(_motor.Spec.TrotSpeed, Is.EqualTo(HorseSpec.DefaultTrotSpeed));
            Assert.That(_motor.TopSpeed, Is.EqualTo(HorseSpec.DefaultCanterSpeed));
            Assert.That(_motor.Input, Is.EqualTo(VehicleMotorInput.Idle));
            Assert.That(_motor.Gait, Is.EqualTo(HorseGait.Idle));
            Assert.That(_motor, Is.InstanceOf<IVehicleMotor>());
            Assert.That(_motor, Is.InstanceOf<IVehicleTopSpeed>());
            Assert.That(_motor.GetComponent<Rigidbody>(), Is.Not.Null);
        }

        [Test]
        public void ConfigureKeepsTheSpecAndSetsTheMass()
        {
            var spec = new HorseSpec(mass: 700f, canterSpeed: 12f);

            _motor.Configure(spec);

            Assert.That(_motor.Spec, Is.SameAs(spec));
            Assert.That(_motor.Body.mass, Is.EqualTo(700f));
            Assert.That(_motor.TopSpeed, Is.EqualTo(12f));
            Assert.Throws<ArgumentNullException>(() => _motor.Configure(null));
        }

        [Test]
        public void TheSpecIsReadFromTheDefinitionWhenOneIsSet()
        {
            var definition = ScriptableObject.CreateInstance<HorseDefinition>();
            _created.Add(definition);
            definition.CopyFrom(new HorseSpec(mass: 420f, canterSpeed: 10f, jumpSpeed: 3f));
            _motor.Configure(new HorseSpec(jumpSpeed: 8f));

            _motor.Definition = definition;

            Assert.That(_motor.Spec.Mass, Is.EqualTo(420f));
            Assert.That(_motor.Spec.JumpSpeed, Is.EqualTo(3f), "setting the asset replaces a configured spec");
            Assert.That(_motor.Body.mass, Is.EqualTo(420f));
            Assert.That(_motor.TopSpeed, Is.EqualTo(10f));
            _motor.Configure(new HorseSpec(jumpSpeed: 8f));
            Assert.That(_motor.Spec.JumpSpeed, Is.EqualTo(8f), "Configure still wins over the asset");
        }

        [Test]
        public void OnFlatGroundFullThrottleSettlesAtTheTrotSpeed()
        {
            AddFloor();

            var fastest = Drive(FullThrottle, 3f);

            Assert.That(_motor.IsGrounded, Is.True);
            Assert.That(fastest, Is.LessThanOrEqualTo(HorseSpec.DefaultTrotSpeed + Epsilon), "throttle alone never passes the trot");
            Assert.That(_motor.ForwardSpeed, Is.EqualTo(HorseSpec.DefaultTrotSpeed).Within(Epsilon));
            Assert.That(_motor.Body.linearVelocity.z, Is.EqualTo(HorseSpec.DefaultTrotSpeed).Within(Epsilon));
            Assert.That(_motor.Body.linearVelocity.x, Is.EqualTo(0f).Within(Epsilon));
            Assert.That(_motor.Body.linearVelocity.y, Is.EqualTo(0f).Within(Epsilon));
            Assert.That(_motor.Gait, Is.EqualTo(HorseGait.Trot));
        }

        [Test]
        public void GallopRaisesTheSpeedToTheCanterAndReleasingStopsIt()
        {
            AddFloor();
            Drive(FullThrottle, 3f);

            var fastest = Drive(FullGallop, 3f);

            Assert.That(fastest, Is.LessThanOrEqualTo(HorseSpec.DefaultCanterSpeed + Epsilon));
            Assert.That(_motor.ForwardSpeed, Is.EqualTo(HorseSpec.DefaultCanterSpeed).Within(Epsilon));
            Assert.That(_motor.ForwardSpeed, Is.EqualTo(_motor.TopSpeed).Within(Epsilon));
            Assert.That(_motor.Gait, Is.EqualTo(HorseGait.Canter));

            Drive(VehicleMotorInput.Idle, 3f);

            Assert.That(_motor.ForwardSpeed, Is.EqualTo(0f).Within(Epsilon));
            Assert.That(_motor.Body.linearVelocity.magnitude, Is.EqualTo(0f).Within(Epsilon));
            Assert.That(_motor.Gait, Is.EqualTo(HorseGait.Idle));
        }

        [Test]
        public void TheSpeedFollowsTheHorsesHeading()
        {
            AddFloor();
            Place(Vector3.zero, Quaternion.Euler(0f, 90f, 0f));

            Drive(FullThrottle, 3f);

            Assert.That(_motor.Body.linearVelocity.x, Is.EqualTo(HorseSpec.DefaultTrotSpeed).Within(Epsilon), "the head faces +x");
            Assert.That(_motor.Body.linearVelocity.z, Is.EqualTo(0f).Within(Epsilon));
        }

        [Test]
        public void ReverseThrottleBacksUpAtTheReverseSpeed()
        {
            AddFloor();

            Drive(new VehicleMotorInput(-1f), 2f);

            Assert.That(_motor.ForwardSpeed, Is.EqualTo(-HorseSpec.DefaultReverseSpeed).Within(Epsilon));
            Assert.That(_motor.Gait, Is.EqualTo(HorseGait.Walk));
        }

        [Test]
        public void SidewaysSlidingIsBrakedOnTheGround()
        {
            AddFloor();
            _motor.Body.linearVelocity = new Vector3(2f, 0f, 0f);

            _motor.Step(0.1f);
            Assert.That(_motor.Body.linearVelocity.x, Is.EqualTo(2f - (HorseSpec.DefaultDeceleration * 0.1f)).Within(Epsilon));

            Drive(VehicleMotorInput.Idle, 1f);
            Assert.That(_motor.Body.linearVelocity.x, Is.EqualTo(0f).Within(Epsilon));
        }

        [Test]
        public void TurningSpinsOnlyAboutTheVerticalAxis()
        {
            AddFloor();
            _motor.SetInput(new VehicleMotorInput(0f, 1f));

            _motor.Step(StepSeconds);

            var spin = _motor.Body.angularVelocity;
            Assert.That(spin.y, Is.EqualTo(HorseSpec.DefaultTurnRate * Mathf.Deg2Rad).Within(Epsilon));
            Assert.That(spin.x, Is.EqualTo(0f));
            Assert.That(spin.z, Is.EqualTo(0f));
            Assert.That(_motor.Body.constraints & RigidbodyConstraints.FreezeRotationX, Is.EqualTo(RigidbodyConstraints.FreezeRotationX));
            Assert.That(_motor.Body.constraints & RigidbodyConstraints.FreezeRotationZ, Is.EqualTo(RigidbodyConstraints.FreezeRotationZ));
            Assert.That(_motor.Body.constraints & RigidbodyConstraints.FreezeRotationY, Is.EqualTo(RigidbodyConstraints.None), "yaw stays free");

            _motor.SetInput(new VehicleMotorInput(0f, -1f));
            _motor.Step(StepSeconds);
            Assert.That(_motor.Body.angularVelocity.y, Is.EqualTo(-HorseSpec.DefaultTurnRate * Mathf.Deg2Rad).Within(Epsilon));
        }

        [Test]
        public void TurningIsSlowerAtTheCanter()
        {
            AddFloor();
            Drive(FullGallop, 6f);
            _motor.SetInput(new VehicleMotorInput(1f, 1f, 1f));

            _motor.Step(StepSeconds);

            var expected = HorseSpec.DefaultTurnRate * HorseSpec.DefaultCanterTurnScale * Mathf.Deg2Rad;
            Assert.That(_motor.Body.angularVelocity.y, Is.EqualTo(expected).Within(Epsilon));
        }

        [Test]
        public void OnTheGroundPressingJumpLaunchesOnce()
        {
            AddFloor();
            _motor.SetInput(Jump);

            _motor.Step(StepSeconds);

            Assert.That(_motor.Body.linearVelocity.y, Is.EqualTo(HorseSpec.DefaultJumpSpeed).Within(Epsilon));
            Assert.That(_motor.IsGrounded, Is.False, "rising off the ground is not standing on it");

            // 昇っている途中を作る。足もとのレイはまだ床に届いているが、もう一度押しても跳び直さない。
            _motor.Body.linearVelocity = new Vector3(0f, 2f, 0f);
            _motor.SetInput(VehicleMotorInput.Idle);
            _motor.SetInput(Jump);
            _motor.Step(StepSeconds);

            Assert.That(_motor.Body.linearVelocity.y, Is.EqualTo(2f).Within(Epsilon), "a second press while rising does not jump again");
        }

        [Test]
        public void InTheAirJumpDoesNothing()
        {
            AddFloor();
            Place(new Vector3(0f, 3f, 0f), Quaternion.identity);
            _motor.SetInput(Jump);

            _motor.Step(StepSeconds);

            Assert.That(_motor.IsGrounded, Is.False);
            Assert.That(_motor.Body.linearVelocity.y, Is.EqualTo(0f).Within(Epsilon));

            Place(Vector3.zero, Quaternion.identity);
            _motor.Step(StepSeconds);
            Assert.That(_motor.IsGrounded, Is.True);
            Assert.That(_motor.Body.linearVelocity.y, Is.EqualTo(0f).Within(Epsilon), "a press made in the air is not saved for the landing");
        }

        [Test]
        public void HoldingJumpDoesNotBounceAfterLanding()
        {
            AddFloor();
            _motor.SetInput(Jump);
            _motor.Step(StepSeconds);

            _motor.Body.linearVelocity = Vector3.zero;
            _motor.SetInput(Jump);
            _motor.Step(StepSeconds);

            Assert.That(_motor.IsGrounded, Is.True, "falling back to the floor lands");
            Assert.That(_motor.Body.linearVelocity.y, Is.EqualTo(0f).Within(Epsilon));
        }

        [Test]
        public void InTheAirTheThrottleDoesNotChangeTheSpeed()
        {
            AddFloor();
            Place(new Vector3(0f, 3f, 0f), Quaternion.identity);
            _motor.Body.linearVelocity = new Vector3(0.5f, 0f, 3f);

            Drive(FullGallop, 1f);

            Assert.That(_motor.Body.linearVelocity.z, Is.EqualTo(3f).Within(Epsilon));
            Assert.That(_motor.Body.linearVelocity.x, Is.EqualTo(0.5f).Within(Epsilon));
            Assert.That(_motor.Gait, Is.EqualTo(HorseGait.Trot), "the gait still follows the forward speed");
        }

        [Test]
        public void ItsOwnCollidersAreNotTheGround()
        {
            var hoof = GameObject.CreatePrimitive(PrimitiveType.Cube);
            hoof.transform.SetParent(_motor.transform, false);
            hoof.transform.localPosition = new Vector3(0f, -0.1f, 0f);
            Physics.SyncTransforms();

            _motor.SetInput(Jump);
            _motor.Step(StepSeconds);

            Assert.That(_motor.IsGrounded, Is.False);
            Assert.That(_motor.Body.linearVelocity.y, Is.EqualTo(0f).Within(Epsilon));
        }

        [Test]
        public void GroundOutsideTheMaskIsNotStoodOn()
        {
            AddFloor();
            _motor.GroundMask = 0;

            _motor.Step(StepSeconds);

            Assert.That(_motor.IsGrounded, Is.False);
            Assert.Throws<ArgumentOutOfRangeException>(() => _motor.GroundDistance = -1f);
            Assert.Throws<ArgumentOutOfRangeException>(() => _motor.GroundProbeHeight = float.NaN);
        }

        [Test]
        public void ASlopeSteeperThanTheLimitIsNotClimbed()
        {
            AddRamp(50f);

            Drive(FullThrottle, 1f);

            Assert.That(_motor.IsGrounded, Is.True);
            Assert.That(Vector3.Angle(_motor.GroundNormal, Vector3.up), Is.EqualTo(50f).Within(0.1f));
            Assert.That(_motor.ForwardSpeed, Is.EqualTo(0f).Within(Epsilon));
            Assert.That(_motor.Body.linearVelocity.magnitude, Is.EqualTo(0f).Within(Epsilon));
        }

        [Test]
        public void ASteepSlopeCanStillBeWalkedDown()
        {
            AddRamp(50f);
            Place(Vector3.zero, Quaternion.Euler(0f, 180f, 0f));

            Drive(FullThrottle, 3f);

            Assert.That(_motor.ForwardSpeed, Is.EqualTo(HorseSpec.DefaultTrotSpeed).Within(Epsilon));
        }

        [Test]
        public void AGentleSlopeIsClimbed()
        {
            AddRamp(20f);

            Drive(FullThrottle, 3f);

            Assert.That(_motor.IsGrounded, Is.True);
            Assert.That(_motor.ForwardSpeed, Is.EqualTo(HorseSpec.DefaultTrotSpeed).Within(Epsilon));
        }

        [Test]
        public void AKinematicBodyComputesTheGaitButIsNotMoved()
        {
            AddFloor();
            _motor.Body.isKinematic = true;
            _motor.SetInput(FullThrottle);

            _motor.Step(StepSeconds);

            Assert.That(_motor.ForwardSpeed, Is.EqualTo(HorseSpec.DefaultAcceleration * StepSeconds).Within(Epsilon));
            Assert.That(_motor.Body.linearVelocity, Is.EqualTo(Vector3.zero));
        }

        [Test]
        public void StepRejectsANegativeOrEndlessDeltaTime()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => _motor.Step(-0.01f));
            Assert.Throws<ArgumentOutOfRangeException>(() => _motor.Step(float.PositiveInfinity));
            Assert.Throws<ArgumentOutOfRangeException>(() => _motor.Step(float.NaN));
        }

        // 走らせた間の前向きの速さの最大を返す。
        private float Drive(VehicleMotorInput input, float seconds)
        {
            _motor.SetInput(input);
            var fastest = float.NegativeInfinity;
            var steps = Mathf.RoundToInt(seconds / StepSeconds);
            for (var i = 0; i < steps; i++)
            {
                _motor.Step(StepSeconds);
                fastest = Mathf.Max(fastest, _motor.ForwardSpeed);
            }

            return fastest;
        }

        // 上の面が高さ 0 を通る広い床。
        private void AddFloor()
        {
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _created.Add(floor);
            floor.transform.position = new Vector3(0f, -0.5f, 0f);
            floor.transform.localScale = new Vector3(200f, 1f, 200f);
            Physics.SyncTransforms();
        }

        // +z へ向かって登る坂。上の面が原点を通る。
        private void AddRamp(float degrees)
        {
            var ramp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _created.Add(ramp);
            var rotation = Quaternion.Euler(-degrees, 0f, 0f);
            ramp.transform.SetPositionAndRotation(-(rotation * Vector3.up) * 0.5f, rotation);
            ramp.transform.localScale = new Vector3(40f, 1f, 40f);
            Physics.SyncTransforms();
        }

        private void Place(Vector3 position, Quaternion rotation)
        {
            _motor.transform.SetPositionAndRotation(position, rotation);
            _motor.Body.position = position;
            _motor.Body.rotation = rotation;
            Physics.SyncTransforms();
        }
    }
}
