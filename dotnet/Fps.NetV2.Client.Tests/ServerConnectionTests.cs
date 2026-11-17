using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Numerics;
using System.Threading;
using Fps.Core;
using Fps.Core.Navigation;
using Fps.Protocol;
using Fps.Protocol.Tests;
using Fps.Server;
using NUnit.Framework;

namespace Fps.NetV2.Tests
{
    /// <summary>
    /// <c>make server-dotnet</c> と同じ起動 (コミットした server-data・既定のマップとルール) の .NET のサーバーに、
    /// クライアントの中身を 2 つつなぐ。
    /// </summary>
    [NonParallelizable]
    public sealed class ServerConnectionTests
    {
        private const string _eosServerId = "0002aaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        private const string _eosFirstId = "0002bbbbbbbbbbbbbbbbbbbbbbbbbbbb";
        private const string _eosSecondId = "0002cccccccccccccccccccccccccccc";
        private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(15);

        private readonly List<Player> _players = new List<Player>();
        private StopSignal _stop;
        private StringWriter _output;
        private TextWriter _synchronizedOutput;
        private Thread _serverThread;
        private int _exitCode = -1;
        private ushort _port;
        private string _root;

        [SetUp]
        public void SetUp()
        {
            _root = ServerDataCopy();
            _stop = new StopSignal();
            _output = new StringWriter();
            _synchronizedOutput = TextWriter.Synchronized(_output);
            _port = FreePort();
        }

        [TearDown]
        public void TearDown()
        {
            try
            {
                Directory.Delete(_root, true);
            }
            catch (IOException)
            {
            }

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

            _synchronizedOutput.Dispose();
            _output.Dispose();
        }

        [Test]
        public void TwoClientsSeeEachOtherMoveAndAreToldWhenTheServerStops()
        {
            StartServer();
            var mover = Join();
            var watcher = Join();
            Pump(() => mover.Match.IsPredicting && watcher.Match.IsPredicting
                && HasRemote(mover, watcher.Client.PlayerId) && HasRemote(watcher, mover.Client.PlayerId));

            Assert.That(watcher.Match.TrySampleRemote(mover.Client.PlayerId, out var before), Is.True);
            var start = mover.Match.LocalState.Position;
            var forward = new Vector2(0f, 1f);
            var elapsed = Stopwatch.StartNew();
            Pump(() =>
            {
                mover.Move = forward;
                return elapsed.Elapsed > TimeSpan.FromSeconds(1.5) && Moved(watcher, mover.Client.PlayerId, before.Position) > 2f;
            });
            mover.Move = Vector2.Zero;
            Pump(() => mover.Match.Prediction.PendingFrames < 4 && Settled(watcher, mover));

            var predicted = mover.Match.LocalState.Position;
            Assert.That(Horizontal(predicted - start), Is.GreaterThan(2f), "the moving client predicts its own walk");
            Assert.That(watcher.Match.TrySampleRemote(mover.Client.PlayerId, out var seen), Is.True);
            Assert.That(Horizontal(seen.Position - predicted), Is.LessThan(0.1f), "the other client shows the walk where the mover ended");
            Assert.That(mover.Match.Corrections, Is.LessThanOrEqualTo(2), "the prediction runs the server's movement on the same map");
            Assert.That(watcher.Client.UnresolvedSnapshots, Is.Zero);

            _stop.Raise();
            Pump(() => mover.Notice != null && watcher.Notice != null);

            Assert.That(mover.Notice.Kind, Is.EqualTo(DisconnectKind.ServerStopped));
            Assert.That(watcher.Notice.Kind, Is.EqualTo(DisconnectKind.ServerStopped));
            Assert.That(mover.Notice.WasConnected, Is.True);
            Assert.That(_serverThread.Join(_timeout), Is.True);
            Assert.That(_exitCode, Is.EqualTo(0), _output.ToString());
        }

