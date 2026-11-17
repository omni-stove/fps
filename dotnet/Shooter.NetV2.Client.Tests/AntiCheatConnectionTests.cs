using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using NUnit.Framework;
using Shooter.Core;
using Shooter.Protocol;
using Shooter.Server;
using Shooter.Server.Eos;

namespace Shooter.NetV2.Tests
{
    /// <summary>
    /// .NET のサーバーと クライアントが、UDP の上で EAC の Hello とメッセージを送り合う。
    /// EAC の SDK は両側とも差し替えるので、EOS の認証情報も SDK も無い手元で回せる。
    /// </summary>
    [NonParallelizable]
    public sealed class AntiCheatConnectionTests
    {
        private const string _version = "0.2.0";
        private const string _clientUser = "0123456789abcdef0123456789abcdef";
        private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(15);

        private readonly List<Player> _players = new List<Player>();
        private ServerConnectionTests.StopSignal _stop;
        private StringWriter _output;
        private TextWriter _synchronizedOutput;
        private Thread _serverThread;
        private ushort _port;
        private string _root;
        private FakeServerBackend _backend;

        [SetUp]
        public void SetUp()
        {
            _root = ServerConnectionTests.ServerDataCopy();
            _stop = new ServerConnectionTests.StopSignal();
            _output = new StringWriter();
            _synchronizedOutput = TextWriter.Synchronized(_output);
            _port = ServerConnectionTests.FreePort();
            _backend = null;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var player in _players)
            {
                player.Client.Dispose();
            }

            _players.Clear();
            if (_serverThread != null && _serverThread.IsAlive)
            {
                _stop.Raise();
                _stop.Raise();
                _serverThread.Join(_timeout);
            }

            try
            {
                Directory.Delete(_root, true);
            }
            catch (IOException)
            {
            }

            _backend?.Dispose();
            _synchronizedOutput.Dispose();
            _output.Dispose();
        }

        [Test]
        public void TheDefaultServerPlaysWithAClientWithoutAntiCheat()
        {
            StartServer(false);
            var player = Join(null);

            Pump(() => Output.Contains("also runs without anti-cheat", StringComparison.Ordinal)
                && player.Log.Any(line => line.Contains("server id=0 also runs without anti-cheat", StringComparison.Ordinal)));
            Wait(TimeSpan.FromSeconds(0.5));

            Assert.That(player.Client.State, Is.EqualTo(MatchClientState.Connected));
            Assert.That(player.RemovedReason, Is.Null);
            Assert.That(Output, Does.Contain("[AntiCheat] disabled on the dedicated server by default"));
        }

        [Test]
        public void TheDefaultServerTurnsAwayAClientThatRequiresAntiCheat()
        {
            StartServer(false);
            var player = Join(new FakeSession());

            Pump(() => player.Notice != null || player.RemovedReason != null);
            Pump(() => Output.Contains("[Match] left player=1", StringComparison.Ordinal));
            Pump(() => player.Log.Any(line => line.Contains("removing peer", StringComparison.Ordinal)));

            AssertTurnedAway(player, "the peer runs without anti-cheat (-noeac or an unprotected build)",
                "removing peer id=1: the peer requires anti-cheat but this side runs without it");
            Assert.That(player.Session.Ended, Is.True);
        }

        [Test]
        public void AProtectedServerRelaysEacMessagesBothWays()
        {
            StartServer(true);
            var player = Join(new FakeSession());

            Pump(() => player.Session.Received.Count > 0);
            player.Session.SendToServer(5, 6, 7);
            Pump(() => !_backend.Received.IsEmpty);

            Assert.That(Output, Does.Contain("[AntiCheat] registered peer id=1 user=" + _clientUser + " handle=1"));
            Assert.That(_backend.Received.Single(), Is.EqualTo(new byte[] { 5, 6, 7 }));
            Assert.That(player.Session.Received.Single(), Is.EqualTo(FakeServerBackend.Greeting));
            Assert.That(player.Log, Has.Some.Contains("server id=0 runs anti-cheat"));
            Assert.That(player.Client.State, Is.EqualTo(MatchClientState.Connected));
        }

        [Test]
        public void AProtectedServerTurnsAwayAClientWithoutAntiCheat()
        {
            StartServer(true);
            var player = Join(null);

            Pump(() => player.Notice != null || player.RemovedReason != null);
            Pump(() => Output.Contains("[Match] left player=1", StringComparison.Ordinal));
            Pump(() => player.Log.Any(line => line.Contains("removing peer", StringComparison.Ordinal)));

            AssertTurnedAway(player, "the peer requires anti-cheat but this side runs without it",
                "removing peer id=1: the peer runs without anti-cheat (-noeac or an unprotected build)");
            Assert.That(_backend.Registered, Is.Empty);
        }

