using System;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class VehicleSpecTests
    {
        private static readonly VehicleSeatSpec[] _threeSeats =
        {
            new VehicleSeatSpec(VehicleSeatRole.Driver),
            new VehicleSeatSpec(VehicleSeatRole.Passenger, VehicleSeatPose.Seated, VehicleCameraView.Seat, 120f),
            new VehicleSeatSpec(VehicleSeatRole.Gunner, VehicleSeatPose.Standing),
        };

        // 定数配列を直接渡すと CA1861 になるので params で受ける
        private static int[] Indices(params int[] indices) => indices;

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
            Assert.That(spec.Weapons.Count, Is.EqualTo(1));
            Assert.That(spec.Weapons[0].WeaponId, Is.EqualTo("mg"));
            Assert.That(spec.Parts.Count, Is.EqualTo(2));
            Assert.That(spec.FindPart("fuel").DamageMultiplier, Is.EqualTo(3f));
            Assert.That(spec.FindPart("wheel"), Is.Null);
            Assert.That(spec.EnterRange, Is.EqualTo(3f));
            Assert.That(spec.ChaseDistance, Is.EqualTo(7f));
            Assert.That(spec.ChaseHeight, Is.EqualTo(2.5f));
            Assert.That(spec.WeaponsOfSeat(2), Is.EqualTo(Indices(0)));
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
        public void GettingInAndOutIsInstantUnlessTheGameGivesItTime()
        {
            var instant = new VehicleSpec("jeep", 800, _threeSeats);
            Assert.That(instant.EnterSeconds, Is.EqualTo(0f));
            Assert.That(instant.ExitSeconds, Is.EqualTo(0f));

            var timed = new VehicleSpec("jeep", 800, _threeSeats, enterSeconds: 1.5f, exitSeconds: 0.75f);
            Assert.That(timed.EnterSeconds, Is.EqualTo(1.5f));
            Assert.That(timed.ExitSeconds, Is.EqualTo(0.75f));

            Assert.Throws<ArgumentOutOfRangeException>(() => new VehicleSpec("jeep", 800, _threeSeats, enterSeconds: -0.1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new VehicleSpec("jeep", 800, _threeSeats, enterSeconds: float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => new VehicleSpec("jeep", 800, _threeSeats, exitSeconds: float.PositiveInfinity));
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

        [Test]
        public void TheSeatFiresTheVehicleWeaponItsOwnWeaponOrNothing()
        {
            var weapons = new[] { new VehicleWeaponSpec("mg", 2) };
            var spec = new VehicleSpec("jeep", 800, _threeSeats, weapons);

            Assert.That(spec.FiringOf(0), Is.EqualTo(VehicleSeatFiring.None), "the driver cannot shoot");
            Assert.That(spec.FiringOf(1), Is.EqualTo(VehicleSeatFiring.OwnWeapon));
            Assert.That(spec.FiringOf(2), Is.EqualTo(VehicleSeatFiring.VehicleWeapon));
            Assert.That(new VehicleSpec("jeep", 800, _threeSeats).FiringOf(2), Is.EqualTo(VehicleSeatFiring.None));
            Assert.Throws<ArgumentOutOfRangeException>(() => spec.FiringOf(3));
        }

        [Test]
        public void AVehicleWeaponOnTheDriverSeatLetsTheDriverShoot()
        {
            var spec = new VehicleSpec("jet", 800, _threeSeats, new[] { new VehicleWeaponSpec("cannon", 0) });

            Assert.That(spec.FiringOf(0), Is.EqualTo(VehicleSeatFiring.VehicleWeapon));
        }

        [Test]
        public void AVehicleWeaponTakesPrecedenceOverTheOwnWeaponArc()
        {
            var spec = new VehicleSpec("jeep", 800, _threeSeats, new[] { new VehicleWeaponSpec("mg", 1) });

            Assert.That(spec.FiringOf(1), Is.EqualTo(VehicleSeatFiring.VehicleWeapon));
        }

        [Test]
        public void TheOwnWeaponArcIsCenteredOnTheSeatFront()
        {
            var seat = new VehicleSeatSpec(VehicleSeatRole.Passenger, VehicleSeatPose.Seated, VehicleCameraView.Seat, 120f, 2f);

            Assert.That(seat.CanFireOwnWeapon, Is.True);
            Assert.That(seat.OwnWeaponSpreadMultiplier, Is.EqualTo(2f));
            Assert.That(seat.AllowsOwnWeaponAt(0f), Is.True);
            Assert.That(seat.AllowsOwnWeaponAt(60f), Is.True);
            Assert.That(seat.AllowsOwnWeaponAt(-60f), Is.True);
            Assert.That(seat.AllowsOwnWeaponAt(61f), Is.False);
            Assert.That(seat.AllowsOwnWeaponAt(180f), Is.False);
            Assert.That(seat.AllowsOwnWeaponAt(360f), Is.True, "angles wrap");

            var all = new VehicleSeatSpec(VehicleSeatRole.Passenger, VehicleSeatPose.Seated, VehicleCameraView.Seat, 360f);
            Assert.That(all.AllowsOwnWeaponAt(180f), Is.True);

            var none = new VehicleSeatSpec(VehicleSeatRole.Passenger);
            Assert.That(none.CanFireOwnWeapon, Is.False);
            Assert.That(none.AllowsOwnWeaponAt(0f), Is.False);
            Assert.That(none.OwnWeaponSpreadMultiplier, Is.EqualTo(VehicleSeatSpec.DefaultOwnWeaponSpreadMultiplier));

            var driver = new VehicleSeatSpec(VehicleSeatRole.Driver, VehicleSeatPose.Seated, VehicleCameraView.Chase, 120f);
            Assert.That(driver.CanFireOwnWeapon, Is.False, "the driver keeps both hands on the controls");
            Assert.That(driver.AllowsOwnWeaponAt(0f), Is.False);

            Assert.Throws<ArgumentOutOfRangeException>(() => seat.AllowsOwnWeaponAt(float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new VehicleSeatSpec(VehicleSeatRole.Passenger, VehicleSeatPose.Seated, VehicleCameraView.Seat, 90f, 0.5f));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new VehicleSeatSpec(VehicleSeatRole.Passenger, VehicleSeatPose.Seated, VehicleCameraView.Seat, 90f, float.PositiveInfinity));
        }

        [Test]
        public void WeaponLogsCarryTheTag()
        {
            Assert.That(
                VehicleLog.Firing("Jeep", "Player", 1, VehicleSeatFiring.OwnWeapon),
                Is.EqualTo("[Vehicle] 'Jeep' firing rider=Player seat=1 with=OwnWeapon"));
            Assert.That(
                VehicleLog.WeaponFired("Jeep", "Player", 2, "mg", -12.54f, 3f, "Dummy", 25, 99),
                Is.EqualTo("[Vehicle] 'Jeep' fire rider=Player seat=2 weapon=mg yaw=-12.5 pitch=3.0 hit=Dummy dmg=25 ammo=99"));
            Assert.That(
                VehicleLog.WeaponFired("Jeep", "Player", 2, "mg", 0f, 0f, null, 0, 0),
                Is.EqualTo("[Vehicle] 'Jeep' fire rider=Player seat=2 weapon=mg yaw=0.0 pitch=0.0 hit=- dmg=0 ammo=0"));
            Assert.That(
                VehicleLog.WeaponFired("Post", "Player", 0, "mg", 0f, 0f, null, 0, 1, 0.3f),
                Is.EqualTo("[Vehicle] 'Post' fire rider=Player seat=0 weapon=mg yaw=0.0 pitch=0.0 hit=- dmg=0 ammo=1 heat=0.30"));
            Assert.That(
                VehicleLog.Overheated("Post", 0, "mg", 1.5f),
                Is.EqualTo("[Vehicle] 'Post' overheated seat=0 weapon=mg lock=1.50"));
            Assert.That(VehicleLog.Cooled("Post", 0, "mg"), Is.EqualTo("[Vehicle] 'Post' cooled seat=0 weapon=mg"));
        }
    }
}
