using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using NUnit.Framework;
using Shooter.Core;
using Shooter.Protocol;
using ChatChannel = Shooter.Protocol.ChatChannel;

namespace Shooter.Server.Tests
{
    // ルールの JSON を server-data から読み、ServerModules が組むモジュール一式を回して、
    // textChat・spectating・actions.abilities の Off がスナップショットで届き、チャット・観戦・設置の要求がどれも断られることを確かめる
    public sealed class RuleOffEndToEndTests
    {
        private const float _tickSeconds = 1f / TickClock.DefaultTickRate;
        private const string _rulesName = "TeamDeathmatch";
        private const string _shieldId = "deploy-shield";
        private static readonly DateTime _time = new DateTime(2026, 11, 19, 9, 0, 0, DateTimeKind.Utc);
        private static readonly int[] _players = { 1, 2, 3 };
        private static readonly string[] _textHello = { "hello" };

        // 床 (TestMaps の箱) の上面は y=0.5。Alpha (1 と 3) は x=-5、Bravo (2) は x=5 に出る
        private static readonly Vector3 _alphaSpawn = new Vector3(-5f, 1.6f, 0f);
        private static readonly Vector3 _bravoSpawn = new Vector3(5f, 1.6f, 0f);
        private static readonly Vector3 _alphaFront = new Vector3(-3f, 0.5f, 0f);

        private TempDirectory _root;
        private StringWriter _log;
        private StringWriter _json;
        private RecordingTransport _transport;
        private List<IServerModule> _modules;
        private ServerMatch _match;
        private ServerHost _host;
        private MatchRulesModule _rules;
        private ServerDeployables _deployables;

        [SetUp]
        public void SetUp()
        {
            _root = new TempDirectory();
            _log = new StringWriter();
            _json = new StringWriter();
            _transport = new RecordingTransport();
            _modules = new List<IServerModule>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_host != null && _host.State == ServerHostState.Running)
            {
                _host.Stop();
            }

            foreach (var module in _modules)
            {
                (module as IDisposable)?.Dispose();
            }

            _transport.Dispose();
            _log.Dispose();
            _json.Dispose();
            _root.Dispose();
        }

        [Test]
        public void TheRulesFileTurnsTheThreeOffAndTheSnapshotCarriesThem()
        {
            var data = Load(OffRules());

            Assert.That(data.Rules.TextChat, Is.EqualTo(TextChat.Off));
            Assert.That(data.Rules.Spectating, Is.EqualTo(Spectating.Off));
            Assert.That(data.Rules.Actions.Abilities, Is.EqualTo(Abilities.Off));

            Start(data);
            JoinAll();
            Step(0.2f);

            foreach (var id in _players)
            {
                var synced = LatestRules(id);
                Assert.That(synced.TextChat, Is.EqualTo(TextChat.Off), "player " + id);
                Assert.That(synced.Spectating, Is.EqualTo(Spectating.Off), "player " + id);
                Assert.That(synced.Abilities, Is.EqualTo(Abilities.Off), "player " + id);
            }
        }

        [Test]
        public void ChatSpectatingAndPlacingAreAllRefusedWhenTheRulesFileTurnsThemOff()
        {
            Start(Load(OffRules()));
            JoinAll();
            Step(0.2f);

            Send(2, new ChatRequestMessage { Channel = ChatChannel.All, Text = "hello" });

            foreach (var id in _players)
            {
                Assert.That(Chats(id), Is.Empty, "player " + id);
            }

            Assert.That(_log.ToString(), Does.Contain("[Chat] refused sender=2 reason=RuleOff"));
            Assert.That(_log.ToString(), Does.Not.Contain("[Chat] accepted"));

            Assert.That(_rules.ApplyDamage(2, 1, 1000), Is.EqualTo(DamageOutcome.Killed));
            Send(1, new SpectateRequestMessage { Step = SpectateStep.Next });
            Send(1, new SpectateRequestMessage { Step = SpectateStep.Previous });

            Assert.That(_rules.TryGetSpectateTarget(1, out _), Is.False, "a living ally (3) is there, but spectating is off");

            Send(3, new DeployRequestMessage { TypeId = _shieldId, Position = _alphaFront, Yaw = 90f });
            Step(_tickSeconds);

            Assert.That(_deployables.Count, Is.Zero);
            Assert.That(LatestDeployableCount(3), Is.Zero);
            Assert.That(_log.ToString(), Does.Contain("[Deploy] owner=3 kind=- net refused reason=RuleOff"));
            Assert.That(_log.ToString(), Does.Not.Contain(" placed serial="));
        }

