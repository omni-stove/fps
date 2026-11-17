using System;
using System.Numerics;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class VehicleWeaponAimTests
    {
        private const float _epsilon = 1e-3f;

        private static VehicleWeaponAim Limited()
        {
            return new VehicleWeaponAim(new VehicleWeaponSpec("mg", 0, -90f, 90f, -10f, 30f, 90f));
        }

        private static VehicleWeaponAim FullCircle()
        {
            return new VehicleWeaponAim(new VehicleWeaponSpec("mg", 0, -180f, 180f, -10f, 30f, 90f));
        }

        [Test]
        public void StartsFacingForwardOrTheNearestEdge()
        {
            var aim = Limited();
            Assert.That(aim.YawDegrees, Is.Zero);
            Assert.That(aim.PitchDegrees, Is.Zero);
            Assert.That(aim.IsFullCircle, Is.False);
            Assert.That(FullCircle().IsFullCircle, Is.True);

            var sideways = new VehicleWeaponAim(new VehicleWeaponSpec("mg", 0, 30f, 120f, 5f, 20f));
            Assert.That(sideways.YawDegrees, Is.EqualTo(30f));
            Assert.That(sideways.PitchDegrees, Is.EqualTo(5f));
        }

        [Test]
        public void TurnsAtTheTurnSpeed()
        {
            var aim = Limited();

            Assert.That(aim.Step(60f, 0f, 0.5f), Is.False);
            Assert.That(aim.YawDegrees, Is.EqualTo(45f).Within(_epsilon));

            Assert.That(aim.Step(60f, 0f, 0.5f), Is.True);
            Assert.That(aim.YawDegrees, Is.EqualTo(60f).Within(_epsilon));
        }

        [Test]
        public void NeverTurnsOutsideTheRange()
        {
            var aim = Limited();
            for (var i = 0; i < 20; i++)
            {
                aim.Step(170f, 80f, 0.5f);
            }

            Assert.That(aim.YawDegrees, Is.EqualTo(90f).Within(_epsilon));
            Assert.That(aim.PitchDegrees, Is.EqualTo(30f).Within(_epsilon));

            for (var i = 0; i < 20; i++)
            {
                aim.Step(-170f, -80f, 0.5f);
            }

            Assert.That(aim.YawDegrees, Is.EqualTo(-90f).Within(_epsilon));
            Assert.That(aim.PitchDegrees, Is.EqualTo(-10f).Within(_epsilon));
        }

        [Test]
        public void ALimitedRangeTurnsThroughTheFrontNotTheGap()
        {
            var aim = Limited();
            for (var i = 0; i < 20; i++)
            {
                aim.Step(85f, 0f, 0.5f);
            }

            aim.Step(-85f, 0f, 0.5f);

            Assert.That(aim.YawDegrees, Is.EqualTo(40f).Within(_epsilon), "it swings back through 0, not across the rear");
        }

        [Test]
        public void AFullCircleTurnsTheShortWay()
        {
            var aim = FullCircle();
            for (var i = 0; i < 20; i++)
            {
                aim.Step(170f, 0f, 0.5f);
            }

            aim.Step(-170f, 0f, 0.1f);

            Assert.That(aim.YawDegrees, Is.EqualTo(179f).Within(_epsilon));
            aim.Step(-170f, 0f, 0.1f);
            Assert.That(aim.YawDegrees, Is.EqualTo(-172f).Within(_epsilon));
        }

        [Test]
        public void ClampsToTheNearerEdge()
        {
            var aim = Limited();

            Assert.That(aim.ClampYaw(100f), Is.EqualTo(90f));
            Assert.That(aim.ClampYaw(-100f), Is.EqualTo(-90f));
            Assert.That(aim.ClampYaw(181f), Is.EqualTo(-90f), "181 is -179, nearer the left edge");
            Assert.That(aim.ClampYaw(45f), Is.EqualTo(45f));
            Assert.That(aim.ClampPitch(50f), Is.EqualTo(30f));
            Assert.That(FullCircle().ClampYaw(270f), Is.EqualTo(-90f).Within(_epsilon));
        }

        [Test]
        public void DirectionsAndAnglesRoundTrip()
        {
            Assert.That(VehicleWeaponAim.TryToAngles(new Vector3(1f, 0f, 0f), out var yaw, out var pitch), Is.True);
            Assert.That(yaw, Is.EqualTo(90f).Within(_epsilon));
            Assert.That(pitch, Is.Zero.Within(_epsilon));

            Assert.That(VehicleWeaponAim.TryToAngles(new Vector3(0f, 1f, 1f), out yaw, out pitch), Is.True);
            Assert.That(yaw, Is.Zero.Within(_epsilon));
            Assert.That(pitch, Is.EqualTo(45f).Within(_epsilon));

            Assert.That(VehicleWeaponAim.TryToAngles(Vector3.Zero, out _, out _), Is.False);

            var direction = VehicleWeaponAim.ToDirection(-90f, 0f);
            Assert.That(direction.X, Is.EqualTo(-1f).Within(_epsilon));
            Assert.That(direction.Z, Is.Zero.Within(_epsilon));

            var aim = Limited();
            Assert.That(aim.Step(new Vector3(1f, 0f, 1f), 1f), Is.True);
            Assert.That(aim.YawDegrees, Is.EqualTo(45f).Within(_epsilon));
            Assert.That(aim.Step(Vector3.Zero, 1f), Is.False, "no aim leaves the weapon where it is");
            Assert.That(aim.YawDegrees, Is.EqualTo(45f).Within(_epsilon));
        }

        [Test]
        public void RejectsBadInput()
        {
            var aim = Limited();

            Assert.Throws<ArgumentNullException>(() => new VehicleWeaponAim(null));
            Assert.Throws<ArgumentOutOfRangeException>(() => aim.Step(0f, 0f, -1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => aim.Step(float.NaN, 0f, 0.1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => aim.Step(Vector3.UnitZ, float.PositiveInfinity));
        }

        [Test]
        public void ResetFacesForwardAgain()
        {
            var aim = Limited();
            aim.Step(60f, 20f, 1f);

            aim.Reset();

            Assert.That(aim.YawDegrees, Is.Zero);
            Assert.That(aim.PitchDegrees, Is.Zero);
        }

        [Test]
        public void CanReachOnlyDirectionsInsideTheRange()
        {
            var aim = Limited();

            Assert.That(aim.CanReach(Vector3.UnitZ), Is.True);
            Assert.That(aim.CanReach(Vector3.UnitX), Is.True, "the right edge itself");
            Assert.That(aim.CanReach(-Vector3.UnitZ + (Vector3.UnitX * 0.01f)), Is.False, "behind");
            Assert.That(aim.CanReach(VehicleWeaponAim.ToDirection(0f, 30f)), Is.True);
            Assert.That(aim.CanReach(VehicleWeaponAim.ToDirection(0f, 31f)), Is.False, "above the top");
            Assert.That(aim.CanReach(Vector3.Zero), Is.False);
            Assert.That(FullCircle().CanReach(-Vector3.UnitZ), Is.True);
        }

        [Test]
        public void OffsetIsTheAngleFromWhereTheGunPoints()
        {
            var aim = Limited();

            Assert.That(aim.OffsetDegrees(Vector3.UnitZ * 5f), Is.EqualTo(0f).Within(_epsilon));
            Assert.That(aim.OffsetDegrees(Vector3.UnitX), Is.EqualTo(90f).Within(_epsilon));

            aim.Step(Vector3.UnitX, 0.5f);
            Assert.That(aim.OffsetDegrees(Vector3.UnitX), Is.EqualTo(45f).Within(_epsilon));
            Assert.Throws<ArgumentException>(() => aim.OffsetDegrees(Vector3.Zero));
        }
    }
}
