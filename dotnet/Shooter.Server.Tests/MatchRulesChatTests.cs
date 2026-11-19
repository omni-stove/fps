using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Shooter.Core;
using Shooter.Protocol;
using ChatChannel = Shooter.Protocol.ChatChannel;

namespace Shooter.Server.Tests
{
    // 試合のルールの TextChat が Off ならサーバーが誰にもチャットを配らず、On なら今どおり配ることを確かめる
    public sealed class MatchRulesChatTests
    {
        private const float _tickSeconds = 1f / 60f;
        private static readonly DateTime _time = new DateTime(2026, 11, 18, 9, 0, 0, DateTimeKind.Utc);
        private static readonly int[] _players = { 1, 2, 3 };
        private static readonly string[] _textHello = { "hello" };

        private StringWriter _log;
        private StringWriter _json;
        private RecordingTransport _transport;
        private ServerMatch _match;
        private MatchRulesModule _module;
        private long _tick;

        [SetUp]
        public void SetUp()
        {
            _log = new StringWriter();
            _json = new StringWriter();
            _transport = new RecordingTransport();
            _tick = 0;
        }

        [TearDown]
        public void TearDown()
        {
            _module?.Stop();
            _transport.Dispose();
            _log.Dispose();
            _json.Dispose();
        }

        [Test]
        public void AllChatIsDeliveredToNobodyWhenTheRuleIsOff()
        {
            StartWith(TextChat.Off);
            JoinAll();

            Assert.That(_module.ServerChat(1, ChatChannel.All, "hello"), Is.EqualTo(ChatRefusal.RuleOff));

            foreach (var id in _players)
            {
                Assert.That(_transport.Read<ChatMessage>(id, Copy), Is.Empty, "player " + id);
            }

            Assert.That(_log.ToString(), Does.Contain("[Chat] refused sender=1 reason=RuleOff"));
            Assert.That(_log.ToString(), Does.Not.Contain("[Chat] accepted"));
            Assert.That(JsonOf("chat"), Is.Empty);
            Assert.That(JsonOf("chat_refused").Single(), Does.Contain("\"playerId\":\"1\"").And.Contain("\"reason\":\"RuleOff\""));
        }

        [Test]
        public void TeamChatIsDeliveredToNobodyWhenTheRuleIsOff()
        {
            StartWith(TextChat.Off);
            JoinAll();

            Assert.That(_module.ServerChat(2, ChatChannel.Team, "push"), Is.EqualTo(ChatRefusal.RuleOff));

            foreach (var id in _players)
            {
                Assert.That(_transport.Read<ChatMessage>(id, Copy), Is.Empty, "player " + id);
            }

            Assert.That(_log.ToString(), Does.Contain("[Chat] refused sender=2 reason=RuleOff"));
        }

        [Test]
        public void RuleOffRefusalsAreLoggedNoMoreThanOncePerInterval()
        {
            StartWith(TextChat.Off);
            JoinAll();

            Assert.That(_module.ServerChat(1, ChatChannel.All, "a"), Is.EqualTo(ChatRefusal.RuleOff));
            Assert.That(_module.ServerChat(1, ChatChannel.All, "b"), Is.EqualTo(ChatRefusal.RuleOff));
            Assert.That(_module.ServerChat(2, ChatChannel.All, "c"), Is.EqualTo(ChatRefusal.RuleOff));

            Assert.That(RefusalLines(), Has.Count.EqualTo(1), "refused again within the interval");
            Assert.That(JsonOf("chat_refused"), Has.Count.EqualTo(1));

            Advance(1.1f);
            Assert.That(_module.ServerChat(2, ChatChannel.All, "d"), Is.EqualTo(ChatRefusal.RuleOff));

            Assert.That(RefusalLines(), Has.Count.EqualTo(2));
            Assert.That(RefusalLines()[1], Does.Contain("[Chat] refused sender=2 reason=RuleOff"));
        }

        [Test]
        public void ChatOutsideTheMatchIsStillDroppedWithoutAReasonWhenTheRuleIsOff()
        {
            StartWith(TextChat.Off);
            JoinAll();

            Assert.That(_module.ServerChat(9, ChatChannel.All, "hello"), Is.Null);
            Assert.That(RefusalLines(), Is.Empty);
        }

        [Test]
        public void ChatIsDeliveredAsBeforeWithTheDefaultRules()
        {
            Assert.That(GameRules.Default.TextChat, Is.EqualTo(TextChat.On));
            Start(GameRules.Default);
            JoinAll();

            Assert.That(_module.ServerChat(1, ChatChannel.All, "hello"), Is.EqualTo(ChatRefusal.None));

            foreach (var id in _players)
            {
                var received = _transport.Read<ChatMessage>(id, Copy);
                Assert.That(received.Select(m => m.Text), Is.EqualTo(_textHello), "player " + id);
                Assert.That(received[0].SenderId, Is.EqualTo(1));
                Assert.That(received[0].Channel, Is.EqualTo(ChatChannel.All));
            }

            Assert.That(_log.ToString(), Does.Contain("[Chat] accepted sender=1 channel=All length=5"));
            Assert.That(RefusalLines(), Is.Empty);
            Assert.That(JsonOf("chat"), Has.Count.EqualTo(1));
        }

        [Test]
        public void ChatIsDeliveredWhenTheRuleIsOn()
        {
            StartWith(TextChat.On);
            JoinAll();

            Assert.That(_module.ServerChat(2, ChatChannel.All, "hello"), Is.EqualTo(ChatRefusal.None));

            foreach (var id in _players)
            {
                Assert.That(_transport.Read<ChatMessage>(id, Copy).Select(m => m.Text), Is.EqualTo(_textHello), "player " + id);
            }

            Assert.That(_log.ToString(), Does.Not.Contain("reason=RuleOff"));
        }

        private void StartWith(TextChat textChat)
        {
            Start(GameRules.Default.WithTextChat(textChat));
        }

        private void Start(GameRules rules)
        {
            var settings = NetMatchSettings.Default;
            _match = new ServerMatch(settings.RestartDelaySeconds, _log, new ServerEventWriter(true, _json, _log), () => _time);
            var context = new ServerContext(
                new NetLaunchOptions(NetLaunchMode.Server, NetLaunchOptions.AnyAddress, NetLaunchOptions.DefaultPort),
                "1.0.0",
                _log,
                TestMaps.Data("NetTest", TestRules.Points()),
                _match,
                new ServerMessenger(_transport),
                new StubControl(),
                TestRules.Data(rules, settings));
            var module = new MatchRulesModule();
            module.Start(context);
            _module = module;
        }

        private void JoinAll()
        {
            foreach (var id in _players)
            {
                _match.Join(new ConnectionId((ulong)id), "device" + id);
            }
        }

        private void Advance(float seconds)
        {
            for (var elapsed = 0f; elapsed < seconds; elapsed += _tickSeconds)
            {
                _match.Tick(_tickSeconds);
                _module.Tick(++_tick, _tickSeconds);
            }
        }

        private List<string> RefusalLines()
        {
            return _log.ToString()
                .Split('\n')
                .Where(line => line.Contains("[Chat] refused", StringComparison.Ordinal))
                .ToList();
        }

        private List<string> JsonOf(string type)
        {
            return _json.ToString().Split('\n').Where(line => line.Contains("\"type\":\"" + type + "\"", StringComparison.Ordinal)).ToList();
        }

        private static T Copy<T>(T message)
            where T : class, IMessage
        {
            var writer = new MessageWriter();
            writer.WriteMessage(message);
            var table = MatchMessages.CreateClientTable();
            return (T)table.Read(writer.AsSegment());
        }
    }
}