        [Test]
        public void WithTheDefaultsForChatAndSpectatingAndAbilitiesOnTheSameRequestsGoThrough()
        {
            Assert.That(GameRules.Default.TextChat, Is.EqualTo(TextChat.On));
            Assert.That(GameRules.Default.Spectating, Is.EqualTo(Spectating.On));
            var data = Load(OnRules());
            Assert.That(data.Rules.TextChat, Is.EqualTo(TextChat.On));
            Assert.That(data.Rules.Spectating, Is.EqualTo(Spectating.On));
            Assert.That(data.Rules.Actions.Abilities, Is.EqualTo(Abilities.On));

            Start(data);
            JoinAll();
            Step(0.2f);

            var synced = LatestRules(1);
            Assert.That(synced.TextChat, Is.EqualTo(TextChat.On));
            Assert.That(synced.Spectating, Is.EqualTo(Spectating.On));
            Assert.That(synced.Abilities, Is.EqualTo(Abilities.On));

            Send(2, new ChatRequestMessage { Channel = ChatChannel.All, Text = "hello" });

            foreach (var id in _players)
            {
                var received = Chats(id);
                Assert.That(received.Select(m => m.Text), Is.EqualTo(_textHello), "player " + id);
                Assert.That(received[0].SenderId, Is.EqualTo(2));
            }

            Assert.That(_log.ToString(), Does.Contain("[Chat] accepted sender=2 channel=All length=5"));

            Assert.That(_rules.ApplyDamage(2, 1, 1000), Is.EqualTo(DamageOutcome.Killed));
            Send(1, new SpectateRequestMessage { Step = SpectateStep.Next });

            Assert.That(_rules.TryGetSpectateTarget(1, out var target), Is.True);
            Assert.That(target, Is.EqualTo(3));

            Send(3, new DeployRequestMessage { TypeId = _shieldId, Position = _alphaFront, Yaw = 90f });
            Step(_tickSeconds);

            Assert.That(_deployables.Count, Is.EqualTo(1), _log.ToString());
            Assert.That(LatestDeployableCount(3), Is.EqualTo(1));
            Assert.That(_log.ToString(), Does.Contain("[Deploy] owner=3 kind=deploy-shield placed serial=1"));
            Assert.That(_log.ToString(), Does.Not.Contain("reason=RuleOff"));
        }

        [Test]
        public void WithTheDefaultAbilitiesPlacingStaysRefusedAsBefore()
        {
            Assert.That(GameRules.Default.Actions.Abilities, Is.EqualTo(Abilities.Off));
            Start(Load(BaseRules()));
            JoinAll();
            Step(0.2f);

            Assert.That(LatestRules(3).Abilities, Is.EqualTo(Abilities.Off));

            Send(3, new DeployRequestMessage { TypeId = _shieldId, Position = _alphaFront, Yaw = 90f });

            Assert.That(_deployables.Count, Is.Zero);
            Assert.That(_log.ToString(), Does.Contain("[Deploy] owner=3 kind=- net refused reason=RuleOff"));
        }

        // 観戦は倒れた後に味方を映す決まり (Ally) でしか始まらないので、Off と On で他の規則をそろえるためにここだけ既定から変える
        private static GameRules BaseRules()
        {
            return GameRules.Default.WithDeathCamera(DeathCamera.Ally);
        }

        private static GameRules OffRules()
        {
            var rules = BaseRules();
            return rules
                .WithTextChat(TextChat.Off)
                .WithSpectating(Spectating.Off)
                .WithActions(rules.Actions.WithAbilities(Abilities.Off));
        }

        private static GameRules OnRules()
        {
            var rules = BaseRules();
            return rules.WithActions(rules.Actions.WithAbilities(Abilities.On));
        }

