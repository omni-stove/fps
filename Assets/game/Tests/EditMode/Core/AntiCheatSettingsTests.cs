using System;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    // Unity の EditMode テストと dotnet/Fps.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class AntiCheatSettingsTests
    {
        private const string _serverId = "0123456789abcdef0123456789abcdef";

        private static readonly string[] _noArgs = { "fps" };
        private static readonly string[] _noEacArgs = { "fps", "-mode", "host", "-NoEac" };
        private static readonly string[] _eacArgs = { "fps", "-eac" };
        private static readonly string[] _bothArgs = { "fps", "-noeac", "-eac" };
        private static readonly string[] _similarArgs = { "fps", "-noeacx", "--noeac", "noeac" };

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
        public void EnabledForEosHostAndClientInAPlayer()
        {
            var host = Resolve(_noArgs, false, Eos(NetLaunchMode.Host));
            var client = Resolve(_noArgs, false, Eos(NetLaunchMode.Client));

            Assert.That(host.Enabled, Is.True);
            Assert.That(client.Enabled, Is.True);
            Assert.That(host.Describe(), Does.StartWith("[AntiCheat] enabled"));
        }

        [Test]
        public void NoEacDisables()
        {
            var settings = Resolve(_noEacArgs, false, Eos(NetLaunchMode.Host));

            Assert.That(settings.Enabled, Is.False);
            Assert.That(settings.Describe(), Is.EqualTo("[AntiCheat] disabled by -noeac"));
        }

        [Test]
        public void DisabledInTheEditorUnlessForced()
        {
            var byDefault = Resolve(_noArgs, true, Eos(NetLaunchMode.Host));
            var forced = Resolve(_eacArgs, true, Eos(NetLaunchMode.Host));

            Assert.That(byDefault.Enabled, Is.False);
            Assert.That(byDefault.Reason, Does.Contain("editor"));
            Assert.That(forced.Enabled, Is.True);
        }

        [Test]
        public void DisabledWithUdpEvenWhenForced()
        {
            var settings = Resolve(_eacArgs, false, Udp(NetLaunchMode.Host));

            Assert.That(settings.Enabled, Is.False);
            Assert.That(settings.Reason, Does.Contain("-transport eos"));
        }

        [Test]
        public void DisabledOnADedicatedServer()
        {
            var settings = Resolve(_noArgs, false, Eos(NetLaunchMode.Server));

            Assert.That(settings.Enabled, Is.False);
            Assert.That(settings.Reason, Does.Contain("-mode host"));
        }

        [Test]
        public void RejectsBothFlags()
        {
            var ok = AntiCheatSettings.TryResolve(
                _bothArgs, false, Eos(NetLaunchMode.Host), out var settings, out var error);

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
            Assert.Throws<ArgumentNullException>(() => AntiCheatSettings.TryResolve(null, false, Eos(NetLaunchMode.Host), out _, out _));
            Assert.Throws<ArgumentNullException>(() => AntiCheatSettings.TryResolve(_noArgs, false, null, out _, out _));
        }
    }
}
