using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Shooter.Core;
using Shooter.Protocol;
using Shooter.Server.Eos;
using Shooter.Server.Eos.Tests;

namespace Shooter.Server.Tests
{
    public sealed class ServerStatsModuleTests
    {
        private const float _tickSeconds = 1f / 60f;
        private const string _alice = "0123456789abcdef0123456789abcdef";
        private const string _bob = "fedcba9876543210fedcba9876543210";
        private static readonly DateTime _time = new DateTime(2026, 11, 18, 9, 0, 0, DateTimeKind.Utc);

        private StringWriter _log;
        private RecordingTransport _transport;
        private FakeStatsIngestBackend _backend;
        private ServerMatch _match;
        private MatchRulesModule _rules;
        private ServerStatsModule _module;
        private long _tick;
        private float _now;

        [SetUp]
        public void SetUp()
        {
            _log = new StringWriter();
            _transport = new RecordingTransport();
            _backend = new FakeStatsIngestBackend();
            _tick = 0;
            _now = 0f;
        }

        [TearDown]
        public void TearDown()
        {
            _module?.Dispose();
            _rules?.Stop();
            _transport.Dispose();
            _log.Dispose();
        }

        [Test]
        public void SendsTheDefaultBoardsStatsForEachPlayerAtTheEndOfTheMatch()
        {
            Start(LeaderboardSpec.Default);
            Join(1, _alice);
            Join(2, _bob);
            Assert.That(TeamOf(1), Is.EqualTo(Team.Alpha));

            KillTwice(1, 2);

            Assert.That(_match.State, Is.EqualTo(GameModeState.Ended));
            Assert.That(_log.ToString(), Does.Contain("[Stats] match " + _match.MatchId + " ended; queued stats for 1 player(s)"));
            Assert.That(_backend.Calls, Is.Empty, "the update is sent on the next tick, not inside the rules' end");

            _module.Tick(++_tick, _tickSeconds);

            Assert.That(_backend.Calls, Has.Count.EqualTo(1), "bob has no kills and no win, which add nothing to a sum");
            Assert.That(_backend.Calls[0].UserId, Is.EqualTo(_alice));
            Assert.That(_backend.Calls[0].Stats, Is.EqualTo(new[] { new StatIngest("Kills", 2), new StatIngest("MatchesWon", 1) }));
            _backend.CompleteNext();
            Assert.That(_module.Queue.Sent, Is.EqualTo(1));
        }

        [Test]
        public void SendsEveryConfiguredStatWithItsPortalName()
        {
            var spec = new LeaderboardSpec(new[]
            {
                new LeaderboardDefinition("kills", "leaderboard.kills", PlayerStat.Kills, LeaderboardAggregation.Sum, "TotalKills"),
                new LeaderboardDefinition("deaths", "leaderboard.deaths", PlayerStat.Deaths),
                new LeaderboardDefinition("played", "leaderboard.played", PlayerStat.MatchesPlayed),
                new LeaderboardDefinition("best", "leaderboard.best", PlayerStat.Kills, LeaderboardAggregation.Max, "BestKills"),
            });
            Start(spec);
            Join(1, _alice);
            Join(2, _bob);

            KillTwice(1, 2);
            SendAll();

            Assert.That(_backend.Calls.Select(c => c.UserId), Is.EqualTo(new[] { _alice, _bob }));
            Assert.That(_backend.Calls[0].Stats, Is.EqualTo(new[]
            {
                new StatIngest("TotalKills", 2), new StatIngest("MatchesPlayed", 1), new StatIngest("BestKills", 2),
            }));
            Assert.That(_backend.Calls[1].Stats, Is.EqualTo(new[]
            {
                new StatIngest("Deaths", 2), new StatIngest("MatchesPlayed", 1), new StatIngest("BestKills", 0),
            }), "a max is sent even when it is 0");
        }

        [Test]
        public void SkipsPlayersWithoutAVerifiedEosId()
        {
            Start(LeaderboardSpec.Default);
            Join(1, null);
            Join(2, _bob);

            KillTwice(1, 2);
            SendAll();

            Assert.That(_backend.Calls, Is.Empty);
            Assert.That(_log.ToString(), Does.Contain("queued stats for 0 player(s), skipped 1 without a verified EOS id"));
        }

        [Test]
        public void KeepsTheCountsOfAPlayerWhoRejoins()
        {
            Start(LeaderboardSpec.Default);
            Join(1, _alice);
            Join(2, _bob);
            _rules.ApplyDamage(1, 2, 100);

            _match.Leave(new ConnectionId(1));
            _match.Join(new ConnectionId(3), "device1", productUserId: _alice);
            Assert.That(TeamOf(3), Is.EqualTo(Team.Alpha));
            Advance(1.1f);
            _rules.ApplyDamage(3, 2, 100);
            SendAll();

            Assert.That(_match.State, Is.EqualTo(GameModeState.Ended));
            Assert.That(_backend.Calls, Has.Count.EqualTo(1));
            Assert.That(_backend.Calls[0].Stats, Is.EqualTo(new[] { new StatIngest("Kills", 2), new StatIngest("MatchesWon", 1) }));
        }

