using System;
using System.IO;
using System.Linq;
using System.Numerics;
using NUnit.Framework;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.Server.Tests
{
    // 移動・ルール・戦闘・Bot・設置物を MatchServer と同じ順で載せ、人の置いた囮に Bot が気を引かれるかを確かめる
    public sealed class ServerDeployablesBotTests
    {
        private const float _tickSeconds = 1f / TickClock.DefaultTickRate;
        private const string _decoyId = "deploy-decoy";
        private static readonly DateTime _time = new DateTime(2026, 11, 19, 9, 0, 0, DateTimeKind.Utc);

        // 床の上面は y=0。人 (Alpha) は x=-6、Bot (Bravo) は x=6 に出る。囮は人の 3 m 前 (Bot から 9 m、人より 3 m 近い) に置く
        private static readonly Vector3 _decoyAt = new Vector3(-3f, 0f, 0f);

        private StringWriter _log;
        private RecordingTransport _transport;
        private ServerMatch _match;
        private ServerContext _context;
        private ServerHost _host;
        private MatchRulesModule _rules;
        private ServerBots _bots;
        private ServerDeployables _deployables;

        [SetUp]
        public void SetUp()
        {
            _log = new StringWriter();
            _transport = new RecordingTransport();
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
        }

        [Test]
        public void ABotThatSeesADecoyNearerThanItsTargetTurnsToTheDecoyAndLosesItWhenItBreaks()
        {
            Start(30f, DecoySpec.Default);
            Join(1);
            Step(0.2f);
            var bot = _bots.BotIds.Single();
            Assert.That(_match.TryGetPlayer(bot, out var player) && player.Team == Team.Bravo, Is.True);

            Assert.That(_deployables.Place(1, _decoyId, _decoyAt, 0f), Is.EqualTo(ServerDeployRefusal.None), _log.ToString());
            Step(0.1f);

            var botName = bot.ToString(System.Globalization.CultureInfo.InvariantCulture);
            Assert.That(_log.ToString(), Does.Contain("[Decoy] enemy=" + botName + " sees owner=1 kind=deploy-decoy serial=1"), _log.ToString());

            Assert.That(_deployables.Damage(1, bot, DecoySpec.DefaultMaxHealth), Is.True);
            Step(0.1f);

            Assert.That(
                _log.ToString(),
                Does.Contain("[Decoy] enemy=" + botName + " lost owner=1 kind=deploy-decoy serial=1 last=(-3.00, 0.00, 0.00) gone=True"));
        }

        [Test]
        public void ABotOutOfSightHearsTheDecoyCallOnce()
        {
            // 囮は人の 2 m 前 (Bot から 10 m)。見える距離 5 m の外で、呼び声の届く 15 m の内。呼ぶ間隔は試す間より長いので、呼ぶのは置いた時の 1 回だけ
            Start(5f, new DecoySpec(20f, 10f, 15f));
            Join(1);
            Step(0.2f);
            var bot = _bots.BotIds.Single();

            Assert.That(_deployables.Place(1, _decoyId, new Vector3(-4f, 0f, 0f), 0f), Is.EqualTo(ServerDeployRefusal.None), _log.ToString());
            Step(0.3f);

            var heard = "[Decoy] enemy=" + bot.ToString(System.Globalization.CultureInfo.InvariantCulture)
                + " hears owner=1 kind=deploy-decoy serial=1 at=(-4.00, 0.00, 0.00)";
            Assert.That(_log.ToString().Split(heard).Length - 1, Is.EqualTo(1), "one call is heard once: " + _log);
            Assert.That(_log.ToString(), Does.Not.Contain(" sees owner=1"));
        }

        [Test]
        public void ADecoyOfTheBotsOwnTeamDoesNotLureIt()
        {
            // 4 人に満たすので、人と同じ Alpha に Bot が 1 人、Bravo に 2 人入る
            Start(30f, DecoySpec.Default, 4);
            Join(1);
            Step(0.2f);
            var teammate = _bots.BotIds.Single(id => _match.TryGetPlayer(id, out var p) && p.Team == Team.Alpha);
            var enemy = _bots.BotIds.First(id => _match.TryGetPlayer(id, out var p) && p.Team == Team.Bravo);

            Assert.That(_deployables.Place(1, _decoyId, _decoyAt, 0f), Is.EqualTo(ServerDeployRefusal.None), _log.ToString());
            Step(0.1f);

            var culture = System.Globalization.CultureInfo.InvariantCulture;
            Assert.That(_log.ToString(), Does.Not.Contain("[Decoy] enemy=" + teammate.ToString(culture) + " "));
            Assert.That(_log.ToString(), Does.Contain("[Decoy] enemy=" + enemy.ToString(culture) + " sees owner=1"));
        }

        private static NetBotSpec SightOf(float range)
        {
            var defaults = NetBotSpec.Default;
            return new NetBotSpec(
                new EnemySpec(range, Math.Min(range, defaults.Brain.AttackRange), defaults.Brain.LoseSightSeconds, defaults.Brain.SearchSeconds, defaults.Brain.ReactionSeconds),
                defaults.Input,
                defaults.NavSampleRadius,
                defaults.RepathSeconds,
                defaults.RepathDistance,
                defaults.StuckSeconds,
                defaults.StuckProgress);
        }

        private void Start(float sightRange, DecoySpec decoy, int bots = 2)
        {
            var actions = ActionRules.Default.WithDowned(Downed.Off).WithAbilities(Abilities.On);
            var rules = GameRules.Default.WithActions(actions).WithSpawnProtectionSeconds(0f).WithIdleKickSeconds(0f).WithRegenPerSecond(0f);
            var data = new ServerRulesData("TeamDeathmatch", TestRules.File(rules), PingSpec.Default, "memory", null, bots: SightOf(sightRange));
            var map = BotTestMaps.Flat(BotTestMaps.Points(new Vector3(-6f, 1.1f, 0f), new Vector3(6f, 1.1f, 0f)));
            _match = new ServerMatch(NetMatchSettings.DefaultRestartDelaySeconds, _log, new ServerEventWriter(false, TextWriter.Null, _log), () => _time);
            var options = new NetLaunchOptions(
                NetLaunchMode.Server, NetLaunchOptions.AnyAddress, NetLaunchOptions.DefaultPort, NetTransportKind.Udp, null, null, null, bots, 0, 0, 0, 0);
            _context = new ServerContext(options, "1.0.0", _log, map, _match, new ServerMessenger(_transport), new StubControl(), data);
            var movement = new ServerMovement(ServerModules.MotorSettingsFrom(data));
            _rules = new MatchRulesModule();
            var combat = new ServerCombat(_rules, movement);
            var catalog = new DeployCatalog(new[]
            {
                new DeployType(_decoyId, DeployRole.Decoy, DeployType.DefaultDeploy(DeployRole.Decoy), decoy: decoy),
            });
            _deployables = new ServerDeployables(_rules, combat, catalog, "memory");
            _bots = new ServerBots(_rules, movement, combat, _deployables);
            _host = new ServerHost();
            _host.Register(movement);
            _host.Register(_rules);
            _host.Register(combat);
            _host.Register(_bots);
            _host.Register(_deployables);
            _host.Start(_context);
        }

        private void Join(int id)
        {
            var connection = new ConnectionId((ulong)id);
            _match.Join(connection, "device" + id);
            _host.OnConnected(connection);
        }

        private void Step(float seconds)
        {
            for (var elapsed = 0f; elapsed < seconds; elapsed += _tickSeconds)
            {
                _match.Tick(_tickSeconds);
                _host.Tick(_tickSeconds);
            }
        }
    }
}
