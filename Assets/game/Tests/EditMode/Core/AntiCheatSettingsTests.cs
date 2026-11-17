using System;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class AntiCheatSettingsTests
    {
        private const string _serverId = "0123456789abcdef0123456789abcdef";

        private static readonly string[] _noArgs = { "shooter" };
        private static readonly string[] _noEacArgs = { "shooter", "-mode", "client", "-NoEac" };
        private static readonly string[] _eacArgs = { "shooter", "-eac" };
        private static readonly string[] _bothArgs = { "shooter", "-noeac", "-eac" };
        private static readonly string[] _similarArgs = { "shooter", "-noeacx", "--noeac", "noeac" };

        private static NetLaunchOptions Eos(NetLaunchMode mode)
        {
            var serverId = mode == NetLaunchMode.Client ? _serverId : null;
            return new NetLaunchOptions(mode, NetLaunchOptions.DefaultAddress, NetLaunchOptions.DefaultPort, NetTransportKind.Eos, serverId);
        }

        private static NetLaunchOptions Udp(NetLaunchMode mode)
        {
            return new NetLaunchOptions(mode, NetLaunchOptions.DefaultAddress, NetLaunchOptions.DefaultPort);
        }

        private static AntiCheatSettings Resolve(string[] args, bool isEditor, NetLaunchOptions options)
        {
            Assert.That(AntiCheatSettings.TryResolve(args, isEditor, options, out var settings, out var error), Is.True, error);
            Assert.That(error, Is.Null);
            return settings;
        }

        [Test]
        public void EnabledForAnEosClientInAPlayer()
        {
            var client = Resolve(_noArgs, false, Eos(NetLaunchMode.Client));

            Assert.That(client.Enabled, Is.True);
            Assert.That(client.Describe(), Does.StartWith("[AntiCheat] enabled"));
        }

        [Test]
        public void NoEacDisables()
        {
            var settings = Resolve(_noEacArgs, false, Eos(NetLaunchMode.Client));

            Assert.That(settings.Enabled, Is.False);
            Assert.That(settings.Describe(), Is.EqualTo("[AntiCheat] disabled by -noeac"));
        }

        [Test]
        public void DisabledInTheEditorUnlessForced()
        {
            var byDefault = Resolve(_noArgs, true, Eos(NetLaunchMode.Client));
            var forced = Resolve(_eacArgs, true, Eos(NetLaunchMode.Client));

            Assert.That(byDefault.Enabled, Is.False);
            Assert.That(byDefault.Reason, Does.Contain("editor"));
            Assert.That(forced.Enabled, Is.True);
        }

        [Test]
        public void DisabledWithUdpEvenWhenForced()
        {
            var settings = Resolve(_eacArgs, false, Udp(NetLaunchMode.Client));

            Assert.That(settings.Enabled, Is.False);
            Assert.That(settings.Reason, Does.Contain("-transport eos"));
        }

        [Test]
        public void DisabledOnADedicatedServer()
        {
            var settings = Resolve(_noArgs, false, Eos(NetLaunchMode.Server));

            Assert.That(settings.Enabled, Is.False);
            Assert.That(settings.Reason, Does.Contain(nameof(AntiCheatSettings.TryResolveDedicatedServer)));
        }

        [Test]
        public void RejectsBothFlags()
        {
            var ok = AntiCheatSettings.TryResolve(
                _bothArgs, false, Eos(NetLaunchMode.Client), out var settings, out var error);

            Assert.That(ok, Is.False);
            Assert.That(settings, Is.Null);
            Assert.That(error, Does.Contain("-noeac").And.Contain("-eac"));
        }

        [Test]
        public void IgnoresSimilarArguments()
        {
            var settings = Resolve(_similarArgs, false, Eos(NetLaunchMode.Client));

            Assert.That(settings.Enabled, Is.True);
        }

        [Test]
        public void DisabledNeedsAReason()
        {
            Assert.Throws<ArgumentException>(() => AntiCheatSettings.Disabled(" "));
            Assert.Throws<ArgumentException>(() => AntiCheatSettings.Disabled(null));
        }

        [Test]
        public void RejectsNullArguments()
        {
            Assert.Throws<ArgumentNullException>(() => AntiCheatSettings.TryResolve(null, false, Eos(NetLaunchMode.Client), out _, out _));
            Assert.Throws<ArgumentNullException>(() => AntiCheatSettings.TryResolve(_noArgs, false, null, out _, out _));
        }

        [Test]
        public void DedicatedServerIsDisabledByDefault()
        {
            Assert.That(AntiCheatSettings.TryResolveDedicatedServer(_noArgs, Eos(NetLaunchMode.Server), out var settings, out var error), Is.True);

            Assert.That(error, Is.Null);
            Assert.That(settings.Enabled, Is.False);
            Assert.That(settings.Describe(), Does.StartWith("[AntiCheat] disabled").And.Contain("-eac"));
        }

        [Test]
        public void DedicatedServerNoEacDisables()
        {
            Assert.That(AntiCheatSettings.TryResolveDedicatedServer(_noEacArgs, Eos(NetLaunchMode.Server), out var settings, out _), Is.True);

            Assert.That(settings.Describe(), Is.EqualTo("[AntiCheat] disabled by -noeac"));
        }

        [Test]
        public void DedicatedServerEacEnablesWithEos()
        {
            Assert.That(AntiCheatSettings.TryResolveDedicatedServer(_eacArgs, Eos(NetLaunchMode.Server), out var settings, out var error), Is.True);

            Assert.That(error, Is.Null);
            Assert.That(settings.Enabled, Is.True);
            Assert.That(settings.Describe(), Is.EqualTo("[AntiCheat] enabled (client-server, dedicated server)"));
        }

        [Test]
        public void DedicatedServerEacNeedsEos()
        {
            var ok = AntiCheatSettings.TryResolveDedicatedServer(_eacArgs, Udp(NetLaunchMode.Server), out var settings, out var error);

            Assert.That(ok, Is.False);
            Assert.That(settings, Is.Null);
            Assert.That(error, Does.Contain("-eac needs -transport eos"));
        }

        [Test]
        public void DedicatedServerRejectsBothFlags()
        {
            var ok = AntiCheatSettings.TryResolveDedicatedServer(_bothArgs, Eos(NetLaunchMode.Server), out var settings, out var error);

            Assert.That(ok, Is.False);
            Assert.That(settings, Is.Null);
            Assert.That(error, Does.Contain("-noeac").And.Contain("-eac"));
        }

        [Test]
        public void DedicatedServerRejectsNullArguments()
        {
            Assert.Throws<ArgumentNullException>(() => AntiCheatSettings.TryResolveDedicatedServer(null, Eos(NetLaunchMode.Server), out _, out _));
            Assert.Throws<ArgumentNullException>(() => AntiCheatSettings.TryResolveDedicatedServer(_noArgs, null, out _, out _));
        }
    }
}
