using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using NUnit.Framework;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.Server.Tests
{
    // 作った HordeRules を手元に持ち、ServerObjectiveParts を組まずに門の防衛物だけを渡すため、MatchRulesModule の選び方を差し替えて HordeRules を載せ、
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

        [Test]
        public void ADefenderBuildsTheGateBetweenWavesAndEveryoneIsSentIt()
        {
            Start(withGate: true);
            Join(1);
            Join(2);
            Tick();

            Assert.That(_combat.Structures.DestroyedCount(0), Is.EqualTo(1), "the gate starts unbuilt");
            Assert.That(GateStates(1), Is.EqualTo(_gateDown), "everyone is sent the gate taken out");
            Assert.That(GateStates(2), Is.EqualTo(_gateDown));
            Assert.That(_horde.Offer(1).Value.Verdict, Is.EqualTo(HordePurchaseVerdict.WaveInProgress));

            _host.OnMessage(new ConnectionId(1), new HordeFortifyRequestMessage());
            Assert.That(_combat.Structures.DestroyedCount(0), Is.EqualTo(1), "not while the wave fights");

            KillAllBots();
            var currency = _horde.Economy.Currency;
            _host.OnMessage(new ConnectionId(1), new HordeFortifyRequestMessage());

            Assert.That(_combat.Structures.DestroyedCount(0), Is.Zero);
            Assert.That(_horde.Economy.Currency, Is.EqualTo(currency - HordeSpec.DefaultBuildCost));
            Assert.That(GateStates(1).Last(), Is.EqualTo(0UL), "player 1 sees the gate standing");
            Assert.That(GateStates(2).Last(), Is.EqualTo(0UL), "so does player 2");
            Assert.That(_horde.Offer(1), Is.Null, "nothing is left to do at the gate");
            Assert.That(_log.ToString(), Does.Contain("[Mode] build structure=Gate pieces=1 cost=" + HordeSpec.DefaultBuildCost));

            _host.OnMessage(new ConnectionId(2), new HordeFortifyRequestMessage());
            Assert.That(_horde.Economy.Currency, Is.EqualTo(currency - HordeSpec.DefaultBuildCost), "a built gate is not paid for twice");
            Assert.That(GateStates(2), Has.Length.EqualTo(2));
        }

        [Test]
        public void AJoinerIsSentTheGateThatIsNotBuiltYet()
        {
            Start(withGate: true);
            Join(1);
            Tick();

            Join(3);

            Assert.That(GateStates(3), Is.EqualTo(_gateDown));
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

        // 門の部品の当たり判定の箱の中心。守る側の出現地点 (-10, -10) から 3 m で、手の届く距離 (3.5 m) の内
        private static readonly Vector3 _gateCenter = new Vector3(-7f, 0.5f, -10f);
        private static readonly int[] _gateHealth = { 100 };
        private static readonly bool[] _gateGrounded = { true };

        // 門の 1 枚だけの部品が外れている
        private static readonly ulong[] _gateDown = { 1UL };

        private ServerCombat _combat;

        private void Start(bool withGate = false)
        {
            var actions = ActionRules.Default.WithDowned(Downed.Off).WithLadder(Ladder.Off);
            var rules = GameRules.Default.WithActions(actions).WithObjective(MatchObjective.Horde)
                .WithSpawnProtectionSeconds(0f).WithIdleKickSeconds(0f).WithRegenPerSecond(0f);
            var settings = new NetMatchSettings(horde: _settings);
            var data = new ServerRulesData("Horde", TestRules.File(rules, settings, "Horde"), PingSpec.Default, "memory", null);
            var motor = ServerModules.MotorSettingsFrom(data);
            _match = new ServerMatch(NetMatchSettings.DefaultRestartDelaySeconds, _log, new ServerEventWriter(true, _json, _log), () => _time);
            var options = new NetLaunchOptions(
                NetLaunchMode.Server, NetLaunchOptions.AnyAddress, NetLaunchOptions.DefaultPort, NetTransportKind.Udp, null, null, null, 0, 0, 0, 0, 0);
            var points = BotTestMaps.Points(new Vector3(-10f, 1.1f, -10f), new Vector3(10f, 1.1f, 10f));
            var map = withGate
                ? BotTestMaps.Flat(
                    points,
                    new NetMapStructures(
                        new[] { new StructureLayout(_gateHealth, _gateGrounded, Array.Empty<(int, int)>()) },
                        new[] { new MapFortification(0, new Vector3(_gateCenter.X, 0f, _gateCenter.Z), false, "Gate") }),
                    new[] { WorldCollider.Box(50, 0, MapColliderGroups.Piece(0, 0), _gateCenter, new Vector3(0.25f, 0.5f, 1f), Quaternion.Identity) })
                : BotTestMaps.Flat(points);
            _context = new ServerContext(options, "1.0.0", _log, map, _match, new ServerMessenger(_transport), _control, data);
            var movement = new ServerMovement(motor);
            IHordeFortifications fortifications = null;
            _rules = new MatchRulesModule((_, matchSettings, host) => _horde = new HordeRules(matchSettings, host, fortifications));
            var combat = new ServerCombat(_rules, movement);
            _combat = combat;
            fortifications = ServerHordeFortifications.For(combat);
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

        // 門 (建物 0) について送られた、壊れた部品のビットの並びの最初の語
        private ulong[] GateStates(int connection)
        {
            var table = MatchMessages.CreateClientTable();
            var states = new List<ulong>();
            foreach (var sent in _transport.Sent)
            {
                if (sent.Connection.Value == (ulong)connection
                    && table.Read(new ArraySegment<byte>(sent.Payload)) is StructureStateMessage state && state.StructureId == 0)
                {
                    states.Add(state[0]);
                }
            }

            return states.ToArray();
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
