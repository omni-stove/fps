using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading;
using NUnit.Framework;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.Server.Tests
{
    // 3 クライアントを UDP でつなぎ、投票で外された人が理由付きで切られ、同じ試合には入り直せず、次の試合では入れることを確かめる
    [NonParallelizable]
    public sealed class MatchServerVoteKickTests
    {
        private const float _tickSeconds = 1f / 60f;
        private static readonly DateTime _time = new DateTime(2026, 11, 19, 9, 0, 0, DateTimeKind.Utc);
        private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(20);

        private readonly List<TestClient> _clients = new List<TestClient>();
        private StringWriter _log;
        private UdpMatchTransport _transport;
        private ushort _port;
        private float _now;
        private MatchServer _server;
        private MatchRulesModule _rules;

        [SetUp]
        public void SetUp()
        {
            _log = new StringWriter();
            _transport = new UdpMatchTransport();
            _port = TestClient.FreePort();
            _now = 0f;
        }

        [TearDown]
        public void TearDown()
        {
            _server?.Shutdown();
            foreach (var client in _clients)
            {
                client.Dispose();
            }

            _clients.Clear();
            _transport.Dispose();
            _log.Dispose();
        }

        [Test]
        public void TwoOfThreeVoteOutTheThirdWhoCannotRejoinUntilTheNextMatch()
        {
            Start(new VoteKickSpec(true, 0.6f, 30f, 120f));
            var first = Request("first");
            var second = Request("second");
            var third = Request("third");
            Assert.That(first.Accepted, Is.Not.Null, first.DisconnectReason);
            Assert.That(second.Accepted, Is.Not.Null, second.DisconnectReason);
            Assert.That(third.Accepted, Is.Not.Null, third.DisconnectReason);
            var firstId = first.Accepted.PlayerId;
            var secondId = second.Accepted.PlayerId;
            var thirdId = third.Accepted.PlayerId;

            first.Send(new VoteKickRequestMessage { TargetPlayerId = thirdId, InFavor = true });
            Pump(() => _rules.CurrentVoteKick.IsOpen && second.Received.Contains(MessageIds.VoteKickState));
            second.Send(new VoteKickRequestMessage { TargetPlayerId = thirdId, InFavor = true });
            Pump(() => third.Disconnected);

            Assert.That(third.DisconnectReason, Is.EqualTo(DisconnectNotice.VoteKickedReason));
            Assert.That(DisconnectNotice.Classify(third.DisconnectReason, true, "0.2.0").Kind, Is.EqualTo(DisconnectKind.VoteKicked));
            Assert.That(first.Disconnected, Is.False);
            Assert.That(second.Disconnected, Is.False);
            Assert.That(_server.Match.HumanCount, Is.EqualTo(2));
            Assert.That(
                _log.ToString(),
                Does.Contain("[Net] dropped client " + Text(thirdId) + ": vote kicked (yes 2/2)"));

            var refused = Request("third");

            Assert.That(refused.Accepted, Is.Null);
            Assert.That(refused.DisconnectReason, Is.EqualTo(DisconnectNotice.VoteKickedReason));
            Assert.That(_log.ToString(), Does.Contain(VoteKickLog.RefusedRejoin(_server.Match.MatchId)));
            Assert.That(Request("fourth").Accepted, Is.Not.Null, "others can still join");

            Assert.That(_server.Match.TryGetPlayer(firstId, out var firstPlayer), Is.True);
            Assert.That(_server.Match.TryGetPlayer(secondId, out var secondPlayer), Is.True);
            Assert.That(firstPlayer.Team, Is.Not.EqualTo(secondPlayer.Team));
            var ended = _server.Match.MatchId;
            Assert.That(_rules.ApplyDamage(firstId, secondId, 100), Is.EqualTo(DamageOutcome.Killed));
            Assert.That(_server.Match.State, Is.EqualTo(GameModeState.Ended));
            Assert.That(Request("third").Accepted, Is.Null, "still refused while the ended match shows its result");

            Pump(() => _server.Match.MatchId != ended);

            var back = Request("third");
            Assert.That(back.Accepted, Is.Not.Null, back.DisconnectReason);
        }

        private void Start(VoteKickSpec voteKick)
        {
            var environment = new Dictionary<string, string>
            {
                [ServerLaunch.AddressVariable] = "127.0.0.1",
                [ServerLaunch.PortVariable] = _port.ToString(CultureInfo.InvariantCulture),
            };
            Assert.That(
                ServerLaunch.TryParse(Args("-buildVersion", "0.2.0"), name => environment.TryGetValue(name, out var value) ? value : null, out var launch, out var error),
                Is.True,
                error);

            // 待ちを短くすると、終わった試合で入り直しを断るのを確かめている間に次の試合が始まってしまう
            var settings = new NetMatchSettings(scoreLimit: 1, restartDelaySeconds: 5f, voteKick: voteKick);
            var rules = GameRules.Default
                .WithActions(ActionRules.Default.WithDowned(Downed.Off).WithPing(Ping.Off))
                .WithSpawnProtectionSeconds(0f)
                .WithIdleKickSeconds(0f)
                .WithRegenPerSecond(0f);
            var data = TestRules.Data(rules, settings);
            var host = new ServerHost();
            _rules = new MatchRulesModule();
            host.Register(_rules);
            _server = new MatchServer(
                launch,
                _transport,
                host,
                TestMaps.Data("NetTest", TestRules.Points()),
                _log,
                new ServerEventWriter(false, _log, _log),
                () => _time,
                settings.RestartDelaySeconds,
                data);
            _server.Start();
        }

        private TestClient Request(string key)
        {
            var client = new TestClient();
            _clients.Add(client);
            client.Connect(_port);
            Pump(() => client.Connected);
            client.SendRequest("0.2.0", key);
            Pump(() => client.Disconnected || client.Accepted != null);
            return client;
        }

        private void Pump(Func<bool> until)
        {
            var watch = Stopwatch.StartNew();
            do
            {
                _now += _tickSeconds;
                _server.Tick(_now, _tickSeconds);
                foreach (var client in _clients)
                {
                    client.Poll();
                }

                if (until())
                {
                    return;
                }

                Thread.Sleep(1);
            }
            while (watch.Elapsed < _timeout);

            Assert.Fail("timed out waiting for the server and the clients\n" + _log);
        }

        private static string Text(int value) => value.ToString(CultureInfo.InvariantCulture);

        // 定数配列を直接渡すと CA1861 になるので params で受ける
        private static string[] Args(params string[] values) => values;
    }
}
