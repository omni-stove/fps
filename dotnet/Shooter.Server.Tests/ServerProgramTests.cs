using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Sockets;
using NUnit.Framework;
using Shooter.Core;
using Shooter.EosSdk;
using Shooter.Protocol;

namespace Shooter.Server.Tests
{
    [NonParallelizable]
    public sealed class ServerProgramTests
    {
        private StringWriter _output;
        private StringWriter _error;
        private TempDirectory _data;
        private IStopSignal _stop;
        private Dictionary<string, string> _environment;
        private ushort _port;
        private ServerRuntime.EosTransportFactory _eosTransport;
        private string _builtVersion;

        [SetUp]
        public void SetUp()
        {
            _output = new StringWriter();
            _error = new StringWriter();
            _data = new TempDirectory();
            TestMaps.Write(_data.Path, "NetTest");
            TestRules.Write(_data.Path, ServerLaunch.DefaultRules);
            _stop = new ImmediateStopSignal();
            _eosTransport = null;
            _builtVersion = null;
            _port = TestClient.FreePort();
            _environment = new Dictionary<string, string>
            {
                [ServerLaunch.PortVariable] = _port.ToString(CultureInfo.InvariantCulture),
            };
        }

        [TearDown]
        public void TearDown()
        {
            _output.Dispose();
            _error.Dispose();
            _data.Dispose();
        }

        [Test]
        public void ListensOnEveryAddressAndStopsWithExitZeroWhenNobodyIsConnected()
        {
            var code = Run("-mode", "server");

            Assert.That(code, Is.EqualTo(ServerProgram.ExitOk), _error.ToString());
            var log = _output.ToString();
            Assert.That(log, Does.Contain("[Net] server listening on 0.0.0.0:" + _port));
            Assert.That(log, Does.Contain("[Server] map NetTest colliders=1"));
            Assert.That(log, Does.Contain("[Net] stop requested; stopping now because no players are connected"));
            Assert.That(log, Does.Contain("[Net] server stopping; told 0 client(s) and disconnected them"));
            Assert.That(_error.ToString(), Is.Empty);
        }

        [Test]
        public void PrintsTheVersionFirstAndWhetherTheEosSdkIsIncluded()
        {
            Run("-buildVersion", "v1.2.3");

            Assert.That(FirstLine(_output), Is.EqualTo("[Version] 1.2.3"));
            Assert.That(_output.ToString(), Does.Contain(EosSdkBuild.LogLine(AppContext.BaseDirectory)));
        }

        [Test]
        public void PrintsTheBuiltVersionWithoutTheArgument()
        {
            _builtVersion = "v2.0.0";

            Run();

            Assert.That(FirstLine(_output), Is.EqualTo("[Version] 2.0.0"));
        }

        [Test]
        public void WithoutVersionPrintsUnknown()
        {
            Run();

            Assert.That(FirstLine(_output), Is.EqualTo("[Version] unknown"));
        }

        [Test]
        public void JsonFormatWritesServerStartedAndStopping()
        {
            Assert.That(Run("-log-format", "json", "-buildVersion", "0.2.0"), Is.EqualTo(ServerProgram.ExitOk));

            Assert.That(_output.ToString(), Does.Contain(
                "{\"type\":\"server_started\",\"time\":\"").And.Contain(
                "\",\"version\":\"0.2.0\",\"transport\":\"udp\",\"endpoint\":\"0.0.0.0:" + _port + "\"}"));
            Assert.That(_output.ToString(), Does.Contain("{\"type\":\"server_stopping\",\"time\":\""));
        }

        [Test]
        public void TextFormatWritesNoJson()
        {
            Run();

            Assert.That(_output.ToString(), Does.Not.Contain("\"type\""));
        }

        [TestCase("host")]
        [TestCase("client")]
        public void OtherModesAreRejected(string mode)
        {
            var code = Run("-mode", mode);

            Assert.That(code, Is.EqualTo(ServerProgram.ExitUsage));
            Assert.That(_error.ToString(), Does.StartWith("[Net] invalid arguments: ").And.Contain("-mode must be server"));
            Assert.That(_output.ToString(), Is.Empty);
        }

