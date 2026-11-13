using System;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    // Unity の EditMode テストと dotnet/Fps.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class LobbyLaunchOptionsTests
    {
        private const string _ownerId = "0123456789abcdef0123456789abcdef";

        private static string[] Args(string commandLine)
        {
            return commandLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        }

        private static LobbyLaunchOptions Parse(string commandLine)
        {
            var ok = LobbyLaunchOptions.TryParse(Args(commandLine), out var options, out var error);
            Assert.That(ok, Is.True, error);
            Assert.That(error, Is.Null);
            return options;
        }

        private static string ParseError(string commandLine)
        {
            var ok = LobbyLaunchOptions.TryParse(Args(commandLine), out var options, out var error);
            Assert.That(ok, Is.False);
            Assert.That(options, Is.Null);
            Assert.That(error, Is.Not.Null.And.Not.Empty);
            return error;
        }

        [Test]
        public void WithoutLobbyFlagIsNone()
        {
            var options = Parse("fps -batchmode -mode client -address 10.0.0.2");

            Assert.That(options, Is.SameAs(LobbyLaunchOptions.None));
            Assert.That(options.IsRequested, Is.False);
            Assert.That(options.Command, Is.EqualTo(LobbyCommand.None));
        }

        [Test]
        public void CreateUsesDefaultMaxPlayers()
        {
            var options = Parse("fps -batchmode -nographics -lobby create");

            Assert.That(options.IsRequested, Is.True);
            Assert.That(options.Command, Is.EqualTo(LobbyCommand.Create));
            Assert.That(options.MaxPlayers, Is.EqualTo(LobbyLaunchOptions.DefaultMaxPlayers));
            Assert.That(options.LobbyId, Is.Null);
        }

        [Test]
        public void CreateAcceptsMaxPlayers()
        {
            var options = Parse("-lobby create -lobby-max-players 4");

            Assert.That(options.MaxPlayers, Is.EqualTo(4));
        }

        [Test]
        public void JoinWithoutIdSearches()
        {
            var options = Parse("-lobby join");

            Assert.That(options.Command, Is.EqualTo(LobbyCommand.Join));
            Assert.That(options.LobbyId, Is.Null);
        }

        [Test]
        public void JoinAcceptsLobbyId()
        {
            var options = Parse("-lobby join -lobby-id abc123");

            Assert.That(options.LobbyId, Is.EqualTo("abc123"));
        }

        [Test]
        public void FlagsAndCommandsIgnoreCase()
        {
            var options = Parse("-LOBBY Join -Lobby-Id abc");

            Assert.That(options.Command, Is.EqualTo(LobbyCommand.Join));
            Assert.That(options.LobbyId, Is.EqualTo("abc"));
        }

        [Test]
        public void EosTransportIsAllowed()
        {
            var options = Parse("-lobby create -transport EOS");

            Assert.That(options.Command, Is.EqualTo(LobbyCommand.Create));
        }

        [TestCase("-lobby host", "-lobby")]
        [TestCase("-lobby", "-lobby requires a value")]
        [TestCase("-lobby -batchmode", "-lobby requires a value")]
        [TestCase("-lobby create -lobby join", "more than once")]
        [TestCase("-lobby create -lobby-max-players 1", "-lobby-max-players")]
        [TestCase("-lobby create -lobby-max-players 65", "-lobby-max-players")]
        [TestCase("-lobby create -lobby-max-players eight", "-lobby-max-players")]
        [TestCase("-lobby create -lobby-max-players +8", "-lobby-max-players")]
        [TestCase("-lobby create -lobby-id abc", "-lobby-id")]
        [TestCase("-lobby join -lobby-max-players 4", "-lobby-max-players")]
        [TestCase("-lobby-id abc", "-lobby-id")]
        [TestCase("-lobby-max-players 4", "-lobby-max-players")]
        [TestCase("-lobby create -transport udp", "-transport")]
        public void RejectsInvalidArguments(string commandLine, string expected)
        {
            Assert.That(ParseError(commandLine), Does.Contain(expected));
        }

        [TestCase("-lobby create -mode host", "-mode")]
        [TestCase("-lobby join -mode client", "-mode")]
        [TestCase("-lobby join -address 10.0.0.1", "-address")]
        [TestCase("-lobby join -port 9000", "-port")]
        [TestCase("-lobby join -eos-server " + _ownerId, "-eos-server")]
        [TestCase("-lobby create -register-lobby 203.0.113.10:7777", "-register-lobby")]
        public void RejectsFlagsTheLobbyDecides(string commandLine, string flag)
        {
            var error = ParseError(commandLine);

            Assert.That(error, Does.StartWith(flag));
            Assert.That(error, Does.Contain("-lobby"));
        }

        [Test]
        public void RejectsBlankOrOverlongLobbyId()
        {
            Assert.That(ParseError("-lobby join -lobby-id " + new string('a', LobbyLaunchOptions.MaxLobbyIdLength + 1)), Does.Contain("-lobby-id"));
            Assert.Throws<ArgumentException>(() => LobbyLaunchOptions.Join(" "));
            Assert.Throws<ArgumentException>(() => LobbyLaunchOptions.Join("a b"));
        }

        [Test]
        public void FactoriesValidate()
        {
            Assert.That(LobbyLaunchOptions.Create().MaxPlayers, Is.EqualTo(LobbyLaunchOptions.DefaultMaxPlayers));
            Assert.That(LobbyLaunchOptions.Join("id").LobbyId, Is.EqualTo("id"));
            Assert.Throws<ArgumentOutOfRangeException>(() => LobbyLaunchOptions.Create(LobbyLaunchOptions.MinMaxPlayers - 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => LobbyLaunchOptions.Create(LobbyLaunchOptions.MaxMaxPlayers + 1));
        }

        [Test]
        public void CreateStartsAnEosHost()
        {
            var net = LobbyLaunchOptions.Create().ToNetOptions(null);

            Assert.That(net.Mode, Is.EqualTo(NetLaunchMode.Host));
            Assert.That(net.Transport, Is.EqualTo(NetTransportKind.Eos));
            Assert.That(net.EosServerId, Is.Null);
        }

        [Test]
        public void JoinConnectsToTheOwnerOverEos()
        {
            var net = LobbyLaunchOptions.Join().ToNetOptions(_ownerId);

            Assert.That(net.Mode, Is.EqualTo(NetLaunchMode.Client));
            Assert.That(net.Transport, Is.EqualTo(NetTransportKind.Eos));
            Assert.That(net.EosServerId, Is.EqualTo(_ownerId));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("not-a-product-user-id")]
        public void JoinRejectsAnOwnerThatIsNotAProductUserId(string owner)
        {
            Assert.Throws<ArgumentException>(() => LobbyLaunchOptions.Join().ToNetOptions(owner));
        }

        [Test]
        public void NoneHasNoNetOptions()
        {
            Assert.Throws<InvalidOperationException>(() => LobbyLaunchOptions.None.ToNetOptions(_ownerId));
        }

        [Test]
        public void BucketIdSeparatesVersions()
        {
            Assert.That(LobbyLaunchOptions.BucketId("1.2.0"), Is.EqualTo("fps:1.2.0"));
            Assert.That(LobbyLaunchOptions.BucketId(" 1.2.0 "), Is.EqualTo("fps:1.2.0"));
            Assert.That(LobbyLaunchOptions.BucketId(null), Is.EqualTo("fps:0"));
            Assert.That(LobbyLaunchOptions.BucketId("1.2.0"), Is.Not.EqualTo(LobbyLaunchOptions.BucketId("1.3.0")));
            Assert.That(
                LobbyLaunchOptions.BucketId(new string('9', 100)),
                Has.Length.EqualTo(LobbyLaunchOptions.BucketPrefix.Length + LobbyLaunchOptions.MaxBucketVersionLength));
        }

        [Test]
        public void HasLobbyFlagIgnoresSimilarFlags()
        {
            Assert.That(LobbyLaunchOptions.HasLobbyFlag(Args("fps -lobby join")), Is.True);
            Assert.That(LobbyLaunchOptions.HasLobbyFlag(Args("fps -lobby-id abc")), Is.False);
            Assert.That(LobbyLaunchOptions.HasLobbyFlag(Args("fps -mode client")), Is.False);
        }

        [Test]
        public void RejectsNullArguments()
        {
            Assert.Throws<ArgumentNullException>(() => LobbyLaunchOptions.TryParse(null, out _, out _));
            Assert.Throws<ArgumentNullException>(() => LobbyLaunchOptions.HasLobbyFlag(null));
        }
    }
}
