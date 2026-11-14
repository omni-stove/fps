using System;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    // Unity の EditMode テストと dotnet/Fps.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class VehicleSpecTests
    {
        private static readonly VehicleSeatSpec[] _threeSeats =
        {
            new VehicleSeatSpec(VehicleSeatRole.Driver),
            new VehicleSeatSpec(VehicleSeatRole.Passenger, VehicleSeatPose.Seated, VehicleCameraView.Seat, 120f),
            new VehicleSeatSpec(VehicleSeatRole.Gunner, VehicleSeatPose.Standing),
        };

        [Test]
        public void KeepsEveryValueItWasBuiltWith()
        {
            var weapons = new[] { new VehicleWeaponSpec("mg", 2, -90f, 90f, -5f, 45f, 120f) };
            var parts = new[] { new VehiclePartSpec("engine", 2f), new VehiclePartSpec("fuel", 3f) };

            var spec = new VehicleSpec("jeep", 800, _threeSeats, weapons, parts, 3f, 7f, 2.5f);

            Assert.That(spec.Id, Is.EqualTo("jeep"));
            Assert.That(spec.MaxHealth, Is.EqualTo(800));
            Assert.That(spec.Seats, Is.EqualTo(_threeSeats));
            Assert.That(spec.DriverSeatIndex, Is.EqualTo(0));
            Assert.That(spec.Weapons, Has.Count.EqualTo(1));
            Assert.That(spec.Weapons[0].WeaponId, Is.EqualTo("mg"));
            Assert.That(spec.Parts, Has.Count.EqualTo(2));
            Assert.That(spec.FindPart("fuel").DamageMultiplier, Is.EqualTo(3f));
            Assert.That(spec.FindPart("wheel"), Is.Null);
            Assert.That(spec.EnterRange, Is.EqualTo(3f));
            Assert.That(spec.ChaseDistance, Is.EqualTo(7f));
            Assert.That(spec.ChaseHeight, Is.EqualTo(2.5f));
            Assert.That(spec.WeaponsOfSeat(2), Is.EqualTo(new[] { 0 }));
            Assert.That(spec.WeaponsOfSeat(0), Is.Empty);
        }

        [Test]
        public void DefaultsLeaveWeaponsAndPartsEmpty()
        {
            var spec = new VehicleSpec(VehicleSpec.DefaultId, VehicleSpec.DefaultMaxHealth, new[] { new VehicleSeatSpec(VehicleSeatRole.Gunner) });

            Assert.That(spec.Weapons, Is.Empty);
            Assert.That(spec.Parts, Is.Empty);
            Assert.That(spec.DriverSeatIndex, Is.EqualTo(-1), "a mounted gun has no driver");
            Assert.That(spec.EnterRange, Is.EqualTo(VehicleSpec.DefaultEnterRange));
            Assert.That(spec.ChaseDistance, Is.EqualTo(VehicleSpec.DefaultChaseDistance));
            Assert.That(spec.ChaseHeight, Is.EqualTo(VehicleSpec.DefaultChaseHeight));
        }

        [Test]
        public void TheSeatListIsCopied()
        {
            var seats = new[] { new VehicleSeatSpec(VehicleSeatRole.Driver) };
            var spec = new VehicleSpec("car", 100, seats);

            seats[0] = new VehicleSeatSpec(VehicleSeatRole.Passenger);

            Assert.That(spec.Seats[0].Role, Is.EqualTo(VehicleSeatRole.Driver));
        }

        [Test]
        public void RejectsInvalidValues()
        {
            var seats = new[] { new VehicleSeatSpec(VehicleSeatRole.Driver) };

            Assert.Throws<ArgumentException>(() => new VehicleSpec("", 100, seats));
            Assert.Throws<ArgumentOutOfRangeException>(() => new VehicleSpec("car", 0, seats));
            Assert.Throws<ArgumentNullException>(() => new VehicleSpec("car", 100, null));
            Assert.Throws<ArgumentException>(() => new VehicleSpec("car", 100, Array.Empty<VehicleSeatSpec>()));
            Assert.Throws<ArgumentException>(() => new VehicleSpec("car", 100, new VehicleSeatSpec[] { null }));
            Assert.Throws<ArgumentOutOfRangeException>(() => new VehicleSpec("car", 100, seats, enterRange: 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new VehicleSpec("car", 100, seats, enterRange: float.PositiveInfinity));
            Assert.Throws<ArgumentOutOfRangeException>(() => new VehicleSpec("car", 100, seats, chaseDistance: -1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new VehicleSpec("car", 100, seats, chaseHeight: float.NaN));
        }

        [Test]
        public void AVehicleHasAtMostOneDriverSeat()
        {
            var seats = new[] { new VehicleSeatSpec(VehicleSeatRole.Driver), new VehicleSeatSpec(VehicleSeatRole.Driver) };

            Assert.Throws<ArgumentException>(() => new VehicleSpec("car", 100, seats));
        }

        [Test]
        public void WeaponsMustBeOperatedFromAnExistingSeat()
        {
            var seats = new[] { new VehicleSeatSpec(VehicleSeatRole.Driver) };

            Assert.Throws<ArgumentException>(() => new VehicleSpec("car", 100, seats, new[] { new VehicleWeaponSpec("mg", 1) }));
            Assert.Throws<ArgumentException>(() => new VehicleSpec("car", 100, seats, new VehicleWeaponSpec[] { null }));
            Assert.Throws<ArgumentOutOfRangeException>(() => new VehicleSpec("car", 100, seats).WeaponsOfSeat(1));
        }

        [Test]
        public void PartIdsMustNotRepeat()
        {
            var seats = new[] { new VehicleSeatSpec(VehicleSeatRole.Driver) };
            var parts = new[] { new VehiclePartSpec("engine", 1f), new VehiclePartSpec("engine", 2f) };

            Assert.Throws<ArgumentException>(() => new VehicleSpec("car", 100, seats, parts: parts));
            Assert.Throws<ArgumentException>(() => new VehicleSpec("car", 100, seats, parts: new VehiclePartSpec[] { null }));
        }

        [Test]
        public void SeatSpecDefaultsAndValidation()
        {
            var seat = new VehicleSeatSpec(VehicleSeatRole.Passenger);

            Assert.That(seat.Pose, Is.EqualTo(VehicleSeatSpec.DefaultPose));
            Assert.That(seat.CameraView, Is.EqualTo(VehicleSeatSpec.DefaultCameraView));
            Assert.That(seat.OwnWeaponArcDegrees, Is.EqualTo(VehicleSeatSpec.DefaultOwnWeaponArcDegrees));
            Assert.That(seat.CrouchesRider, Is.True);
            Assert.That(new VehicleSeatSpec(VehicleSeatRole.Gunner, VehicleSeatPose.Standing).CrouchesRider, Is.False);
            Assert.That(new VehicleSeatSpec(VehicleSeatRole.Passenger, VehicleSeatPose.Hidden).CrouchesRider, Is.True);

            Assert.Throws<ArgumentOutOfRangeException>(() => new VehicleSeatSpec((VehicleSeatRole)99));
            Assert.Throws<ArgumentOutOfRangeException>(() => new VehicleSeatSpec(VehicleSeatRole.Driver, (VehicleSeatPose)99));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new VehicleSeatSpec(VehicleSeatRole.Driver, VehicleSeatPose.Seated, (VehicleCameraView)99));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new VehicleSeatSpec(VehicleSeatRole.Passenger, VehicleSeatPose.Seated, VehicleCameraView.Chase, -1f));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new VehicleSeatSpec(VehicleSeatRole.Passenger, VehicleSeatPose.Seated, VehicleCameraView.Chase, 361f));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new VehicleSeatSpec(VehicleSeatRole.Passenger, VehicleSeatPose.Seated, VehicleCameraView.Chase, float.NaN));
        }

        [Test]
        public void WeaponSpecDefaultsAndValidation()
        {
            var weapon = new VehicleWeaponSpec("mg", 0);

            Assert.That(weapon.YawMinDegrees, Is.EqualTo(VehicleWeaponSpec.DefaultYawMinDegrees));
            Assert.That(weapon.YawMaxDegrees, Is.EqualTo(VehicleWeaponSpec.DefaultYawMaxDegrees));
            Assert.That(weapon.PitchMinDegrees, Is.EqualTo(VehicleWeaponSpec.DefaultPitchMinDegrees));
            Assert.That(weapon.PitchMaxDegrees, Is.EqualTo(VehicleWeaponSpec.DefaultPitchMaxDegrees));
            Assert.That(weapon.TurnSpeedDegreesPerSecond, Is.EqualTo(VehicleWeaponSpec.DefaultTurnSpeedDegreesPerSecond));

            Assert.Throws<ArgumentException>(() => new VehicleWeaponSpec(" ", 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new VehicleWeaponSpec("mg", -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new VehicleWeaponSpec("mg", 0, 10f, -10f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new VehicleWeaponSpec("mg", 0, -181f, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new VehicleWeaponSpec("mg", 0, 0f, 0f, 10f, -10f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new VehicleWeaponSpec("mg", 0, 0f, 0f, -91f, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new VehicleWeaponSpec("mg", 0, turnSpeedDegreesPerSecond: 0f));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new VehicleWeaponSpec("mg", 0, turnSpeedDegreesPerSecond: float.PositiveInfinity));
        }

        [Test]
        public void PartSpecValidation()
        {
            Assert.Throws<ArgumentException>(() => new VehiclePartSpec("", 1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new VehiclePartSpec("engine", -0.1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new VehiclePartSpec("engine", float.NaN));
            Assert.That(new VehiclePartSpec("armor", 0f).DamageMultiplier, Is.Zero);
        }
    }
}