        [Test]
        public void BadArgumentsAreRejected()
        {
            Assert.That(Run("-port", "0"), Is.EqualTo(ServerProgram.ExitUsage));
            Assert.That(Run("-buildVersion", "+bad"), Is.EqualTo(ServerProgram.ExitUsage));
        }

        [Test]
        public void MissingMapExitsMisconfigured()
        {
            var code = Run("-map", "NetArena01");

            Assert.That(code, Is.EqualTo(ServerProgram.ExitMisconfigured));
            Assert.That(_error.ToString(), Does.StartWith("[Server] map NetArena01 has no collision: NetArena01.fpsmap"));
            Assert.That(_output.ToString(), Does.Not.Contain("listening"));
        }

        [Test]
        public void DominationRulesStartTheControlPointsOfTheMap()
        {
            TestMaps.Write(_data.Path, "NetArena01", points: TestRules.Points(3));
            TestRules.Write(_data.Path, "Domination", TestRules.File(Shooter.Core.GameRules.Default.WithObjective(Shooter.Core.MatchObjective.Domination), name: "Domination"));

            var code = ServerProgram.Run(Args("-rules", "Domination", "-map", "NetArena01"), new IServerModule[] { new MatchRulesModule() }, Runtime());

            Assert.That(code, Is.EqualTo(ServerProgram.ExitOk), _error.ToString());
            Assert.That(_output.ToString(), Does.Contain("[Server] rules Domination objective=Domination"));
            Assert.That(_output.ToString(), Does.Contain("[Domination] net points=3 scoreLimit=100 captureSeconds=8 pointsPerSecond=1 maxCapturers=3"));
        }

        [Test]
        public void TwoCandidateMapsAreBothLoadedAtStart()
        {
            TestMaps.Write(_data.Path, "NetArena02");
            WritePool("NetTest", "NetArena02");

            var code = Run();

            Assert.That(code, Is.EqualTo(ServerProgram.ExitOk), _error.ToString());
            var log = _output.ToString();
            Assert.That(log, Does.Contain("[Server] map NetTest colliders=1"));
            Assert.That(log, Does.Contain("[Server] map NetArena02 colliders=1"));
            Assert.That(log, Does.Contain("[Server] map pool loaded 2 maps: NetTest, NetArena02"));
            Assert.That(log, Does.Contain("[Net] server listening on"));
        }

        [Test]
        public void WithoutAMapArgumentTheFirstCandidateIsPlayed()
        {
            TestMaps.Write(_data.Path, "NetArena02");
            WritePool("NetArena02", "NetTest");

            Assert.That(Run(), Is.EqualTo(ServerProgram.ExitOk), _error.ToString());

            Assert.That(_output.ToString(), Does.Contain("[Server] map pool loaded 2 maps: NetArena02, NetTest"));
        }

        [Test]
        public void AnEmptyPoolLoadsOnlyOneMap()
        {
            Assert.That(Run(), Is.EqualTo(ServerProgram.ExitOk), _error.ToString());

            Assert.That(_output.ToString(), Does.Not.Contain("map pool"));
        }

        [Test]
        public void ACandidateThatCannotBeReadStopsTheStart()
        {
            WritePool("NetTest", "NetArena09");

            var code = Run();

            Assert.That(code, Is.EqualTo(ServerProgram.ExitMisconfigured));
            Assert.That(_error.ToString(), Does.StartWith("[Server] map pool: map NetArena09 has no collision: NetArena09.fpsmap"));
            Assert.That(_output.ToString(), Does.Not.Contain("listening"));
        }

        [Test]
        public void TheRulesModuleRefusesACandidateWithoutSpawnPoints()
        {
            TestMaps.Write(_data.Path, "NetTest", points: TestRules.Points());
            TestMaps.Write(_data.Path, "NetArena02");
            WritePool("NetTest", "NetArena02");

            var code = ServerProgram.Run(System.Array.Empty<string>(), new IServerModule[] { new MatchRulesModule() }, Runtime());

            Assert.That(code, Is.EqualTo(ServerProgram.ExitFailure));
            Assert.That(_error.ToString(), Does.Contain("map NetArena02 has no spawn points: NetArena02.points.json"));
        }