        [Test]
        public void APlayerWhoLeftKeepsTheirKillsButNotTheMatch()
        {
            var spec = new LeaderboardSpec(new[]
            {
                new LeaderboardDefinition("kills", "leaderboard.kills", PlayerStat.Kills),
                new LeaderboardDefinition("played", "leaderboard.played", PlayerStat.MatchesPlayed),
            });
            Start(spec, new NetMatchSettings(scoreLimit: 5, respawnSeconds: 1f, timeLimitSeconds: 2f));
            Join(1, _alice);
            Join(2, _bob);
            _rules.ApplyDamage(1, 2, 100);
            _match.Leave(new ConnectionId(1));

            Advance(2.1f);
            SendAll();

            Assert.That(_match.State, Is.EqualTo(GameModeState.Ended));
            Assert.That(_backend.Calls.Select(c => c.UserId), Is.EqualTo(new[] { _alice, _bob }));
            Assert.That(_backend.Calls[0].Stats, Is.EqualTo(new[] { new StatIngest("Kills", 1) }));
            Assert.That(_backend.Calls[1].Stats, Is.EqualTo(new[] { new StatIngest("MatchesPlayed", 1) }));
        }

        [Test]
        public void CountsTeamKillsAndSuicidesOnlyAsDeaths()
        {
            var stats = new ServerMatchStats();
            stats.RecordKill(_alice, _bob, new[] { "carol" }, headshot: true, friendly: false, self: false);
            stats.RecordKill(_alice, _bob, new[] { "carol" }, headshot: false, friendly: true, self: false);
            stats.RecordKill(_alice, _alice, null, headshot: false, friendly: false, self: true);
            stats.RecordKill(null, _bob, null, headshot: false, friendly: false, self: false);

            Assert.That(stats.Get(_alice, PlayerStat.Kills), Is.EqualTo(1));
            Assert.That(stats.Get(_alice, PlayerStat.Headshots), Is.EqualTo(1));
            Assert.That(stats.Get(_alice, PlayerStat.Deaths), Is.EqualTo(1));
            Assert.That(stats.Get(_bob, PlayerStat.Deaths), Is.EqualTo(3));
            Assert.That(stats.Get("carol", PlayerStat.Assists), Is.EqualTo(1));
            Assert.That(stats.Get(_alice.ToUpperInvariant(), PlayerStat.Kills), Is.EqualTo(1), "EOS ids are hexadecimal in either case");
            Assert.That(stats.Users, Is.EqualTo(new[] { _bob, _alice, "carol" }));
        }

        [Test]
        public void SendsAStatNameUsedByTwoBoardsOnce()
        {
            var spec = new LeaderboardSpec(new[]
            {
                new LeaderboardDefinition("kills", "leaderboard.kills", PlayerStat.Kills, LeaderboardAggregation.Sum, "Score"),
                new LeaderboardDefinition("wins", "leaderboard.wins", PlayerStat.MatchesWon, LeaderboardAggregation.Sum, "Score"),
            });

            var ingest = ServerMatchStats.Build(spec, stat => stat == PlayerStat.Kills ? 4 : 1);

            Assert.That(ingest, Is.EqualTo(new[] { new StatIngest("Score", 4) }));
            Assert.That(ServerMatchStats.DuplicateStatNames(spec).Select(b => b.Id), Is.EqualTo(new[] { "wins" }));

            Start(spec);
            Assert.That(_log.ToString(), Does.Contain(
                "[Stats] board wins uses stat Score that an earlier board already sends; it is sent once with the earlier board's value"));
        }

        [Test]
        public void ForgetsTheCountsWhenTheNextMatchStarts()
        {
            Start(LeaderboardSpec.Default, new NetMatchSettings(scoreLimit: 2, respawnSeconds: 1f, restartDelaySeconds: 1f));
            Join(1, _alice);
            Join(2, _bob);
            KillTwice(1, 2);
            SendAll();
            _backend.Calls.Clear();

            Advance(1.1f);
            Assert.That(_match.State, Is.EqualTo(GameModeState.Playing));
            Assert.That(_module.Stats.Get(_alice, PlayerStat.Kills), Is.Zero);
            Assert.That(_module.Stats.Users, Is.EquivalentTo(new[] { _alice, _bob }));

            KillTwice(1, 2);
            SendAll();
            Assert.That(_backend.Calls.Single().Stats, Is.EqualTo(new[] { new StatIngest("Kills", 2), new StatIngest("MatchesWon", 1) }));
        }