        [Test]
        public void EosClientsPlayOnAnEosServerAndTheJsonEventsCarryTheirProductUserIds()
        {
            var network = new FakeEosNetwork();
            StartServer(network);
            Assert.That(_output.ToString(), Does.Contain("[Net] EOS server ready id=" + _eosServerId));

            var first = Join(EosClient(network, _eosFirstId), _eosServerId.ToUpperInvariant());
            var second = Join(EosClient(network, _eosSecondId), _eosServerId);
            Pump(() => first.Match.IsPredicting && second.Match.IsPredicting
                && HasRemote(first, second.Client.PlayerId) && HasRemote(second, first.Client.PlayerId));

            var log = _output.ToString();
            Assert.That(log, Does.Contain("\"type\":\"server_started\"").And.Contain("\"transport\":\"eos\""));
            Assert.That(log, Does.Contain("\"type\":\"player_joined\"").And.Contain("\"productUserId\":\"" + _eosFirstId + "\""));
            Assert.That(log, Does.Contain("\"productUserId\":\"" + _eosSecondId + "\""));

            first.Client.Leave();
            Pump(() => _output.ToString().Contains("\"type\":\"player_left\"", StringComparison.Ordinal) && !HasRemote(second, first.Client.PlayerId));
            Assert.That(PlayerLeftLine(), Does.Contain("\"productUserId\":\"" + _eosFirstId + "\""));

            _stop.Raise();
            Pump(() => second.Notice != null);
            Assert.That(second.Notice.Kind, Is.EqualTo(DisconnectKind.ServerStopped));
            Assert.That(_serverThread.Join(_timeout), Is.True);
            Assert.That(_exitCode, Is.EqualTo(0), _output.ToString());
        }

        [Test]
        public void ThePredictionUsesTheMotorValuesOfTheServersRulesAndLiesDownWithTheServer()
        {
            // 既定と違う移動の値とルールにする。クライアントが自分の既定値で予測していれば、歩くたびに巻き戻される
            var rulesPath = Path.Combine(_root, "server-data", "rules", ServerLaunch.DefaultRules + ".json");
            var text = File.ReadAllText(rulesPath);
            text = Replace(text, "\"walkSpeed\": 5", "\"walkSpeed\": 7.5");
            text = Replace(text, "\"radius\": 0.5", "\"radius\": 0.4");
            text = Replace(text, "\"standingHeight\": 2", "\"standingHeight\": 1.8");
            text = Replace(text, "\"proneSpeed\": 1.2", "\"proneSpeed\": 2");
            text = Replace(text, "\"prone\": \"Off\"", "\"prone\": \"On\"");
            File.WriteAllText(rulesPath, text);

            StartServer();
            var mover = Join();
            Pump(() => mover.Match.IsPredicting);

            Assert.That(mover.Client.Motor.Motor.WalkSpeed, Is.EqualTo(7.5f));
            Assert.That(mover.Client.Motor.Stance.Radius, Is.EqualTo(0.4f));
            Assert.That(mover.Client.Motor.Stance.Standing.Height, Is.EqualTo(1.8f));
            Assert.That(mover.Client.Motor.Prone.Speed, Is.EqualTo(2f));
            Assert.That(mover.Client.LatestSnapshot.Match.Rules.Prone, Is.EqualTo(Prone.On));

            var start = mover.Match.LocalState.Position;
            var elapsed = Stopwatch.StartNew();
            Pump(() =>
            {
                mover.Move = new Vector2(0f, 1f);
                return elapsed.Elapsed > TimeSpan.FromSeconds(1.5);
            });
            mover.Move = Vector2.Zero;
            Pump(() => mover.Match.Prediction.PendingFrames < 4);
            Assert.That(Horizontal(mover.Match.LocalState.Position - start), Is.GreaterThan(7.5f), "walking at the rules' speed");

            mover.Buttons = InputButtons.Prone;
            Pump(() => mover.Match.LocalState.IsProne);
            mover.Buttons = InputButtons.None;
            Pump(() => ServerRow(mover).IsProne && mover.Match.Prediction.PendingFrames < 4);

            var crawlStart = mover.Match.LocalState.Position;
            elapsed.Restart();
            Pump(() =>
            {
                mover.Move = new Vector2(0f, 1f);
                return elapsed.Elapsed > TimeSpan.FromSeconds(1);
            });
            mover.Move = Vector2.Zero;
            Pump(() => mover.Match.Prediction.PendingFrames < 4
                && Horizontal(ServerRow(mover).Position - mover.Match.LocalState.Position) < 0.01f);

            Assert.That(mover.Match.LocalState.IsProne, Is.True);
            Assert.That(Horizontal(mover.Match.LocalState.Position - crawlStart), Is.InRange(1f, 3f), "crawling at the rules' prone speed");
            Assert.That(mover.Match.Corrections, Is.LessThanOrEqualTo(2), "the prediction moves with the server's values");
        }