        [Test]
        public void AnEacRemovalOnTheServerDisconnectsTheClientWithTheAntiCheatReason()
        {
            StartServer(true);
            var player = Join(new FakeSession());
            Pump(() => player.Session.Received.Count > 0);

            _backend.RemoveEveryone = true;
            Pump(() => player.Notice != null);

            Assert.That(player.Notice.Kind, Is.EqualTo(DisconnectKind.Rejected));
            Assert.That(player.Notice.Detail, Is.EqualTo(AntiCheatSettings.RejectedDetail));
            Assert.That(Output, Does.Contain("removing peer id=1: anti-cheat violation RemovePlayer"));
        }

        private void AssertTurnedAway(Player player, string clientReason, string serverLine)
        {
            Assert.That(player.Log, Has.Some.EqualTo("[AntiCheat] removing peer id=0: " + clientReason));
            Assert.That(Output, Does.Contain(serverLine));

            // サーバーが切るのとクライアントが抜けるのは同じ食い違いで起き、どちらが先かは届く順で変わる
            if (player.Notice == null)
            {
                Assert.That(player.RemovedReason, Is.EqualTo(clientReason));
                return;
            }

            Assert.That(player.Notice.Kind, Is.EqualTo(DisconnectKind.Rejected));
            Assert.That(player.Notice.Detail, Is.EqualTo(AntiCheatSettings.RejectedDetail));
        }

        private string Output
        {
            get
            {
                lock (_synchronizedOutput)
                {
                    return _output.ToString();
                }
            }
        }

        private void StartServer(bool protectedServer)
        {
            var environment = new Dictionary<string, string>
            {
                [ServerLaunch.AddressVariable] = "127.0.0.1",
                [ServerLaunch.PortVariable] = _port.ToString(CultureInfo.InvariantCulture),
            };
            var runtime = new ServerRuntime(
                _synchronizedOutput,
                _synchronizedOutput,
                name => environment.TryGetValue(name, out var value) ? value : null,
                _stop,
                new[] { _root },
                new[] { _root },
                () => new UserIdTransport(_clientUser));
            if (protectedServer)
            {
                _backend = new FakeServerBackend();
            }

            var args = new[] { "-mode", "server", "-buildVersion", _version, "-drain-timeout", "0" };
            _serverThread = new Thread(() => ServerProgram.Run(args, CreateModules, runtime)) { IsBackground = true };
            _serverThread.Start();
            Pump(() => Output.Contains("[Net] server listening on", StringComparison.Ordinal));
        }

        // -eac は EOS の transport (#594) が入るまで起動引数では選べないので、EAC のモジュールだけを差し替える
        private bool CreateModules(ServerLaunch launch, ServerRuntime runtime, ServerRulesData rules, ICollection<IServerModule> modules, out string error)
        {
            var created = new List<IServerModule>();
            if (!ServerModules.Create(launch, runtime, rules, created, out error))
            {
                return false;
            }

            if (_backend != null)
            {
                var options = new NetLaunchOptions(NetLaunchMode.Server, NetLaunchOptions.AnyAddress, NetLaunchOptions.DefaultPort, NetTransportKind.Eos, null);
                Assert.That(AntiCheatSettings.TryResolveDedicatedServer(new[] { AntiCheatSettings.EacFlag }, options, out var settings, out error), Is.True);
                var backend = _backend;
                created[0] = new ServerAntiCheat(settings, _ => backend);
            }

            foreach (var module in created)
            {
                modules.Add(module);
            }

            return true;
        }

        private Player Join(FakeSession session)
        {
            var client = new MatchClient(new UdpMatchTransport(), _version, RejoinKey.NewDeviceKey(), "tester", _ => { });
            var player = new Player(client, session);
            _players.Add(player);
            client.Connect("127.0.0.1", _port);
            Pump(() => client.State == MatchClientState.Connected || player.Notice != null || player.RemovedReason != null);
            return player;
        }

        private void Wait(TimeSpan duration)
        {
            var watch = Stopwatch.StartNew();
            Pump(() => watch.Elapsed >= duration);
        }

        private void Pump(Func<bool> until)
        {
            var watch = Stopwatch.StartNew();
            do
            {
                foreach (var player in _players)
                {
                    player.Update(watch.Elapsed.TotalSeconds);
                }

                if (until())
                {
                    return;
                }

                Thread.Sleep(5);
            }
            while (watch.Elapsed < _timeout);

            Assert.Fail("timed out waiting for the server and the clients\n" + Output);
        }

        private sealed class Player
        {
            private double _now;

            public Player(MatchClient client, FakeSession session)
            {
                Client = client;
                Session = session;
                AntiCheat = new ClientAntiCheat(
                    session != null,
                    session,
                    message => client.Send(message, TransportChannel.Reliable),
                    Log.Add,
                    reason =>
                    {
                        RemovedReason = reason;
                        client.Leave();
                    });
                client.Disconnected += notice =>
                {
                    Notice = notice;
                    AntiCheat.End();
                };
                client.Accepted += () => Assert.That(AntiCheat.ServerConnected(_now, out var error), Is.True, error);
                client.MessageReceived += message =>
                {
                    if (message is AntiCheatToClientMessage antiCheat)
                    {
                        AntiCheat.HandleMessage(antiCheat, _now);
                    }
                };
            }

