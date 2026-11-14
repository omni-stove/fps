using System;
using System.Collections.Generic;
using System.Globalization;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    // Unity の EditMode テストと dotnet/Fps.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class HardpointTests
    {
        private const int _alphaId = 1;
        private const int _bravoId = 2;

        private static readonly string[] _expectedFirstMove = { "0>1" };
        private static readonly string[] _expectedFullCycle = { "1>2", "2>3", "3>0" };
        private static readonly string[] _expectedHeldHudLines = { "拠点 A (次は B まで 9 秒)", "ALPHA が確保中" };

        [Test]
        public void SpecRejectsInvalidValues()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new HardpointSpec(1, 10f, 1f, 100, 300f, 3f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new HardpointSpec(4, 0f, 1f, 100, 300f, 3f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new HardpointSpec(4, float.PositiveInfinity, 1f, 100, 300f, 3f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new HardpointSpec(4, 10f, float.NaN, 100, 300f, 3f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new HardpointSpec(4, 10f, 1f, 0, 300f, 3f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new HardpointSpec(4, 10f, 1f, 100, -1f, 3f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new HardpointSpec(4, 10f, 1f, 100, 300f, -0.1f));
            Assert.Throws<ArgumentNullException>(() => new Hardpoint(null));
        }

        [Test]
        public void TheActivePointMovesInOrderEveryRotateSeconds()
        {
            var hardpoint = Create(rotateSeconds: 10f);
            var moves = RecordMoves(hardpoint);
            hardpoint.Session.Start();

            Assert.That(hardpoint.ActivePoint, Is.Zero);
            Assert.That(hardpoint.NextPoint, Is.EqualTo(1));

            hardpoint.Session.Tick(9.5f);
            Assert.That(hardpoint.ActivePoint, Is.Zero);
            Assert.That(hardpoint.SecondsUntilMove, Is.EqualTo(0.5f));

            hardpoint.Session.Tick(0.5f);
            Assert.That(hardpoint.ActivePoint, Is.EqualTo(1));
            Assert.That(hardpoint.SecondsUntilMove, Is.EqualTo(10f));
            Assert.That(moves, Is.EqualTo(_expectedFirstMove));

            moves.Clear();
            hardpoint.Session.Tick(30f);
            Assert.That(moves, Is.EqualTo(_expectedFullCycle), "one long tick moves once per elapsed period and wraps to point 0");
            Assert.That(hardpoint.ActivePoint, Is.Zero);
            Assert.That(hardpoint.SecondsUntilMove, Is.EqualTo(10f));
        }

        [Test]
        public void NothingMovesBeforeTheStart()
        {
            var hardpoint = Create(rotateSeconds: 10f);
            var moves = RecordMoves(hardpoint);

            hardpoint.Session.Tick(20f);

            Assert.That(hardpoint.ActivePoint, Is.Zero);
            Assert.That(hardpoint.SecondsUntilMove, Is.EqualTo(10f));
            Assert.That(moves, Is.Empty);
        }

        [Test]
        public void ATeamAloneOnTheActivePointScores()
        {
            var hardpoint = CreateWithPlayers();
            hardpoint.SetPlayerPoint(_alphaId, 0);

            Assert.That(hardpoint.Status, Is.EqualTo(HardpointStatus.Held));
            Assert.That(hardpoint.Holder, Is.EqualTo((Side?)Side.Alpha));

            hardpoint.Session.Tick(2f);

            Assert.That(hardpoint.Session.GetScore(Side.Alpha), Is.EqualTo(2d));
            Assert.That(hardpoint.Session.GetScore(Side.Bravo), Is.Zero);
        }

        [Test]
        public void BothTeamsOnTheActivePointStopTheScore()
        {
            var hardpoint = CreateWithPlayers();
            hardpoint.SetPlayerPoint(_alphaId, 0);
            hardpoint.SetPlayerPoint(_bravoId, 0);

            hardpoint.Session.Tick(3f);

            Assert.That(hardpoint.Status, Is.EqualTo(HardpointStatus.Contested));
            Assert.That(hardpoint.Holder, Is.Null);
            Assert.That(hardpoint.Session.GetScore(Side.Alpha), Is.Zero);
            Assert.That(hardpoint.Session.GetScore(Side.Bravo), Is.Zero);
        }

        [Test]
        public void StandingOnAnInactivePointScoresNothing()
        {
            var hardpoint = CreateWithPlayers();
            hardpoint.SetPlayerPoint(_alphaId, 2);

            hardpoint.Session.Tick(3f);

            Assert.That(hardpoint.Status, Is.EqualTo(HardpointStatus.Empty));
            Assert.That(hardpoint.CountOnActivePoint(Side.Alpha), Is.Zero);
            Assert.That(hardpoint.Session.GetScore(Side.Alpha), Is.Zero);
        }

        [Test]
        public void ADownedPlayerDoesNotHoldOrContest()
        {
            var hardpoint = CreateWithPlayers();
            hardpoint.SetPlayerPoint(_alphaId, 0);
            hardpoint.SetPlayerPoint(_bravoId, 0);

            hardpoint.Session.ReportKill(_alphaId, _bravoId);

            Assert.That(hardpoint.Session.IsDown(_bravoId), Is.True);
            Assert.That(hardpoint.Holder, Is.EqualTo((Side?)Side.Alpha));

            hardpoint.Session.Tick(1f);
            Assert.That(hardpoint.Session.GetScore(Side.Alpha), Is.EqualTo(1d), "kills give no points; holding does");
        }

        [Test]
        public void AfterTheMoveThePreviousPointNoLongerScores()
        {
            var hardpoint = CreateWithPlayers(rotateSeconds: 2f);
            hardpoint.SetPlayerPoint(_alphaId, 0);

            hardpoint.Session.Tick(2f);
            Assert.That(hardpoint.ActivePoint, Is.EqualTo(1));
            Assert.That(hardpoint.Session.GetScore(Side.Alpha), Is.EqualTo(2d), "the tick that moved the point is counted with the holder at its start");

            hardpoint.Session.Tick(1f);
            Assert.That(hardpoint.Status, Is.EqualTo(HardpointStatus.Empty));
            Assert.That(hardpoint.Session.GetScore(Side.Alpha), Is.EqualTo(2d));

            hardpoint.SetPlayerPoint(_alphaId, 1);
            hardpoint.Session.Tick(0.5f);
            Assert.That(hardpoint.Session.GetScore(Side.Alpha), Is.EqualTo(2.5d));
        }

        [Test]
        public void ReachingTheScoreLimitEndsWithTheHolderAsWinner()
        {
            var hardpoint = CreateWithPlayers(rotateSeconds: 30f, scoreLimit: 5);
            hardpoint.SetPlayerPoint(_bravoId, 0);

            hardpoint.Session.Tick(10f);

            Assert.That(hardpoint.Session.State, Is.EqualTo(GameModeState.Ended));
            Assert.That(hardpoint.Session.Winner, Is.EqualTo((Side?)Side.Bravo));
            Assert.That(hardpoint.Session.GetScore(Side.Bravo), Is.EqualTo(5d));
            Assert.That(hardpoint.Session.RemainingSeconds, Is.EqualTo(295f), "the clock stops when the limit is reached");
            Assert.That(hardpoint.SecondsUntilMove, Is.EqualTo(25f), "the rotation stops there too");
        }

        [Test]
        public void TimeUpGivesTheWinToTheTeamWithMorePoints()
        {
            var hardpoint = CreateWithPlayers(rotateSeconds: 100f, timeLimitSeconds: 10f);
            hardpoint.SetPlayerPoint(_alphaId, 0);
            hardpoint.Session.Tick(4f);
            hardpoint.SetPlayerPoint(_alphaId, null);
            hardpoint.Session.Tick(6f);

            Assert.That(hardpoint.Session.State, Is.EqualTo(GameModeState.Ended));
            Assert.That(hardpoint.Session.Winner, Is.EqualTo((Side?)Side.Alpha));
        }

        [Test]
        public void ResetReturnsToTheFirstPointAndForgetsWhoWasThere()
        {
            var hardpoint = CreateWithPlayers(rotateSeconds: 2f);
            var moves = RecordMoves(hardpoint);
            hardpoint.SetPlayerPoint(_alphaId, 1);
            hardpoint.Session.Tick(3f);
            Assert.That(hardpoint.ActivePoint, Is.EqualTo(1));
            moves.Clear();

            hardpoint.Reset();

            Assert.That(hardpoint.Session.State, Is.EqualTo(GameModeState.Waiting));
            Assert.That(hardpoint.ActivePoint, Is.Zero);
            Assert.That(hardpoint.SecondsUntilMove, Is.EqualTo(2f));
            Assert.That(hardpoint.GetPlayerPoint(_alphaId), Is.Null);
            Assert.That(hardpoint.Session.GetScore(Side.Alpha), Is.Zero);
            Assert.That(moves, Is.Empty, "reset does not report a move");
        }

        [Test]
        public void SetPlayerPointRejectsUnknownPlayersAndPoints()
        {
            var hardpoint = CreateWithPlayers();

            Assert.Throws<ArgumentOutOfRangeException>(() => hardpoint.SetPlayerPoint(99, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => hardpoint.SetPlayerPoint(_alphaId, 4));
            Assert.Throws<ArgumentOutOfRangeException>(() => hardpoint.SetPlayerPoint(_alphaId, -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => hardpoint.GetPlayerPoint(99));
            Assert.That(hardpoint.GetPlayerPoint(_alphaId), Is.Null);
        }

        [Test]
        public void ARemovedPlayerNoLongerHoldsThePoint()
        {
            var hardpoint = CreateWithPlayers();
            hardpoint.SetPlayerPoint(_alphaId, 0);

            hardpoint.Session.RemovePlayer(_alphaId);

            Assert.That(hardpoint.Status, Is.EqualTo(HardpointStatus.Empty));
        }

        [Test]
        public void TheHudAndLogTextNameThePointsByLetter()
        {
            Assert.That(GameText.Resolve(HardpointText.ActivePoint(1, 2, 22.3f)), Is.EqualTo("拠点 B (次は C まで 23 秒)"));
            Assert.That(GameText.Resolve(HardpointText.Status(HardpointStatus.Held, Side.Bravo)), Is.EqualTo("BRAVO が確保中"));
            Assert.That(HardpointText.Status(HardpointStatus.Contested, null), Is.EqualTo(HardpointText.Contested));
            Assert.That(GameText.Resolve(HardpointText.Contested), Is.EqualTo("争奪中"));
            Assert.That(HardpointText.Status(HardpointStatus.Empty, null), Is.EqualTo(HardpointText.Empty));
            Assert.That(GameText.Resolve(HardpointText.Empty), Is.EqualTo("拠点は無人"));
            Assert.Throws<ArgumentException>(() => HardpointText.Status(HardpointStatus.Held, null));
            Assert.That(HardpointText.MovedLog(3, 0), Is.EqualTo("[Mode] hardpoint moved from=D to=A"));
            Assert.That(HardpointText.StatusLog(1, HardpointStatus.Held, Side.Bravo), Is.EqualTo("[Mode] hardpoint point=B status=Held holder=1"));
            Assert.That(HardpointText.StatusLog(1, HardpointStatus.Contested, null), Is.EqualTo("[Mode] hardpoint point=B status=Contested holder=-"));

            var hardpoint = CreateWithPlayers(rotateSeconds: 10f);
            hardpoint.SetPlayerPoint(_alphaId, 0);
            hardpoint.Session.Tick(1f);
            Assert.That(Array.ConvertAll(HardpointText.HudLines(hardpoint), GameText.Resolve), Is.EqualTo(_expectedHeldHudLines));
        }

        private static Hardpoint Create(
            float rotateSeconds = 10f, int scoreLimit = 1000, float timeLimitSeconds = 300f)
        {
            return new Hardpoint(new HardpointSpec(4, rotateSeconds, 1f, scoreLimit, timeLimitSeconds, 1f));
        }

        private static Hardpoint CreateWithPlayers(
            float rotateSeconds = 10f, int scoreLimit = 1000, float timeLimitSeconds = 300f)
        {
            var hardpoint = Create(rotateSeconds, scoreLimit, timeLimitSeconds);
            hardpoint.Session.AddPlayer(_alphaId, Side.Alpha);
            hardpoint.Session.AddPlayer(_bravoId, Side.Bravo);
            hardpoint.Session.Start();
            return hardpoint;
        }

        private static List<string> RecordMoves(Hardpoint hardpoint)
        {
            var moves = new List<string>();
            hardpoint.PointMoved += (previous, next) => moves.Add(
                previous.ToString(CultureInfo.InvariantCulture) + ">" + next.ToString(CultureInfo.InvariantCulture));
            return moves;
        }
    }
}
