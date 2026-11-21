using System;
using System.IO;
using System.Linq;
using System.Numerics;
using NUnit.Framework;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.Server.Tests
{
    // ServerObjectiveFactory への登録は結線の issue で行うので、MatchRulesModule の選び方を差し替えて HordeRules を載せ、
    // 移動・ルール・戦闘・Bot を MatchServer と同じ順で回す。敵は ServerBots の Bot として波ごとに入る
    public sealed class HordeEndToEndTests
    {
        private const float _tickSeconds = 1f / TickClock.DefaultTickRate;
        private static readonly DateTime _time = new DateTime(2026, 11, 21, 9, 0, 0, DateTimeKind.Utc);

        // 1 波目は 2 体、2 波目は 3 体。休憩 1 秒、倒された Bot は 0.5 秒で抜く
        private static readonly HordeSettings _settings = new HordeSettings(
            waveCount: 2, restSeconds: 1f, firstEnemyCount: 2, enemiesPerWave: 1f, maxAlive: 4, corpseSeconds: 0.5f);

        private StringWriter _log;
        private StringWriter _json;
        private RecordingTransport _transport;
        private StubControl _control;
        private ServerMatch _match;
        private ServerContext _context;
        private ServerHost _host;
        private MatchRulesModule _rules;
        private ServerBots _bots;
        private HordeRules _horde;

        [SetUp]
        public void SetUp()
        {
            _log = new StringWriter();
            _json = new StringWriter();
            _transport = new RecordingTransport();
            _control = new StubControl();
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
        public void TwoPlayersDefendTheWavesOfBotsUntilTheLastWaveIsWipedOut()
        {
            Start();
            Join(1);
            Join(2);
            Tick();

            Assert.That(TeamOf(1), Is.EqualTo(Team.Alpha));
            Assert.That(TeamOf(2), Is.EqualTo(Team.Alpha), "every player defends");
            Assert.That(_horde.Schedule.WaveNumber, Is.EqualTo(1));
            Assert.That(_bots.Count, Is.EqualTo(2), _log.ToString());
            Assert.That(_bots.BotIds.All(id => TeamOf(id) == Team.Bravo), Is.True, "every bot is an enemy");
            Assert.That(_horde.Schedule.Alive, Is.EqualTo(2));
            Assert.That(_match.State, Is.EqualTo(GameModeState.Playing), "the first enemies start the match");
            Assert.That(_log.ToString(), Does.Contain("[Mode] wave start number=1/2 enemies=2"));

            KillAllBots();

            Assert.That(_horde.Schedule.Phase, Is.EqualTo(WavePhase.Resting));
            Assert.That(_horde.Economy.Currency, Is.EqualTo(HordeSpec.DefaultStartingCurrency + (2 * HordeSpec.DefaultKillReward)));
            Assert.That(_log.ToString(), Does.Contain("[Mode] wave cleared number=1"));

            Step(0.7f);

            Assert.That(_bots.Count, Is.Zero, "the bodies are taken away after the corpse seconds");

            Step(0.5f);

            Assert.That(_horde.Schedule.WaveNumber, Is.EqualTo(2));
            Assert.That(_bots.Count, Is.EqualTo(3), "the second wave adds one enemy");
            Assert.That(_bots.BotIds.All(id => TeamOf(id) == Team.Bravo), Is.True);

            KillAllBots();
            Tick();

            Assert.That(_horde.Schedule.Phase, Is.EqualTo(WavePhase.Completed));
            Assert.That(_rules.Session.State, Is.EqualTo(GameModeState.Ended));
            Assert.That(_rules.Session.Winner, Is.EqualTo((Team?)Team.Alpha));
            Assert.That(_match.State, Is.EqualTo(GameModeState.Ended));
            Assert.That(_log.ToString(), Does.Contain("[Mode] horde victory waves=2/2"));
        }

        [Test]
        public void TheBotsWinWhenEveryDefenderFalls()
        {
            Start();
            Join(1);
            Join(2);
            Tick();
            var bot = _bots.BotIds.First();

            _rules.ApplyDamage(bot, 1, 1000);
            _rules.ApplyDamage(bot, 2, 1000);
            Tick();

            Assert.That(_rules.Session.State, Is.EqualTo(GameModeState.Ended));
            Assert.That(_rules.Session.Winner, Is.EqualTo((Team?)Team.Bravo));
            Assert.That(_log.ToString(), Does.Contain("[Mode] horde defeat wave=1/2"));
        }

        [Test]
        public void AFallenDefenderReturnsBetweenWaves()
        {
            Start();
            Join(1);
            Join(2);
            Tick();
            var bot = _bots.BotIds.First();
            _rules.ApplyDamage(bot, 1, 1000);
            Tick();

            Assert.That(_rules.Session.IsAlive(1), Is.False);
            Assert.That(_rules.Session.State, Is.EqualTo(GameModeState.Playing), "player 2 still stands");

            KillAllBots();
            Tick();

            Assert.That(_rules.Session.IsAlive(1), Is.True);
            Assert.That(_log.ToString(), Does.Contain("[Horde] returned player=1 after wave=1"));
        }

        [Test]
        public void NoBotsAreAddedWithoutADefender()
        {
            Start();
            Step(0.5f);

            Assert.That(_bots.Count, Is.Zero);
            Assert.That(_horde.Schedule.Phase, Is.EqualTo(WavePhase.Waiting));
        }

        // 人を全員守る側に入れるので、人数をそろえる (maxTeamSizeDifference) よりも目的のチームを先にする
        [Test]
        public void APartyIsNotSplitIntoTheEnemies()
        {
            Start();
            _match.Join(new ConnectionId(1), "device1", partyId: "party-a");
            _host.OnConnected(new ConnectionId(1));
            _match.Join(new ConnectionId(2), "device2", partyId: "party-b");
            _host.OnConnected(new ConnectionId(2));
            _match.Join(new ConnectionId(3), "device3");
            _host.OnConnected(new ConnectionId(3));

            Assert.That(TeamOf(1), Is.EqualTo(Team.Alpha));
            Assert.That(TeamOf(2), Is.EqualTo(Team.Alpha));
            Assert.That(TeamOf(3), Is.EqualTo(Team.Alpha));
        }

        // 撃つのは player 2。どのテストでも最後まで立っている
        private void KillAllBots()
        {
            foreach (var botId in _bots.BotIds.ToArray())
            {
                if (_rules.Session.IsAlive(botId))
                {
                    _rules.ApplyDamage(2, botId, 1000);
                }
            }
        }

        private void Start()
        {
            var actions = ActionRules.Default.WithDowned(Downed.Off).WithLadder(Ladder.Off);
            var rules = GameRules.Default.WithActions(actions).WithSpawnProtectionSeconds(0f).WithIdleKickSeconds(0f).WithRegenPerSecond(0f);
            var settings = new NetMatchSettings(horde: _settings);
            var data = new ServerRulesData("Horde", TestRules.File(rules, settings, "Horde"), PingSpec.Default, "memory", null);
            var motor = ServerModules.MotorSettingsFrom(data);
            _match = new ServerMatch(NetMatchSettings.DefaultRestartDelaySeconds, _log, new ServerEventWriter(true, _json, _log), () => _time);
            var options = new NetLaunchOptions(
                NetLaunchMode.Server, NetLaunchOptions.AnyAddress, NetLaunchOptions.DefaultPort, NetTransportKind.Udp, null, null, null, 0, 0, 0, 0, 0);
            var map = BotTestMaps.Flat(BotTestMaps.Points(new Vector3(-10f, 1.1f, -10f), new Vector3(10f, 1.1f, 10f)));
            _context = new ServerContext(options, "1.0.0", _log, map, _match, new ServerMessenger(_transport), _control, data);
            var movement = new ServerMovement(motor);
            _rules = new MatchRulesModule((_, matchSettings, host) => _horde = new HordeRules(matchSettings, host));
            var combat = new ServerCombat(_rules, movement);
            _bots = new ServerBots(_rules, movement, combat);
            _host = new ServerHost();
            _host.Register(movement);
            _host.Register(_rules);
            _host.Register(combat);
            _host.Register(_bots);
            _host.Register(new ServerSnapshots(rules, new ISnapshotContributor[] { movement, _rules, combat }, TestAntiCheat.Off));
            _host.Start(_context);
        }

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
                Tick();
            }
        }

        private void Tick()
        {
            _match.Tick(_tickSeconds);
            _host.Tick(_tickSeconds);
        }
    }
}