        [Test]
        public void MissingRulesExitMisconfigured()
        {
            var code = Run("-rules", "Domination");

            Assert.That(code, Is.EqualTo(ServerProgram.ExitMisconfigured));
            Assert.That(_error.ToString(), Does.StartWith("[Server] rules Domination not found"));
        }

        [Test]
        public void TheRulesModuleNeedsTheMapsPoints()
        {
            var code = ServerProgram.Run(System.Array.Empty<string>(), new IServerModule[] { new MatchRulesModule() }, Runtime());

            Assert.That(code, Is.EqualTo(ServerProgram.ExitFailure));
            Assert.That(_error.ToString(), Does.Contain("map NetTest has no spawn points: NetTest.points.json"));
        }

        [Test]
        public void PortInUseExitsStartFailed()
        {
            using (var taken = new UdpClient(new IPEndPoint(IPAddress.Any, _port)))
            {
                Assert.That(Run(), Is.EqualTo(ServerProgram.ExitStartFailed));
            }

            Assert.That(_error.ToString(), Does.StartWith("[Net] failed to start the server: "));
        }

        [Test]
        public void ModulesAreStartedAndStopped()
        {
            var calls = new List<string>();

            var code = Run(new[] { new RecordingModule("a", calls) });

            Assert.That(code, Is.EqualTo(ServerProgram.ExitOk));
            Assert.That(calls[0], Is.EqualTo("a.Start"));
            Assert.That(calls[^1], Is.EqualTo("a.Stop"));
        }

        [Test]
        public void AModuleThatFailsTheServerSetsTheExitCode()
        {
            var calls = new List<string>();
            _stop = new NeverStopSignal();

            var code = Run(new[] { new RecordingModule("a", calls) { FailOnTickWith = ServerProgram.ExitLobbyFailed } });

            Assert.That(code, Is.EqualTo(ServerProgram.ExitLobbyFailed));
            Assert.That(_output.ToString(), Does.Contain("a cannot go on; stopping with exit code 7"));
            Assert.That(calls[^1], Is.EqualTo("a.Stop"));
        }

        [Test]
        public void RegisterLobbyWithoutEosCredentialsExitsMisconfigured()
        {
            var code = RunWithServerModules("-register-lobby", "203.0.113.10:7777");

            Assert.That(code, Is.EqualTo(ServerProgram.ExitMisconfigured));
            Assert.That(_error.ToString(), Does.StartWith(
                "[Lobby] -register-lobby needs EOS credentials; missing EOS_PRODUCT_ID, EOS_SANDBOX_ID, EOS_DEPLOYMENT_ID, EOS_CLIENT_ID, EOS_CLIENT_SECRET"));
            Assert.That(_output.ToString(), Does.Not.Contain("listening"));
        }

        [Test]
        public void WithoutRegisterLobbyOnlyTheMatchModulesAreAdded()
        {
            TestMaps.Write(_data.Path, "NetTest", points: TestRules.Points());

            Assert.That(RunWithServerModules(), Is.EqualTo(ServerProgram.ExitOk), _error.ToString());
            Assert.That(_output.ToString(), Does.Contain("[ServerHost] started with 8 module(s)"));
            Assert.That(_output.ToString(), Does.Contain("[AntiCheat] disabled on the dedicated server by default"));
            Assert.That(_output.ToString(), Does.Contain("[AntiCheat] session not started (disabled)"));
        }

        [Test]
        public void EacWithoutTheEosTransportIsAUsageError()
        {
            var code = RunWithServerModules("-eac");

            Assert.That(code, Is.EqualTo(ServerProgram.ExitUsage));
            Assert.That(_error.ToString(), Does.Contain("-eac needs -transport eos"));
        }

        [Test]
        public void FailingModuleExitsOne()
        {
            var calls = new List<string>();

            var startCode = Run(new[] { new RecordingModule("a", calls) { ThrowOnStart = true } });
            var stopCode = Run(new[] { new RecordingModule("b", calls) { ThrowOnStop = true } });

            Assert.That(startCode, Is.EqualTo(ServerProgram.ExitFailure));
            Assert.That(stopCode, Is.EqualTo(ServerProgram.ExitFailure));
            Assert.That(_error.ToString(), Does.Contain("failed to start: a cannot start"));
            Assert.That(_error.ToString(), Does.Contain("failed to stop: b cannot stop"));
        }

