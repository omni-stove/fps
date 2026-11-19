using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using NUnit.Framework;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.Server.Tests
{
    // ルール・移動・スナップショットを同じホストで動かし、倒れた人が映す味方とそのまわりを受け取れることを確かめる
    public sealed class MatchRulesSpectateTests
    {
        private const float _tickSeconds = 1f / TickClock.DefaultTickRate;
        private static readonly int[] _allPlayerIds = { 1, 2, 3 };
        private static readonly DateTime _time = new DateTime(2026, 11, 17, 9, 0, 0, DateTimeKind.Utc);

        // 床 (TestMaps の箱) の上面は y=0.5、体の原点はカプセルの中心 (高さ 2) なので、足を床に付けるには y=1.5 より少し上に置く
        private static readonly Vector3 _alphaSpawn = new Vector3(-5f, 1.6f, 0f);
        private static readonly Vector3 _bravoSpawn = new Vector3(5f, 1.6f, 0f);

        private StringWriter _log;
        private StringWriter _json;
        private RecordingTransport _transport;
        private ServerMatch _match;
        private ServerHost _host;
        private ServerMovement _movement;
        private MatchRulesModule _rules;
        private int _inputTick;

        [SetUp]
        public void SetUp()
        {
            _log = new StringWriter();
            _json = new StringWriter();
            _transport = new RecordingTransport();
            _inputTick = 0;
        }

        [TearDown]
        public void TearDown()
        {
            if (_host != null && _host.State == ServerHostState.Running)
            {
                _host.Stop();
            }

            _transport.Dispose();
            _log.Dispose();
            _json.Dispose();
        }

        [Test]
        public void ADeadPlayersSnapshotCarriesTheWatchedAllysRow()
        {
            Start(Rules());
            Join(1);
            Join(2);
            Join(3);
            Assert.That(TeamOf(1), Is.EqualTo(Team.Alpha));
            Assert.That(TeamOf(2), Is.EqualTo(Team.Bravo));
            Assert.That(TeamOf(3), Is.EqualTo(Team.Alpha));
            Walk(3, ticks: 30);
            Assert.That(_movement.TryGetState(3, out var watched), Is.True);
            Assert.That(watched.Position.Z, Is.GreaterThan(1f));

            Assert.That(_rules.ApplyDamage(2, 1, 1000), Is.EqualTo(DamageOutcome.Killed));
            Spectate(1, SpectateStep.Next);
            Assert.That(_rules.TryGetSpectateTarget(1, out var target), Is.True);
            Assert.That(target, Is.EqualTo(3));

            _transport.Sent.Clear();
            Tick();
            var dead = LatestRows(1);
            var ally = LatestRows(3);

            Assert.That(dead.Keys, Is.EquivalentTo(_allPlayerIds));
            Assert.That(dead[1].Health, Is.Zero);
            Assert.That(dead[3].Team, Is.EqualTo(Team.Alpha));
            Assert.That(dead[3].Health, Is.EqualTo(NetMatchSettings.DefaultMaxHealth));
            Assert.That(dead[3].Position, Is.EqualTo(watched.Position));
            Assert.That(dead[2].Position, Is.EqualTo(ally[2].Position));
            Assert.That(dead.Keys, Is.EquivalentTo(ally.Keys), "a spectator gets no rows the watched ally does not get");
        }

        [Test]
        public void NextAndPreviousCycleThroughLivingAlliesOnly()
        {
            Start(Rules());
            for (var id = 1; id <= 5; id++)
            {
                Join(id);
            }

            Assert.That(new[] { TeamOf(1), TeamOf(3), TeamOf(5) }, Is.All.EqualTo(Team.Alpha));
            _rules.ApplyDamage(2, 1, 1000);

            Assert.That(_rules.ServerSpectate(1, SpectateStep.Next), Is.EqualTo(3));
            Assert.That(_rules.ServerSpectate(1, SpectateStep.Next), Is.EqualTo(5));
            Assert.That(_rules.ServerSpectate(1, SpectateStep.Next), Is.EqualTo(3));
            Assert.That(_rules.ServerSpectate(1, SpectateStep.Previous), Is.EqualTo(5));
        }

        [Test]
        public void TheRecordIsDroppedWhenTheWatchedAllyDies()
        {
            Start(Rules());
            for (var id = 1; id <= 5; id++)
            {
                Join(id);
            }

            _rules.ApplyDamage(2, 1, 1000);
            Spectate(1, SpectateStep.Next);
            Assert.That(_rules.TryGetSpectateTarget(1, out _), Is.True);

            _rules.ApplyDamage(2, 3, 1000);

            Assert.That(_rules.TryGetSpectateTarget(1, out _), Is.False);
            Assert.That(_rules.ServerSpectate(1, SpectateStep.Next), Is.EqualTo(5));
        }

        [Test]
        public void TheRecordIsDroppedWhenTheWatchedAllyLeaves()
        {
            Start(Rules());
            Join(1);
            Join(2);
            Join(3);
            _rules.ApplyDamage(2, 1, 1000);
            Spectate(1, SpectateStep.Next);

            Leave(3);

            Assert.That(_rules.TryGetSpectateTarget(1, out _), Is.False);
        }

        [Test]
        public void RespawningEndsSpectating()
        {
            Start(Rules());
            Join(1);
            Join(2);
            Join(3);
            _rules.ApplyDamage(2, 1, 1000);
            Spectate(1, SpectateStep.Next);
            Assert.That(_rules.TryGetSpectateTarget(1, out _), Is.True);

            Step(NetMatchSettings.DefaultRespawnSeconds + 0.2f);

            Assert.That(_rules.TryGetSpectateTarget(1, out _), Is.False);
        }

        [Test]
        public void LivingPlayersAndPlayersWithoutLivingAlliesCannotSpectate()
        {
            Start(Rules());
            Join(1);
            Join(2);
            Join(3);

            Assert.That(_rules.ServerSpectate(1, SpectateStep.Next), Is.Null, "alive");

            _rules.ApplyDamage(2, 1, 1000);
            _rules.ApplyDamage(2, 3, 1000);

            Assert.That(_rules.ServerSpectate(1, SpectateStep.Next), Is.Null, "only an enemy is alive");
            Assert.That(_rules.TryGetSpectateTarget(1, out _), Is.False);
        }

        [Test]
        public void NoOneSpectatesUnlessTheRuleWatchesAllies()
        {
            Start(Rules(DeathCamera.Free));
            Join(1);
            Join(2);
            Join(3);
            _rules.ApplyDamage(2, 1, 1000);
            var sent = _transport.Sent.Count;

            Spectate(1, SpectateStep.Next);

            Assert.That(_rules.TryGetSpectateTarget(1, out _), Is.False);
            Assert.That(_transport.Sent, Has.Count.EqualTo(sent));
        }

        [Test]
        public void BetweenRoundsALivingPlayerCanSpectateEvenWhenTheRuleDoesNotWatchAllies()
        {
            Start(Rules(DeathCamera.Free));
            Join(1);
            Join(2);
            Join(3);
            var phase = (RoundPhase?)RoundPhase.Preparing;
            _rules.RoundPhaseSource = () => phase;

            Assert.That(_rules.ServerSpectate(1, SpectateStep.Next), Is.EqualTo(3));

            phase = RoundPhase.Live;
            Assert.That(_rules.ServerSpectate(1, SpectateStep.Next), Is.Null);
            Assert.That(_rules.TryGetSpectateTarget(1, out _), Is.False);
        }

        [Test]
        public void NoOneSpectatesWhenSpectatingIsOff()
        {
            Start(Rules(spectating: Spectating.Off));
            Join(1);
            Join(2);
            Join(3);
            _rules.ApplyDamage(2, 1, 1000);
            var sent = _transport.Sent.Count;

            Spectate(1, SpectateStep.Next);
            Spectate(1, SpectateStep.Previous);

            Assert.That(_rules.TryGetSpectateTarget(1, out _), Is.False);
            Assert.That(_transport.Sent, Has.Count.EqualTo(sent));
            Assert.That(_rules.ServerSpectate(1, SpectateStep.Next), Is.Null);
        }

        [Test]
        public void BetweenRoundsNoOneSpectatesWhenSpectatingIsOff()
        {
            Start(Rules(spectating: Spectating.Off));
            for (var id = 1; id <= 5; id++)
            {
                Join(id);
            }

            var phase = (RoundPhase?)RoundPhase.Preparing;
            _rules.RoundPhaseSource = () => phase;
            Tick();
            _rules.ApplyDamage(2, 5, 1000);

            Spectate(1, SpectateStep.Next);
            Spectate(5, SpectateStep.Next);

            Assert.That(_rules.TryGetSpectateTarget(1, out _), Is.False, "alive");
            Assert.That(_rules.TryGetSpectateTarget(5, out _), Is.False, "dead");
            Assert.That(_rules.ServerSpectate(5, SpectateStep.Next), Is.Null);

            phase = RoundPhase.Live;
            Tick();

            Assert.That(_rules.TryGetSpectateTarget(1, out _), Is.False, "alive");
            Assert.That(_rules.TryGetSpectateTarget(5, out _), Is.False, "dead");
        }

        [Test]
        public void TheDefaultRulesKeepSpectatingOnBetweenRoundsAndAfterDeath()
        {
            Assert.That(GameRules.Default.Spectating, Is.EqualTo(Spectating.On));
            Start(Rules(spectating: GameRules.Default.Spectating));
            for (var id = 1; id <= 5; id++)
            {
                Join(id);
            }

            var phase = (RoundPhase?)RoundPhase.Preparing;
            _rules.RoundPhaseSource = () => phase;
            Tick();
            _rules.ApplyDamage(2, 5, 1000);

            Spectate(1, SpectateStep.Next);
            Spectate(5, SpectateStep.Next);

            Assert.That(_rules.TryGetSpectateTarget(1, out var living), Is.True);
            Assert.That(living, Is.EqualTo(3));
            Assert.That(_rules.TryGetSpectateTarget(5, out var dead), Is.True);
            Assert.That(dead, Is.EqualTo(1));

            phase = RoundPhase.Live;
            Tick();

            Assert.That(_rules.TryGetSpectateTarget(1, out _), Is.False, "alive");
            Assert.That(_rules.TryGetSpectateTarget(5, out _), Is.True, "dead under the Ally rule");
        }

        [Test]
        public void WhenThePreparationEndsLivingSpectatorsStopButTheDeadKeepWatching()
        {
            Start(Rules());
            for (var id = 1; id <= 5; id++)
            {
                Join(id);
            }

            var phase = (RoundPhase?)RoundPhase.Preparing;
            _rules.RoundPhaseSource = () => phase;
            Tick();
            _rules.ApplyDamage(2, 5, 1000);
            Spectate(1, SpectateStep.Next);
            Spectate(5, SpectateStep.Next);
            Assert.That(_rules.TryGetSpectateTarget(1, out _), Is.True);
            Assert.That(_rules.TryGetSpectateTarget(5, out _), Is.True);

            phase = RoundPhase.Live;
            Tick();

            Assert.That(_rules.TryGetSpectateTarget(1, out _), Is.False, "alive");
            Assert.That(_rules.TryGetSpectateTarget(5, out _), Is.True, "dead under the Ally rule");
            Assert.That(_log.ToString(), Does.Contain("[Match] round phase=Preparing"));
            Assert.That(_log.ToString(), Does.Contain("[Match] round phase=Live"));
        }

        [Test]
        public void TheSnapshotCarriesTheRoundPhaseOnlyWhenARoundModeIsAttached()
        {
            Start(Rules());
            Join(1);
            Join(2);

            _transport.Sent.Clear();
            Tick();
            var withoutRounds = LatestMatch(1);
            Assert.That(withoutRounds.HasRound, Is.False);

            _rules.RoundPhaseSource = () => RoundPhase.Preparing;
            _transport.Sent.Clear();
            Tick();
            var withRounds = LatestMatch(1);

            Assert.That(withRounds.HasRound, Is.True);
            Assert.That(withRounds.Round, Is.EqualTo(RoundPhase.Preparing));
        }

        [Test]
        public void AnUndefinedRoundPhaseFromTheSourceIsRejected()
        {
            Start(Rules());
            _rules.RoundPhaseSource = () => (RoundPhase)9;

            Assert.Throws<InvalidOperationException>(() => _rules.CurrentRoundPhase());
        }

        private static GameRules Rules(DeathCamera deathCamera = DeathCamera.Ally, Spectating spectating = Spectating.On)
        {
            var actions = ActionRules.Default.WithDowned(Downed.Off).WithPing(Ping.Off);
            return GameRules.Default
                .WithActions(actions)
                .WithDeathCamera(deathCamera)
                .WithSpectating(spectating)
                .WithSpawnProtectionSeconds(0f)
                .WithIdleKickSeconds(0f)
                .WithRegenPerSecond(0f);
        }

        private void Start(GameRules rules)
        {
            var points = new NetMapPoints(
                new[] { new SpawnPoint(_alphaSpawn, Team.Alpha), new SpawnPoint(_bravoSpawn, Team.Bravo) },
                Array.Empty<ControlPointArea>());
            var data = new ServerRulesData("TeamDeathmatch", TestRules.File(rules), PingSpec.Default, "memory", null);
            var settings = ServerModules.MotorSettingsFrom(data);
            _match = new ServerMatch(NetMatchSettings.DefaultRestartDelaySeconds, _log, new ServerEventWriter(true, _json, _log), () => _time);
            var context = new ServerContext(
                new NetLaunchOptions(NetLaunchMode.Server, NetLaunchOptions.AnyAddress, NetLaunchOptions.DefaultPort),
                "1.0.0",
                _log,
                TestMaps.Data("NetTest", points),
                _match,
                new ServerMessenger(_transport),
                new StubControl(),
                data);
            _movement = new ServerMovement(settings);
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

        private void Leave(int id)
        {
            var connection = new ConnectionId((ulong)id);
            _match.Leave(connection);
            _host.OnDisconnected(connection);
        }

        private Team TeamOf(int id)
        {
            Assert.That(_match.TryGetPlayer(id, out var player), Is.True);
            return player.Team;
        }

        private void Spectate(int id, SpectateStep step)
        {
            _host.OnMessage(new ConnectionId((ulong)id), new SpectateRequestMessage { Step = step });
        }

        private void Walk(int id, int ticks)
        {
            for (var i = 0; i < ticks; i++)
            {
                var input = new InputMessage();
                input.Add(new InputFrame(++_inputTick, new Vector2(0f, 1f), 0f, InputButtons.None));
                _host.OnMessage(new ConnectionId((ulong)id), input);
                Tick();
            }
        }

        private void Step(float seconds)
        {
            for (var elapsed = 0f; elapsed < seconds; elapsed += _tickSeconds)
            {
                Tick();
            }
        }

        private void Tick()
        {
            _match.Tick(_tickSeconds);
            _host.Tick(_tickSeconds);
        }

        private MatchSnapshot LatestMatch(int id)
        {
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

        // 表は読むたびに同じスナップショットを上書きするので、読んだその場で行を写す
        private Dictionary<int, PlayerSnapshot> LatestRows(int id)
        {
            var rows = new Dictionary<int, PlayerSnapshot>();
            var snapshots = _transport.Read<SnapshotMessage>(id, snapshot =>
            {
                Assert.That(snapshot.IsComplete, Is.True);
                rows.Clear();
                for (var i = 0; i < snapshot.Players.Count; i++)
                {
                    rows[snapshot.Players[i].PlayerId] = snapshot.Players[i];
                }

                return snapshot;
            });
            Assert.That(snapshots, Is.Not.Empty);
            return rows;
        }
    }
}
