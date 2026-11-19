using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Numerics;
using System.Threading;
using NUnit.Framework;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.Server.Tests
{
    // 投票キックを有効にしていない既定のルールと、まだ受け口だけの購入では、ServerModules の一式に UDP で送っても試合が前と同じまま進むことを確かめる
    [NonParallelizable]
    public sealed class MatchRulesVoteKickBuyTests
    {
        private const float _tickSeconds = 1f / TickClock.DefaultTickRate;

        // 届いた後もしばらく回し、遅れて切断や返事が来ないことも見る
        private const int _ticksAfterDelivery = 30;

        private static readonly DateTime _time = new DateTime(2026, 11, 19, 9, 0, 0, DateTimeKind.Utc);
        private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(20);

        private readonly List<string> _calls = new List<string>();
        private StringWriter _log;
        private StringWriter _stdout;
        private UdpMatchTransport _transport;
        private TestClient _first;
        private TestClient _second;
        private ushort _port;
        private float _now;
        private MatchServer _server;

        [SetUp]
        public void SetUp()
        {
            _calls.Clear();
            _log = new StringWriter();
            _stdout = new StringWriter();
            _transport = new UdpMatchTransport();
            _first = new TestClient();
            _second = new TestClient();
            _port = TestClient.FreePort();
            _now = 0f;
        }

        [TearDown]
        public void TearDown()
        {
            _server?.Shutdown();
            _first.Dispose();
            _second.Dispose();
            _transport.Dispose();
            _log.Dispose();
            _stdout.Dispose();
        }

        [Test]
        public void AVoteKickAndABuyRequestChangeNothingAndDisconnectNoOne()
        {
            Start();
            Join(_first, "first");
            Join(_second, "second");
            var callerId = _first.Accepted.PlayerId;
            var targetId = _second.Accepted.PlayerId;
            Pump(() => Row(_first, callerId).HasValue && Row(_first, targetId).HasValue && Row(_second, callerId).HasValue);
            var before = _first.LatestSnapshot.Match;
            var receivedBefore = _second.Received.Count;

            _first.Send(new VoteKickRequestMessage { TargetPlayerId = targetId, InFavor = true });
            _first.Send(new BuyRequestMessage { ItemId = "armor-light" });
            Pump(() => Delivered(MessageIds.VoteKickRequest) && Delivered(MessageIds.BuyRequest));
            var deliveredAt = _first.LatestSnapshot.Tick;
            Pump(() => _first.LatestSnapshot.Tick >= deliveredAt + _ticksAfterDelivery);

            Assert.That(_first.Disconnected, Is.False, "the caller stays connected");
            Assert.That(_second.Disconnected, Is.False, "the target stays connected");
            Assert.That(_first.ServerDisconnectingReason, Is.Null);
            Assert.That(_second.ServerDisconnectingReason, Is.Null);
            Assert.That(_log.ToString(), Does.Not.Contain("[Net] dropped client"));
            Assert.That(_first.Received, Does.Not.Contain(MessageIds.VoteKickState), "vote kicks are off by default");
            Assert.That(_second.Received, Does.Not.Contain(MessageIds.VoteKickState));
            Assert.That(Row(_first, targetId).HasValue, Is.True, "the target is still in the match");
            Assert.That(Row(_second, callerId).HasValue, Is.True);

            var after = _first.LatestSnapshot.Match;
            Assert.That(after.State, Is.EqualTo(before.State));
            Assert.That(after.AlphaScore, Is.EqualTo(before.AlphaScore));
            Assert.That(after.BravoScore, Is.EqualTo(before.BravoScore));
            Assert.That(_second.Received.Count, Is.GreaterThan(receivedBefore), "snapshots keep coming to the target");
        }

        private static GameRules Rules()
        {
            return GameRules.Default
                .WithSpawnProtectionSeconds(0f)
                .WithIdleKickSeconds(0f)
                .WithRegenPerSecond(0f)
                .WithActions(ActionRules.Default.WithDowned(Downed.Off));
        }

        private bool Delivered(ushort messageId)
        {
            var suffix = ":" + messageId.ToString(CultureInfo.InvariantCulture);
            foreach (var call in _calls)
            {
                if (call.StartsWith("recorder.Message", StringComparison.Ordinal) && call.EndsWith(suffix, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private void Start()
        {
            var environment = new Dictionary<string, string>
            {
                [ServerLaunch.AddressVariable] = "127.0.0.1",
                [ServerLaunch.PortVariable] = _port.ToString(CultureInfo.InvariantCulture),
            };
            Assert.That(
                ServerLaunch.TryParse(
                    Args("-buildVersion", "0.2.0", "-log-format", "json"),
                    name => environment.TryGetValue(name, out var value) ? value : null,
                    out var launch,
                    out var error),
                Is.True,
                error);

            var rules = Rules();
            var data = TestRules.Data(rules);
            var points = new NetMapPoints(
                new[] { new SpawnPoint(new Vector3(-5f, 1.6f, 0f), Team.Alpha), new SpawnPoint(new Vector3(5f, 1.6f, 0f), Team.Bravo) },
                Array.Empty<ControlPointArea>());
            var map = new ServerMapData("NetTest", TestMaps.World(), TestMaps.NavMesh(), "memory", points);
            var runtime = new ServerRuntime(
                TextWriter.Null, TextWriter.Null, _ => null, new ImmediateStopSignal(), Array.Empty<string>(), Array.Empty<string>(), () => _transport);
            var modules = new List<IServerModule>();
            Assert.That(ServerModules.Create(launch, runtime, data, modules, out var problem), Is.True, problem);
            var host = new ServerHost();
            foreach (var module in modules)
            {
                host.Register(module);
            }

            // ルールの後ろに置き、ルールまで同じメッセージが届いたことを知る
            host.Register(new RecordingModule("recorder", _calls));
            _server = new MatchServer(
                launch, _transport, host, map, _log, new ServerEventWriter(true, _stdout, _log), () => _time, data.Settings.RestartDelaySeconds, data);
            _server.Start();
        }

        private void Join(TestClient client, string key)
        {
            client.Connect(_port);
            Pump(() => client.Connected);
            client.SendRequest("0.2.0", key);
            Pump(() => client.Accepted != null);
        }

        private static PlayerSnapshot? Row(TestClient client, int playerId)
        {
            var snapshot = client.LatestSnapshot;
            if (snapshot == null)
            {
                return null;
            }

            for (var i = 0; i < snapshot.Players.Count; i++)
            {
                if (snapshot.Players[i].PlayerId == playerId)
                {
                    return snapshot.Players[i];
                }
            }

            return null;
        }

        private void Pump(Func<bool> until)
        {
            var watch = Stopwatch.StartNew();
            do
            {
                _now += _tickSeconds;
                _server.Tick(_now, _tickSeconds);
                _first.Poll();
                _second.Poll();
                if (until())
                {
                    return;
                }

                Thread.Sleep(1);
            }
            while (watch.Elapsed < _timeout);

            Assert.Fail("timed out waiting for the server and the clients\n" + _log);
        }

        // 定数配列を直接渡すと CA1861 になるので params で受ける
        private static string[] Args(params string[] values) => values;
    }
}
