using System;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class MountedGunShapeTests
    {
        private static VehicleSpec Gun(VehicleSeatRole role = VehicleSeatRole.Gunner, bool weapon = true) => new VehicleSpec(
            "post",
            100,
            new[] { new VehicleSeatSpec(role) },
            weapon ? new[] { new VehicleWeaponSpec("mg", 0) } : null);

        [Test]
        public void OneSeatWithAWeaponAndNoMotorIsAMountedGun()
        {
            Assert.That(MountedGunShape.Problem(Gun(), false), Is.Null);
            Assert.That(MountedGunShape.Problem(Gun(VehicleSeatRole.Passenger), false), Is.Null, "any seat but the driver's");
        }

        [Test]
        public void EachMismatchNamesItsReason()
        {
            var twoSeats = new VehicleSpec(
                "jeep",
                100,
                new[] { new VehicleSeatSpec(VehicleSeatRole.Passenger), new VehicleSeatSpec(VehicleSeatRole.Gunner) },
                new[] { new VehicleWeaponSpec("mg", 1) });

            Assert.That(MountedGunShape.Problem(twoSeats, false), Is.EqualTo(MountedGunLog.ReasonSeatCount));
            Assert.That(MountedGunShape.Problem(Gun(VehicleSeatRole.Driver), false), Is.EqualTo(MountedGunLog.ReasonDriverSeat));
            Assert.That(MountedGunShape.Problem(Gun(weapon: false), false), Is.EqualTo(MountedGunLog.ReasonNoWeapon));
            Assert.That(MountedGunShape.Problem(Gun(), true), Is.EqualTo(MountedGunLog.ReasonHasMotor));
            Assert.Throws<ArgumentNullException>(() => MountedGunShape.Problem(null, false));
        }

        [Test]
        public void LogLinesCarryTheTag()
        {
            Assert.That(MountedGunLog.Invalid("Post", MountedGunLog.ReasonHasMotor), Is.EqualTo("[MountedGun] 'Post' is not a mounted gun reason=has_motor"));
            Assert.That(MountedGunLog.Seek("Enemy", "Post", 3.24f), Is.EqualTo("[MountedGun] 'Enemy' seek gun=Post distance=3.2"));
            Assert.That(MountedGunLog.Man("Enemy", "Post"), Is.EqualTo("[MountedGun] 'Enemy' man gun=Post"));
            Assert.That(
                MountedGunLog.Leave("Enemy", "Post", MountedGunLeaveCause.OutOfArc),
                Is.EqualTo("[MountedGun] 'Enemy' leave gun=Post cause=OutOfArc"));
        }
    }
}
