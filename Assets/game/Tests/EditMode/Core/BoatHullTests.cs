using System;
using System.Numerics;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    // 物理エンジンは使わず、テストの中で力から速さと位置を進めて、浮いて止まる・進む・曲がる・傾きが戻るを確かめる。
    public sealed class BoatHullTests
    {
        private const float _gravity = 9.81f;
        private const float _step = 0.02f;

        // 水面を高さ 0 に置いた広い水。既定の船は底が原点の 0.5 m 下にあり、喫水 0.3 m まで沈むと原点は 0.2 m に来る。
        private static readonly BoxWater _sea = new BoxWater(new WaterBox(new Vector3(-100f, -10f, -100f), new Vector3(100f, 5f, 100f), 0f));
        private static readonly float _restHeight = -BoatSpec.DefaultDraft - BoatSpec.DefaultHullBottom;

        [Test]
        public void TheDefaultSpecKeepsTheDefaults()
        {
            var spec = BoatSpec.Default;

            Assert.That(spec.Mass, Is.EqualTo(800f));
            Assert.That(spec.Length, Is.EqualTo(4f));
            Assert.That(spec.Width, Is.EqualTo(2f));
            Assert.That(spec.HullBottom, Is.EqualTo(-0.5f));
            Assert.That(spec.HullHeight, Is.EqualTo(1f));
            Assert.That(spec.Draft, Is.EqualTo(0.3f));
            Assert.That(spec.HeaveDamping, Is.EqualTo(4f));
            Assert.That(spec.ForwardDrag, Is.EqualTo(0.3f));
            Assert.That(spec.SideDrag, Is.EqualTo(3f));
            Assert.That(spec.Thrust, Is.EqualTo(6f));
            Assert.That(spec.MaxForwardSpeed, Is.EqualTo(14f));
            Assert.That(spec.MaxReverseSpeed, Is.EqualTo(4f));
            Assert.That(spec.TurnRate, Is.EqualTo(50f));
            Assert.That(spec.TurnSpeed, Is.EqualTo(3f));
            Assert.That(spec.TurnResponsiveness, Is.EqualTo(2f));
        }

        [Test]
        public void TheSpecRejectsValuesOutOfRange()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new BoatSpec(mass: 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new BoatSpec(length: -1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new BoatSpec(width: float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => new BoatSpec(hullBottom: float.PositiveInfinity));
            Assert.Throws<ArgumentOutOfRangeException>(() => new BoatSpec(hullHeight: 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new BoatSpec(draft: 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new BoatSpec(hullHeight: 1f, draft: 1.5f), "a draft deeper than the hull can never float");
            Assert.Throws<ArgumentOutOfRangeException>(() => new BoatSpec(heaveDamping: -1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new BoatSpec(forwardDrag: -1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new BoatSpec(sideDrag: float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => new BoatSpec(thrust: -1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new BoatSpec(maxForwardSpeed: -1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new BoatSpec(maxReverseSpeed: -1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new BoatSpec(turnRate: -1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new BoatSpec(turnSpeed: 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new BoatSpec(turnResponsiveness: -1f));
            Assert.DoesNotThrow(() => new BoatSpec(hullHeight: 1f, draft: 1f));
            Assert.DoesNotThrow(() => new BoatSpec(hullBottom: 0.25f));
        }

        [Test]
        public void TheHullPointsSitAtTheCornersOfTheBottomAndThePropellerAtTheStern()
        {
            var spec = new BoatSpec(length: 6f, width: 3f, hullBottom: -0.4f);

            Assert.That(spec.HullPoint(0), Is.EqualTo(new Vector3(-1.5f, -0.4f, 3f)));
            Assert.That(spec.HullPoint(1), Is.EqualTo(new Vector3(1.5f, -0.4f, 3f)));
            Assert.That(spec.HullPoint(2), Is.EqualTo(new Vector3(-1.5f, -0.4f, -3f)));
            Assert.That(spec.HullPoint(3), Is.EqualTo(new Vector3(1.5f, -0.4f, -3f)));
            Assert.That(spec.Propeller, Is.EqualTo(new Vector3(0f, -0.4f, -3f)));
            Assert.Throws<ArgumentOutOfRangeException>(() => spec.HullPoint(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => spec.HullPoint(BoatSpec.HullPointCount));
        }

        [Test]
        public void TheDepthIsZeroOutOfTheWaterAndStopsAtTheHullHeight()
        {
            var spec = BoatSpec.Default;

            Assert.That(BoatHull.Depth(spec, _sea, new Vector3(0f, 1f, 0f)), Is.EqualTo(0f), "above the surface");
            Assert.That(BoatHull.Depth(spec, _sea, new Vector3(0f, -0.25f, 0f)), Is.EqualTo(0.25f).Within(1e-5f));
            Assert.That(BoatHull.Depth(spec, _sea, new Vector3(0f, -3f, 0f)), Is.EqualTo(spec.HullHeight), "fully under");
            Assert.That(BoatHull.Depth(spec, _sea, new Vector3(500f, -1f, 0f)), Is.EqualTo(0f), "outside every water");
            Assert.That(BoatHull.Depth(spec, NoWaterVolumes.Instance, new Vector3(0f, -1f, 0f)), Is.EqualTo(0f));
        }

        [Test]
        public void AtTheDraftTheBuoyancyCarriesTheWholeWeight()
        {
            var spec = BoatSpec.Default;

            var command = BoatHull.Compute(spec, VehicleMotorInput.Idle, Level(_restHeight), _sea, _gravity);

            Assert.That(command.Force.Y, Is.EqualTo(spec.Mass * _gravity).Within(0.5f));
            Assert.That(command.Force.X, Is.EqualTo(0f).Within(1e-3f));
            Assert.That(command.Force.Z, Is.EqualTo(0f).Within(1e-3f));
            Assert.That(command.Torque.Length(), Is.EqualTo(0f).Within(0.5f), "a level hull pushes evenly at every corner");
            Assert.That(command.AngularAcceleration, Is.EqualTo(Vector3.Zero));
            Assert.That(command.WetFraction, Is.EqualTo(1f).Within(1e-4f));
            Assert.That(command.PropellerImmersion, Is.EqualTo(1f).Within(1e-4f));
            Assert.That(command.IsAfloat, Is.True);
        }

        [Test]
        public void DroppedOnTheSurfaceItFloatsAndComesToRestAtTheDraft()
        {
            var spec = BoatSpec.Default;
            var height = -spec.HullBottom;
            var velocity = 0f;

            for (var i = 0; i < 1000; i++)
            {
                var command = BoatHull.Compute(spec, VehicleMotorInput.Idle, Level(height, new Vector3(0f, velocity, 0f)), _sea, _gravity);
                velocity += ((command.Force.Y / spec.Mass) - _gravity) * _step;
                height += velocity * _step;
            }

            Assert.That(height, Is.EqualTo(_restHeight).Within(0.005f));
            Assert.That(Math.Abs(velocity), Is.LessThan(0.01f));
        }

        [Test]
        public void SinkingFasterIsHeldBackByTheWater()
        {
            var spec = BoatSpec.Default;

            var still = BoatHull.Compute(spec, VehicleMotorInput.Idle, Level(_restHeight), _sea, _gravity);
            var sinking = BoatHull.Compute(spec, VehicleMotorInput.Idle, Level(_restHeight, new Vector3(0f, -1f, 0f)), _sea, _gravity);

            Assert.That(sinking.Force.Y - still.Force.Y, Is.EqualTo(spec.Mass * spec.HeaveDamping).Within(0.5f));
        }

        [Test]
        public void FullThrottleFromRestPushesAlongTheBow()
        {
            var spec = BoatSpec.Default;
            var input = VehicleMotorInput.Idle.With(BoatHull.ThrottleAxis, 1f);

            var ahead = BoatHull.Compute(spec, input, Level(_restHeight), _sea, _gravity);
            var turned = BoatHull.Compute(spec, input, Level(_restHeight, yawDegrees: 90f), _sea, _gravity);

            Assert.That(ahead.ThrustAcceleration, Is.EqualTo(spec.Thrust).Within(1e-4f));
            Assert.That(ahead.Force.Z, Is.EqualTo(spec.Mass * spec.Thrust).Within(0.5f));
            Assert.That(ahead.Force.X, Is.EqualTo(0f).Within(1e-3f));
            Assert.That(turned.Force.X, Is.EqualTo(spec.Mass * spec.Thrust).Within(0.5f), "a bow turned to +x is pushed along +x");
            Assert.That(turned.Force.Z, Is.EqualTo(0f).Within(0.5f));
        }

        [Test]
        public void TheThrustFadesTowardTheTopSpeed()
        {
            var spec = new BoatSpec(thrust: 10f, maxForwardSpeed: 10f, maxReverseSpeed: 4f);

            Assert.That(BoatHull.ThrustAcceleration(spec, 1f, 0f), Is.EqualTo(10f));
            Assert.That(BoatHull.ThrustAcceleration(spec, 1f, 5f), Is.EqualTo(5f).Within(1e-5f));
            Assert.That(BoatHull.ThrustAcceleration(spec, 1f, 10f), Is.EqualTo(0f));
            Assert.That(BoatHull.ThrustAcceleration(spec, 1f, 12f), Is.EqualTo(0f), "no thrust past the top speed");
            Assert.That(BoatHull.ThrustAcceleration(spec, 0.5f, -2f), Is.EqualTo(5f).Within(1e-5f), "going backward the forward thrust is not cut");
            Assert.That(BoatHull.ThrustAcceleration(spec, -1f, -2f), Is.EqualTo(-5f).Within(1e-5f));
            Assert.That(BoatHull.ThrustAcceleration(spec, -1f, -4f), Is.EqualTo(0f));
            Assert.That(BoatHull.ThrustAcceleration(spec, 0f, 3f), Is.EqualTo(0f));
            Assert.That(BoatHull.ThrustAcceleration(new BoatSpec(maxForwardSpeed: 0f), 1f, 0f), Is.EqualTo(0f));
        }

        [Test]
        public void TheRudderTurnsTowardTheSteerAndBackwardWhenReversing()
        {
            var spec = BoatSpec.Default;
            var expected = spec.TurnRate * (float)(Math.PI / 180.0) * spec.TurnResponsiveness;

            var right = BoatHull.Compute(spec, new VehicleMotorInput(1f, 1f), Level(_restHeight), _sea, _gravity);
            var left = BoatHull.Compute(spec, new VehicleMotorInput(1f, -1f), Level(_restHeight), _sea, _gravity);
            var reverse = BoatHull.Compute(spec, new VehicleMotorInput(-1f, 1f), Level(_restHeight), _sea, _gravity);
            var drifting = BoatHull.Compute(spec, new VehicleMotorInput(0f, 1f), Level(_restHeight), _sea, _gravity);

            Assert.That(right.AngularAcceleration.Y, Is.EqualTo(expected).Within(1e-4f));
            Assert.That(left.AngularAcceleration.Y, Is.EqualTo(-expected).Within(1e-4f));
            Assert.That(reverse.AngularAcceleration.Y, Is.EqualTo(-expected).Within(1e-4f), "backing up with the rudder right swings the bow left");
            Assert.That(drifting.AngularAcceleration.Y, Is.EqualTo(0f), "at rest without throttle the rudder bites no water");
        }

        [Test]
        public void WithoutSteeringTheWaterStopsASpin()
        {
            var spec = BoatSpec.Default;

            var command = BoatHull.Compute(spec, VehicleMotorInput.Idle, Level(_restHeight, angularVelocity: new Vector3(0f, 1f, 0f)), _sea, _gravity);

            Assert.That(command.AngularAcceleration.Y, Is.EqualTo(-spec.TurnResponsiveness).Within(1e-4f));
        }

        [Test]
        public void HoldingThrottleAndRudderDrivesForwardAndTurnsRight()
        {
            var spec = BoatSpec.Default;
            var input = new VehicleMotorInput(1f, 1f);
            var position = new Vector3(0f, _restHeight, 0f);
            var velocity = Vector3.Zero;
            var yaw = 0f;
            var yawRate = 0f;

            for (var i = 0; i < 250; i++)
            {
                var rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, yaw);
                var state = new BoatHullState(position, rotation, position, velocity, new Vector3(0f, yawRate, 0f));
                var command = BoatHull.Compute(spec, input, state, _sea, _gravity);
                var acceleration = command.Force / spec.Mass;
                velocity += new Vector3(acceleration.X, 0f, acceleration.Z) * _step;
                position += velocity * _step;
                yawRate += command.AngularAcceleration.Y * _step;
                yaw += yawRate * _step;
            }

            var heading = Vector3.Transform(Vector3.UnitZ, Quaternion.CreateFromAxisAngle(Vector3.UnitY, yaw));
            Assert.That(yawRate, Is.EqualTo(spec.TurnRate * (float)(Math.PI / 180.0)).Within(0.05f), "the turn settles at the full rudder rate");
            Assert.That(yaw, Is.GreaterThan(2f), "turned right by more than 2 rad in 5 s");
            Assert.That(Vector3.Dot(velocity, heading), Is.GreaterThan(3f), "still moving along the bow");
            Assert.That(velocity.Length(), Is.LessThan(spec.MaxForwardSpeed));
        }

        [Test]
        public void OnLandNothingPushesEvenAtFullThrottle()
        {
            var spec = BoatSpec.Default;
            var input = new VehicleMotorInput(1f, 1f);
            var besideTheLake = new BoxWater(new WaterBox(new Vector3(20f, -10f, 20f), new Vector3(40f, 0f, 40f), 0f));

            var dry = BoatHull.Compute(spec, input, Level(_restHeight), NoWaterVolumes.Instance, _gravity);
            var shore = BoatHull.Compute(spec, input, Level(_restHeight), besideTheLake, _gravity);

            Assert.That(dry.Force, Is.EqualTo(Vector3.Zero));
            Assert.That(dry.Torque, Is.EqualTo(Vector3.Zero));
            Assert.That(dry.AngularAcceleration, Is.EqualTo(Vector3.Zero));
            Assert.That(dry.ThrustAcceleration, Is.EqualTo(0f));
            Assert.That(dry.WetFraction, Is.EqualTo(0f));
            Assert.That(dry.PropellerImmersion, Is.EqualTo(0f));
            Assert.That(dry.IsAfloat, Is.False);
            Assert.That(shore.Force, Is.EqualTo(Vector3.Zero));
            Assert.That(shore.IsAfloat, Is.False);
        }

        [Test]
        public void BeachedWithTheSternOutOfTheWaterTheScrewDoesNotPush()
        {
            var spec = BoatSpec.Default;
            // 船首の側 (z > 0) にだけ水がある。船尾のスクリューは陸の上にある。
            var bowOnly = new BoxWater(new WaterBox(new Vector3(-10f, -10f, 0.5f), new Vector3(10f, 5f, 20f), 0f));

            var command = BoatHull.Compute(spec, new VehicleMotorInput(1f, 1f), Level(_restHeight), bowOnly, _gravity);

            Assert.That(command.WetFraction, Is.EqualTo(0.5f).Within(1e-4f), "the two bow corners are wet");
            Assert.That(command.PropellerImmersion, Is.EqualTo(0f));
            Assert.That(command.ThrustAcceleration, Is.EqualTo(0f));
            Assert.That(command.Force.Z, Is.EqualTo(0f).Within(1e-3f));
            Assert.That(command.AngularAcceleration.Y, Is.EqualTo(0f));
        }

        [Test]
        public void ALeanFromABroadsideWaveIsPushedBack()
        {
            var spec = BoatSpec.Default;
            var lean = 15f * (float)(Math.PI / 180.0);

            var right = BoatHull.Compute(spec, VehicleMotorInput.Idle, Rolled(lean, 0f), _sea, _gravity);
            var left = BoatHull.Compute(spec, VehicleMotorInput.Idle, Rolled(-lean, 0f), _sea, _gravity);

            Assert.That(right.Torque.Z, Is.LessThan(0f), "rolled about +z, the torque rolls it back about -z");
            Assert.That(left.Torque.Z, Is.GreaterThan(0f));
        }

        [Test]
        public void ALeanFromABroadsideWaveSettlesBackToLevel()
        {
            var spec = BoatSpec.Default;
            // 幅 2 m・高さ 1 m の箱の、前後の軸まわりの慣性。
            var inertia = spec.Mass * ((spec.Width * spec.Width) + (spec.HullHeight * spec.HullHeight)) / 12f;
            var roll = 20f * (float)(Math.PI / 180.0);
            var rollRate = 0f;
            var peak = 0f;

            for (var i = 0; i < 1500; i++)
            {
                var command = BoatHull.Compute(spec, VehicleMotorInput.Idle, Rolled(roll, rollRate), _sea, _gravity);
                rollRate += command.Torque.Z / inertia * 0.01f;
                roll += rollRate * 0.01f;
                if (i > 10)
                {
                    peak = Math.Max(peak, Math.Abs(roll));
                }
            }

            Assert.That(Math.Abs(roll), Is.LessThan(0.5f * (float)(Math.PI / 180.0)));
            Assert.That(Math.Abs(rollRate), Is.LessThan(0.01f));
            Assert.That(peak, Is.LessThan(20f * (float)(Math.PI / 180.0)), "the swing never grows past the first lean");
        }

        [Test]
        public void ComputeRejectsMissingOrInvalidArguments()
        {
            var state = Level(_restHeight);

            Assert.Throws<ArgumentNullException>(() => BoatHull.Compute(null, VehicleMotorInput.Idle, state, _sea, _gravity));
            Assert.Throws<ArgumentNullException>(() => BoatHull.Compute(BoatSpec.Default, VehicleMotorInput.Idle, state, null, _gravity));
            Assert.Throws<ArgumentOutOfRangeException>(() => BoatHull.Compute(BoatSpec.Default, VehicleMotorInput.Idle, state, _sea, -1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => BoatHull.Compute(BoatSpec.Default, VehicleMotorInput.Idle, state, _sea, float.NaN));
            Assert.Throws<ArgumentException>(() => new BoatHullState(new Vector3(float.NaN, 0f, 0f), Quaternion.Identity, Vector3.Zero, Vector3.Zero, Vector3.Zero));
            Assert.Throws<ArgumentException>(() => new BoatHullState(Vector3.Zero, new Quaternion(0f, 0f, 0f, 0f), Vector3.Zero, Vector3.Zero, Vector3.Zero));
            Assert.Throws<ArgumentException>(() => new BoatHullState(Vector3.Zero, Quaternion.Identity, Vector3.Zero, new Vector3(0f, float.PositiveInfinity, 0f), Vector3.Zero));
        }

        [Test]
        public void WithoutGravityNothingFloatsTheHullUp()
        {
            var command = BoatHull.Compute(BoatSpec.Default, VehicleMotorInput.Idle, Level(_restHeight), _sea, 0f);

            Assert.That(command.Force.Y, Is.EqualTo(0f).Within(1e-4f));
        }

        private static BoatHullState Level(float height, Vector3 velocity = default, float yawDegrees = 0f, Vector3 angularVelocity = default)
        {
            var position = new Vector3(0f, height, 0f);
            var rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, yawDegrees * (float)(Math.PI / 180.0));
            return new BoatHullState(position, rotation, position, velocity, angularVelocity);
        }

        private static BoatHullState Rolled(float roll, float rollRate)
        {
            var position = new Vector3(0f, _restHeight, 0f);
            return new BoatHullState(position, Quaternion.CreateFromAxisAngle(Vector3.UnitZ, roll), position, Vector3.Zero, new Vector3(0f, 0f, rollRate));
        }

        private sealed class BoxWater : IWaterVolumes
        {
            private readonly WaterBox _box;

            public BoxWater(WaterBox box)
            {
                _box = box;
            }

            public float? SurfaceAt(Vector3 position)
            {
                return _box.Contains(position) ? _box.SurfaceY : (float?)null;
            }
        }
    }
}