        // サーバーの起動と同じく server-data/rules の JSON と server-data/tuning の置ける物の一覧から読む
        private ServerRulesData Load(GameRules rules)
        {
            TestRules.Write(_root.Path, _rulesName, TestRules.File(rules, name: _rulesName));
            var tuning = Path.Combine(_root.Path, ServerMapData.DataDirectoryName, ServerRulesData.TuningDirectoryName);
            Directory.CreateDirectory(tuning);
            File.WriteAllText(
                Path.Combine(tuning, ServerDeployables.CatalogFileName),
                "{\"types\": [{\"id\": \"" + _shieldId + "\", \"role\": \"Shield\"}]}");

            Assert.That(ServerRulesData.TryLoad(_rulesName, new[] { _root.Path }, out var data, out var error), Is.True, error);
            return data;
        }

        private void Start(ServerRulesData data)
        {
            Assert.That(ServerLaunch.TryParse(Array.Empty<string>(), _ => null, out var launch, out var parseError), Is.True, parseError);
            var runtime = new ServerRuntime(
                TextWriter.Null, TextWriter.Null, _ => null, new ImmediateStopSignal(), new[] { _root.Path }, new[] { _root.Path }, () => _transport);
            Assert.That(ServerModules.Create(launch, runtime, data, _modules, out var problem), Is.True, problem);
            _rules = _modules.OfType<MatchRulesModule>().Single();
            _deployables = _modules.OfType<ServerDeployables>().Single();

            var points = new NetMapPoints(
                new[] { new SpawnPoint(_alphaSpawn, Team.Alpha), new SpawnPoint(_bravoSpawn, Team.Bravo) },
                Array.Empty<ControlPointArea>());
            _match = new ServerMatch(data.Settings.RestartDelaySeconds, _log, new ServerEventWriter(true, _json, _log), () => _time);
            var context = new ServerContext(
                new NetLaunchOptions(NetLaunchMode.Server, NetLaunchOptions.AnyAddress, NetLaunchOptions.DefaultPort),
                "1.0.0",
                _log,
                TestMaps.Data("NetTest", points),
                _match,
                new ServerMessenger(_transport),
                new StubControl(),
                data);
            _host = new ServerHost();
            foreach (var module in _modules)
            {
                _host.Register(module);
            }

            _host.Start(context);
        }

        // MatchServer と同じく、試合に加えてからモジュールに接続を知らせる
        private void JoinAll()
        {
            foreach (var id in _players)
            {
                var connection = new ConnectionId((ulong)id);
                _match.Join(connection, "device" + id);
                _host.OnConnected(connection);
            }

            Assert.That(_rules.Session.GetTeam(1), Is.EqualTo(Team.Alpha));
            Assert.That(_rules.Session.GetTeam(2), Is.EqualTo(Team.Bravo));
            Assert.That(_rules.Session.GetTeam(3), Is.EqualTo(Team.Alpha));
        }

        private void Send(int id, IMessage message)
        {
            _host.OnMessage(new ConnectionId((ulong)id), message);
        }

        private void Step(float seconds)
        {
            for (var elapsed = 0f; elapsed < seconds; elapsed += _tickSeconds)
            {
                _match.Tick(_tickSeconds);
                _host.Tick(_tickSeconds);
            }
        }

        private List<ChatMessage> Chats(int id)
        {
            return _transport.Read<ChatMessage>(id, m => new ChatMessage { SenderId = m.SenderId, Channel = m.Channel, Text = m.Text });
        }

        // 入力で受け取りを知らせていないので、どのスナップショットも基準なしの全部入りで届く
        private MatchRulesSync LatestRules(int id)
        {
            var latest = default(MatchRulesSync);
            var snapshots = _transport.Read<SnapshotMessage>(id, snapshot =>
            {
                Assert.That(snapshot.IsComplete, Is.True);
                latest = snapshot.Match.Rules;
                return snapshot;
            });
            Assert.That(snapshots, Is.Not.Empty);
            return latest;
        }

        private int LatestDeployableCount(int id)
        {
            var count = -1;
            var snapshots = _transport.Read<SnapshotMessage>(id, snapshot =>
            {
                count = snapshot.Deployables.Count;
                return snapshot;
            });
            Assert.That(snapshots, Is.Not.Empty);
            return count;
        }
    }
}
