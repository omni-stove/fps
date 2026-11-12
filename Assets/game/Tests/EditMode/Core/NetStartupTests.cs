using System;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    // Unity の EditMode テストと dotnet/Fps.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class NetStartupTests
    {
        private static string[] Args(string commandLine)
        {
            return commandLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        }

        [Test]
        public void DedicatedServerDefaultsToServer()
        {
            Assert.That(NetStartup.DefaultMode(true, NetLaunchMode.Host), Is.EqualTo(NetLaunchMode.Server));
            Assert.That(NetStartup.DefaultMode(true, NetLaunchMode.Client), Is.EqualTo(NetLaunchMode.Server));
        }

        [TestCase(NetLaunchMode.Host)]
        [TestCase(NetLaunchMode.Server)]
        [TestCase(NetLaunchMode.Client)]
        public void NormalBuildKeepsConfiguredDefault(NetLaunchMode configured)
        {
            Assert.That(NetStartup.DefaultMode(false, configured), Is.EqualTo(configured));
        }

        [Test]
        public void DedicatedServerStartsAsServerWithoutArguments()
        {
            var ok = NetStartup.TryResolve(Args("fps-server -batchmode -nographics"), true, NetLaunchMode.Host, out var options, out var error);

            Assert.That(ok, Is.True, error);
            Assert.That(error, Is.Null);
            Assert.That(options.Mode, Is.EqualTo(NetLaunchMode.Server));
            Assert.That(options.Port, Is.EqualTo(NetLaunchOptions.DefaultPort));
        }

        [Test]
        public void DedicatedServerAcceptsExplicitServerModeAndAddress()
        {
            var ok = NetStartup.TryResolve(Args("-mode server -address 0.0.0.0 -port 9000"), true, NetLaunchMode.Host, out var options, out var error);

            Assert.That(ok, Is.True, error);
            Assert.That(options.Address, Is.EqualTo(NetLaunchOptions.AnyAddress));
            Assert.That(options.Port, Is.EqualTo((ushort)9000));
        }

        [TestCase("-mode host")]
        [TestCase("-mode client")]
        public void DedicatedServerRejectsOtherModes(string commandLine)
        {
            var ok = NetStartup.TryResolve(Args(commandLine), true, NetLaunchMode.Host, out var options, out var error);

            Assert.That(ok, Is.False);
            Assert.That(options, Is.Null);
            Assert.That(error, Does.Contain("dedicated server"));
        }

        [Test]
        public void NormalBuildAllowsAnyMode()
        {
            var ok = NetStartup.TryResolve(Args("-mode client"), false, NetLaunchMode.Host, out var options, out var error);

            Assert.That(ok, Is.True, error);
            Assert.That(options.Mode, Is.EqualTo(NetLaunchMode.Client));
        }

        [Test]
        public void ResolvePassesThroughParseErrors()
        {
            var ok = NetStartup.TryResolve(Args("-port 0"), true, NetLaunchMode.Host, out var options, out var error);

            Assert.That(ok, Is.False);
            Assert.That(options, Is.Null);
            Assert.That(error, Does.Contain("-port"));
        }

        [Test]
        public void LoadsNetSceneOnDedicatedServerOrWithModeFlag()
        {
            Assert.That(NetStartup.ShouldLoadNetScene(false, true, Args("fps-server")), Is.True);
            Assert.That(NetStartup.ShouldLoadNetScene(false, false, Args("fps -mode client")), Is.True);
            Assert.That(NetStartup.ShouldLoadNetScene(false, false, Args("fps")), Is.False);
        }

        [Test]
        public void LoadsNetSceneWithLobbyFlag()
        {
            Assert.That(NetStartup.ShouldLoadNetScene(false, false, Args("fps -lobby join")), Is.True);
            Assert.That(NetStartup.ShouldLoadNetScene(true, false, Args("fps -lobby join")), Is.False);
        }

        [Test]
        public void ResolveLobbyWithoutFlagIsNone()
        {
            var ok = NetStartup.TryResolveLobby(Args("fps -mode host"), true, out var options, out var error);

            Assert.That(ok, Is.True, error);
            Assert.That(options.IsRequested, Is.False);
        }

        [Test]
        public void ResolveLobbyInNormalBuild()
        {
            var ok = NetStartup.TryResolveLobby(Args("fps -lobby create"), false, out var options, out var error);

            Assert.That(ok, Is.True, error);
            Assert.That(options.Command, Is.EqualTo(LobbyCommand.Create));
        }

        [TestCase("-lobby create")]
        [TestCase("-lobby join")]
        public void DedicatedServerRejectsLobby(string commandLine)
        {
            var ok = NetStartup.TryResolveLobby(Args(commandLine), true, out var options, out var error);

            Assert.That(ok, Is.False);
            Assert.That(options, Is.Null);
            Assert.That(error, Does.Contain("dedicated server"));
        }

        [Test]
        public void ResolveLobbyPassesThroughParseErrors()
        {
            var ok = NetStartup.TryResolveLobby(Args("-lobby create -mode host"), false, out var options, out var error);

            Assert.That(ok, Is.False);
            Assert.That(options, Is.Null);
            Assert.That(error, Does.Contain("-mode"));
        }

        [Test]
        public void NeverLoadsNetSceneInEditor()
        {
            Assert.That(NetStartup.ShouldLoadNetScene(true, true, Args("-mode server")), Is.False);
        }

        [Test]
        public void HeadlessPlayerIsBatchModeOrDedicatedServerOutsideEditor()
        {
            Assert.That(NetStartup.IsHeadlessPlayer(false, true, false), Is.True);
            Assert.That(NetStartup.IsHeadlessPlayer(false, false, true), Is.True);
            Assert.That(NetStartup.IsHeadlessPlayer(false, false, false), Is.False);
            Assert.That(NetStartup.IsHeadlessPlayer(true, true, true), Is.False);
        }

        [Test]
        public void DisablesPresentationOnlyOnDedicatedServer()
        {
            Assert.That(NetStartup.ShouldDisablePresentation(true), Is.True);
            Assert.That(NetStartup.ShouldDisablePresentation(false), Is.False);
        }

        [Test]
        public void EnablesFirstPersonViewOnlyForOwnerWithScreen()
        {
            Assert.That(NetStartup.ShouldEnableFirstPersonView(true, false, false), Is.True);
            Assert.That(NetStartup.ShouldEnableFirstPersonView(false, false, false), Is.False);
            Assert.That(NetStartup.ShouldEnableFirstPersonView(true, true, false), Is.False);
            Assert.That(NetStartup.ShouldEnableFirstPersonView(true, false, true), Is.False);
        }

        [Test]
        public void RejectsNullArguments()
        {
            Assert.Throws<ArgumentNullException>(
                () => NetStartup.TryResolve(null, true, NetLaunchMode.Host, out _, out _));
            Assert.Throws<ArgumentNullException>(() => NetStartup.ShouldLoadNetScene(false, true, null));
            Assert.Throws<ArgumentNullException>(() => NetStartup.TryResolveLobby(null, false, out _, out _));
        }
    }
}
