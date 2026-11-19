using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.Server.Tests
{
    public sealed class MatchRulesNextRulesTests
    {
        private const float _tickSeconds = 1f / 60f;
        private static readonly DateTime _time = new DateTime(2026, 11, 19, 9, 0, 0, DateTimeKind.Utc);

        private StringWriter _log;
        private StringWriter _json;
        private RecordingTransport _transport;
        private ServerMatch _match;
        private ServerMovement _movement;
        private MatchRulesModule _rules;
        private ServerHost _host;

        [SetUp]
        public void SetUp()
        {
            _log = new StringWriter();
            _json = new StringWriter();
            _transport = new RecordingTransport();
        }

        [TearDown]
        public void TearDown()
        {
            _host?.Stop();
            _transport.Dispose();
            _log.Dispose();
            _json.Dispose();
        }

        [Test]
        public void DominationRulesAfterATeamDeathmatchStartTheNextMatchAsDomination()
        {
            Start(Plain(), controlPoints: 3);
            Join(1);
            Join(2);
            Assert.That(LatestMatch(1).Objective, Is.EqualTo(MatchObjective.None));
            var teamDeathmatch = _rules.Session;
            var domination = Plain().WithObjective(MatchObjective.Domination);

            Assert.That(_match.RequestNextRules(domination, out var error), Is.True, error);
            Assert.That(error, Is.Null);
            Assert.That(_match.NextRules, Is.SameAs(domination));

            // 試合の途中では替えない
            Step(0.5f);
            Assert.That(_rules.Objective.Objective, Is.EqualTo(MatchObjective.None));
            Assert.That(_rules.Domination, Is.Null);

            Assert.That(_rules.ApplyDamage(1, 2, 100), Is.EqualTo(DamageOutcome.Killed));
            Assert.That(_match.State, Is.EqualTo(GameModeState.Ended));
            Assert.That(_rules.Objective.Objective, Is.EqualTo(MatchObjective.None));
            Step(1.1f);

            Assert.That(_match.State, Is.EqualTo(GameModeState.Playing));
            Assert.That(_match.NextRules, Is.Null);
            Assert.That(_rules.Rules, Is.SameAs(domination));
            Assert.That(_rules.Objective.Objective, Is.EqualTo(MatchObjective.Domination));
            Assert.That(_rules.Domination, Is.Not.Null);
            Assert.That(_rules.Session, Is.Not.SameAs(teamDeathmatch));
            Assert.That(_rules.Session.State, Is.EqualTo(GameModeState.Playing));
            Assert.That(_rules.Session.GetTeam(1), Is.EqualTo(TeamOf(1)));
            Assert.That(_rules.Session.GetTeam(2), Is.EqualTo(TeamOf(2)));
            Assert.That(_movement.Settings.Rules, Is.SameAs(domination));
            Assert.That(_log.ToString(), Does.Contain("[Match] rules changed objective from=None to=Domination"));

            _transport.Sent.Clear();
            Step(0.1f);
            var match = LatestMatch(1);
            Assert.That(match.Objective, Is.EqualTo(MatchObjective.Domination));
            Assert.That(LatestControlPointCount(1), Is.EqualTo(3));

            // 拠点制圧はキルでは終わらない (TDM の scoreLimit 1 を引き継がない)
            Assert.That(_rules.ApplyDamage(1, 2, 100), Is.EqualTo(DamageOutcome.Killed));
            Assert.That(_match.State, Is.EqualTo(GameModeState.Playing));
        }

        [Test]
        public void TeamDeathmatchRulesAfterDominationPutNoObjectiveInTheSnapshot()
        {
            Start(Plain().WithObjective(MatchObjective.Domination), controlPoints: 3, scoreLimit: 1);
            Join(1);
            Join(2);
            Assert.That(LatestMatch(1).Objective, Is.EqualTo(MatchObjective.Domination));
            var teamDeathmatch = Plain();

            Assert.That(_match.RequestNextRules(teamDeathmatch, out var error), Is.True, error);
            _rules.Session.Finish(Team.Alpha);
            Step(0.1f);
            Assert.That(_match.State, Is.EqualTo(GameModeState.Ended));
            Step(1.1f);

            Assert.That(_match.State, Is.EqualTo(GameModeState.Playing));
            Assert.That(_rules.Objective.Objective, Is.EqualTo(MatchObjective.None));
            Assert.That(_rules.Domination, Is.Null);
            _transport.Sent.Clear();
            Step(0.1f);
            Assert.That(LatestMatch(1).Objective, Is.EqualTo(MatchObjective.None));
            Assert.That(LatestControlPointCount(1), Is.Zero);

            // TDM の scoreLimit 1 に戻ったので、1 キルで終わる
            Assert.That(_rules.ApplyDamage(1, 2, 100), Is.EqualTo(DamageOutcome.Killed));
            Assert.That(_match.State, Is.EqualTo(GameModeState.Ended));
        }

        [Test]
        public void TheSameRulesRestartTheSameSessionAsBefore()
        {
            var rules = Plain();
            Start(rules, controlPoints: 3);
            Join(1);
            Join(2);
            var session = _rules.Session;
            var objective = _rules.Objective;

            Assert.That(_match.RequestNextRules(rules, out var error), Is.True, error);
            Assert.That(_rules.ApplyDamage(1, 2, 100), Is.EqualTo(DamageOutcome.Killed));
            Step(1.1f);

            Assert.That(_match.State, Is.EqualTo(GameModeState.Playing));
            Assert.That(_rules.Session, Is.SameAs(session));
            Assert.That(_rules.Objective, Is.SameAs(objective));
            Assert.That(_rules.Session.GetKills(1), Is.Zero);
            Assert.That(_log.ToString(), Does.Not.Contain("rules changed"));
        }

        [Test]
        public void RulesTheMapCannotPlayAreRefusedAndTheMatchStaysTheSame()
        {
            Start(Plain(), controlPoints: 0);
            Join(1);
            Join(2);
            var session = _rules.Session;

            Assert.That(_match.RequestNextRules(Plain().WithObjective(MatchObjective.Domination), out var error), Is.False);
            Assert.That(error, Is.EqualTo(
                "cannot use the rules with objective Domination for the next match: cannot change to map NetTest for the Domination match: it has no control points"));
            Assert.That(_match.NextRules, Is.Null);

            Assert.That(_rules.ApplyDamage(1, 2, 100), Is.EqualTo(DamageOutcome.Killed));
            Step(1.1f);

            Assert.That(_match.State, Is.EqualTo(GameModeState.Playing));
            Assert.That(_rules.Session, Is.SameAs(session));
            Assert.That(_rules.Objective.Objective, Is.EqualTo(MatchObjective.None));
        }

        [Test]
        public void ARefusedRequestKeepsTheRulesAcceptedBefore()
        {
            Start(Plain(), controlPoints: 3);
            var domination = Plain().WithObjective(MatchObjective.Domination);
            Assert.That(_match.RequestNextRules(domination, out _), Is.True);

            _match.CheckNextRules = _ => "refused";
            Assert.That(_match.RequestNextRules(Plain(), out var error), Is.False);

            Assert.That(error, Is.EqualTo("refused"));
            Assert.That(_match.NextRules, Is.SameAs(domination));
        }

        [Test]
        public void TheMatchOnlyAnnouncesNewRulesWhenTheNextMatchStarts()
        {
            var match = new ServerMatch(1f, _log, new ServerEventWriter(false, _json, _log), () => _time);
            var announced = new List<GameRules>();
            match.RulesChanged += announced.Add;
            var rules = Plain().WithObjective(MatchObjective.Domination);

            Assert.Throws<ArgumentNullException>(() => match.RequestNextRules(null, out _));
            Assert.That(match.RequestNextRules(rules, out var error), Is.True);
            Assert.That(error, Is.Null);
            match.Join(new ConnectionId(1), "device1");
            match.Join(new ConnectionId(2), "device2");
            Assert.That(match.State, Is.EqualTo(GameModeState.Playing));
            Assert.That(announced, Is.Empty);

            Assert.That(match.End(Team.Alpha), Is.True);
            match.Tick(0.5f);
            Assert.That(announced, Is.Empty);
            match.Tick(0.6f);

            Assert.That(announced, Is.EqualTo(new[] { rules }));
            Assert.That(match.NextRules, Is.Null);

            // 渡されていない試合では知らせない
            Assert.That(match.End(Team.Bravo), Is.True);
            match.Tick(1.1f);
            Assert.That(announced, Has.Count.EqualTo(1));
        }

        private static GameRules Plain()
        {
            var actions = ActionRules.Default.WithDowned(Downed.Off).WithPing(Ping.Off);
            return GameRules.Default.WithActions(actions).WithSpawnProtectionSeconds(0f).WithIdleKickSeconds(0f).WithRegenPerSecond(0f);
        }

        private void Start(GameRules rules, int controlPoints, int scoreLimit = 1)
        {
            var settings = new NetMatchSettings(scoreLimit: scoreLimit, respawnSeconds: 0f, restartDelaySeconds: 1f);
            var data = TestRules.Data(rules, settings);
            _match = new ServerMatch(settings.RestartDelaySeconds, _log, new ServerEventWriter(true, _json, _log), () => _time);
            var context = new ServerContext(
                new NetLaunchOptions(NetLaunchMode.Server, NetLaunchOptions.AnyAddress, NetLaunchOptions.DefaultPort),
                "1.0.0",
                _log,
                TestMaps.Data("NetTest", TestRules.Points(controlPoints)),
                _match,
                new ServerMessenger(_transport),
                new StubControl(),
                data);
            _movement = new ServerMovement(ServerModules.MotorSettingsFrom(data));
            _rules = new MatchRulesModule();
            _host = new ServerHost();
            _host.Register(_movement);
            _host.Register(_rules);
            _host.Register(new ServerSnapshots(rules, new ISnapshotContributor[] { _movement, _rules }));
            _host.Start(context);
        }

        // MatchServer と同じく、試合に加えてからモジュールに接続を知らせる
        private void Join(int id)
        {
            var connection = new ConnectionId((ulong)id);
            _match.Join(connection, "device" + id);
            _host.OnConnected(connection);
        }

        private Team TeamOf(int id)
        {
            Assert.That(_match.TryGetPlayer(id, out var player), Is.True);
            return player.Team;
        }

        private void Step(float seconds)
        {
            for (var elapsed = 0f; elapsed < seconds; elapsed += _tickSeconds)
            {
                _match.Tick(_tickSeconds);
                _host.Tick(_tickSeconds);
            }
        }

        private MatchSnapshot LatestMatch(int id)
        {
            Step(_tickSeconds);
            var latest = default(MatchSnapshot);
            var snapshots = _transport.Read<SnapshotMessage>(id, snapshot =>
            {
                Assert.That(snapshot.IsComplete, Is.True);
                latest = snapshot.Match;
                return snapshot;
            });
            Assert.That(snapshots, Is.Not.Empty);
            return latest;
        }

        private int LatestControlPointCount(int id)
        {
            var count = -1;
            var snapshots = _transport.Read<SnapshotMessage>(id, snapshot =>
            {
                count = snapshot.ControlPoints.Count;
                return snapshot;
            });
            Assert.That(snapshots, Is.Not.Empty);
            return count;
        }
    }
}
