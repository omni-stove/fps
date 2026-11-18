using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using NUnit.Framework;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.Server.Tests
{
    public sealed class ServerAdminConsoleTests
    {
        private const string _verifiedId = "0002aaaabbbbccccddddeeeeffff0001";
        private static readonly DateTime _time = new DateTime(2026, 11, 18, 9, 30, 0, DateTimeKind.Utc);

        private StringWriter _log;
        private StubControl _control;
        private ServerMatch _match;
        private FakeAddresses _addresses;
        private ServerBanList _bans;
        private TempDirectory _directory;
        private ServerAdminConsole _console;

        [SetUp]
        public void SetUp()
        {
            _log = new StringWriter();
            _control = new StubControl();
            _addresses = new FakeAddresses();
            _directory = new TempDirectory();
            Assert.That(ServerBanList.TryLoad(Path.Combine(_directory.Path, "bans.json"), out _bans, out var error), Is.True, error);
            _match = new ServerMatch(ServerMatch.DefaultRestartDelaySeconds, _log, new ServerEventWriter(false, _log, _log), () => _time);
            _console = Start(null);
        }

        [TearDown]
        public void TearDown()
        {
            _directory.Dispose();
            _log.Dispose();
        }

        [Test]
        public void PlayersListsEachConnectedPlayerWithWhatABanWouldUse()
        {
            Join(1, null, "203.0.113.5");
            Join(2, _verifiedId, null);

            _console.Execute("players");

            Assert.That(_log.ToString(), Does.Contain("[Admin] player 1 team="));
            Assert.That(_log.ToString(), Does.Contain(" client=1 product_user_id=- address=203.0.113.5"));
            Assert.That(_log.ToString(), Does.Contain(" client=2 product_user_id=" + _verifiedId + " address=-"));
            Assert.That(_log.ToString(), Does.Contain("[Admin] 2 player(s)"));
        }

        [Test]
        public void PlayersSaysWhenNobodyIsConnected()
        {
            _console.Execute("  PLAYERS  ");

            Assert.That(_log.ToString(), Does.Contain("[Admin] no players are connected"));
        }

        [Test]
        public void KickDisconnectsThePlayerWithTheKickedReason()
        {
            Join(3, null, "203.0.113.5");

            _console.Execute("kick 3 spamming chat");

            Assert.That(_control.Kicks, Has.Count.EqualTo(1));
            Assert.That(_control.Kicks[0].Connection, Is.EqualTo(new ConnectionId(3)));
            Assert.That(_control.Kicks[0].Reason, Is.EqualTo("shooter:rejected:kicked"));
            Assert.That(_log.ToString(), Does.Contain("[Admin] kicking player 3"));
            Assert.That(_bans.Entries, Is.Empty, "a kick does not ban");
        }

        [TestCase("kick", "[Admin] kick needs a player number")]
        [TestCase("kick 9", "[Admin] no connected player 9")]
        [TestCase("kick abc", "[Admin] no connected player abc")]
        public void KickWithoutAConnectedPlayerSaysSo(string line, string expected)
        {
            _console.Execute(line);

            Assert.That(_control.Kicks, Is.Empty);
            Assert.That(_log.ToString(), Does.Contain(expected));
        }

        [Test]
        public void BanningAPlayerWithAVerifiedIdBansTheIdAndWritesTheFile()
        {
            Join(2, _verifiedId, "203.0.113.5");

            _console.Execute("ban 2 aimbot");

            Assert.That(_bans.Entries, Has.Count.EqualTo(1));
            Assert.That(_bans.Entries[0].Kind, Is.EqualTo(ServerBanKind.ProductUserId));
            Assert.That(_bans.Entries[0].Value, Is.EqualTo(_verifiedId));
            Assert.That(_bans.Entries[0].Reason, Is.EqualTo("aimbot"));
            Assert.That(_bans.Entries[0].BannedAt, Is.EqualTo(_time));
            Assert.That(_control.Kicks, Has.Count.EqualTo(1));
            Assert.That(_control.Kicks[0].Reason, Is.EqualTo("shooter:rejected:banned"));
            Assert.That(_log.ToString(), Does.Contain(
                "[Admin] banned product_user_id=" + _verifiedId + " banned_at=2026-11-18T09:30:00Z reason=\"aimbot\"; disconnecting 1 player(s)"));
            Assert.That(_log.ToString(), Does.Contain("[Admin] wrote 1 ban(s) to " + _bans.Path));
            Assert.That(File.ReadAllText(_bans.Path), Does.Contain("\"productUserId\": \"" + _verifiedId + "\""));
        }

        [Test]
        public void BanningAPlayerWithoutAVerifiedIdBansTheAddressAndEveryoneOnIt()
        {
            Join(1, null, "203.0.113.5");
            Join(2, null, "203.0.113.5");
            Join(3, null, "198.51.100.7");

            _console.Execute("ban 1");

            Assert.That(_bans.Entries, Has.Count.EqualTo(1));
            Assert.That(_bans.Entries[0].Kind, Is.EqualTo(ServerBanKind.Address));
            Assert.That(_bans.Entries[0].Value, Is.EqualTo("203.0.113.5"));
            Assert.That(_bans.Entries[0].Reason, Is.Null);
            Assert.That(_control.Kicks, Has.Count.EqualTo(2));
            Assert.That(_control.Kicks[0].Connection, Is.EqualTo(new ConnectionId(1)));
            Assert.That(_control.Kicks[1].Connection, Is.EqualTo(new ConnectionId(2)));
        }

        [Test]
        public void ABanByAddressOrIdNeedsNoConnectedPlayer()
        {
            _console.Execute("ban ::ffff:192.0.2.1 griefing last week");
            _console.Execute("ban " + _verifiedId.ToUpperInvariant());

            Assert.That(_bans.Entries, Has.Count.EqualTo(2));
            Assert.That(_bans.Entries[0].Value, Is.EqualTo("192.0.2.1"), "an IPv4 address mapped into IPv6 is kept as IPv4");
            Assert.That(_bans.Entries[0].Reason, Is.EqualTo("griefing last week"));
            Assert.That(_bans.Entries[1].Kind, Is.EqualTo(ServerBanKind.ProductUserId));
            Assert.That(_log.ToString(), Does.Contain("; disconnecting 0 player(s)"));
            Assert.That(_control.Kicks, Is.Empty);
        }

        [Test]
        public void BanningTwiceKeepsTheFirstBanButStillDisconnects()
        {
            _console.Execute("ban 203.0.113.5 first");
            Join(4, null, "203.0.113.5");

            _console.Execute("ban 203.0.113.5 second");

            Assert.That(_bans.Entries, Has.Count.EqualTo(1));
            Assert.That(_bans.Entries[0].Reason, Is.EqualTo("first"));
            Assert.That(_control.Kicks, Has.Count.EqualTo(1));
            Assert.That(_log.ToString(), Does.Contain("[Admin] already banned: address=203.0.113.5"));
        }

        [TestCase("ban", "[Admin] ban needs a player number, a product user id or an address")]
        [TestCase("ban 9", "[Admin] no connected player 9")]
        [TestCase("ban not-an-address", "[Admin] 'not-an-address' is not a player number, a 32-digit product user id or an IP address")]
        [TestCase("ban 0002aaaa", "[Admin] '0002aaaa' is not a player number")]
        public void ABanWithoutATargetIsRefused(string line, string expected)
        {
            _console.Execute(line);

            Assert.That(_bans.Entries, Is.Empty);
            Assert.That(File.Exists(_bans.Path), Is.False);
            Assert.That(_log.ToString(), Does.Contain(expected));
        }

        [Test]
        public void APlayerWithNothingToBanIsLeftToKick()
        {
            Join(5, null, null);

            _console.Execute("ban 5");

            Assert.That(_bans.Entries, Is.Empty);
            Assert.That(_control.Kicks, Is.Empty);
            Assert.That(_log.ToString(), Does.Contain("[Admin] player 5 has no product user id or address to ban; kick them instead"));
        }

        [Test]
        public void ATooLongReasonIsRefused()
        {
            _console.Execute("ban 203.0.113.5 " + new string('x', ServerBanList.MaxReasonLength + 1));

            Assert.That(_bans.Entries, Is.Empty);
            Assert.That(_log.ToString(), Does.Contain("[Admin] the reason must be at most 200 characters"));
        }

        [Test]
        public void UnbanRemovesTheBanAndWritesTheFile()
        {
            _console.Execute("ban 203.0.113.5 griefing");
            _console.Execute("unban 203.0.113.5");

            Assert.That(_bans.Entries, Is.Empty);
            Assert.That(_log.ToString(), Does.Contain("[Admin] unbanned address=203.0.113.5"));
            Assert.That(ServerBanList.TryLoad(_bans.Path, out var reloaded, out var error), Is.True, error);
            Assert.That(reloaded.Entries, Is.Empty);
        }

        [TestCase("unban 203.0.113.9", "[Admin] no ban for address 203.0.113.9")]
        [TestCase("unban", "[Admin] unban needs a product user id or an address")]
        [TestCase("unban 3", "[Admin] '3' is not a player number, a 32-digit product user id or an IP address")]
        public void UnbanWithoutAMatchingBanSaysSo(string line, string expected)
        {
            _console.Execute(line);

            Assert.That(_log.ToString(), Does.Contain(expected));
        }

        [Test]
        public void BansListsTheBans()
        {
            _console.Execute("bans");
            _console.Execute("ban 203.0.113.5");
            _console.Execute("bans");

            Assert.That(_log.ToString(), Does.Contain("[Admin] no bans"));
            Assert.That(_log.ToString(), Does.Contain("[Admin] ban address=203.0.113.5 banned_at=2026-11-18T09:30:00Z"));
        }

        [Test]
        public void BotsAreNotKickedOrBanned()
        {
            var botId = BotRoster.FirstBotPlayerId;
            _match.JoinBot(botId);

            _console.Execute("kick " + botId);
            _console.Execute("ban " + botId);

            Assert.That(_control.Kicks, Is.Empty);
            Assert.That(_bans.Entries, Is.Empty);
            Assert.That(_log.ToString(), Does.Contain("is a bot; bots are not kicked or banned"));
        }

        [Test]
        public void HelpAndUnknownCommandsAreAnswered()
        {
            _console.Execute("help");
            _console.Execute("restart");
            _console.Execute("   ");

            Assert.That(_log.ToString(), Does.Contain("[Admin]   ban <player|product user id|address> [reason]"));
            Assert.That(_log.ToString(), Does.Contain("[Admin] unknown command 'restart'; type help to list the commands"));
        }

        [Test]
        public void WithoutABanFileTheBanIsKeptInMemory()
        {
            _bans = ServerBanList.InMemory();
            _console = Start(null);

            _console.Execute("ban 203.0.113.5");

            Assert.That(_bans.Entries, Has.Count.EqualTo(1));
            Assert.That(_log.ToString(), Does.Contain("[Admin] the ban is kept until the server stops (pass --ban-file to keep it in a file)"));
        }

        [Test]
        public void LinesFromTheInputRunOnTheTickAndTheEndIsReported()
        {
            Join(3, null, "203.0.113.5");
            _console = Start(new StringReader("kick 3\n"));

            Assert.That(_log.ToString(), Does.Contain("[Admin] reading commands from stdin; type help to list them"));
            var watch = Stopwatch.StartNew();
            while (!_log.ToString().Contains("[Admin] stdin closed", StringComparison.Ordinal) && watch.Elapsed < TimeSpan.FromSeconds(10))
            {
                _console.Tick(0, 0f);
                Thread.Sleep(1);
            }

            Assert.That(_control.Kicks, Has.Count.EqualTo(1));
            Assert.That(_log.ToString(), Does.Contain("[Admin] stdin closed; no more commands are read"));
        }

        private ServerAdminConsole Start(TextReader input)
        {
            var console = new ServerAdminConsole(input, _bans, () => _time);
            console.Start(new ServerContext(
                new NetLaunchOptions(NetLaunchMode.Server, NetLaunchOptions.AnyAddress, NetLaunchOptions.DefaultPort),
                "1.0.0",
                _log,
                TestMaps.Data(),
                _match,
                new ServerMessenger(new RecordingTransport()),
                _control,
                addresses: _addresses));
            return console;
        }

        private void Join(int id, string productUserId, string address)
        {
            var connection = new ConnectionId((ulong)id);
            _match.Join(connection, "device" + id, productUserId: productUserId);
            if (address != null)
            {
                _addresses.Set(connection, address);
            }
        }

        private sealed class FakeAddresses : IConnectionAddresses
        {
            private readonly Dictionary<ConnectionId, string> _addresses = new Dictionary<ConnectionId, string>();

            public void Set(ConnectionId connection, string address)
            {
                _addresses[connection] = address;
            }

            public bool TryGetRemoteAddress(ConnectionId connection, out string address)
            {
                return _addresses.TryGetValue(connection, out address);
            }
        }
    }
}