        [Test]
        public void EosWithoutCredentialsSaysSoAndListensOverUdp()
        {
            var code = Run("-transport", "eos", "-log-format", "json");

            Assert.That(code, Is.EqualTo(ServerProgram.ExitOk), _error.ToString());
            var log = _output.ToString();
            Assert.That(log, Does.Contain(NetTransportPlan.CredentialsMissingLog + Environment.NewLine));
            Assert.That(log, Does.Contain("[Net] missing EOS settings: EOS_PRODUCT_ID"));
            Assert.That(log, Does.Contain("[Net] server listening on 0.0.0.0:" + _port));
            Assert.That(log, Does.Contain("\"transport\":\"udp\""));
        }

        [Test]
        public void EosWithAntiCheatButWithoutCredentialsDoesNotFallBackToUdp()
        {
            Assert.That(Run("-transport", "eos", "-eac"), Is.EqualTo(ServerProgram.ExitMisconfigured));
            Assert.That(_error.ToString(), Does.Contain("[AntiCheat] -eac needs EOS credentials; missing EOS_PRODUCT_ID"));
            Assert.That(_output.ToString(), Does.Not.Contain("listening"));
        }

        [Test]
        public void EosWithCredentialsListensOverTheEosTransport()
        {
            AddCredentials();
            EosCredentials given = null;
            var transport = new RecordingTransport();
            _eosTransport = (EosCredentials credentials, string version, TextWriter log, out IMatchTransport created, out string error) =>
            {
                given = credentials;
                created = transport;
                error = null;
                return true;
            };

            var code = Run("-transport", "eos", "-log-format", "json");

            Assert.That(code, Is.EqualTo(ServerProgram.ExitOk), _error.ToString());
            Assert.That(given.ProductId, Is.EqualTo("product"));
            Assert.That(_output.ToString(), Does.Contain("[Net] server listening on eos"));
            Assert.That(_output.ToString(), Does.Contain("\"transport\":\"eos\",\"endpoint\":\"eos\""));
            Assert.That(_output.ToString(), Does.Not.Contain(NetTransportPlan.CredentialsMissingLog));
        }

        [Test]
        public void EosThatCannotBeUsedExitsMisconfigured()
        {
            AddCredentials();
            _eosTransport = (EosCredentials credentials, string version, TextWriter log, out IMatchTransport created, out string error) =>
            {
                created = null;
                error = "the EOS C# SDK is not in this build";
                return false;
            };

            Assert.That(Run("-transport", "eos"), Is.EqualTo(ServerProgram.ExitMisconfigured));
            Assert.That(_error.ToString(), Does.Contain("[Net] -transport eos cannot use EOS: the EOS C# SDK is not in this build"));
            Assert.That(_output.ToString(), Does.Not.Contain("listening"));
        }

        [Test]
        public void EosLoginFailureExitsAsAFailedStart()
        {
            AddCredentials();
            _eosTransport = (EosCredentials credentials, string version, TextWriter log, out IMatchTransport created, out string error) =>
            {
                created = new FailingListenTransport();
                error = null;
                return true;
            };

            Assert.That(Run("-transport", "eos"), Is.EqualTo(ServerProgram.ExitStartFailed));
            Assert.That(_error.ToString(), Does.Contain("[Net] failed to start the server: EOS connect login failed"));
        }

        [Test]
        public void BrokenCredentialsFileExitsMisconfigured()
        {
            File.WriteAllText(Path.Combine(_data.Path, EosCredentials.LocalFileName), "not json");

            Assert.That(Run("-transport", "eos"), Is.EqualTo(ServerProgram.ExitMisconfigured));
            Assert.That(_error.ToString(), Does.Contain("[Net] cannot load EOS credentials: "));
        }

        [Test]
        public void WithoutPrivateTheServerSaysItIsPublic()
        {
            Assert.That(Run(), Is.EqualTo(ServerProgram.ExitOk), _error.ToString());

            Assert.That(_output.ToString(), Does.Contain("[Server] public match"));
            Assert.That(_output.ToString(), Does.Not.Contain("join_code="));
        }

