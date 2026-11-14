using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Numerics;
using System.Threading;
using Fps.Core;
using Fps.Protocol;
using NUnit.Framework;

namespace Fps.Server.Tests
{
    [NonParallelizable]
    public sealed class ServerMovementNetworkTests
    {
        private const float _tickSeconds = 1f / TickClock.DefaultTickRate;
        private static readonly DateTime _time = new DateTime(2026, 11, 13, 9, 0, 0, DateTimeKind.Utc);
        private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(10);
        private static readonly Vector3 _spawn = new Vector3(0f, 1.7f, 0f);

        private StringWriter _log;
        private UdpMatchTransport _transport;
        private TestClient _client;
        private ushort _port;
        private float _now;
        private MatchServer _server;

        [SetUp]
        public void SetUp()
        {
            _log = new StringWriter();
            _transport = new UdpMatchTransport();
            _client = new TestClient();
            _port = TestClient.FreePort();
            _now = 0f;
        }

        [TearDown]
        public void TearDown()
        {
            _client.Dispose();
            _transport.Dispose();
            _log.Dispose();
        }

        [Test]
        public void InputFromAClientComesBackAsASnapshotWithTheMovedPosition()
        {
            Start();
            _client.Connect(_port);
            Pump(() => _client.Connected);
            _client.SendRequest("0.2.0");
            Pump(() => _client.Accepted != null);
            Pump(() => _client.LatestSnapshot != null);

            var start = _client.LatestSnapshot.Players[0].Position;
            Assert.That(_client.LatestSnapshot.Owner.LastProcessedInputTick, Is.EqualTo(-1));

            var inputTick = 0;
            Pump(() =>
            {
                var message = new InputMessage
                {
                    AckedSnapshotTick = _client.LatestSnapshot?.Tick ?? InputMessage.NoSnapshot,
                };
                message.Add(new InputFrame(inputTick, new Vector2(0f, 1f), 0f, InputButtons.None));
                _client.Send(message, TransportChannel.Unreliable);
                inputTick++;
                var latest = _client.LatestSnapshot;
                return latest.Owner.LastProcessedInputTick >= 60
                    && latest.Players[0].Position.Z > start.Z + 1f;
            });

            var snapshot = _client.LatestSnapshot;
            var row = snapshot.Players[0];
            Assert.That(row.PlayerId, Is.EqualTo(_client.Accepted.PlayerId));
            Assert.That(row.Position.Z, Is.GreaterThan(start.Z + 1f));
            Assert.That(row.Position.X, Is.EqualTo(start.X).Within(1e-3f));
            Assert.That(row.IsGrounded, Is.True);
            Assert.That(_client.DeltaSnapshots, Is.GreaterThan(0), "acknowledged snapshots should become baselines");
            Assert.That(_client.UnresolvedSnapshots, Is.Zero);
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
                    Args("-buildVersion", "0.2.0"),
                    name => environment.TryGetValue(name, out var value) ? value : null,
                    out var launch,
                    out var error),
                Is.True,
                error);

            var settings = ServerMotorSettings.Default.WithSpawnPosition(_spawn);
            var movement = new ServerMovement(settings);
            var host = new ServerHost();
            host.Register(movement);
            host.Register(new ServerSnapshots(settings.Rules, new ISnapshotContributor[] { movement }));
            _server = new MatchServer(launch, _transport, host, TestMaps.Data(), _log, new ServerEventWriter(false, _log, _log), () => _time);
            _server.Start();
        }

        private void Pump(Func<bool> until)
        {
            var watch = Stopwatch.StartNew();
            do
            {
                _now += _tickSeconds;
                _server.Tick(_now, _tickSeconds);
                _client.Poll();
                if (until())
                {
                    return;
                }

                Thread.Sleep(1);
            }
            while (watch.Elapsed < _timeout);

            Assert.Fail("timed out waiting for the server and the client");
        }

        // 定数配列を直接渡すと CA1861 になるので params で受ける
        private static string[] Args(params string[] values) => values;
    }
}
