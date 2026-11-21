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
        private const int ExpectedCount = 32;

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
            "[Vehicle] 'BotCar' bot driver phase=Cornering target=2",
            "[Vehicle] 'Helicopter' board rider=Player seat=0 role=Driver",
            "[VehicleSmokeRun] hovered 'Helicopter' altitude=5.1 seconds=3.2",
            "[VehicleSmokeRun] advanced 'Helicopter' distance=10.2 altitude=5.6",
            "[Vehicle] 'Helicopter' leave rider=Player seat=0 reason=Request exit=0",
            "[Vehicle] 'Horse' board rider=Player seat=0 role=Driver",
            "[VehicleSmokeRun] rode 'Horse' distance=15.3 speed=9.0 gait=Canter",
            "[Vehicle] 'Horse' leave rider=Player seat=0 reason=Request exit=0",
            "[Vehicle] 'Boat' board rider=Player seat=0 role=Driver",
            "[VehicleSmokeRun] sailed 'Boat' distance=10.1 speed=6.3",
            "[Vehicle] 'Boat' leave rider=Player seat=0 reason=Request exit=0",
            "[VehicleSmokeRun] lapped 'BotCar' points=4 seconds=31.5",
            "[VehicleSmokeRun] circling 'BotAircraft' altitude=158.2 waypoint=1",
            "[Vehicle] 'Aircraft' board rider=Player seat=0 role=Driver",
            "[VehicleSmokeRun] taxied 'Aircraft' turned=15.4 speed=5.2",
            "[VehicleSmokeRun] flew 'Aircraft' altitude=15.3 speed=33.1",
            "[VehicleSmokeRun] held 'Aircraft' seconds=4.0 aoa=14.2 altitude=96.5",
            "[VehicleSmokeRun] locked 'Aircraft' by=LockOnTurret distance=118.4",
            "[Vehicle] 'Aircraft' flare deployed id=0 remaining=5/6",
            "[VehicleSmokeRun] evaded 'Aircraft' flares=5/6",
            "[Vehicle] 'Aircraft' damaged dmg=300 hp=700/1000",
            "[Vehicle] 'Aircraft' part broken part=wing_left effect=LeftLift lift=0.20/1.00 control=1.00 thrust=1.00",
            "[VehicleSmokeRun] banked 'Aircraft' left=5.3",
            "[Vehicle] 'Aircraft' leave rider=Player seat=0 reason=BailOut exit=-1",
            "[Vehicle] 'Aircraft' bail out rider=Player speed=13.2 parachute=Open",
            "[VehicleSmokeRun] landed 'Aircraft' rider=Player seconds=14.8 drift=22.4",
        };

        private static int IndexOf(string start)
        {
            return Array.FindIndex(_passingRun, line => line.StartsWith(start, StringComparison.Ordinal));
        }

        private static string[] Without(string start)
        {
            return _passingRun.Where(line => !line.StartsWith(start, StringComparison.Ordinal)).ToArray();
        }

        [Test]
        public void ExpectsEveryVehicleInTheOrderTheRunVisitsThem()
        {
            var expected = VehicleSmokeRun.ExpectedLogs();

            Assert.That(expected.Count, Is.EqualTo(ExpectedCount));
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
            Assert.That(expected[9].Prefix, Is.EqualTo("[Vehicle] 'Helicopter' board rider="));
            Assert.That(expected[9].Fragment, Is.EqualTo(" seat=0 role=Driver"));
            Assert.That(expected[10].Prefix, Is.EqualTo("[VehicleSmokeRun] hovered 'Helicopter' "));
            Assert.That(expected[11].Prefix, Is.EqualTo("[VehicleSmokeRun] advanced 'Helicopter' "));
            Assert.That(expected[12].Prefix, Is.EqualTo("[Vehicle] 'Helicopter' leave rider="));
            Assert.That(expected[12].Fragment, Is.EqualTo(" seat=0 reason=Request "));
            Assert.That(expected[13].Prefix, Is.EqualTo("[Vehicle] 'Horse' board rider="));
            Assert.That(expected[13].Fragment, Is.EqualTo(" seat=0 role=Driver"));
            Assert.That(expected[14].Prefix, Is.EqualTo("[VehicleSmokeRun] rode 'Horse' "));
            Assert.That(expected[14].Fragment, Is.EqualTo(" gait=Canter"));
            Assert.That(expected[15].Prefix, Is.EqualTo("[Vehicle] 'Horse' leave rider="));
            Assert.That(expected[15].Fragment, Is.EqualTo(" seat=0 reason=Request "));
            Assert.That(expected[16].Prefix, Is.EqualTo("[Vehicle] 'Boat' board rider="));
            Assert.That(expected[17].Prefix, Is.EqualTo("[VehicleSmokeRun] sailed 'Boat' "));
            Assert.That(expected[18].Prefix, Is.EqualTo("[Vehicle] 'Boat' leave rider="));
            Assert.That(expected[19].Prefix, Is.EqualTo("[VehicleSmokeRun] lapped 'BotCar' "));
            Assert.That(expected[20].Prefix, Is.EqualTo("[VehicleSmokeRun] circling 'BotAircraft' "));
            Assert.That(expected[21].Prefix, Is.EqualTo("[Vehicle] 'Aircraft' board rider="));
            Assert.That(expected[21].Fragment, Is.EqualTo(" seat=0 role=Driver"));
            Assert.That(expected[22].Prefix, Is.EqualTo("[VehicleSmokeRun] taxied 'Aircraft' "));
            Assert.That(expected[23].Prefix, Is.EqualTo("[VehicleSmokeRun] flew 'Aircraft' "));
            Assert.That(expected[24].Prefix, Is.EqualTo("[VehicleSmokeRun] held 'Aircraft' "));
            Assert.That(expected[25].Prefix, Is.EqualTo("[VehicleSmokeRun] locked 'Aircraft' "));
            Assert.That(expected[26].Prefix, Is.EqualTo("[Vehicle] 'Aircraft' flare deployed "));
            Assert.That(expected[27].Prefix, Is.EqualTo("[VehicleSmokeRun] evaded 'Aircraft' "));
            Assert.That(expected[28].Prefix, Is.EqualTo("[Vehicle] 'Aircraft' part broken part="));
            Assert.That(expected[28].Fragment, Is.EqualTo(" effect=LeftLift "));
            Assert.That(expected[29].Prefix, Is.EqualTo("[VehicleSmokeRun] banked 'Aircraft' "));
            Assert.That(expected[30].Prefix, Is.EqualTo("[Vehicle] 'Aircraft' bail out rider="));
            Assert.That(expected[30].Fragment, Is.EqualTo(" parachute=Open"));
            Assert.That(expected[31].Prefix, Is.EqualTo("[VehicleSmokeRun] landed 'Aircraft' "));
        }

        [Test]
        public void ALogOfTheWholeRunMatchesEveryExpectation()
        {
            Assert.That(VehicleSmokeRun.CountInOrder(_passingRun, VehicleSmokeRun.ExpectedLogs()), Is.EqualTo(ExpectedCount));
        }

        [Test]
        public void TheFlightStepsMustComeInOrder()
        {
            var expected = VehicleSmokeRun.ExpectedLogs();
            var rightWing = _passingRun
                .Select(line => line.Replace("part=wing_left effect=LeftLift", "part=wing_right effect=RightLift"))
                .ToArray();

            Assert.That(VehicleSmokeRun.CountInOrder(Without("[VehicleSmokeRun] taxied"), expected), Is.EqualTo(22), "a run that never turned on the ground stops after boarding");
            Assert.That(VehicleSmokeRun.CountInOrder(Without("[VehicleSmokeRun] held"), expected), Is.EqualTo(24), "a run that fell while pulling up stops after the climb");
            Assert.That(VehicleSmokeRun.CountInOrder(rightWing, expected), Is.EqualTo(28), "breaking the right wing is not the left wing");
            Assert.That(VehicleSmokeRun.CountInOrder(Without("[VehicleSmokeRun] banked"), expected), Is.EqualTo(29));
        }

        [Test]
        public void TheHelicopterHorseBoatAndBotStepsMustComeInOrder()
        {
            var expected = VehicleSmokeRun.ExpectedLogs();
            var neverLifted = _passingRun.Take(IndexOf("[VehicleSmokeRun] hovered")).ToArray();
            var aground = Without("[VehicleSmokeRun] sailed");
            var stuckBot = Without("[VehicleSmokeRun] lapped");
            var groundedBot = Without("[VehicleSmokeRun] circling");

            Assert.That(VehicleSmokeRun.CountInOrder(neverLifted, expected), Is.EqualTo(10), "boarding the helicopter counts, the flight does not");
            Assert.That(VehicleSmokeRun.CountInOrder(Without("[VehicleSmokeRun] advanced"), expected), Is.EqualTo(11));
            Assert.That(VehicleSmokeRun.CountInOrder(aground, expected), Is.EqualTo(17), "a boat that never moved stops after boarding it");
            Assert.That(VehicleSmokeRun.CountInOrder(stuckBot, expected), Is.EqualTo(19));
            Assert.That(VehicleSmokeRun.CountInOrder(groundedBot, expected), Is.EqualTo(20));
        }

        [Test]
        public void TheHorseMustBeRiddenAtACanterAndLeft()
        {
            var expected = VehicleSmokeRun.ExpectedLogs();
            var neverBoarded = Without("[Vehicle] 'Horse' board");
            var neverRode = Without("[VehicleSmokeRun] rode");
            var onlyTrotted = _passingRun.Select(line => line.Replace("gait=Canter", "gait=Trot")).ToArray();
            var stillRiding = Without("[Vehicle] 'Horse' leave");
            var thrown = _passingRun.Select(line => line.Replace("'Horse' leave rider=Player seat=0 reason=Request", "'Horse' leave rider=Player seat=0 reason=Wrecked")).ToArray();

            Assert.That(VehicleSmokeRun.CountInOrder(neverBoarded, expected), Is.EqualTo(13), "the helicopter is done, the horse is not");
            Assert.That(VehicleSmokeRun.CountInOrder(neverRode, expected), Is.EqualTo(14), "boarding the horse counts, the ride does not");
            Assert.That(VehicleSmokeRun.CountInOrder(onlyTrotted, expected), Is.EqualTo(14), "a ride that never reached a canter is not a pass");
            Assert.That(VehicleSmokeRun.CountInOrder(stillRiding, expected), Is.EqualTo(15));
            Assert.That(VehicleSmokeRun.CountInOrder(thrown, expected), Is.EqualTo(15), "only getting off on request counts as leaving");
        }

        [Test]
        public void TheLockOnFlareAndBailOutStepsMustComeInOrder()
        {
            var expected = VehicleSmokeRun.ExpectedLogs();
            var noFlare = Without("[Vehicle] 'Aircraft' flare deployed");
            var stillLocked = Without("[VehicleSmokeRun] evaded");
            var noParachute = _passingRun.Select(line => line.Replace("parachute=Open", "parachute=TooLow")).ToArray();
            var neverLanded = Without("[VehicleSmokeRun] landed");
            var flareBeforeLock = _passingRun.ToArray();
            var locked = IndexOf("[VehicleSmokeRun] locked");
            var deployed = IndexOf("[Vehicle] 'Aircraft' flare deployed");
            (flareBeforeLock[locked], flareBeforeLock[deployed]) = (flareBeforeLock[deployed], flareBeforeLock[locked]);

            Assert.That(VehicleSmokeRun.CountInOrder(Without("[VehicleSmokeRun] locked"), expected), Is.EqualTo(25));
            Assert.That(VehicleSmokeRun.CountInOrder(noFlare, expected), Is.EqualTo(26));
            Assert.That(VehicleSmokeRun.CountInOrder(flareBeforeLock, expected), Is.EqualTo(26), "a flare dropped before the lock does not count as breaking it");
            Assert.That(VehicleSmokeRun.CountInOrder(stillLocked, expected), Is.EqualTo(27));
            Assert.That(VehicleSmokeRun.CountInOrder(noParachute, expected), Is.EqualTo(30), "bailing out without a parachute is not a pass");
            Assert.That(VehicleSmokeRun.CountInOrder(neverLanded, expected), Is.EqualTo(31));
        }

        [Test]
        public void OnlyTheStepLinesOfTheRunAreCollected()
        {
            foreach (var verb in new[] { "drove", "hovered", "advanced", "rode", "sailed", "lapped", "circling", "taxied", "flew", "held", "locked", "evaded", "banked", "landed" })
            {
                var index = IndexOf("[VehicleSmokeRun] " + verb + " ");
                Assert.That(index, Is.GreaterThanOrEqualTo(0), verb);
                Assert.That(VehicleSmokeRun.IsStepLog(_passingRun[index]), Is.True, verb);
            }

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
            Assert.That(VehicleSmokeRun.LockedLog("Aircraft", "LockOnTurret", 118.42f), Is.EqualTo("[VehicleSmokeRun] locked 'Aircraft' by=LockOnTurret distance=118.4"));
            Assert.That(VehicleSmokeRun.EvadedLog("Aircraft", 5, 6), Is.EqualTo("[VehicleSmokeRun] evaded 'Aircraft' flares=5/6"));
            Assert.That(VehicleSmokeRun.LandedLog("Aircraft", "Player", 14.84f, 22.36f), Is.EqualTo("[VehicleSmokeRun] landed 'Aircraft' rider=Player seconds=14.8 drift=22.4"));
        }

        [Test]
        public void TheHelicopterBoatAndBotLogsCarryWhatTheyMeasured()
        {
            Assert.That(VehicleSmokeRun.HoveredLog("Helicopter", 5.14f, 3.2f), Is.EqualTo("[VehicleSmokeRun] hovered 'Helicopter' altitude=5.1 seconds=3.2"));
            Assert.That(VehicleSmokeRun.AdvancedLog("Helicopter", 10.24f, 5.56f), Is.EqualTo("[VehicleSmokeRun] advanced 'Helicopter' distance=10.2 altitude=5.6"));
            Assert.That(VehicleSmokeRun.RodeLog("Horse", 15.26f, 8.96f, HorseGait.Canter), Is.EqualTo("[VehicleSmokeRun] rode 'Horse' distance=15.3 speed=9.0 gait=Canter"));
            Assert.That(VehicleSmokeRun.RodeLog("Horse", 15.26f, 8.96f, HorseGait.Canter), Does.StartWith(VehicleSmokeRun.RodeLogPrefix("Horse")));
            Assert.That(VehicleSmokeRun.SailedLog("Boat", 10.06f, 6.31f), Is.EqualTo("[VehicleSmokeRun] sailed 'Boat' distance=10.1 speed=6.3"));
            Assert.That(VehicleSmokeRun.LappedLog("BotCar", 4, 31.52f), Is.EqualTo("[VehicleSmokeRun] lapped 'BotCar' points=4 seconds=31.5"));
            Assert.That(VehicleSmokeRun.CirclingLog("BotAircraft", 158.24f, 1), Is.EqualTo("[VehicleSmokeRun] circling 'BotAircraft' altitude=158.2 waypoint=1"));
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
        public void TheTurretGoesOnTheGroundAheadSoTheAircraftFliesIntoItsReach()
        {
            var aircraft = new Vector3(-30f, 100f, 400f);

            var ground = VehicleSmokeRun.TurretGroundAhead(aircraft, new Vector3(0f, 0.2f, 1f), 100f, 150f, 0.9f);

            Assert.That(ground.HasValue, Is.True);
            Assert.That(ground.Value.y, Is.EqualTo(0f).Within(1e-3f), "the turret stands on the ground under the aircraft");
            Assert.That(ground.Value.x, Is.EqualTo(aircraft.x).Within(1e-3f));
            Assert.That(ground.Value.z, Is.GreaterThan(aircraft.z), "it waits ahead, not behind");
            Assert.That(Vector3.Distance(ground.Value, aircraft), Is.EqualTo(135f).Within(1e-2f), "the aircraft starts at 90% of the range");

            Assert.That(VehicleSmokeRun.TurretGroundAhead(aircraft, Vector3.forward, 140f, 150f, 0.9f), Is.Null, "too high to reach even from straight below");
            Assert.That(VehicleSmokeRun.TurretGroundAhead(aircraft, Vector3.up, 10f, 150f, 0.9f).Value.z, Is.GreaterThan(aircraft.z), "a nose pointing straight up still picks a side");
            Assert.Throws<ArgumentOutOfRangeException>(() => VehicleSmokeRun.TurretGroundAhead(aircraft, Vector3.forward, 10f, 0f, 0.9f));
            Assert.Throws<ArgumentOutOfRangeException>(() => VehicleSmokeRun.TurretGroundAhead(aircraft, Vector3.forward, 10f, 150f, 0f));
        }

        [Test]
        public void TheFlightChecksLeaveRoomForTheTuning()
        {
            Assert.That(VehicleSmokeRun.TaxiTurnDegrees, Is.LessThan(VehicleSmokeRun.TaxiAimDegrees), "the aim leads the turn so the nose wheel keeps steering");
            Assert.That(VehicleSmokeRun.PullPitchDegrees, Is.LessThan(90f), "aiming past straight up would start a loop");
            Assert.That(VehicleSmokeRun.BrokenWingBankDegrees, Is.LessThan(VehicleSmokeRun.LevelToleranceDegrees));
            Assert.That(VehicleSmokeRun.HoverAltitude, Is.GreaterThan(HelicopterSpec.DefaultGroundEffectHeight), "the hover is checked out of the ground effect");
            Assert.That(VehicleSmokeRun.LockOnRetrySeconds, Is.LessThan(VehicleSmokeRun.StepTimeoutSeconds));
            Assert.That(VehicleSmokeRun.BotAircraftClimbedAltitude, Is.LessThan(Shooter.Editor.VehicleRangeSceneBuilder.BotAircraftAltitude));
        }

        [Test]
        public void TheRideReachesACanterAndStaysOnTheFloor()
        {
            var spec = HorseSpec.Default;
            var toCanter = spec.CanterThreshold * spec.CanterThreshold / (2f * spec.Acceleration);
            var toStop = spec.CanterSpeed * spec.CanterSpeed / (2f * spec.Deceleration);

            Assert.That(VehicleSmokeRun.RideDistance, Is.GreaterThan(toCanter), "the horse reaches a canter before the ride is long enough");
            Assert.That(
                VehicleRangeSceneBuilder.HorseOrigin.x + VehicleSmokeRun.RideDistance + toStop,
                Is.LessThan(VehicleRangeSceneBuilder.FloorHalfExtent),
                "the horse rides east and stops before the edge of the floor");
            Assert.That(Vector3.Distance(VehicleRangeSceneBuilder.HorseRotation * Vector3.forward, Vector3.right), Is.LessThan(1e-4f), "the horse is placed facing east");
        }

        [Test]
        public void LongStepsGetLongerTimeouts()
        {
            Assert.That(VehicleSmokeRun.TimeoutFor(VehicleSmokeRun.Step.DestroyCar), Is.EqualTo(VehicleSmokeRun.DestroyTimeoutSeconds));
            Assert.That(VehicleSmokeRun.TimeoutFor(VehicleSmokeRun.Step.WatchBotCar), Is.EqualTo(VehicleSmokeRun.LapTimeoutSeconds));
            Assert.That(VehicleSmokeRun.TimeoutFor(VehicleSmokeRun.Step.WatchBotAircraft), Is.EqualTo(VehicleSmokeRun.LapTimeoutSeconds));
            Assert.That(VehicleSmokeRun.TimeoutFor(VehicleSmokeRun.Step.Parachute), Is.EqualTo(VehicleSmokeRun.LandTimeoutSeconds));
            Assert.That(VehicleSmokeRun.TimeoutFor(VehicleSmokeRun.Step.Lift), Is.EqualTo(VehicleSmokeRun.StepTimeoutSeconds));
            Assert.That(VehicleSmokeRun.TimeoutFor(VehicleSmokeRun.Step.RideHorse), Is.EqualTo(VehicleSmokeRun.StepTimeoutSeconds));
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
            var withoutDrive = Without("[VehicleSmokeRun]");
            var leftBeforeShooting = new[] { _passingRun[0], _passingRun[1], _passingRun[2], _passingRun[6], _passingRun[5] };

            Assert.That(VehicleSmokeRun.CountInOrder(withoutDrive, expected), Is.EqualTo(1));
            Assert.That(VehicleSmokeRun.CountInOrder(leftBeforeShooting, expected), Is.EqualTo(4));
            Assert.That(VehicleSmokeRun.CountInOrder(Array.Empty<string>(), expected), Is.Zero);
            Assert.Throws<ArgumentNullException>(() => VehicleSmokeRun.CountInOrder(null, expected));
            Assert.Throws<ArgumentNullException>(() => VehicleSmokeRun.CountInOrder(_passingRun, null));
        }

        [Test]
        public void AFlightThatNeverClimbsStopsTheCountAtTaxiing()
        {
            var grounded = _passingRun.Take(IndexOf("[VehicleSmokeRun] flew")).ToArray();

            Assert.That(VehicleSmokeRun.CountInOrder(grounded, VehicleSmokeRun.ExpectedLogs()), Is.EqualTo(23), "boarding and taxiing count, the rest does not");
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
            Assert.That(VehicleRangeSceneBuilder.HelicopterName, Is.EqualTo("Helicopter"));
            Assert.That(VehicleRangeSceneBuilder.HorseName, Is.EqualTo("Horse"));
            Assert.That(VehicleRangeSceneBuilder.BoatName, Is.EqualTo("Boat"));
            Assert.That(VehicleRangeSceneBuilder.BotCarName, Is.EqualTo("BotCar"));
            Assert.That(VehicleRangeSceneBuilder.BotAircraftName, Is.EqualTo("BotAircraft"));
            Assert.That(VehicleRangeSceneBuilder.BotCarVehicleId, Is.Not.EqualTo(CarFixtureBuilder.VehicleId), "the bot car is found apart from the car the smoke destroys");
            Assert.That(VehicleRangeSceneBuilder.BotAircraftVehicleId, Is.Not.EqualTo(AircraftFixtureBuilder.VehicleId));
        }
    }
}
