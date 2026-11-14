using System;
using System.Collections.Generic;
using System.IO;
using Fps.Core;
using Fps.EosSdk;
using NUnit.Framework;

namespace Fps.Server.Tests
{
    // SDK を置いた手元でも置いていない CI でも同じテストが通るよう、どちらのビルドかで期待を分ける
    public sealed class ServerModulesTests
    {
        private TempDirectory _roots;
        private TempDirectory _library;
        private Dictionary<string, string> _environment;
        private List<IServerModule> _modules;

        [SetUp]
        public void SetUp()
        {
            _roots = new TempDirectory();
            _library = new TempDirectory();
            _environment = new Dictionary<string, string>();
            _modules = new List<IServerModule>();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var module in _modules)
            {
                (module as IDisposable)?.Dispose();
            }

            _roots.Dispose();
            _library.Dispose();
        }

        [Test]
        public void WithoutRegisterLobbyAddsNothing()
        {
            Assert.That(Create(_library.Path), Is.Null);
            Assert.That(_modules, Is.Empty);
        }

        [Test]
        public void MissingCredentialsAreNamed()
        {
            _environment[EosCredentials.ProductIdVariable] = "p";

            var error = Create(_library.Path, "-register-lobby", "203.0.113.10:7777");

            Assert.That(error, Does.StartWith("[Lobby] -register-lobby needs EOS credentials; missing EOS_SANDBOX_ID,"));
            Assert.That(error, Does.Contain(EosCredentials.LocalFileName));
            Assert.That(_modules, Is.Empty);
        }

        [Test]
        public void AnUnreadableLocalFileIsReported()
        {
            File.WriteAllText(Path.Combine(_roots.Path, EosCredentials.LocalFileName), "[1]");

            var error = Create(_library.Path, "-register-lobby", "203.0.113.10:7777");

            Assert.That(error, Does.StartWith("[Lobby] cannot load EOS credentials: "));
        }

        [Test]
        public void WithoutTheSdkOrItsLibraryTheLobbyCannotBeUsed()
        {
            WriteLocalFile();

            var error = Create(_library.Path, "-register-lobby", "203.0.113.10:7777");

            Assert.That(error, Does.StartWith("[Lobby] -register-lobby cannot use EOS: "));
            Assert.That(error, EosSdkBuild.Included ? Does.Contain("native library") : Does.Contain("not in this build"));
            Assert.That(_modules, Is.Empty);
        }

        [Test]
        public void WithTheSdkAndCredentialsTheLobbyModuleIsAdded()
        {
            if (!EosSdkBuild.Included || !EosSdkBuild.HasNativeLibrary(AppContext.BaseDirectory))
            {
                Assert.Pass("this build does not include the EOS SDK and its native library");
            }

            WriteLocalFile();

            Assert.That(Create(AppContext.BaseDirectory, "-register-lobby", "203.0.113.10:7777"), Is.Null);
            Assert.That(_modules, Has.Count.EqualTo(1));
            Assert.That(_modules[0], Is.InstanceOf<ServerLobbyModule>());
        }

        private void WriteLocalFile()
        {
            var values = new List<KeyValuePair<string, string>>();
            foreach (var name in EosCredentials.VariableNames)
            {
                values.Add(new KeyValuePair<string, string>(name, "x"));
            }

            File.WriteAllText(Path.Combine(_roots.Path, EosCredentials.LocalFileName), FlatJsonObject.Format(values));
        }

        private string Create(string libraryDirectory, params string[] args)
        {
            Assert.That(ServerLaunch.TryParse(args, _ => null, out var launch, out var parseError), Is.True, parseError);
            var runtime = new ServerRuntime(
                TextWriter.Null,
                TextWriter.Null,
                name => _environment.TryGetValue(name, out var value) ? value : null,
                new ImmediateStopSignal(),
                new[] { _roots.Path },
                new[] { _roots.Path },
                () => throw new InvalidOperationException("no transport in this test"));
            return ServerModules.Create(launch, runtime, _modules, libraryDirectory, out var error) ? null : error;
        }
    }
}
