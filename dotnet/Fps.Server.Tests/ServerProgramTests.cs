using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Sockets;
using Fps.EosSdk;
using Fps.Protocol;
using NUnit.Framework;

namespace Fps.Server.Tests
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

        [SetUp]
        public void SetUp()
        {
            _output = new StringWriter();
            _error = new StringWriter();
            _data = new TempDirectory();
            TestMaps.Write(_data.Path, "NetTest");
            _stop = new ImmediateStopSignal();
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
        public void WithoutRegisterLobbyOnlyMovementAndSnapshotsAreAdded()
        {
            Assert.That(RunWithServerModules(), Is.EqualTo(ServerProgram.ExitOk));
            Assert.That(_output.ToString(), Does.Contain("[ServerHost] started with 2 module(s)"));
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
                () => new UdpMatchTransport());
        }

        private static string FirstLine(StringWriter writer)
        {
            return writer.ToString().Split('\n')[0].TrimEnd('\r');
        }
    }
}
