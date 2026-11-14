using System;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    // Unity の EditMode テストと dotnet/Fps.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    /// <summary>
    /// <see cref="NetLaunchOptions"/> の <c>-map</c> / <c>-bots</c> / <c>-net-latency</c> / <c>-net-loss</c> / <c>-net-jitter</c> /
    /// <c>-drain-timeout</c> の解析と検証。
    /// </summary>
    public sealed class NetLaunchOptionsServerArgsTests
    {
        private static string[] Args(string commandLine)
        {
            return commandLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        }

        private static NetLaunchOptions ParseOrFail(string commandLine)
        {
            var ok = NetLaunchOptions.TryParse(Args(commandLine), NetLaunchMode.Host, out var options, out var error);
            Assert.That(ok, Is.True, error);
            Assert.That(error, Is.Null);
            return options;
        }

        private static string ParseError(string commandLine)
        {
            var ok = NetLaunchOptions.TryParse(Args(commandLine), NetLaunchMode.Host, out var options, out var error);
            Assert.That(ok, Is.False);
            Assert.That(options, Is.Null);
            Assert.That(error, Is.Not.Null.And.Not.Empty);
            return error;
        }

        private static NetLaunchOptions Create(
            NetLaunchMode mode,
            string map = null,
            int bots = 0,
            int netLatencyMs = 0,
            int netLossPercent = 0,
            int netJitterMs = 0,
            int drainTimeoutSeconds = 0)
        {
            return new NetLaunchOptions(
                mode, NetLaunchOptions.DefaultAddress, NetLaunchOptions.DefaultPort, NetTransportKind.Udp, null, null,
                map, bots, netLatencyMs, netLossPercent, netJitterMs, drainTimeoutSeconds);
        }

        [Test]
        public void AbsentFlagsLeaveEverythingOff()
        {
            var options = ParseOrFail("-mode server");

            Assert.That(options.Map, Is.Null);
            Assert.That(options.Bots, Is.Zero);
            Assert.That(options.NetLatencyMs, Is.Zero);
            Assert.That(options.NetLossPercent, Is.Zero);
            Assert.That(options.NetJitterMs, Is.Zero);
            Assert.That(options.SimulatesNetwork, Is.False);
            Assert.That(options.DrainTimeoutSeconds, Is.Zero);
            Assert.That(options.ToString(), Is.EqualTo("server 127.0.0.1:7777"));
        }

        [Test]
        public void OlderConstructorsLeaveEverythingOff()
        {
            var options = new NetLaunchOptions(NetLaunchMode.Server, NetLaunchOptions.AnyAddress, 7000);

            Assert.That(options.Map, Is.Null);
            Assert.That(options.Bots, Is.Zero);
            Assert.That(options.SimulatesNetwork, Is.False);
            Assert.That(options.DrainTimeoutSeconds, Is.Zero);
        }

        [Test]
        public void ParsesBotsAndLatencyOnAServer()
        {
            var options = ParseOrFail("-batchmode -mode server -bots 4 -net-latency 100");

            Assert.That(options.Mode, Is.EqualTo(NetLaunchMode.Server));
            Assert.That(options.Bots, Is.EqualTo(4));
            Assert.That(options.NetLatencyMs, Is.EqualTo(100));
            Assert.That(options.NetLossPercent, Is.Zero);
            Assert.That(options.NetJitterMs, Is.Zero);
            Assert.That(options.SimulatesNetwork, Is.True);
            Assert.That(options.ToString(), Is.EqualTo("server 127.0.0.1:7777 bots=4 latency=100ms loss=0% jitter=0ms"));
        }

        [Test]
        public void ParsesEveryServerFlagTogether()
        {
            var options = ParseOrFail(
                "-mode server -map NetArena_02 -bots 64 -net-latency 5000 -net-loss 100 -net-jitter 5000 -drain-timeout 3600");

            Assert.That(options.Map, Is.EqualTo("NetArena_02"));
            Assert.That(options.Bots, Is.EqualTo(NetLaunchOptions.MaxBots));
            Assert.That(options.NetLatencyMs, Is.EqualTo(NetLaunchOptions.MaxNetDelayMs));
            Assert.That(options.NetLossPercent, Is.EqualTo(NetLaunchOptions.MaxNetLossPercent));
            Assert.That(options.NetJitterMs, Is.EqualTo(NetLaunchOptions.MaxNetDelayMs));
            Assert.That(options.DrainTimeoutSeconds, Is.EqualTo(NetLaunchOptions.MaxDrainTimeoutSeconds));
            Assert.That(
                options.ToString(),
                Is.EqualTo("server 127.0.0.1:7777 map=NetArena_02 bots=64 latency=5000ms loss=100% jitter=5000ms drain=3600s"));
        }

        [Test]
        public void FlagNamesIgnoreCase()
        {
            var options = ParseOrFail("-mode host -BOTS 2 -Net-Loss 5 -MAP Arena");

            Assert.That(options.Bots, Is.EqualTo(2));
            Assert.That(options.NetLossPercent, Is.EqualTo(5));
            Assert.That(options.Map, Is.EqualTo("Arena"));
        }

        [Test]
        public void AClientMaySimulateABadNetwork()
        {
            var options = ParseOrFail("-mode client -net-latency 80 -net-loss 3 -net-jitter 20");

            Assert.That(options.NetLatencyMs, Is.EqualTo(80));
            Assert.That(options.NetLossPercent, Is.EqualTo(3));
            Assert.That(options.NetJitterMs, Is.EqualTo(20));
            Assert.That(options.SimulatesNetwork, Is.True);
        }

        [TestCase("-net-loss 1")]
        [TestCase("-net-jitter 1")]
        public void AnySingleImpairmentSimulatesTheNetwork(string flags)
        {
            Assert.That(ParseOrFail("-mode host " + flags).SimulatesNetwork, Is.True);
        }

        [Test]
        public void AClientMayPassZeroBots()
        {
            Assert.That(ParseOrFail("-mode client -bots 0").Bots, Is.Zero);
        }

        [TestCase("-bots", "65")]
        [TestCase("-net-latency", "5001")]
        [TestCase("-net-loss", "101")]
        [TestCase("-net-jitter", "5001")]
        [TestCase("-drain-timeout", "3601")]
        [TestCase("-bots", "four")]
        [TestCase("-bots", "+4")]
        [TestCase("-net-latency", "1.5")]
        [TestCase("-net-loss", "3%")]
        [TestCase("-drain-timeout", "99999999999")]
        public void RejectsCountsThatAreNotIntegersInRange(string flag, string value)
        {
            var error = ParseError("-mode server " + flag + " " + value);

            Assert.That(error, Does.StartWith(flag + " must be an integer between 0 and "));
            Assert.That(error, Does.Contain("'" + value + "'"));
        }

        [TestCase("-bots")]
        [TestCase("-net-latency")]
        [TestCase("-drain-timeout")]
        public void ReadsANegativeNumberAsAMissingValue(string flag)
        {
            Assert.That(ParseError("-mode server " + flag + " -1"), Is.EqualTo(flag + " requires a value"));
        }

        [TestCase("-map Arena -map Other", "-map")]
        [TestCase("-bots 1 -bots 2", "-bots")]
        public void RejectsAFlagGivenTwice(string flags, string flag)
        {
            Assert.That(ParseError("-mode server " + flags), Is.EqualTo(flag + " is given more than once"));
        }

        [TestCase("Assets/game/Scenes/NetArena.unity")]
        [TestCase("NetArena.unity")]
        [TestCase("Net Arena")]
        [TestCase("アリーナ")]
        public void RejectsMapsThatAreNotPlainSceneNames(string map)
        {
            var args = new[] { "-mode", "server", "-map", map };

            var ok = NetLaunchOptions.TryParse(args, NetLaunchMode.Host, out var options, out var error);

            Assert.That(ok, Is.False);
            Assert.That(options, Is.Null);
            Assert.That(error, Does.StartWith("-map must be a scene name"));
        }

        [Test]
        public void RecognizesMapNames()
        {
            Assert.That(NetLaunchOptions.IsMapName("NetTest"), Is.True);
            Assert.That(NetLaunchOptions.IsMapName("arena-2_b"), Is.True);
            Assert.That(NetLaunchOptions.IsMapName(new string('a', NetLaunchOptions.MaxMapNameLength)), Is.True);
            Assert.That(NetLaunchOptions.IsMapName(new string('a', NetLaunchOptions.MaxMapNameLength + 1)), Is.False);
            Assert.That(NetLaunchOptions.IsMapName(null), Is.False);
            Assert.That(NetLaunchOptions.IsMapName(""), Is.False);
            Assert.That(NetLaunchOptions.IsMapName("a/b"), Is.False);
        }

        [TestCase("-map Arena")]
        [TestCase("-bots 2")]
        public void AClientCannotChooseTheMapOrBots(string flags)
        {
            Assert.That(ParseError("-mode client " + flags), Does.Contain("is only used by -mode host or server (got client)"));
        }

        [TestCase("host")]
        [TestCase("client")]
        public void OnlyADedicatedServerDrains(string mode)
        {
            Assert.That(
                ParseError("-mode " + mode + " -drain-timeout 30"),
                Is.EqualTo("-drain-timeout is only used by -mode server (got " + mode + ")"));
        }

        [Test]
        public void ADrainTimeoutOfZeroIsAllowedAnywhere()
        {
            Assert.That(ParseOrFail("-mode client -drain-timeout 0").DrainTimeoutSeconds, Is.Zero);
        }

        [Test]
        public void WithTransportKeepsTheServerArguments()
        {
            var eos = Create(NetLaunchMode.Server, "Arena", 3, 50, 2, 10, 120).WithTransport(NetTransportKind.Eos);
            var udp = eos.WithTransport(NetTransportKind.Udp);

            foreach (var options in new[] { eos, udp })
            {
                Assert.That(options.Map, Is.EqualTo("Arena"));
                Assert.That(options.Bots, Is.EqualTo(3));
                Assert.That(options.NetLatencyMs, Is.EqualTo(50));
                Assert.That(options.NetLossPercent, Is.EqualTo(2));
                Assert.That(options.NetJitterMs, Is.EqualTo(10));
                Assert.That(options.DrainTimeoutSeconds, Is.EqualTo(120));
            }
        }

        [TestCase("bots", -1)]
        [TestCase("bots", NetLaunchOptions.MaxBots + 1)]
        [TestCase("netLatencyMs", -1)]
        [TestCase("netLatencyMs", NetLaunchOptions.MaxNetDelayMs + 1)]
        [TestCase("netLossPercent", -1)]
        [TestCase("netLossPercent", NetLaunchOptions.MaxNetLossPercent + 1)]
        [TestCase("netJitterMs", -1)]
        [TestCase("netJitterMs", NetLaunchOptions.MaxNetDelayMs + 1)]
        [TestCase("drainTimeoutSeconds", -1)]
        [TestCase("drainTimeoutSeconds", NetLaunchOptions.MaxDrainTimeoutSeconds + 1)]
        public void ConstructorRejectsCountsOutOfRange(string parameter, int value)
        {
            var ex = Assert.Throws<ArgumentOutOfRangeException>(() => Create(
                NetLaunchMode.Server,
                bots: parameter == "bots" ? value : 0,
                netLatencyMs: parameter == "netLatencyMs" ? value : 0,
                netLossPercent: parameter == "netLossPercent" ? value : 0,
                netJitterMs: parameter == "netJitterMs" ? value : 0,
                drainTimeoutSeconds: parameter == "drainTimeoutSeconds" ? value : 0));

            Assert.That(ex.ParamName, Is.EqualTo(parameter));
        }

        [Test]
        public void ConstructorRejectsServerArgumentsInTheWrongMode()
        {
            Assert.That(Assert.Throws<ArgumentException>(() => Create(NetLaunchMode.Client, map: "Arena")).ParamName, Is.EqualTo("map"));
            Assert.That(Assert.Throws<ArgumentException>(() => Create(NetLaunchMode.Server, map: "a.unity")).ParamName, Is.EqualTo("map"));
            Assert.That(Assert.Throws<ArgumentException>(() => Create(NetLaunchMode.Client, bots: 1)).ParamName, Is.EqualTo("bots"));
            Assert.That(
                Assert.Throws<ArgumentException>(() => Create(NetLaunchMode.Host, drainTimeoutSeconds: 1)).ParamName,
                Is.EqualTo("drainTimeoutSeconds"));
        }
    }
}
