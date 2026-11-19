using System;
using System.Linq;
using Shooter.Core;
using Shooter.Editor;
using NUnit.Framework;
using UnityEngine;

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
            "[Vehicle] 'Aircraft' board rider=Player seat=0 role=Driver",
            "[VehicleSmokeRun] taxied 'Aircraft' turned=15.4 speed=5.2",
            "[VehicleSmokeRun] flew 'Aircraft' altitude=15.3 speed=33.1",
            "[VehicleSmokeRun] held 'Aircraft' seconds=4.0 aoa=14.2 altitude=96.5",
            "[Vehicle] 'Aircraft' damaged dmg=300 hp=700/1000",
            "[Vehicle] 'Aircraft' part broken part=wing_left effect=LeftLift lift=0.20/1.00 control=1.00 thrust=1.00",
            "[VehicleSmokeRun] banked 'Aircraft' left=5.3",
        };

        private static int IndexOf(string start)
        {
            return Array.FindIndex(_passingRun, line => line.StartsWith(start, StringComparison.Ordinal));
        }

        [Test]
        public void ExpectsBoardDriveShootLeaveDestroyAndFlyInThatOrder()
        {
            var expected = VehicleSmokeRun.ExpectedLogs();

            Assert.That(expected.Count, Is.EqualTo(15));
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
            Assert.That(expected[9].Prefix, Is.EqualTo("[Vehicle] 'Aircraft' board rider="));
            Assert.That(expected[9].Fragment, Is.EqualTo(" seat=0 role=Driver"));
            Assert.That(expected[10].Prefix, Is.EqualTo("[VehicleSmokeRun] taxied 'Aircraft' "));
            Assert.That(expected[11].Prefix, Is.EqualTo("[VehicleSmokeRun] flew 'Aircraft' "));
            Assert.That(expected[12].Prefix, Is.EqualTo("[VehicleSmokeRun] held 'Aircraft' "));
            Assert.That(expected[13].Prefix, Is.EqualTo("[Vehicle] 'Aircraft' part broken part="));
            Assert.That(expected[13].Fragment, Is.EqualTo(" effect=LeftLift "));
            Assert.That(expected[14].Prefix, Is.EqualTo("[VehicleSmokeRun] banked 'Aircraft' "));
        }

        [Test]
        public void ALogOfTheWholeRunMatchesEveryExpectation()
        {
            Assert.That(VehicleSmokeRun.CountInOrder(_passingRun, VehicleSmokeRun.ExpectedLogs()), Is.EqualTo(15));
        }

        [Test]
        public void TheFlightStepsMustComeInOrder()
        {
            var expected = VehicleSmokeRun.ExpectedLogs();
            var withoutTaxi = _passingRun.Where(line => !line.StartsWith("[VehicleSmokeRun] taxied", StringComparison.Ordinal)).ToArray();
            var withoutHold = _passingRun.Where(line => !line.StartsWith("[VehicleSmokeRun] held", StringComparison.Ordinal)).ToArray();
            var rightWing = _passingRun
                .Select(line => line.Replace("part=wing_left effect=LeftLift", "part=wing_right effect=RightLift"))
                .ToArray();
            var withoutBank = _passingRun.Take(_passingRun.Length - 1).ToArray();

            Assert.That(VehicleSmokeRun.CountInOrder(withoutTaxi, expected), Is.EqualTo(10), "a run that never turned on the ground stops after boarding");
            Assert.That(VehicleSmokeRun.CountInOrder(withoutHold, expected), Is.EqualTo(12), "a run that fell while pulling up stops after the climb");
            Assert.That(VehicleSmokeRun.CountInOrder(rightWing, expected), Is.EqualTo(13), "breaking the right wing is not the left wing");
            Assert.That(VehicleSmokeRun.CountInOrder(withoutBank, expected), Is.EqualTo(14));
        }

        [Test]
        public void OnlyTheStepLinesOfTheRunAreCollected()
        {
            Assert.That(VehicleSmokeRun.IsStepLog(_passingRun[IndexOf("[VehicleSmokeRun] drove")]), Is.True);
            Assert.That(VehicleSmokeRun.IsStepLog(_passingRun[IndexOf("[VehicleSmokeRun] taxied")]), Is.True);
            Assert.That(VehicleSmokeRun.IsStepLog(_passingRun[IndexOf("[VehicleSmokeRun] flew")]), Is.True);
            Assert.That(VehicleSmokeRun.IsStepLog(_passingRun[IndexOf("[VehicleSmokeRun] held")]), Is.True);
            Assert.That(VehicleSmokeRun.IsStepLog(_passingRun[IndexOf("[VehicleSmokeRun] banked")]), Is.True);
            Assert.That(VehicleSmokeRun.IsStepLog("[VehicleSmokeRun] step=Done vehicle logs=15 errors=0"), Is.False);
            Assert.That(VehicleSmokeRun.IsStepLog("[VehicleSmokeRun] passed (exit code 0)"), Is.False);
            Assert.That(VehicleSmokeRun.IsStepLog(_passingRun[0]), Is.False, "vehicle lines are collected by their own tag");
            Assert.That(VehicleSmokeRun.IsStepLog(null), Is.False);
        }

        [Test]
        public void TheAircraftStepLogsCarryWhatTheyMeasured()
        {
            Assert.That(VehicleSmokeRun.TaxiedLog("Aircraft", 15.44f, 5.16f), Is.EqualTo("[VehicleSmokeRun] taxied 'Aircraft' turned=15.4 speed=5.2"));
            Assert.That(VehicleSmokeRun.HeldLog("Aircraft", 4.02f, 14.23f, 96.47f), Is.EqualTo("[VehicleSmokeRun] held 'Aircraft' seconds=4.0 aoa=14.2 altitude=96.5"));
            Assert.That(VehicleSmokeRun.BankedLog("Aircraft", 5.26f), Is.EqualTo("[VehicleSmokeRun] banked 'Aircraft' left=5.3"));
        }

        [Test]
        public void BankPitchAndTurnAreMeasuredFromTheLevelRunwayHeading()
        {
            Assert.That(VehicleSmokeRun.LeftBankDegrees(Quaternion.identity), Is.EqualTo(0f).Within(1e-3f));
            Assert.That(VehicleSmokeRun.LeftBankDegrees(Quaternion.Euler(0f, 0f, 10f)), Is.EqualTo(10f).Within(1e-3f), "right wing up is a left bank");
            Assert.That(VehicleSmokeRun.LeftBankDegrees(Quaternion.Euler(0f, 0f, -10f)), Is.EqualTo(-10f).Within(1e-3f));
            Assert.That(VehicleSmokeRun.PitchDegrees(Quaternion.Euler(-20f, 0f, 0f)), Is.EqualTo(20f).Within(1e-3f), "a negative x rotation raises the nose");

            Assert.That(VehicleSmokeRun.LeftTurnDegrees(Vector3.forward, Quaternion.Euler(0f, -15f, 0f)), Is.EqualTo(15f).Within(1e-3f));
            Assert.That(VehicleSmokeRun.LeftTurnDegrees(Vector3.forward, Quaternion.Euler(0f, 15f, 0f)), Is.EqualTo(-15f).Within(1e-3f));

            var straightUp = Quaternion.Euler(-90f, 0f, 0f);
            Assert.That(VehicleSmokeRun.Heading(straightUp, Vector3.right), Is.EqualTo(Vector3.right), "straight up has no heading of its own");
            Assert.That(Vector3.Distance(VehicleSmokeRun.Heading(Quaternion.Euler(-30f, 90f, 0f), Vector3.forward), Vector3.right), Is.LessThan(1e-3f));
        }

        [Test]
        public void TheFlightChecksLeaveRoomForTheTuning()
        {
            Assert.That(VehicleSmokeRun.TaxiTurnDegrees, Is.LessThan(VehicleSmokeRun.TaxiAimDegrees), "the aim leads the turn so the nose wheel keeps steering");
            Assert.That(VehicleSmokeRun.PullPitchDegrees, Is.LessThan(90f), "aiming past straight up would start a loop");
            Assert.That(VehicleSmokeRun.BrokenWingBankDegrees, Is.LessThan(VehicleSmokeRun.LevelToleranceDegrees));
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
        public void AFlightThatNeverClimbsStopsTheCountAtBoarding()
        {
            var grounded = _passingRun.Take(IndexOf("[VehicleSmokeRun] flew")).ToArray();

            Assert.That(VehicleSmokeRun.CountInOrder(grounded, VehicleSmokeRun.ExpectedLogs()), Is.EqualTo(11), "boarding and taxiing count, the rest does not");
        }

        [Test]
        public void TheFlightLogCarriesTheAltitudeAndSpeed()
        {
            Assert.That(VehicleSmokeRun.FlewLog("Aircraft", 15.26f, 33.14f), Is.EqualTo("[VehicleSmokeRun] flew 'Aircraft' altitude=15.3 speed=33.1"));
            Assert.That(VehicleSmokeRun.FlewLog("Aircraft", 15.26f, 33.14f), Does.StartWith(VehicleSmokeRun.FlewLogPrefix("Aircraft")));
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
