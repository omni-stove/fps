using System;
using System.Collections.Generic;
using System.Globalization;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class CaptureTheFlagTests
    {
        private const int _alpha1 = 1;
        private const int _alpha2 = 2;
        private const int _bravo1 = 3;
        private const int _bravo2 = 4;
        private const float _returnSeconds = 10f;
        private const float _respawnSeconds = 5f;
        private static readonly string[] _expectedTaken = { "1:1" };
        private static readonly string[] _expectedDropped = { "1:1" };
        private static readonly string[] _expectedReturned = { "1:4" };
        private static readonly string[] _expectedByItself = { "1:-" };
        private static readonly string[] _expectedCaptured = { "1:1 score=1" };
        private static readonly string[] _expectedOrder = { "captured 1", "ended 0", "captured 2" };

        [Test]
        public void TheModeRejectsANullSpec()
        {
            Assert.Throws<ArgumentNullException>(() => new CaptureTheFlag(null));
        }

        [Test]
        public void BothFlagsStartAtTheirBases()
        {
            var game = Start();

            foreach (var side in CaptureTheFlag.FlagSides)
            {
                Assert.That(game.GetFlagState(side), Is.EqualTo(FlagState.AtBase));
                Assert.That(game.GetCarrierId(side), Is.Null);
                Assert.That(game.GetReturnRemaining(side), Is.Null);
            }

            Assert.That(CaptureTheFlag.FlagSides, Is.EqualTo(new[] { Side.Alpha, Side.Bravo }));
        }

        [Test]
        public void TheOpponentOfEachTeamIsTheOtherTeam()
        {
            Assert.That(CaptureTheFlag.Opponent(Side.Alpha), Is.EqualTo(Side.Bravo));
            Assert.That(CaptureTheFlag.Opponent(Side.Bravo), Is.EqualTo(Side.Alpha));
            Assert.Throws<ArgumentOutOfRangeException>(() => CaptureTheFlag.Opponent(new Side(2)));
        }

        [Test]
        public void AnEnemyTouchingTheFlagAtItsBaseCarriesIt()
        {
            var game = Start();
            var taken = new List<string>();
            game.FlagTaken += (flag, player) => taken.Add(flag.Id + ":" + player);

            Assert.That(game.Touch(Side.Bravo, _alpha1), Is.True);

            Assert.That(game.GetFlagState(Side.Bravo), Is.EqualTo(FlagState.Carried));
            Assert.That(game.GetCarrierId(Side.Bravo), Is.EqualTo(_alpha1));
            Assert.That(game.GetReturnRemaining(Side.Bravo), Is.Null);
            Assert.That(game.IsCarrying(_alpha1), Is.True);
            Assert.That(game.GetCarriedFlag(_alpha1), Is.EqualTo(Side.Bravo));
            Assert.That(game.IsCarrying(_alpha2), Is.False);
            Assert.That(game.GetFlagState(Side.Alpha), Is.EqualTo(FlagState.AtBase));
            Assert.That(taken, Is.EqualTo(_expectedTaken));
        }

        [Test]
        public void TouchingYourOwnFlagAtItsBaseDoesNothing()
        {
            var game = Start();

            Assert.That(game.Touch(Side.Alpha, _alpha1), Is.False);

            Assert.That(game.GetFlagState(Side.Alpha), Is.EqualTo(FlagState.AtBase));
            Assert.That(game.IsCarrying(_alpha1), Is.False);
        }

        [Test]
        public void ACarriedFlagCannotBeTakenByAnotherEnemy()
        {
            var game = Start();
            game.Touch(Side.Bravo, _alpha1);

            Assert.That(game.Touch(Side.Bravo, _alpha2), Is.False);
            Assert.That(game.Touch(Side.Bravo, _bravo1), Is.False, "an ally cannot return a flag that is being carried");

            Assert.That(game.GetCarrierId(Side.Bravo), Is.EqualTo(_alpha1));
        }

        [Test]
        public void TheCarrierFallingDropsTheFlagWhereTheyFell()
        {
            var game = Start();
            var dropped = new List<string>();
            game.FlagDropped += (flag, player) => dropped.Add(flag.Id + ":" + player);
            game.Touch(Side.Bravo, _alpha1);

            game.ReportKill(_bravo1, _alpha1);

            Assert.That(game.GetFlagState(Side.Bravo), Is.EqualTo(FlagState.Dropped));
            Assert.That(game.GetCarrierId(Side.Bravo), Is.Null);
            Assert.That(game.GetReturnRemaining(Side.Bravo), Is.EqualTo(_returnSeconds));
            Assert.That(game.IsCarrying(_alpha1), Is.False);
            Assert.That(game.Session.GetKills(_bravo1), Is.EqualTo(1));
            Assert.That(dropped, Is.EqualTo(_expectedTaken));
        }

        [Test]
        public void ACarrierDyingWithoutAKillerAlsoDropsTheFlag()
        {
            var game = Start();
            game.Touch(Side.Bravo, _alpha1);

            game.ReportDeath(_alpha1);

            Assert.That(game.GetFlagState(Side.Bravo), Is.EqualTo(FlagState.Dropped));
            Assert.That(game.Session.GetDeaths(_alpha1), Is.EqualTo(1));
        }

        [Test]
        public void AKillReportedStraightToTheSessionDropsTheFlagOnTheNextTick()
        {
            var game = Start();
            game.Touch(Side.Bravo, _alpha1);

            game.Session.ReportKill(_bravo1, _alpha1);
            Assert.That(game.GetFlagState(Side.Bravo), Is.EqualTo(FlagState.Carried), "the flag is only dropped when the mode looks again");

            game.Session.Tick(0f);

            Assert.That(game.GetFlagState(Side.Bravo), Is.EqualTo(FlagState.Dropped));
            Assert.That(game.GetReturnRemaining(Side.Bravo), Is.EqualTo(_returnSeconds));
        }

        [Test]
        public void ACarrierWhoRespawnsAtOnceStillDropsTheFlag()
        {
            var game = Start(new CaptureTheFlagSpec(respawnSeconds: 0f, returnSeconds: _returnSeconds));
            game.Touch(Side.Bravo, _alpha1);

            game.ReportKill(_bravo1, _alpha1);

            Assert.That(game.Session.IsDown(_alpha1), Is.False);
            Assert.That(game.GetFlagState(Side.Bravo), Is.EqualTo(FlagState.Dropped));
        }

        [Test]
        public void DroppingAfterAKillReportedStraightToTheSessionDropsTheFlagAtOnce()
        {
            var game = Start(new CaptureTheFlagSpec(respawnSeconds: 0f, returnSeconds: _returnSeconds));
            var dropped = new List<string>();
            game.FlagDropped += (flag, player) => dropped.Add(flag.Id + ":" + player);
            game.Touch(Side.Bravo, _alpha1);

            game.Session.ReportKill(_bravo1, _alpha1);
            Assert.That(game.Session.IsDown(_alpha1), Is.False, "a zero respawn brings the carrier straight back");

            Assert.That(game.DropCarriedFlag(_alpha1), Is.True);

            Assert.That(game.GetFlagState(Side.Bravo), Is.EqualTo(FlagState.Dropped));
            Assert.That(game.GetReturnRemaining(Side.Bravo), Is.EqualTo(_returnSeconds));
            Assert.That(dropped, Is.EqualTo(_expectedDropped));
            Assert.That(game.DropCarriedFlag(_alpha1), Is.False, "nothing is left to drop");
        }

        [Test]
        public void DroppingDoesNothingForSomeoneNotCarryingOrOutsideTheMatch()
        {
            var waiting = Create();
            Assert.That(waiting.DropCarriedFlag(_alpha1), Is.False);

            var game = Start();
            Assert.That(game.DropCarriedFlag(_alpha1), Is.False);
            Assert.That(game.DropCarriedFlag(99), Is.False, "an unknown player carries nothing");
            Assert.That(game.GetFlagState(Side.Bravo), Is.EqualTo(FlagState.AtBase));
        }

        [Test]
        public void KillingSomeoneWhoIsNotCarryingLeavesTheFlagsAlone()
        {
            var game = Start();
            game.Touch(Side.Bravo, _alpha1);

            game.ReportKill(_bravo1, _alpha2);

            Assert.That(game.GetCarrierId(Side.Bravo), Is.EqualTo(_alpha1));
        }

        [Test]
        public void ACarrierLeavingDropsTheFlag()
        {
            var game = Start();
            game.Touch(Side.Bravo, _alpha1);

            game.RemovePlayer(_alpha1);

            Assert.That(game.Session.HasPlayer(_alpha1), Is.False);
            Assert.That(game.GetFlagState(Side.Bravo), Is.EqualTo(FlagState.Dropped));
            Assert.That(game.GetCarrierId(Side.Bravo), Is.Null);
        }

        [Test]
        public void AnAllyTouchingTheDroppedFlagReturnsItToTheBase()
        {
            var game = Start();
            var returned = new List<string>();
            game.FlagReturned += (flag, player) => returned.Add(flag.Id + ":" + (player.HasValue ? player.Value.ToString(CultureInfo.InvariantCulture) : "-"));
            game.Touch(Side.Bravo, _alpha1);
            game.ReportKill(_bravo1, _alpha1);

            Assert.That(game.Touch(Side.Bravo, _bravo2), Is.True);

            Assert.That(game.GetFlagState(Side.Bravo), Is.EqualTo(FlagState.AtBase));
            Assert.That(game.GetReturnRemaining(Side.Bravo), Is.Null);
            Assert.That(returned, Is.EqualTo(_expectedReturned));
        }

        [Test]
        public void AnEnemyTouchingTheDroppedFlagCarriesItAgain()
        {
            var game = Start();
            game.Touch(Side.Bravo, _alpha1);
            game.ReportKill(_bravo1, _alpha1);

            Assert.That(game.Touch(Side.Bravo, _alpha2), Is.True);

            Assert.That(game.GetFlagState(Side.Bravo), Is.EqualTo(FlagState.Carried));
            Assert.That(game.GetCarrierId(Side.Bravo), Is.EqualTo(_alpha2));
            Assert.That(game.GetReturnRemaining(Side.Bravo), Is.Null);
        }

        [Test]
        public void ADroppedFlagReturnsByItselfAfterTheReturnSeconds()
        {
            var game = Start();
            var returned = new List<string>();
            game.FlagReturned += (flag, player) => returned.Add(flag.Id + ":" + (player.HasValue ? player.Value.ToString(CultureInfo.InvariantCulture) : "-"));
            game.Touch(Side.Bravo, _alpha1);
            game.ReportKill(_bravo1, _alpha1);

            game.Session.Tick(4f);
            Assert.That(game.GetFlagState(Side.Bravo), Is.EqualTo(FlagState.Dropped));
            Assert.That(game.GetReturnRemaining(Side.Bravo), Is.EqualTo(6f).Within(1e-4f));
            Assert.That(returned, Is.Empty);

            game.Session.Tick(6f);

            Assert.That(game.GetFlagState(Side.Bravo), Is.EqualTo(FlagState.AtBase));
            Assert.That(game.GetReturnRemaining(Side.Bravo), Is.Null);
            Assert.That(returned, Is.EqualTo(_expectedByItself));
        }

        [Test]
        public void ManySmallTicksStillReturnTheFlagOnTime()
        {
            var game = Start();
            game.Touch(Side.Bravo, _alpha1);
            game.ReportKill(_bravo1, _alpha1);

            for (var i = 0; i < 100; i++)
            {
                game.Session.Tick(_returnSeconds / 100f);
            }

            Assert.That(game.GetFlagState(Side.Bravo), Is.EqualTo(FlagState.AtBase));
        }

        [Test]
        public void ReachingYourBaseWithTheEnemyFlagScoresAPoint()
        {
            var game = Start();
            var captured = new List<string>();
            game.FlagCaptured += (flag, player) => captured.Add(flag.Id + ":" + player + " score=" + game.Session.GetScore(Side.Alpha));
            game.Touch(Side.Bravo, _alpha1);

            Assert.That(game.ReachBase(_alpha1), Is.True);

            Assert.That(game.Session.GetScore(Side.Alpha), Is.EqualTo(1d));
            Assert.That(game.Session.GetScore(Side.Bravo), Is.Zero);
            Assert.That(game.GetCaptures(Side.Alpha), Is.EqualTo(1));
            Assert.That(game.GetCaptures(Side.Bravo), Is.Zero);
            Assert.That(game.GetFlagState(Side.Bravo), Is.EqualTo(FlagState.AtBase), "the captured flag goes back to the enemy base");
            Assert.That(game.IsCarrying(_alpha1), Is.False);
            Assert.That(captured, Is.EqualTo(_expectedCaptured), "the point is already added when the capture is announced");
        }

        [Test]
        public void ReachingYourBaseWithoutTheEnemyFlagScoresNothing()
        {
            var game = Start();

            Assert.That(game.ReachBase(_alpha1), Is.False);

            Assert.That(game.Session.GetScore(Side.Alpha), Is.Zero);
            Assert.That(game.GetCaptures(Side.Alpha), Is.Zero);
        }

        [Test]
        public void NoCaptureWhileYourOwnFlagIsAwayFromItsBase()
        {
            var game = Start();
            game.Touch(Side.Bravo, _alpha1);
            game.Touch(Side.Alpha, _bravo1);

            Assert.That(game.ReachBase(_alpha1), Is.False, "the own flag is being carried");
            Assert.That(game.IsCarrying(_alpha1), Is.True, "the carrier keeps the flag");

            game.ReportKill(_alpha2, _bravo1);
            Assert.That(game.GetFlagState(Side.Alpha), Is.EqualTo(FlagState.Dropped));
            Assert.That(game.ReachBase(_alpha1), Is.False, "the own flag is lying on the ground");

            game.Touch(Side.Alpha, _alpha2);
            Assert.That(game.ReachBase(_alpha1), Is.True, "the own flag is back at its base");
            Assert.That(game.Session.GetScore(Side.Alpha), Is.EqualTo(1d));
        }

        [Test]
        public void AFallenPlayerCanNeitherTouchNorCapture()
        {
            var game = Start();
            game.Touch(Side.Bravo, _alpha1);
            game.ReportKill(_alpha1, _bravo1);

            Assert.That(game.Touch(Side.Alpha, _bravo1), Is.False);
            Assert.That(game.GetFlagState(Side.Alpha), Is.EqualTo(FlagState.AtBase));

            game.Session.ReportKill(_bravo2, _alpha1);
            Assert.That(game.ReachBase(_alpha1), Is.False);
            Assert.That(game.GetFlagState(Side.Bravo), Is.EqualTo(FlagState.Dropped), "the fallen carrier's flag is dropped before the capture is judged");
            Assert.That(game.Session.GetScore(Side.Alpha), Is.Zero);
        }

        [Test]
        public void APlayerWhoRespawnedCanTouchAgain()
        {
            var game = Start();
            game.ReportKill(_bravo1, _alpha1);

            game.Session.Tick(_respawnSeconds);

            Assert.That(game.Session.IsDown(_alpha1), Is.False);
            Assert.That(game.Touch(Side.Bravo, _alpha1), Is.True);
        }

        [Test]
        public void ReachingTheScoreLimitWinsTheMatch()
        {
            var game = Start(new CaptureTheFlagSpec(scoreLimit: 2, respawnSeconds: _respawnSeconds, returnSeconds: _returnSeconds));
            var order = new List<string>();
            game.Session.Ended += winner => order.Add("ended " + (winner.HasValue ? winner.Value.Id.ToString(CultureInfo.InvariantCulture) : "draw"));
            game.FlagCaptured += (flag, player) => order.Add("captured " + player);

            game.Touch(Side.Bravo, _alpha1);
            game.ReachBase(_alpha1);
            Assert.That(game.Session.State, Is.EqualTo(GameModeState.Playing));

            game.Touch(Side.Bravo, _alpha2);
            game.ReachBase(_alpha2);

            Assert.That(game.Session.State, Is.EqualTo(GameModeState.Ended));
            Assert.That(game.Session.Winner, Is.EqualTo(Side.Alpha));
            Assert.That(game.Session.GetScore(Side.Alpha), Is.EqualTo(2d));
            Assert.That(order, Is.EqualTo(_expectedOrder));
        }

        [Test]
        public void WhenTimeRunsOutTheTeamWithMoreCapturesWins()
        {
            var game = Start(new CaptureTheFlagSpec(scoreLimit: 3, timeLimitSeconds: 60f, respawnSeconds: _respawnSeconds, returnSeconds: _returnSeconds));
            game.Touch(Side.Alpha, _bravo1);
            game.ReachBase(_bravo1);

            game.Session.Tick(60f);

            Assert.That(game.Session.State, Is.EqualTo(GameModeState.Ended));
            Assert.That(game.Session.Winner, Is.EqualTo(Side.Bravo));
        }

        [Test]
        public void WhenTimeRunsOutLevelTheMatchIsADraw()
        {
            var game = Start(new CaptureTheFlagSpec(timeLimitSeconds: 60f));

            game.Session.Tick(60f);

            Assert.That(game.Session.State, Is.EqualTo(GameModeState.Ended));
            Assert.That(game.Session.Winner, Is.Null);
        }

        [Test]
        public void NothingHappensToTheFlagsOutsideTheMatch()
        {
            var game = Create();

            Assert.That(game.Touch(Side.Bravo, _alpha1), Is.False, "before the start");
            Assert.That(game.GetFlagState(Side.Bravo), Is.EqualTo(FlagState.AtBase));

            game.Session.Start();
            game.Touch(Side.Bravo, _alpha1);
            game.Session.Finish(null);

            Assert.That(game.ReachBase(_alpha1), Is.False, "after the end");
            Assert.That(game.Touch(Side.Alpha, _bravo1), Is.False, "after the end");
            Assert.That(game.GetCarrierId(Side.Bravo), Is.EqualTo(_alpha1), "the flags stay as they were when the match ended");
            Assert.That(game.Session.GetScore(Side.Alpha), Is.Zero);
        }

        [Test]
        public void ResetSendsBothFlagsHomeAndClearsTheCaptures()
        {
            var game = Start();
            var events = 0;
            game.Touch(Side.Bravo, _alpha1);
            game.ReachBase(_alpha1);
            game.Touch(Side.Bravo, _alpha2);
            game.Touch(Side.Alpha, _bravo1);
            game.ReportKill(_alpha1, _bravo1);
            game.FlagReturned += (flag, player) => events++;
            game.FlagDropped += (flag, player) => events++;

            game.Reset();

            Assert.That(game.Session.State, Is.EqualTo(GameModeState.Waiting));
            Assert.That(game.Session.GetScore(Side.Alpha), Is.Zero);
            Assert.That(game.GetCaptures(Side.Alpha), Is.Zero);
            foreach (var side in CaptureTheFlag.FlagSides)
            {
                Assert.That(game.GetFlagState(side), Is.EqualTo(FlagState.AtBase));
                Assert.That(game.GetCarrierId(side), Is.Null);
            }

            Assert.That(events, Is.Zero);
            Assert.That(game.Session.HasPlayer(_alpha1), Is.True, "the players stay");
        }

        [Test]
        public void ResettingInsideAnEventStopsTheRest()
        {
            var game = Start();
            var captured = 0;
            game.FlagCaptured += (flag, player) => captured++;
            game.Touch(Side.Bravo, _alpha1);
            game.Touch(Side.Alpha, _bravo1);
            game.ReportKill(_alpha2, _bravo1);
            game.FlagReturned += (flag, player) => game.Reset();

            Assert.That(game.Touch(Side.Alpha, _alpha2), Is.True);
            Assert.That(game.ReachBase(_alpha1), Is.False, "the match was reset when the own flag came home");
            Assert.That(captured, Is.Zero);
            Assert.That(game.GetFlagState(Side.Bravo), Is.EqualTo(FlagState.AtBase));
        }

        [Test]
        public void UnknownFlagsAndPlayersAreRejected()
        {
            var game = Start();

            Assert.Throws<ArgumentOutOfRangeException>(() => game.Touch(new Side(2), _alpha1));
            Assert.Throws<ArgumentOutOfRangeException>(() => game.Touch(Side.Bravo, 99));
            Assert.Throws<ArgumentOutOfRangeException>(() => game.ReachBase(99));
            Assert.Throws<ArgumentOutOfRangeException>(() => game.GetFlagState(new Side(2)));
            Assert.Throws<ArgumentOutOfRangeException>(() => game.ReportKill(_alpha1, 99));
        }

        [Test]
        public void TheStatusCopiesBothFlagsAndWhetherTheLocalPlayerCarries()
        {
            var game = Start();
            game.Touch(Side.Bravo, _alpha1);
            game.Touch(Side.Alpha, _bravo1);
            game.ReportKill(_alpha2, _bravo1);

            var carrier = new CaptureTheFlagStatus(game, _alpha1);
            var other = new CaptureTheFlagStatus(game, _bravo2);
            var nobody = new CaptureTheFlagStatus(game, null);

            Assert.That(carrier.LocalPlayerId, Is.EqualTo(_alpha1));
            Assert.That(carrier.LocalSide, Is.EqualTo(Side.Alpha));
            Assert.That(carrier.IsLocalCarrying, Is.True);
            Assert.That(carrier.CarriedFlag, Is.EqualTo(Side.Bravo));
            Assert.That(carrier.GetState(Side.Bravo), Is.EqualTo(FlagState.Carried));
            Assert.That(carrier.GetCarrierId(Side.Bravo), Is.EqualTo(_alpha1));
            Assert.That(carrier.GetReturnSeconds(Side.Bravo), Is.Null);
            Assert.That(carrier.GetState(Side.Alpha), Is.EqualTo(FlagState.Dropped));
            Assert.That(carrier.GetCarrierId(Side.Alpha), Is.Null);
            Assert.That(carrier.GetReturnSeconds(Side.Alpha), Is.EqualTo(_returnSeconds));

            Assert.That(other.LocalSide, Is.EqualTo(Side.Bravo));
            Assert.That(other.IsLocalCarrying, Is.False);
            Assert.That(other.CarriedFlag, Is.Null);

            Assert.That(nobody.LocalPlayerId, Is.Null);
            Assert.That(nobody.LocalSide, Is.Null);
            Assert.That(nobody.IsLocalCarrying, Is.False);
            Assert.That(nobody.GetState(Side.Bravo), Is.EqualTo(FlagState.Carried));

            game.ReachBase(_alpha1);
            Assert.That(carrier.GetState(Side.Bravo), Is.EqualTo(FlagState.Carried), "the status is a copy taken when it was made");
            Assert.That(carrier.IsLocalCarrying, Is.True);
        }

        [Test]
        public void TheStatusRejectsANullGameAndUnknownFlags()
        {
            var game = Start();
            var status = new CaptureTheFlagStatus(game, 99);

            Assert.Throws<ArgumentNullException>(() => new CaptureTheFlagStatus(null, _alpha1));
            Assert.Throws<ArgumentOutOfRangeException>(() => status.GetState(new Side(2)));
            Assert.That(status.LocalSide, Is.Null, "a player who has not joined has no side");
            Assert.That(status.IsLocalCarrying, Is.False);
        }

        [Test]
        public void TheStatusCanBeBuiltFromReceivedFlags()
        {
            var status = new CaptureTheFlagStatus(
                FlagState.Dropped, null, 7f,
                FlagState.Carried, _alpha1, null,
                _alpha1, Side.Alpha);

            Assert.That(status.GetState(Side.Alpha), Is.EqualTo(FlagState.Dropped));
            Assert.That(status.GetCarrierId(Side.Alpha), Is.Null);
            Assert.That(status.GetReturnSeconds(Side.Alpha), Is.EqualTo(7f));
            Assert.That(status.GetState(Side.Bravo), Is.EqualTo(FlagState.Carried));
            Assert.That(status.GetCarrierId(Side.Bravo), Is.EqualTo(_alpha1));
            Assert.That(status.GetReturnSeconds(Side.Bravo), Is.Null);
            Assert.That(status.LocalPlayerId, Is.EqualTo(_alpha1));
            Assert.That(status.LocalSide, Is.EqualTo(Side.Alpha));
            Assert.That(status.CarriedFlag, Is.EqualTo(Side.Bravo));
            Assert.That(status.IsLocalCarrying, Is.True);
        }

        [Test]
        public void AReceivedStatusWithoutALocalSideCarriesNothing()
        {
            var status = new CaptureTheFlagStatus(
                FlagState.AtBase, null, null,
                FlagState.Carried, _alpha1, null,
                _alpha1, null);

            Assert.That(status.LocalPlayerId, Is.EqualTo(_alpha1));
            Assert.That(status.LocalSide, Is.Null, "a player missing from the snapshot has no side");
            Assert.That(status.IsLocalCarrying, Is.False);
        }

        [Test]
        public void AReceivedStatusMustBeConsistent()
        {
            Assert.Throws<ArgumentException>(() => new CaptureTheFlagStatus(
                FlagState.Carried, null, null, FlagState.AtBase, null, null, null, null), "a carried flag needs a carrier");
            Assert.Throws<ArgumentException>(() => new CaptureTheFlagStatus(
                FlagState.AtBase, _alpha1, null, FlagState.AtBase, null, null, null, null), "a flag at its base has no carrier");
            Assert.Throws<ArgumentException>(() => new CaptureTheFlagStatus(
                FlagState.AtBase, null, null, FlagState.Dropped, null, null, null, null), "a dropped flag needs its return seconds");
            Assert.Throws<ArgumentException>(() => new CaptureTheFlagStatus(
                FlagState.AtBase, null, 3f, FlagState.AtBase, null, null, null, null), "only a dropped flag has return seconds");
            Assert.Throws<ArgumentOutOfRangeException>(() => new CaptureTheFlagStatus(
                FlagState.Dropped, null, -1f, FlagState.AtBase, null, null, null, null));
            Assert.Throws<ArgumentOutOfRangeException>(() => new CaptureTheFlagStatus(
                (FlagState)9, null, null, FlagState.AtBase, null, null, null, null));
            Assert.Throws<ArgumentOutOfRangeException>(() => new CaptureTheFlagStatus(
                FlagState.AtBase, null, null, FlagState.AtBase, null, null, _alpha1, new Side(2)));
        }

        [Test]
        public void ReachingAFlagLooksAcrossTheGroundWithinAStepUpOrDown()
        {
            var flag = new System.Numerics.Vector3(10f, 1f, 10f);

            Assert.That(CaptureTheFlag.IsWithin(new System.Numerics.Vector3(11f, 1f, 10f), flag, 1.5f), Is.True);
            Assert.That(CaptureTheFlag.IsWithin(new System.Numerics.Vector3(11.5f, 1f, 10f), flag, 1.5f), Is.True, "the edge counts");
            Assert.That(CaptureTheFlag.IsWithin(new System.Numerics.Vector3(11.6f, 1f, 10f), flag, 1.5f), Is.False);
            Assert.That(CaptureTheFlag.IsWithin(new System.Numerics.Vector3(10f, 3f, 10f), flag, 1.5f), Is.True, "2m above is still in reach");
            Assert.That(CaptureTheFlag.IsWithin(new System.Numerics.Vector3(10f, 3.1f, 10f), flag, 1.5f), Is.False);
            Assert.That(CaptureTheFlag.IsWithin(new System.Numerics.Vector3(10f, -1.1f, 10f), flag, 1.5f), Is.False);
            Assert.That(CaptureTheFlag.ReachHeight, Is.EqualTo(2f));
        }

        private static CaptureTheFlag Create(CaptureTheFlagSpec spec = null)
        {
            var game = new CaptureTheFlag(spec ?? new CaptureTheFlagSpec(respawnSeconds: _respawnSeconds, returnSeconds: _returnSeconds));
            game.Session.AddPlayer(_alpha1, Side.Alpha);
            game.Session.AddPlayer(_alpha2, Side.Alpha);
            game.Session.AddPlayer(_bravo1, Side.Bravo);
            game.Session.AddPlayer(_bravo2, Side.Bravo);
            return game;
        }

        private static CaptureTheFlag Start(CaptureTheFlagSpec spec = null)
        {
            var game = Create(spec);
            game.Session.Start();
            return game;
        }
    }
}