            public MatchClient Client { get; }

            public FakeSession Session { get; }

            public ClientAntiCheat AntiCheat { get; }

            public List<string> Log { get; } = new List<string>();

            public DisconnectNotice Notice { get; private set; }

            public string RemovedReason { get; private set; }

            public void Update(double now)
            {
                _now = now;
                Client.Poll();
                AntiCheat.Tick(now);
            }
        }

        private sealed class FakeSession : IAntiCheatClientSession
        {
            private Action<ArraySegment<byte>> _send;

            public List<byte[]> Received { get; } = new List<byte[]>();

            public bool Ended { get; private set; }

            public bool BeginSession(Action<ArraySegment<byte>> sendToServer, Action<string> fail, out string reason)
            {
                _send = sendToServer;
                reason = null;
                return true;
            }

            public bool ReceiveMessageFromServer(ArraySegment<byte> message, out string reason)
            {
                Received.Add(message.ToArray());
                reason = null;
                return true;
            }

            public void EndSession()
            {
                Ended = true;
            }

            public void SendToServer(params byte[] message)
            {
                _send(new ArraySegment<byte>(message));
            }
        }

        /// <summary>サーバーのスレッドで呼ばれ、テストのスレッドから読まれる。</summary>
        private sealed class FakeServerBackend : IAntiCheatServerBackend
        {
            public static readonly byte[] Greeting = { 1, 2, 3 };

            private readonly ConcurrentQueue<long> _toGreet = new ConcurrentQueue<long>();
            private readonly ConcurrentDictionary<long, bool> _clients = new ConcurrentDictionary<long, bool>();

            public event Action<long, ArraySegment<byte>> MessageToClient;

            public event Action<long, bool, string> ClientActionRequired;

            public event Action<long, string> ClientAuthStatusChanged;

            public ConcurrentQueue<long> Registered { get; } = new ConcurrentQueue<long>();

            public ConcurrentQueue<byte[]> Received { get; } = new ConcurrentQueue<byte[]>();

            public volatile bool RemoveEveryone;

            public bool Begin(out string error)
            {
                error = null;
                return true;
            }

            public bool RegisterClient(long handle, string productUserId, out string error)
            {
                Registered.Enqueue(handle);
                _clients[handle] = true;
                _toGreet.Enqueue(handle);
                error = null;
                return true;
            }

            public bool UnregisterClient(long handle, out string error)
            {
                _clients.TryRemove(handle, out _);
                error = null;
                return true;
            }

            public bool ReceiveMessageFromClient(long handle, ArraySegment<byte> message, out string error)
            {
                Received.Enqueue(message.ToArray());
                error = null;
                return true;
            }

            public void Tick()
            {
                while (_toGreet.TryDequeue(out var handle))
                {
                    ClientAuthStatusChanged?.Invoke(handle, "RemoteAuthComplete");
                    MessageToClient?.Invoke(handle, new ArraySegment<byte>(Greeting));
                }

                if (RemoveEveryone)
                {
                    foreach (var handle in _clients.Keys)
                    {
                        ClientActionRequired?.Invoke(handle, true, "RemovePlayer InternalError");
                    }
                }
            }

            public bool End(out string error)
            {
                error = null;
                return true;
            }

            public void Dispose()
            {
            }
        }

        /// <summary>EOS の P2P の transport の代わりに、UDP の接続に決まった ProductUserId を持たせる。</summary>
        private sealed class UserIdTransport : IMatchTransport, IConnectionUserIds
        {
            private readonly UdpMatchTransport _inner = new UdpMatchTransport();
            private readonly string _userId;

            public UserIdTransport(string userId)
            {
                _userId = userId;
            }

            public bool IsActive => _inner.IsActive;

            public void Listen(string address, ushort port) => _inner.Listen(address, port);

            public void Connect(string address, ushort port) => _inner.Connect(address, port);

            public void Disconnect(ConnectionId connection, string reason) => _inner.Disconnect(connection, reason);

            public void Send(ConnectionId connection, TransportChannel channel, ArraySegment<byte> payload) => _inner.Send(connection, channel, payload);

            public int GetMaxPayloadSize(TransportChannel channel) => _inner.GetMaxPayloadSize(channel);

            public bool TryGetRoundTripTime(ConnectionId connection, out int milliseconds) => _inner.TryGetRoundTripTime(connection, out milliseconds);

            public bool TryReceive(out TransportEvent transportEvent) => _inner.TryReceive(out transportEvent);

            public void Shutdown(string reason) => _inner.Shutdown(reason);

            public void Dispose() => _inner.Dispose();

            public bool TryGetProductUserId(ConnectionId connection, out string productUserId)
            {
                productUserId = _userId;
                return connection.IsValid;
            }
        }
    }
}
