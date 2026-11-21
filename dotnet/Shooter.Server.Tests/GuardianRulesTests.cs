using System;
using System.IO;
using System.Numerics;
using NUnit.Framework;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.Server.Tests
{
    public sealed class GuardianRulesTests
    {
        private const int _alphaMember = 1;
        private const int _alphaLeader = 2;
        private const int _bravoMember = 3;
        private const int _bravoLeader = 4;
        private const int _lethal = 1000;

        // 1 tick は 1/60 秒。出直しの 1 秒は 60 tick で、30 tick では届かず 70 tick で必ず越える
        private const int _lessThanRespawn = 30;
        private const int _moreThanRespawn = 70;

        private static readonly Vector3 _leaderSpot = new Vector3(3f, 1f, -4f);

        private StringWriter _log;
        private MatchHost _host;
        private GuardianRules _rules;

        [SetUp]
        public void SetUp()
        {
            _log = new StringWriter();
        }

        [TearDown]
        public void TearDown()
        {
            _rules?.Stop();
            _log.Dispose();
        }

        [Test]
        public void TheObjectiveCountsItsOwnRespawnsAndLetsTheSessionKeepTheTime()
        {
            var settings = Settings(timeLimitSeconds: 45f, respawnSeconds: 7f);
            var rules = new GuardianRules(settings, new MatchHost(_log), Last);

            Assert.That(rules.Objective, Is.EqualTo(MatchObjective.Guardian));
            Assert.That(rules.PlaysInRounds, Is.False);
            Assert.That(rules.KeepsMatchScore, Is.False, "kills score as in the offline mode");
            Assert.That(rules.HoldsFire, Is.False);
            Assert.That(rules.RemainingSeconds, Is.Null);
            Assert.That(rules.CurrentRoundPhase(), Is.Null);
            Assert.That(rules.BotRally, Is.Null);
            Assert.That(rules.LeadersChosen, Is.False);
            Assert.That(rules.LeaderOf(Team.Alpha), Is.Null);
            Assert.That(rules.DecideWinner(Team.Bravo), Is.EqualTo(Team.Bravo), "without leaders the kill winner stands");
            Assert.That(rules.Tick(true, 1f), Is.False, "without a session there is nothing to choose from");
            var spec = rules.CreateSessionSpec(GameRules.Default, settings);
            Assert.That(spec.ScoreLimit, Is.EqualTo(int.MaxValue));
            Assert.That(spec.TimeLimitSeconds, Is.EqualTo(45f));
            Assert.That(spec.RespawnSeconds, Is.EqualTo(7f));
            Assert.That(spec.RespawnsByRound, Is.True, "the session must not bring back a team whose leader fell");

            var snapshot = new MatchSnapshot { AlphaPoints = 4, BravoPoints = 2 };
            rules.FillMatch(ref snapshot);
            Assert.That(snapshot.Objective, Is.EqualTo(MatchObjective.Guardian));
            Assert.That(snapshot.AlphaPoints, Is.Zero);
            Assert.That(snapshot.BravoPoints, Is.Zero);
            var items = Items();
            rules.FillObjectiveItems(items);
            Assert.That(items.Count, Is.Zero);
            var points = new SnapshotList<ControlPointSnapshot>(SnapshotMessage.MaxControlPoints, "control point");
            rules.FillControlPoints(points);
            Assert.That(points.Count, Is.Zero);
        }

        [Test]
        public void StartingLogsTheConfiguredTimes()
        {
            var rules = new GuardianRules(Settings(timeLimitSeconds: 45f, respawnSeconds: 7f), new MatchHost(_log), Last);

            rules.Start();
            rules.Start();
            _rules = rules;

            Assert.That(_log.ToString(), Is.EqualTo("[Guardian] net respawn=7 timeLimit=45" + Environment.NewLine));
        }

        [Test]
        public void EachTeamGetsALeaderPickedFromItsMembersInIdOrder()
        {
            Begin();

            Assert.That(_rules.LeadersChosen, Is.True);
            Assert.That(_rules.LeaderOf(Team.Alpha), Is.EqualTo(_alphaLeader));
            Assert.That(_rules.LeaderOf(Team.Bravo), Is.EqualTo(_bravoLeader));
            Assert.That(_rules.HasLeaderFallen(Team.Alpha), Is.False);
            Assert.That(_rules.HasLeaderFallen(Team.Bravo), Is.False);
            Assert.That(_log.ToString(), Does.Contain("[Guardian] leader team=Alpha player=2"));
            Assert.That(_log.ToString(), Does.Contain("[Guardian] leader team=Bravo player=4"));
        }

        [Test]
        public void TheSameSeedPicksTheSameLeaders()
        {
            var first = BeginWithSeed(7);
            var firstAlpha = first.LeaderOf(Team.Alpha);
            var firstBravo = first.LeaderOf(Team.Bravo);
            first.Stop();

            var second = BeginWithSeed(7);

            Assert.That(firstAlpha, Is.EqualTo(_alphaMember).Or.EqualTo(_alphaLeader));
            Assert.That(firstBravo, Is.EqualTo(_bravoMember).Or.EqualTo(_bravoLeader));
            Assert.That(second.LeaderOf(Team.Alpha), Is.EqualTo(firstAlpha));
            Assert.That(second.LeaderOf(Team.Bravo), Is.EqualTo(firstBravo));
        }

        [Test]
        public void APickOutsideTheTeamIsRefused()
        {
            _host = new MatchHost(_log);
            _rules = new GuardianRules(Settings(), _host, count => count);
            _host.Begin(_rules, Plain(), Settings());
            _rules.Start();
            _host.Join(_alphaMember, Team.Alpha);
            _host.Join(_bravoMember, Team.Bravo);

            var e = Assert.Throws<InvalidOperationException>(() => _host.Steps(1));

            Assert.That(e.Message, Is.EqualTo("pick returned 1 for 1 member(s) of Alpha"));
        }

        [Test]
        public void TheLeadersAreSentAsCarriedItemsAtTheirBodies()
        {
            Begin();
            _host.Place(_alphaLeader, _leaderSpot);

            var items = Items();
            _rules.FillObjectiveItems(items);

            Assert.That(items.Count, Is.EqualTo(2));
            Assert.That(items[0].ItemId, Is.EqualTo(Side.Alpha.Id));
            Assert.That(items[0].Present, Is.EqualTo(ObjectiveItemFields.All));
            Assert.That(items[0].Kind, Is.EqualTo(ObjectiveItemKind.Leader));
            Assert.That(items[0].Team, Is.EqualTo(Team.Alpha));
            Assert.That(items[0].HasOwner, Is.True);
            Assert.That(items[0].OwnerId, Is.EqualTo(_alphaLeader));
            Assert.That(items[0].State, Is.EqualTo(ObjectiveItemState.Carried));
            Assert.That(items[0].HasCarrier, Is.True);
            Assert.That(items[0].CarrierId, Is.EqualTo(_alphaLeader));
            Assert.That(items[0].Position, Is.EqualTo(_leaderSpot));
            Assert.That(items[1].ItemId, Is.EqualTo(Side.Bravo.Id));
            Assert.That(items[1].Team, Is.EqualTo(Team.Bravo));
            Assert.That(items[1].OwnerId, Is.EqualTo(_bravoLeader));
            Assert.That(items[1].Position, Is.EqualTo(new Vector3(20f, 1f, 20f)), "the leader stands on the Bravo spawn point");
        }

        [Test]
        public void AMemberComesBackAfterTheRespawnSecondsWhileTheLeaderStands()
        {
            Begin();

            _host.Kill(_bravoMember, _alphaMember);

            Assert.That(_host.Session.IsAlive(_alphaMember), Is.False);
            Assert.That(_rules.RespawnRemaining(_alphaMember), Is.EqualTo(1f));
            _host.Steps(_lessThanRespawn);
            Assert.That(_host.Session.IsAlive(_alphaMember), Is.False, "the session does not bring anyone back by itself");
            _host.Steps(_moreThanRespawn - _lessThanRespawn);
            Assert.That(_host.Session.IsAlive(_alphaMember), Is.True);
            Assert.That(_rules.RespawnRemaining(_alphaMember), Is.Null);
            Assert.That(_host.Ended, Is.False);
        }

        [Test]
        public void AFallenLeaderDropsTheMarkerWhereTheyFellAndStopsTheTeamFromComingBack()
        {
            Begin();
            _host.Place(_alphaLeader, _leaderSpot);

            _host.Kill(_bravoMember, _alphaLeader);
            _host.Place(_alphaLeader, Vector3.Zero);

            Assert.That(_rules.HasLeaderFallen(Team.Alpha), Is.True);
            Assert.That(_rules.HasLeaderFallen(Team.Bravo), Is.False);
            Assert.That(_rules.RespawnRemaining(_alphaLeader), Is.Null);
            Assert.That(_log.ToString(), Does.Contain("[Guardian] leader-down team=Alpha player=2 reason=killed respawn=off"));
            var items = Items();
            _rules.FillObjectiveItems(items);
            Assert.That(items[0].OwnerId, Is.EqualTo(_alphaLeader));
            Assert.That(items[0].State, Is.EqualTo(ObjectiveItemState.Dropped));
            Assert.That(items[0].HasCarrier, Is.False);
            Assert.That(items[0].CarrierId, Is.Zero);
            Assert.That(items[0].Position, Is.EqualTo(_leaderSpot), "the marker stays where the leader fell");

            _host.Steps(_moreThanRespawn);
            Assert.That(_host.Session.IsAlive(_alphaLeader), Is.False);
            Assert.That(_host.Ended, Is.False, "an Alpha member still stands");

            _host.Kill(_bravoMember, _alphaMember);
            Assert.That(_rules.RespawnRemaining(_alphaMember), Is.Null, "a team without its leader does not come back");
            _host.Steps(1);

            Assert.That(_host.Ended, Is.True);
            Assert.That(_host.Winner, Is.EqualTo(Team.Bravo));
            Assert.That(_rules.DecideWinner(null), Is.EqualTo(Team.Bravo), "the snapshot asks again and gets the same winner");
            Assert.That(_log.ToString(), Does.Contain("[Guardian] eliminated winner=Bravo"));
        }

        [Test]
        public void TheLeaderFallingCancelsTheTeamsWaitingRespawns()
        {
            Begin();

            _host.Kill(_bravoMember, _alphaMember);
            Assert.That(_rules.RespawnRemaining(_alphaMember), Is.EqualTo(1f));
            _host.Kill(_bravoMember, _alphaLeader);

            Assert.That(_rules.RespawnRemaining(_alphaMember), Is.Null);
            _host.Steps(1);
            Assert.That(_host.Ended, Is.True);
            Assert.That(_host.Winner, Is.EqualTo(Team.Bravo));
            Assert.That(_host.Session.IsAlive(_alphaMember), Is.False);
        }

        [Test]
        public void AtTheTimeLimitTheOnlyStandingLeaderWinsOverMoreKills()
        {
            Begin(timeLimitSeconds: 5f);

            _host.Kill(_bravoMember, _alphaMember);
            _host.Steps(_moreThanRespawn);
            _host.Kill(_bravoMember, _alphaMember);
            _host.Kill(_alphaLeader, _bravoLeader);
            Assert.That(_host.Session.GetScore(Team.Bravo), Is.EqualTo(2));
            Assert.That(_host.Session.GetScore(Team.Alpha), Is.EqualTo(1));

            _host.Steps(60 * 6);

            Assert.That(_host.Ended, Is.True);
            Assert.That(_host.Session.Winner, Is.EqualTo(Team.Bravo), "the session alone would pick the team with more kills");
            Assert.That(_host.Winner, Is.EqualTo(Team.Alpha));
            Assert.That(_host.Session.IsAlive(_bravoMember), Is.True, "Bravo was not wiped out");
        }

        [Test]
        public void AtTheTimeLimitWithBothLeadersStandingTheKillsDecide()
        {
            Begin(timeLimitSeconds: 5f);

            _host.Kill(_bravoMember, _alphaMember);
            _host.Steps(60 * 6);

            Assert.That(_host.Ended, Is.True);
            Assert.That(_host.Winner, Is.EqualTo(Team.Bravo));
        }

        [Test]
        public void TheLastMembersFallingTogetherIsADraw()
        {
            Begin();
            _host.Kill(_bravoMember, _alphaLeader);
            _host.Kill(_alphaMember, _bravoLeader);
            _host.Kill(_bravoMember, _alphaMember);
            _host.Kill(_bravoMember, _bravoMember);

            _host.Steps(1);

            Assert.That(_host.Ended, Is.True);
            Assert.That(_host.Winner, Is.Null);
            Assert.That(_log.ToString(), Does.Contain("[Guardian] eliminated winner=draw"));
        }

        [Test]
        public void ALeaderWhoLeavesHasFallen()
        {
            Begin();

            _host.Leave(_bravoLeader);

            Assert.That(_rules.HasLeaderFallen(Team.Bravo), Is.True);
            Assert.That(_log.ToString(), Does.Contain("[Guardian] leader-down team=Bravo player=4 reason=left respawn=off"));
            _host.Steps(1);
            Assert.That(_host.Ended, Is.False, "a Bravo member still stands");

            _host.Kill(_alphaMember, _bravoMember);
            _host.Steps(1);

            Assert.That(_host.Ended, Is.True);
            Assert.That(_host.Winner, Is.EqualTo(Team.Alpha));
        }

        [Test]
        public void ANextMatchPicksTheLeadersAgain()
        {
            Begin();
            _host.Kill(_bravoMember, _alphaLeader);
            _host.Kill(_bravoMember, _alphaMember);
            _host.Steps(1);
            Assert.That(_host.Ended, Is.True);
            Assert.That(_rules.LeaderOf(Team.Alpha), Is.EqualTo(_alphaLeader), "the result keeps the leaders");

            _host.Session.Restart();

            Assert.That(_rules.LeadersChosen, Is.False);
            Assert.That(_rules.LeaderOf(Team.Alpha), Is.Null);
            Assert.That(_rules.HasLeaderFallen(Team.Alpha), Is.False);
            Assert.That(_rules.DecideWinner(Team.Alpha), Is.EqualTo(Team.Alpha));
            Assert.That(_log.ToString(), Does.Contain("[Guardian] restart"));
            _host.Steps(1);
            Assert.That(_rules.LeaderOf(Team.Alpha), Is.EqualTo(_alphaLeader));
            Assert.That(_rules.HasLeaderFallen(Team.Alpha), Is.False);
            Assert.That(_host.Ended, Is.False);
        }

        [Test]
        public void NothingRespawnsAfterTheMatchEnds()
        {
            Begin(timeLimitSeconds: 1f);
            // 時間切れ (1 秒) より後に出直しの時が来るよう、半秒待ってから倒す
            _host.Steps(_lessThanRespawn);
            _host.Kill(_bravoMember, _alphaMember);

            _host.Steps(_moreThanRespawn + 10);

            Assert.That(_host.Ended, Is.True);
            Assert.That(_rules.RespawnRemaining(_alphaMember), Is.Null);
            Assert.That(_host.Session.IsAlive(_alphaMember), Is.False);
        }

        [Test]
        public void TheConstructorRefusesMissingParts()
        {
            Assert.Throws<ArgumentNullException>(() => new GuardianRules(null, new MatchHost(_log)));
            Assert.Throws<ArgumentNullException>(() => new GuardianRules(Settings(), null));
            Assert.Throws<ArgumentNullException>(() => new GuardianRules(Settings(), new MatchHost(_log), (Func<int, int>)null));
        }

        // 人数の最後の人を選ぶ。メンバー 1・3 と リーダー 2・4 を決めておくため
        private static int Last(int count) => count - 1;

        private static SnapshotList<ObjectiveItemSnapshot> Items()
        {
            return new SnapshotList<ObjectiveItemSnapshot>(SnapshotMessage.MaxObjectiveItems, "objective item");
        }

        private static NetMatchSettings Settings(float timeLimitSeconds = 300f, float respawnSeconds = 1f)
        {
            return new NetMatchSettings(timeLimitSeconds: timeLimitSeconds, respawnSeconds: respawnSeconds);
        }

        private static GameRules Plain()
        {
            return GameRules.Default
                .WithActions(ActionRules.Default.WithDowned(Downed.Off))
                .WithSpawnProtectionSeconds(0f)
                .WithIdleKickSeconds(0f)
                .WithRegenPerSecond(0f);
        }

        private void Begin(float timeLimitSeconds = 300f)
        {
            var settings = Settings(timeLimitSeconds);
            _host = new MatchHost(_log);
            _rules = new GuardianRules(settings, _host, Last);
            Join(settings);
        }

        private GuardianRules BeginWithSeed(int seed)
        {
            var settings = Settings();
            _host = new MatchHost(_log);
            _rules = new GuardianRules(settings, _host, seed);
            Join(settings);
            return _rules;
        }

        private void Join(NetMatchSettings settings)
        {
            _host.Begin(_rules, Plain(), settings);
            _rules.Start();
            _host.Join(_alphaMember, Team.Alpha);
            _host.Join(_alphaLeader, Team.Alpha);
            _host.Join(_bravoMember, Team.Bravo);
            _host.Join(_bravoLeader, Team.Bravo);
            _host.Steps(1);
        }

        // 目的だけを確かめるので、MatchRulesModule が目的を回す順 (Session の Tick の後に目的の Tick、
        // 目的が終えたら DecideWinner の勝者で Session を Finish) と出来事の渡し方をここでなぞる (PayloadRulesTests と同じ)
        private sealed class MatchHost : IMatchObjectiveHost
        {
            private const float _tickSeconds = 1f / 60f;

            private GuardianRules _rules;
            private long _tick;

            public MatchHost(TextWriter log)
            {
                Log = log;
                Bodies = new ServerBodies(NetMotorSettings.Default.Stance);
            }

            public TeamDeathmatchSession Session { get; private set; }

            public ServerBodies Bodies { get; }

            public TextWriter Log { get; }

            public bool Ended { get; private set; }

            public Team? Winner { get; private set; }

            public void Begin(GuardianRules rules, GameRules gameRules, NetMatchSettings settings)
            {
                _rules = rules;
                Session = new TeamDeathmatchSession(
                    rules.CreateSessionSpec(gameRules, settings),
                    settings.MaxHealth,
                    TestRules.Points().SpawnPoints,
                    new SpawnSelector(settings.MinEnemySpawnDistance),
                    PositionOf,
                    null,
                    settings.ReconnectGraceSeconds);
                Session.Spawned += (playerId, point) => Bodies.Teleport(playerId, point.Position, 0f, _tick);
                Session.Killed += (killerId, victimId, _) => _rules.PlayerKilled(killerId, victimId);
                Session.Restarted += () =>
                {
                    Ended = false;
                    Winner = null;
                    _rules.MatchRestarted();
                };
                Session.Ended += winner =>
                {
                    Ended = true;
                    Winner = _rules.DecideWinner(winner);
                    _rules.MatchEnded();
                };
            }

            public void Join(int playerId, Team team)
            {
                Bodies.Add(playerId);
                Session.Join(playerId, "device" + playerId, team);
                _rules.PlayerJoined(playerId);
            }

            // MatchRulesModule.HandlePlayerLeft と同じく、目的に知らせてから Session から抜く
            public void Leave(int playerId)
            {
                _rules.PlayerLeft(playerId);
                Session.Leave(playerId);
                Bodies.Remove(playerId);
            }

            public void Place(int playerId, Vector3 position)
            {
                Assert.That(Bodies.TryGet(playerId, out var body), Is.True);
                body.SetPose(position, 0f, PlayerStance.Standing);
            }

            public void Kill(int attackerId, int victimId)
            {
                Assert.That(Session.ApplyDamage(attackerId, victimId, _lethal), Is.EqualTo(DamageOutcome.Killed));
            }

            public void Steps(int count)
            {
                for (var i = 0; i < count; i++)
                {
                    _tick++;
                    Session.Tick(_tickSeconds);
                    if (_rules.Tick(Session.State == GameModeState.Playing, _tickSeconds))
                    {
                        Session.Finish(_rules.DecideWinner(Session.Winner));
                    }
                }
            }

            public void SetMatchScores(int alpha, int bravo)
            {
            }

            public void ClearDowned()
            {
            }

            private Vector3 PositionOf(int playerId)
            {
                return Bodies.TryGet(playerId, out var body) ? body.Position : Vector3.Zero;
            }
        }
    }
}
