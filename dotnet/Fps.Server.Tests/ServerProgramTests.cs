using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;

namespace Fps.Server.Tests
{
    public sealed class ServerProgramTests
    {
        private StringWriter _output;
        private StringWriter _error;

        [SetUp]
        public void SetUp()
        {
            _output = new StringWriter();
            _error = new StringWriter();
        }

        [TearDown]
        public void TearDown()
        {
            _output.Dispose();
            _error.Dispose();
        }

        [Test]
        public void PrintsTheVersionAndExitsZero()
        {
            var code = Run("-mode", "server", "-buildVersion", "v1.2.3");

            Assert.That(code, Is.EqualTo(ServerProgram.ExitOk));
            Assert.That(FirstLine(_output), Is.EqualTo("[Version] 1.2.3"));
            Assert.That(_error.ToString(), Is.Empty);
        }

        [Test]
        public void WithoutVersionPrintsUnknown()
        {
            var code = Run("-mode", "server");

            Assert.That(code, Is.EqualTo(ServerProgram.ExitOk));
            Assert.That(FirstLine(_output), Is.EqualTo("[Version] unknown"));
        }

        [Test]
        public void ModeDefaultsToServer()
        {
            Assert.That(Run(Array.Empty<string>()), Is.EqualTo(ServerProgram.ExitOk));
            Assert.That(_output.ToString(), Does.Contain("[Server] server "));
        }

        [TestCase("host")]
        [TestCase("client")]
        public void OtherModesAreRejected(string mode)
        {
            var code = Run("-mode", mode);

            Assert.That(code, Is.EqualTo(ServerProgram.ExitUsage));
            Assert.That(_error.ToString(), Does.Contain("-mode must be server"));
            Assert.That(_output.ToString(), Is.Empty);
        }

        [Test]
        public void BadArgumentsAreRejected()
        {
            Assert.That(Run("-mode", "server", "-port", "0"), Is.EqualTo(ServerProgram.ExitUsage));
            Assert.That(Run("-mode", "server", "-buildVersion", "+bad"), Is.EqualTo(ServerProgram.ExitUsage));
        }

        [Test]
        public void ModulesAreStartedAndStopped()
        {
            var calls = new List<string>();

            var code = ServerProgram.Run(
                Args("-mode", "server"), new[] { new RecordingModule("a", calls) }, _output, _error);

            Assert.That(code, Is.EqualTo(ServerProgram.ExitOk));
            Assert.That(calls, Is.EqualTo(Calls("a.Start", "a.Stop")));
        }

        [Test]
        public void FailingModuleExitsOne()
        {
            var calls = new List<string>();

            var startCode = ServerProgram.Run(
                Args("-mode", "server"), new[] { new RecordingModule("a", calls) { ThrowOnStart = true } }, _output, _error);
            var stopCode = ServerProgram.Run(
                Args("-mode", "server"), new[] { new RecordingModule("b", calls) { ThrowOnStop = true } }, _output, _error);

            Assert.That(startCode, Is.EqualTo(ServerProgram.ExitFailure));
            Assert.That(stopCode, Is.EqualTo(ServerProgram.ExitFailure));
            Assert.That(_error.ToString(), Does.Contain("failed to start: a cannot start"));
            Assert.That(_error.ToString(), Does.Contain("failed to stop: b cannot stop"));
        }

        private int Run(params string[] args)
        {
            return ServerProgram.Run(args, Array.Empty<IServerModule>(), _output, _error);
        }

        // 定数配列を直接渡すと CA1861 になるので params で受ける
        private static string[] Args(params string[] args) => args;

        private static string[] Calls(params string[] calls) => calls;

        private static string FirstLine(StringWriter writer)
        {
            return writer.ToString().Split('\n')[0].TrimEnd('\r');
        }
    }
}