        [Test]
        public void AClientOfAnotherVersionIsToldTheVersionsDiffer()
        {
            StartServer();
            var player = new Player(new MatchClient(new UdpMatchTransport(), "0.1.0", RejoinKey.NewDeviceKey(), "old", _ => { }));
            _players.Add(player);
            player.Client.Connect("127.0.0.1", _port);
            Pump(() => player.Notice != null);

            Assert.That(player.Notice.Kind, Is.EqualTo(DisconnectKind.VersionMismatch));
            Assert.That(player.Notice.ServerVersion, Is.EqualTo("0.2.0"));
        }

        private static string Replace(string text, string from, string to)
        {
            Assert.That(text, Does.Contain(from));
            return text.Replace(from, to, StringComparison.Ordinal);
        }

        private static PlayerSnapshot ServerRow(Player player)
        {
            var latest = player.Client.LatestSnapshot;
            var own = latest.Players.IndexOf(player.Client.PlayerId);
            Assert.That(own, Is.GreaterThanOrEqualTo(0));
            return latest.Players[own];
        }

        private static bool HasRemote(Player viewer, int playerId)
        {
            return viewer.Match.TrySampleRemote(playerId, out _);
        }

        private static float Moved(Player viewer, int playerId, Vector3 from)
        {
            return viewer.Match.TrySampleRemote(playerId, out var pose) ? Horizontal(pose.Position - from) : 0f;
        }

        private static bool Settled(Player viewer, Player mover)
        {
            return viewer.Match.TrySampleRemote(mover.Client.PlayerId, out var pose)
                && Horizontal(pose.Position - mover.Match.LocalState.Position) < 0.05f;
        }

        private static float Horizontal(Vector3 v)
        {
            return new Vector2(v.X, v.Z).Length();
        }

        private static EosMatchTransport EosClient(FakeEosNetwork network, string userId)
        {
            return new EosMatchTransport(network.Join(userId), new EosTransportOptions(), _ => { });
        }

        private string PlayerLeftLine()
        {
            foreach (var line in _output.ToString().Split('\n'))
            {
                if (line.Contains("\"type\":\"player_left\"", StringComparison.Ordinal))
                {
                    return line;
                }
            }

            return "";
        }

        /// <param name="eos">与えれば、その偽の EOS のネットワークで <c>-transport eos -log-format json</c> のサーバーにする。</param>
        private void StartServer(FakeEosNetwork eos = null)
        {
            var environment = new Dictionary<string, string>
            {
                [ServerLaunch.AddressVariable] = "127.0.0.1",
                [ServerLaunch.PortVariable] = _port.ToString(CultureInfo.InvariantCulture),
            };
            ServerRuntime.EosTransportFactory createEos = null;
            var args = new List<string> { "-mode", "server", "-buildVersion", "0.2.0", "-drain-timeout", "0" };
            if (eos != null)
            {
                foreach (var name in EosCredentials.VariableNames)
                {
                    environment[name] = "test";
                }

                args.Add("-transport");
                args.Add("eos");
                args.Add("-log-format");
                args.Add("json");
                createEos = (EosCredentials credentials, string version, TextWriter log, out IMatchTransport transport, out string error) =>
                {
                    transport = new EosMatchTransport(eos.Join(_eosServerId), new EosTransportOptions(), log.WriteLine);
                    error = null;
                    return true;
                };
            }

            var runtime = new ServerRuntime(
                _synchronizedOutput,
                _synchronizedOutput,
                name => environment.TryGetValue(name, out var value) ? value : null,
                _stop,
                new[] { _root },
                new[] { _root },
                () => new UdpMatchTransport(),
                createEos);
            _serverThread = new Thread(() => _exitCode = ServerProgram.Run(args, ServerModules.Create, runtime)) { IsBackground = true };
            _serverThread.Start();
            Pump(() => _output.ToString().Contains("[Net] server listening on", StringComparison.Ordinal));
        }

        private Player Join(IMatchTransport transport = null, string address = "127.0.0.1")
        {
            var client = new MatchClient(transport ?? new UdpMatchTransport(), "0.2.0", RejoinKey.NewDeviceKey(), "tester", _ => { });
            var player = new Player(client);
            _players.Add(player);
            client.Connect(address, _port);
            Pump(() => client.State == MatchClientState.Connected);
            Assert.That(client.Map, Is.EqualTo(ServerLaunch.DefaultMap));
            player.Match = new ClientMatch(client, Simulation(client));
            return player;
        }

