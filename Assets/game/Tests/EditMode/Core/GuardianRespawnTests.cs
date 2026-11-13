using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    // Unity の EditMode テストと dotnet/Fps.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class GuardianRespawnTests
    {
        private const float RespawnSeconds = 3f;

        private static readonly int[] _three = { 3 };
        private static readonly string[] _chosenZeroAndOne = { "Side 0:0", "Side 1:1" };
        private static readonly string[] _fellBravoOne = { "Side 1:1" };
        private static readonly string[] _leaderLinesOwnBravo = { "BRAVO リーダー 撃破 (復活不可)", "ALPHA リーダー 生存" };

        [Test]
        public void StartingTheMatchPicksOneLeaderPerTeam()
        {
            var chosen = new List<string>();
            var session = GuardianMode.CreateSession(60f, RespawnSeconds, count => count - 1, out var leaders);
            leaders.LeaderChosen += (side, id) => chosen.Add(side + ":" + id);
            AddTeams(session, 3);

            Assert.That(leaders.LeaderSides, Is.Empty, "leaders are chosen when the match starts");
            session.Start();

            Assert.That(leaders.GetLeader(Side.Alpha), Is.EqualTo((int?)4));
            Assert.That(leaders.GetLeader(Side.Bravo), Is.EqualTo((int?)5));
            Assert.That(leaders.IsLeader(5), Is.True);
            Assert.That(leaders.IsLeader(3), Is.False);
            Assert.That(chosen, Has.Count.EqualTo(2));
        }

        [Test]
        public void TeammatesRespawnWhileTheLeaderIsAlive()
        {
            var session = Playing(out var leaders);
            var released = new List<int>();
            session.RespawnReady += released.Add;

            session.ReportKill(0, 3);

            Assert.That(session.IsAwaitingRespawn(3), Is.True);
            Assert.That(session.GetRespawnRemaining(3), Is.EqualTo(RespawnSeconds));
            Assert.That(leaders.CanRespawn(Side.Bravo), Is.True);

            session.Tick(RespawnSeconds);

            Assert.That(released, Is.EqualTo(_three));
            Assert.That(session.IsDown(3), Is.False);
        }

        [Test]
        public void WhenTheLeaderFallsNobodyOnThatTeamReturns()
        {
            var session = Playing(out var leaders);
            var released = new List<int>();
            var fell = new List<string>();
            session.RespawnReady += released.Add;
            leaders.LeaderFell += (side, id) => fell.Add(side + ":" + id);

            session.ReportKill(0, 3);
            session.ReportKill(0, 1);

            Assert.That(fell, Is.EqualTo(_fellBravoOne));
            Assert.That(leaders.HasLeaderFallen(Side.Bravo), Is.True);
            Assert.That(leaders.HasLeaderFallen(Side.Alpha), Is.False);
            Assert.That(session.IsEliminated(1), Is.True, "the leader does not return");
            Assert.That(session.IsEliminated(3), Is.True, "the wait that had begun is cancelled");
            Assert.That(session.GetRespawnRemaining(3), Is.Null);

            session.Tick(RespawnSeconds * 2f);
            Assert.That(released, Is.Empty);
            Assert.That(session.State, Is.EqualTo(GameModeState.Playing), "member 5 is still standing");

            session.ReportKill(0, 5);
            Assert.That(session.IsEliminated(5), Is.True, "after the leader falls, a teammate is out at once");
            session.Tick(0f);

            Assert.That(session.State, Is.EqualTo(GameModeState.Ended));
            Assert.That(session.Winner, Is.EqualTo((Side?)Side.Alpha));
            Assert.That(fell, Has.Count.EqualTo(1), "the leader falls once");
        }

        [Test]
        public void TheOwnLeaderFallingAlsoStopsTheOwnRespawn()
        {
            var session = Playing(out var leaders);

            session.ReportKill(1, 0);
            session.ReportKill(1, 2);

            Assert.That(leaders.CanRespawn(Side.Alpha), Is.False);
            Assert.That(session.IsEliminated(2), Is.True);
            Assert.That(leaders.CanRespawn(Side.Bravo), Is.True);
        }

        [Test]
        public void TheLeaderFallingLastWipesOutTheTeamEvenWithTeammatesWaiting()
        {
            var session = Playing(out _);

            session.ReportKill(0, 3);
            session.ReportKill(0, 5);
            session.Tick(0f);
            Assert.That(session.State, Is.EqualTo(GameModeState.Playing), "teammates waiting for the respawn keep the team in");

            session.ReportKill(0, 1);
            session.Tick(0f);

            Assert.That(session.State, Is.EqualTo(GameModeState.Ended));
            Assert.That(session.Winner, Is.EqualTo((Side?)Side.Alpha));
        }

        [Test]
        public void TheLeaderLeavingCountsAsFalling()
        {
            var session = Playing(out var leaders);
            var released = new List<int>();
            session.RespawnReady += released.Add;
            session.ReportKill(0, 3);

            session.RemovePlayer(1);

            Assert.That(leaders.HasLeaderFallen(Side.Bravo), Is.True);
            Assert.That(leaders.GetLeader(Side.Bravo), Is.EqualTo((int?)1), "the leader stays recorded");
            Assert.That(session.IsEliminated(3), Is.True);

            session.Tick(RespawnSeconds * 2f);
            Assert.That(released, Is.Empty);
        }

        [Test]
        public void AtTimeUpTheTeamWhoseLeaderStandsWinsRegardlessOfKills()
        {
            var session = Playing(out _, 10f);
            session.ReportKill(3, 2);
            session.ReportKill(3, 4);
            session.ReportKill(0, 1);
            Assert.That(session.GetScore(Side.Bravo), Is.GreaterThan(session.GetScore(Side.Alpha)));

            session.Tick(10f);

            Assert.That(session.State, Is.EqualTo(GameModeState.Ended));
            Assert.That(session.Winner, Is.EqualTo((Side?)Side.Alpha));
        }

        [Test]
        public void AtTimeUpWithBothLeadersStandingKillsDecide()
        {
            var session = Playing(out _, 10f);
            session.ReportKill(3, 2);

            session.Tick(10f);

            Assert.That(session.Winner, Is.EqualTo((Side?)Side.Bravo));
        }

        [Test]
        public void ATeamWithoutPlayersGetsNoLeaderAndDoesNotDecideTheTimeUp()
        {
            var session = GuardianMode.CreateSession(10f, RespawnSeconds, _ => 0, out var leaders);
            session.AddPlayer(0, Side.Alpha);
            session.AddPlayer(2, Side.Alpha);
            session.Start();

            Assert.That(leaders.GetLeader(Side.Bravo), Is.Null);
            session.Tick(10f);

            Assert.That(session.State, Is.EqualTo(GameModeState.Ended));
            Assert.That(session.Winner, Is.Null);
        }

        [Test]
        public void ResetForgetsTheLeadersAndTheNextStartPicksAgain()
        {
            var picks = 0;
            var session = GuardianMode.CreateSession(60f, RespawnSeconds, count => picks++ % count, out var leaders);
            AddTeams(session, 3);
            session.Start();
            session.ReportKill(0, leaders.GetLeader(Side.Bravo).Value);
            Assert.That(leaders.HasLeaderFallen(Side.Bravo), Is.True);

            session.Reset();
            Assert.That(leaders.LeaderSides, Is.Empty);
            Assert.That(leaders.HasLeaderFallen(Side.Bravo), Is.False);

            session.Start();
            Assert.That(leaders.GetLeader(Side.Alpha), Is.Not.Null);
            Assert.That(leaders.GetLeader(Side.Bravo), Is.Not.Null);
            Assert.That(picks, Is.EqualTo(4));
        }

        [Test]
        public void TheMatchEndingKeepsTheLeadersForTheResult()
        {
            var session = Playing(out var leaders);
            session.ReportKill(0, 1);
            session.ReportKill(0, 3);
            session.ReportKill(0, 5);
            session.Tick(0f);

            Assert.That(session.State, Is.EqualTo(GameModeState.Ended));
            Assert.That(leaders.GetLeader(Side.Bravo), Is.EqualTo((int?)1));
            Assert.That(leaders.HasLeaderFallen(Side.Bravo), Is.True);
        }

        [Test]
        public void SetLeaderRejectsASecondLeaderAndTreatsADownedPlayerAsFallen()
        {
            var leaders = new GuardianRespawn(new IndividualRespawn(RespawnSeconds));
            var session = new ModeSession(new TwoTeams(), 60f, Array.Empty<IScoreRule>(), leaders, Array.Empty<IWinCondition>());
            AddTeams(session, 2);
            session.Start();
            session.ReportKill(0, 3);
            Assert.That(session.IsAwaitingRespawn(3), Is.True);

            leaders.SetLeader(session, 3);

            Assert.That(leaders.HasLeaderFallen(Side.Bravo), Is.True);
            Assert.That(leaders.GetRemaining(3), Is.Null, "the leader's own wait is cancelled");
            Assert.Throws<InvalidOperationException>(() => leaders.SetLeader(session, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => leaders.SetLeader(session, 99));
        }

        [Test]
        public void ChooseLeadersRejectsAPickOutsideTheTeam()
        {
            var session = GuardianMode.CreateSession(60f, RespawnSeconds, count => count, out _);
            AddTeams(session, 2);

            Assert.Throws<InvalidOperationException>(() => session.Start());
        }

        [Test]
        public void ConstructorsRejectMissingParts()
        {
            Assert.Throws<ArgumentNullException>(() => new GuardianRespawn(null));
            Assert.Throws<ArgumentNullException>(() => new LeaderStandingWin(null));
            Assert.Throws<ArgumentNullException>(() => GuardianMode.CreateSession(60f, 1f, (Func<int, int>)null, out _));
            Assert.Throws<ArgumentNullException>(() => GuardianMode.CreateSession(60f, 1f, (Random)null, out _));
            Assert.Throws<ArgumentOutOfRangeException>(() => GuardianMode.CreateSession(60f, -1f, _ => 0, out _));
        }

        [Test]
        public void TheHudListsTheOwnLeaderFirst()
        {
            var session = Playing(out var leaders);
            session.ReportKill(0, 1);

            Assert.That(GuardianText.Leaders(leaders, Side.Bravo), Is.EqualTo(_leaderLinesOwnBravo));
            Assert.That(GuardianText.Leaders(leaders, null)[0], Is.EqualTo("ALPHA リーダー 生存"));
            Assert.That(GuardianText.LeaderChosenLog(Side.Bravo, 4), Is.EqualTo("[Mode] leader side=1 player=4"));
            Assert.That(GuardianText.LeaderFellLog(Side.Bravo, 4), Is.EqualTo("[Mode] leader-down side=1 player=4 respawn=off"));
        }

        [Test]
        public void TheLeadersArePickedFromEachTeamInPlayerIdOrder()
        {
            var chosen = new List<string>();
            var session = GuardianMode.CreateSession(60f, RespawnSeconds, _ => 0, out var leaders);
            leaders.LeaderChosen += (side, id) => chosen.Add(side + ":" + id);
            AddTeams(session, 2);
            session.Start();

            Assert.That(chosen, Is.EqualTo(_chosenZeroAndOne));
        }

        /// <summary>Alpha に 0, 2, 4...、Bravo に 1, 3, 5... を入れ、各チームの最小の ID (Alpha 0・Bravo 1) をリーダーにして始める。</summary>
        private static ModeSession Playing(out GuardianRespawn leaders, float timeLimitSeconds = 60f)
        {
            var session = GuardianMode.CreateSession(timeLimitSeconds, RespawnSeconds, _ => 0, out leaders);
            AddTeams(session, 3);
            session.Start();
            return session;
        }

        private static void AddTeams(ModeSession session, int perTeam)
        {
            for (var i = 0; i < perTeam * 2; i++)
            {
                session.AddPlayer(i, i % 2 == 0 ? Side.Alpha : Side.Bravo);
            }
        }
    }
}