        [Test]
        public void PrivateWithoutACodeLogsTheGeneratedCode()
        {
            Assert.That(Run("--private"), Is.EqualTo(ServerProgram.ExitOk), _error.ToString());

            Assert.That(
                _output.ToString(),
                Does.Match(@"\[Server\] private match join_code=[" + JoinCode.Alphabet + "]{" + JoinCode.DefaultLength
                    + @"} \(generated; pass --join-code to choose one\)"));
        }

        [Test]
        public void TheJoinCodeArgumentWinsOverTheRulesFile()
        {
            TestRules.Write(
                _data.Path, ServerLaunch.DefaultRules, TestRules.File(settings: new NetMatchSettings(privateMatch: true, joinCode: "FROMRULES")));

            Assert.That(Run("--join-code", "friends"), Is.EqualTo(ServerProgram.ExitOk), _error.ToString());

            Assert.That(_output.ToString(), Does.Contain("[Server] private match join_code=FRIENDS" + Environment.NewLine));
            Assert.That(_output.ToString(), Does.Not.Contain("FROMRULES"));
        }

        [Test]
        public void TheRulesFileAloneMakesThePrivateMatch()
        {
            TestRules.Write(
                _data.Path, ServerLaunch.DefaultRules, TestRules.File(settings: new NetMatchSettings(privateMatch: true, joinCode: "FROMRULES")));

            Assert.That(Run(), Is.EqualTo(ServerProgram.ExitOk), _error.ToString());

            Assert.That(_output.ToString(), Does.Contain("[Server] private match join_code=FROMRULES" + Environment.NewLine));
        }

        [Test]
        public void ABadJoinCodeIsAUsageError()
        {
            Assert.That(Run("--join-code", "no/slash"), Is.EqualTo(ServerProgram.ExitUsage));

            Assert.That(_error.ToString(), Does.StartWith("[Net] invalid arguments: --join-code must be"));
        }

        private void AddCredentials()
        {
            _environment[EosCredentials.ProductIdVariable] = "product";
            _environment[EosCredentials.SandboxIdVariable] = "sandbox";
            _environment[EosCredentials.DeploymentIdVariable] = "deployment";
            _environment[EosCredentials.ClientIdVariable] = "client";
            _environment[EosCredentials.ClientSecretVariable] = "secret";
        }

        private void WritePool(params string[] mapPool)
        {
            TestRules.Write(_data.Path, ServerLaunch.DefaultRules, TestRules.File(settings: new NetMatchSettings(mapPool: mapPool)));
        }

        private int Run(params string[] args)
        {
            return ServerProgram.Run(args, Array.Empty<IServerModule>(), Runtime());
        }

        private int RunWithServerModules(params string[] args)
        {
            return ServerProgram.Run(args, ServerModules.Create, Runtime());
        }

        private int Run(IServerModule[] modules)
        {
            return ServerProgram.Run(Array.Empty<string>(), modules, Runtime());
        }

        private ServerRuntime Runtime()
        {
            return new ServerRuntime(
                _output,
                _error,
                name => _environment.TryGetValue(name, out var value) ? value : null,
                _stop,
                new[] { _data.Path },
                new[] { _data.Path },
                () => new UdpMatchTransport(),
                _eosTransport,
                _builtVersion);
        }

        private sealed class FailingListenTransport : IMatchTransport
        {
            public bool IsActive => false;

            public void Listen(string address, ushort port) => throw new InvalidOperationException("EOS connect login failed: InvalidAuth");

            public void Connect(string address, ushort port) => throw new InvalidOperationException();

            public void Disconnect(ConnectionId connection, string reason)
            {
            }

            public void Send(ConnectionId connection, TransportChannel channel, ArraySegment<byte> payload)
            {
            }

            public int GetMaxPayloadSize(TransportChannel channel) => 1000;

            public bool TryGetRoundTripTime(ConnectionId connection, out int milliseconds)
            {
                milliseconds = 0;
                return false;
            }

            public bool TryReceive(out TransportEvent transportEvent)
            {
                transportEvent = default;
                return false;
            }

            public void Shutdown(string reason)
            {
            }

            public void Dispose()
            {
            }
        }

        // 定数配列を直接渡すと CA1861 になるので params で受ける
        private static string[] Args(params string[] args) => args;

        private static string FirstLine(StringWriter writer)
        {
            return writer.ToString().Split('\n')[0].TrimEnd('\r');
        }
    }
}
