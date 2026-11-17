using System;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class LobbyLaunchOptionsTests
    {
        private const string _serverId = "0123456789abcdef0123456789abcdef";

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
            var options = Parse("shooter -batchmode -mode client -address 10.0.0.2");

            Assert.That(options, Is.SameAs(LobbyLaunchOptions.None));
            Assert.That(options.IsRequested, Is.False);
            Assert.That(options.Command, Is.EqualTo(LobbyCommand.None));
        }

        [TestCase("shooter -lobby quickmatch")]
        [TestCase("shooter -LOBBY QUICKMATCH")]
        [TestCase("shooter -lobby quickmatch -transport udp")]
        public void ParsesQuickMatch(string commandLine)
        {
            var options = Parse(commandLine);

            Assert.That(options.Command, Is.EqualTo(LobbyCommand.QuickMatch));
            Assert.That(options.IsRequested, Is.True);
            Assert.That(options.ToString(), Is.EqualTo("quickmatch"));
        }

        [TestCase("-lobby host", "-lobby must be quickmatch")]
        [TestCase("-lobby create", "-lobby must be quickmatch")]
        [TestCase("-lobby join", "-lobby must be quickmatch")]
        [TestCase("-lobby", "-lobby requires a value")]
        [TestCase("-lobby -batchmode", "-lobby requires a value")]
        [TestCase("-lobby quickmatch -lobby quickmatch", "more than once")]
        public void RejectsInvalidArguments(string commandLine, string expected)
        {
            Assert.That(ParseError(commandLine), Does.Contain(expected));
        }

        [TestCase("-lobby quickmatch -mode client", "-mode")]
        [TestCase("-lobby quickmatch -address 10.0.0.1", "-address")]
        [TestCase("-lobby quickmatch -port 9000", "-port")]
        [TestCase("-lobby quickmatch -eos-server " + _serverId, "-eos-server")]
        [TestCase("-lobby quickmatch -register-lobby 203.0.113.10:7777", "-register-lobby")]
        public void RejectsFlagsTheLobbyDecides(string commandLine, string flag)
        {
            var error = ParseError(commandLine);

            Assert.That(error, Does.StartWith(flag));
            Assert.That(error, Does.Contain("-lobby"));
        }

        [Test]
        public void QuickMatchFactoryMatchesTheParsedOptions()
        {
            Assert.That(LobbyLaunchOptions.QuickMatch().Command, Is.EqualTo(LobbyCommand.QuickMatch));
            Assert.That(LobbyLaunchOptions.CommandName(LobbyCommand.None), Is.EqualTo("none"));
        }

        [Test]
        public void HasLobbyFlagIgnoresSimilarFlags()
        {
            Assert.That(LobbyLaunchOptions.HasLobbyFlag(Args("shooter -lobby quickmatch")), Is.True);
            Assert.That(LobbyLaunchOptions.HasLobbyFlag(Args("shooter -lobby-id abc")), Is.False);
            Assert.That(LobbyLaunchOptions.HasLobbyFlag(Args("shooter -mode client")), Is.False);
        }

        [Test]
        public void RejectsNull()
        {
            Assert.Throws<ArgumentNullException>(() => LobbyLaunchOptions.TryParse(null, out _, out _));
            Assert.Throws<ArgumentNullException>(() => LobbyLaunchOptions.HasLobbyFlag(null));
        }
    }
}