        [Test]
        public void FlushesTheQueueWhenTheServerStops()
        {
            Start(LeaderboardSpec.Default);
            Join(1, _alice);
            Join(2, _bob);
            KillTwice(1, 2);
            _backend.SucceedAtOnce();

            _module.Stop();

            Assert.That(_backend.Calls, Has.Count.EqualTo(1));
            Assert.That(_backend.Disposed, Is.True);
            Assert.That(_module.Queue, Is.Null);
        }

        [Test]
        public void ReadsTheLeaderboardTuningNextToTheServer()
        {
            using var root = new TempDirectory();
            Assert.That(ServerStatsModule.TryLoadSpec(root.Path, out var spec, out var error), Is.True);
            Assert.That(spec, Is.SameAs(LeaderboardSpec.Default));
            Assert.That(error, Is.Null);

            var directory = Path.Combine(root.Path, ServerMapData.DataDirectoryName, ServerRulesData.TuningDirectoryName);
            Directory.CreateDirectory(directory);
            var path = Path.Combine(directory, ServerStatsModule.TuningFileName);
            File.WriteAllText(path, "{ \"boards\": [ { \"id\": \"best\", \"stat\": \"Kills\", \"aggregation\": \"Max\", \"statName\": \"BestKills\" } ] }");

            Assert.That(ServerStatsModule.TryLoadSpec(root.Path, out spec, out error), Is.True);
            Assert.That(spec.Boards.Single().StatName, Is.EqualTo("BestKills"));
            Assert.That(spec.Boards.Single().Aggregation, Is.EqualTo(LeaderboardAggregation.Max));

            File.WriteAllText(path, "{ \"boards\": [ { \"id\": \"best\", \"stat\": \"Nope\" } ] }");

            Assert.That(ServerStatsModule.TryLoadSpec(root.Path, out spec, out error), Is.False);
            Assert.That(spec, Is.Null);
            Assert.That(error, Does.StartWith("cannot read leaderboard tuning: "));
        }

        private static GameRules Plain()
        {
            var actions = ActionRules.Default.WithDowned(Downed.Off).WithPing(Ping.Off);
            return GameRules.Default.WithActions(actions).WithSpawnProtectionSeconds(0f).WithIdleKickSeconds(0f).WithRegenPerSecond(0f);
        }

        private void Start(LeaderboardSpec spec, NetMatchSettings settings = null)
        {
            settings ??= new NetMatchSettings(scoreLimit: 2, respawnSeconds: 1f);
            _match = new ServerMatch(settings.RestartDelaySeconds, _log, new ServerEventWriter(false, _log, _log), () => _time);
            var context = new ServerContext(
                new NetLaunchOptions(NetLaunchMode.Server, NetLaunchOptions.AnyAddress, NetLaunchOptions.DefaultPort),
                "1.0.0",
                _log,
                TestMaps.Data("NetTest", TestRules.Points()),
                _match,
                new ServerMessenger(_transport),
                new StubControl(),
                TestRules.Data(Plain(), settings));
            _rules = new MatchRulesModule();
            _rules.Start(context);
            _module = new ServerStatsModule(_rules, _ => _backend, spec, () => _now, milliseconds => _now += milliseconds / 1000f);
            _module.Start(context);
        }

        private void Join(int id, string productUserId)
        {
            _match.Join(new ConnectionId((ulong)id), "device" + id, productUserId: productUserId);
        }

        private Team TeamOf(int id)
        {
            Assert.That(_match.TryGetPlayer(id, out var player), Is.True);
            return player.Team;
        }

        private void KillTwice(int killer, int victim)
        {
            Assert.That(_rules.ApplyDamage(killer, victim, 100), Is.EqualTo(DamageOutcome.Killed));
            Advance(1.1f);
            Assert.That(_rules.ApplyDamage(killer, victim, 100), Is.EqualTo(DamageOutcome.Killed));
        }

        private void Advance(float seconds)
        {
            for (var elapsed = 0f; elapsed < seconds; elapsed += _tickSeconds)
            {
                _match.Tick(_tickSeconds);
                _rules.Tick(++_tick, _tickSeconds);
                _module.Tick(_tick, _tickSeconds);
            }
        }

        // 1 件ずつ送るので、送り終えるまで Tick と完了を繰り返す
        private void SendAll()
        {
            _backend.SucceedAtOnce();
            for (var i = 0; i < 10 && _module.Queue.Pending > 0; i++)
            {
                _module.Tick(++_tick, _tickSeconds);
            }

            Assert.That(_module.Queue.Pending, Is.Zero);
        }
    }
}
