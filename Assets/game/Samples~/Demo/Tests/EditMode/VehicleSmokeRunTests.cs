using System;
using System.Linq;
using Shooter.Core;
using Shooter.Editor;
using NUnit.Framework;

namespace Shooter.Tests.EditMode
{
    public sealed class VehicleSmokeRunTests
    {
        private static readonly string[] _passingRun =
        {
            "[Vehicle] 'GunCar' board rider=Player seat=0 role=Driver",
            "[VehicleSmokeRun] drove 'GunCar' distance=10.4",
            "[Vehicle] 'GunCar' switch rider=Player from=0 to=2 role=Gunner",
            "[Vehicle] 'GunCar' firing rider=Player seat=2 with=VehicleWeapon",
            "[Vehicle] 'GunCar' fire rider=Player seat=2 weapon=rifle yaw=-80.0 pitch=-5.0 hit=- dmg=0 ammo=29",
            "[Vehicle] 'GunCar' fire rider=Player seat=2 weapon=rifle yaw=-90.0 pitch=-5.0 hit=VehicleTarget1 dmg=25 ammo=28",
            "[Vehicle] 'GunCar' leave rider=Player seat=2 reason=Request exit=0",
            "[Vehicle] 'MountedGun' board rider=Player seat=0 role=Gunner",
            "[Vehicle] 'Car' damaged dmg=22 hp=978/1000",
            "[Vehicle] 'Car' exploded at=(-14.0, 0.9, -24.0) riders=0 damaged=0 killed=0",
            "[Vehicle] 'Car' wrecked riders=0",
            "[Vehicle] 'MountedGun' leave rider=Player seat=0 reason=Request exit=0",
        };

        [Test]
        public void ExpectsBoardDriveShootLeaveAndDestroyInThatOrder()
        {
            var expected = VehicleSmokeRun.ExpectedLogs();

            Assert.That(expected.Count, Is.EqualTo(9));
            Assert.That(expected[0].Prefix, Is.EqualTo("[Vehicle] 'GunCar' board rider="));
            Assert.That(expected[0].Fragment, Is.EqualTo(" seat=0 role=Driver"));
            Assert.That(expected[1].Prefix, Is.EqualTo("[VehicleSmokeRun] drove 'GunCar' "));
            Assert.That(expected[2].Fragment, Is.EqualTo(" to=2 role=Gunner"));
            Assert.That(expected[3].Prefix, Is.EqualTo("[Vehicle] 'GunCar' fire rider="));
            Assert.That(expected[3].Fragment, Is.EqualTo(" hit=VehicleTarget"));
            Assert.That(expected[4].Fragment, Is.EqualTo(" seat=2 reason=Request "));
            Assert.That(expected[5].Prefix, Is.EqualTo("[Vehicle] 'MountedGun' board rider="));
            Assert.That(expected[6].Prefix, Is.EqualTo("[Vehicle] 'Car' exploded at="));
            Assert.That(expected[7].Prefix, Is.EqualTo("[Vehicle] 'Car' wrecked "));
            Assert.That(expected[8].Prefix, Is.EqualTo("[Vehicle] 'MountedGun' leave rider="));
        }

        [Test]
        public void ALogOfTheWholeRunMatchesEveryExpectation()
        {
            Assert.That(VehicleSmokeRun.CountInOrder(_passingRun, VehicleSmokeRun.ExpectedLogs()), Is.EqualTo(9));
        }

        [Test]
        public void AMissShotDoesNotCountAsHittingTheTarget()
        {
            var hit = VehicleSmokeRun.TargetHitLog();

            Assert.That(hit.Matches(_passingRun[4]), Is.False);
            Assert.That(hit.Matches(_passingRun[5]), Is.True);
            Assert.That(hit.Matches("[Vehicle] 'MountedGun' fire rider=Player seat=0 weapon=gun yaw=0.0 pitch=0.0 hit=VehicleTarget0 dmg=22 ammo=1"), Is.False);
        }

        [Test]
        public void CountingStopsAtTheFirstStepThatIsMissingOrOutOfOrder()
        {
            var expected = VehicleSmokeRun.ExpectedLogs();
            var withoutDrive = _passingRun.Where(line => !line.StartsWith("[VehicleSmokeRun]", StringComparison.Ordinal)).ToArray();
            var leftBeforeShooting = new[] { _passingRun[0], _passingRun[1], _passingRun[2], _passingRun[6], _passingRun[5] };

            Assert.That(VehicleSmokeRun.CountInOrder(withoutDrive, expected), Is.EqualTo(1));
            Assert.That(VehicleSmokeRun.CountInOrder(leftBeforeShooting, expected), Is.EqualTo(4));
            Assert.That(VehicleSmokeRun.CountInOrder(Array.Empty<string>(), expected), Is.Zero);
            Assert.Throws<ArgumentNullException>(() => VehicleSmokeRun.CountInOrder(null, expected));
            Assert.Throws<ArgumentNullException>(() => VehicleSmokeRun.CountInOrder(_passingRun, null));
        }

        [Test]
        public void TheDriveLogCarriesTheDistance()
        {
            Assert.That(VehicleSmokeRun.DroveLog("GunCar", 10.44f), Is.EqualTo("[VehicleSmokeRun] drove 'GunCar' distance=10.4"));
            Assert.Throws<ArgumentException>(() => new VehicleSmokeRun.LogExpectation(string.Empty, null));
        }

        [Test]
        public void ItsNamesComeFromTheVehicleLogAndTheRange()
        {
            Assert.That(VehicleLog.Tag, Is.EqualTo("[Vehicle]"));
            Assert.That(VehicleRangeSceneBuilder.GunCarName, Is.EqualTo("GunCar"));
            Assert.That(VehicleRangeSceneBuilder.MountedGunName, Is.EqualTo("MountedGun"));
            Assert.That(VehicleRangeSceneBuilder.CarName, Is.EqualTo("Car"));
        }
    }
}