        private CharacterMotorSimulation Simulation(MatchClient client)
        {
            Assert.That(
                ClientWorld.TryLoad(Path.Combine(_root, "server-data", "maps"), client.Map, out var world, out var error), Is.True, error);
            return world.CreateSimulation(client.Motor, client.TickRate, client.KillPlaneY, client.Knockback);
        }

        private void Pump(Func<bool> until)
        {
            var watch = Stopwatch.StartNew();
            var frame = Stopwatch.StartNew();
            do
            {
                var delta = (float)frame.Elapsed.TotalSeconds;
                frame.Restart();
                foreach (var player in _players)
                {
                    player.Update(delta);
                }

                if (until())
                {
                    return;
                }

                Thread.Sleep(5);
            }
            while (watch.Elapsed < _timeout);

            Assert.Fail("timed out waiting for the server and the clients\n" + _output);
        }

        internal static ushort FreePort()
        {
            using (var probe = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0)))
            {
                return (ushort)((IPEndPoint)probe.Client.LocalEndPoint).Port;
            }
        }

        /// <summary>
        /// コミットした server-data の写し。対戦マップの NavMesh (.fpsnav) の書き出し (#584) がまだ無いうちもサーバーを起動できるよう、
        /// 無ければ 1 枚の三角形の NavMesh を置く (クライアントは NavMesh を使わない)。
        /// </summary>
        internal static string ServerDataCopy()
        {
            var source = Path.Combine(RepositoryRoot(), "server-data");
            var root = Path.Combine(Path.GetTempPath(), "fps-netv2-" + Guid.NewGuid().ToString("N"));
            foreach (var file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
            {
                var target = Path.Combine(root, "server-data", Path.GetRelativePath(source, file));
                Directory.CreateDirectory(Path.GetDirectoryName(target));
                File.Copy(file, target);
            }

            var navigation = Path.Combine(root, "server-data", "maps", ServerLaunch.DefaultMap + NavMeshFile.Extension);
            if (!File.Exists(navigation))
            {
                using (var stream = File.Create(navigation))
                {
                    new NavMeshFile(
                            new[] { new Vector3(0f, 0f, 0f), new Vector3(0f, 0f, 1f), new Vector3(1f, 0f, 0f) },
                            new[] { new NavTriangle(0, 1, 2, 0) })
                        .Write(stream);
                }
            }

            return root;
        }

        private static string RepositoryRoot()
        {
            var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
            while (directory != null && !Directory.Exists(Path.Combine(directory.FullName, "server-data")))
            {
                directory = directory.Parent;
            }

            Assert.That(directory, Is.Not.Null, "no server-data folder above " + TestContext.CurrentContext.TestDirectory);
            return directory.FullName;
        }

        private sealed class Player
        {
            public Player(MatchClient client)
            {
                Client = client;
                client.Disconnected += notice => Notice = notice;
            }

            public MatchClient Client { get; }

            public ClientMatch Match { get; set; }

            public Vector2 Move { get; set; }

            /// <summary>次の tick に押すボタン。押しっぱなしにせず、1 tick 送ったら消す。</summary>
            public InputButtons Buttons { get; set; }

            public DisconnectNotice Notice { get; private set; }

            public void Update(float deltaSeconds)
            {
                Client.Poll();
                if (Match == null || Client.State != MatchClientState.Connected)
                {
                    return;
                }

                var ticks = Match.Advance(deltaSeconds);
                for (var i = 0; i < ticks; i++)
                {
                    Match.Step(Move, 0f, Buttons);
                    Buttons = InputButtons.None;
                }

                Match.SendInput();
            }
        }

        internal sealed class StopSignal : IStopSignal
        {
            private Action _onStop;

            public IDisposable Subscribe(Action onStop)
            {
                _onStop = onStop;
                return new Subscription(this);
            }

            public void Raise()
            {
                _onStop?.Invoke();
            }

            private sealed class Subscription : IDisposable
            {
                private readonly StopSignal _signal;

                public Subscription(StopSignal signal)
                {
                    _signal = signal;
                }

                public void Dispose()
                {
                    _signal._onStop = null;
                }
            }
        }
    }
}
