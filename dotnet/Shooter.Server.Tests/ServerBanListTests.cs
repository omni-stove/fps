using System;
using System.IO;
using NUnit.Framework;

namespace Shooter.Server.Tests
{
    public sealed class ServerBanListTests
    {
        private const string _id = "0002aaaabbbbccccddddeeeeffff0001";

        private TempDirectory _directory;

        [SetUp]
        public void SetUp()
        {
            _directory = new TempDirectory();
        }

        [TearDown]
        public void TearDown()
        {
            _directory.Dispose();
        }

        [Test]
        public void AFileListsBansByIdOrAddress()
        {
            var bans = Load(
                "{ \"bans\": [\n"
                + "  { \"productUserId\": \"" + _id + "\", \"reason\": \"aimbot\", \"bannedAt\": \"2026-11-18T09:00:00Z\" },\n"
                + "  // comments and trailing commas are allowed for hand edits\n"
                + "  { \"address\": \"203.0.113.5\" },\n"
                + "] }");

            Assert.That(bans.Entries, Has.Count.EqualTo(2));
            Assert.That(bans.Entries[0].Kind, Is.EqualTo(ServerBanKind.ProductUserId));
            Assert.That(bans.Entries[0].Value, Is.EqualTo(_id));
            Assert.That(bans.Entries[0].Reason, Is.EqualTo("aimbot"));
            Assert.That(bans.Entries[0].BannedAt, Is.EqualTo(new DateTime(2026, 11, 18, 9, 0, 0, DateTimeKind.Utc)));
            Assert.That(bans.Entries[1].Kind, Is.EqualTo(ServerBanKind.Address));
            Assert.That(bans.Entries[1].Reason, Is.Null);
            Assert.That(bans.Entries[1].BannedAt, Is.Null);
            Assert.That(ServerAdminLog.Loaded(bans), Is.EqualTo("[Admin] ban list " + bans.Path + ": 2 ban(s)"));
        }

        [Test]
        public void AMissingFileIsAnEmptyListThatTheFirstSaveCreates()
        {
            var path = Path.Combine(_directory.Path, "nested", "bans.json");

            Assert.That(ServerBanList.TryLoad(path, out var bans, out var error), Is.True, error);
            Assert.That(bans.Entries, Is.Empty);
            Assert.That(File.Exists(path), Is.False);

            Assert.That(bans.Add(new ServerBanEntry(ServerBanKind.Address, "203.0.113.5", null, null)), Is.True);
            Assert.That(bans.TrySave(out error), Is.True, error);

            Assert.That(File.Exists(path), Is.True);
            Assert.That(File.Exists(path + ".tmp"), Is.False);
        }

        [Test]
        public void AnEmptyFileOrNoBansItemIsAnEmptyList()
        {
            Assert.That(Load("").Entries, Is.Empty);
            Assert.That(Load("{}").Entries, Is.Empty);
            Assert.That(Load("{ \"bans\": null }").Entries, Is.Empty);
        }

        [Test]
        public void SavingAndLoadingKeepsEveryBan()
        {
            var path = Path.Combine(_directory.Path, "bans.json");
            Assert.That(ServerBanList.TryLoad(path, out var bans, out var error), Is.True, error);
            var time = new DateTime(2026, 11, 18, 9, 30, 15, DateTimeKind.Utc);
            bans.Add(new ServerBanEntry(ServerBanKind.ProductUserId, _id, "said \"hi\" too loud", time));
            bans.Add(new ServerBanEntry(ServerBanKind.Address, "2001:db8::1", null, null));

            Assert.That(bans.TrySave(out error), Is.True, error);
            Assert.That(ServerBanList.TryLoad(path, out var reloaded, out error), Is.True, error);

            Assert.That(reloaded.Entries, Has.Count.EqualTo(2));
            Assert.That(reloaded.Entries[0].Reason, Is.EqualTo("said \"hi\" too loud"));
            Assert.That(reloaded.Entries[0].BannedAt, Is.EqualTo(time));
            Assert.That(reloaded.Entries[1].Value, Is.EqualTo("2001:db8::1"));
            Assert.That(bans.ToJson(), Is.EqualTo(
                "{\n"
                + "  \"bans\": [\n"
                + "    {\n"
                + "      \"productUserId\": \"" + _id + "\",\n"
                + "      \"reason\": \"said \\u0022hi\\u0022 too loud\",\n"
                + "      \"bannedAt\": \"2026-11-18T09:30:15Z\"\n"
                + "    },\n"
                + "    {\n"
                + "      \"address\": \"2001:db8::1\"\n"
                + "    }\n"
                + "  ]\n"
                + "}\n"));
        }

        [Test]
        public void TryFindMatchesAVerifiedIdInAnyCaseOrTheAddress()
        {
            var bans = Load("{ \"bans\": [ { \"productUserId\": \"" + _id + "\" }, { \"address\": \"203.0.113.5\" } ] }");

            Assert.That(bans.TryFind(_id.ToUpperInvariant(), null, out var byId), Is.True);
            Assert.That(byId.Kind, Is.EqualTo(ServerBanKind.ProductUserId));
            Assert.That(bans.TryFind(null, "203.0.113.5", out var byAddress), Is.True);
            Assert.That(byAddress.Kind, Is.EqualTo(ServerBanKind.Address));
            Assert.That(bans.TryFind(null, "::ffff:203.0.113.5", out _), Is.True, "an IPv4 address mapped into IPv6 is the same address");
            Assert.That(bans.TryFind("0002aaaabbbbccccddddeeeeffff0002", "198.51.100.7", out _), Is.False);
            Assert.That(bans.TryFind(null, null, out _), Is.False);
        }

        [Test]
        public void ARepeatedBanIsReadOnceAndAddingItAgainIsRefused()
        {
            var bans = Load("{ \"bans\": [ { \"address\": \"203.0.113.5\", \"reason\": \"first\" }, { \"address\": \"203.0.113.5\", \"reason\": \"second\" } ] }");

            Assert.That(bans.Entries, Has.Count.EqualTo(1));
            Assert.That(bans.Entries[0].Reason, Is.EqualTo("first"));
            Assert.That(bans.Add(new ServerBanEntry(ServerBanKind.Address, "203.0.113.5", "third", null)), Is.False);
            Assert.That(bans.Remove(ServerBanKind.Address, "203.0.113.5").Reason, Is.EqualTo("first"));
            Assert.That(bans.Remove(ServerBanKind.Address, "203.0.113.5"), Is.Null);
        }

        [TestCase("[]", "must be a JSON object with a \"bans\" array")]
        [TestCase("{ \"ban\": [] }", "has an unknown item \"ban\"")]
        [TestCase("{ \"bans\": {} }", "\"bans\" must be an array")]
        [TestCase("{ \"bans\": [ 1 ] }", "bans[0] must be an object")]
        [TestCase("{ \"bans\": [ {} ] }", "bans[0] must have exactly one of \"productUserId\" and \"address\"")]
        [TestCase("{ \"bans\": [ { \"address\": \"203.0.113.5\", \"productUserId\": \"" + _id + "\" } ] }", "bans[0] must have exactly one")]
        [TestCase("{ \"bans\": [ { \"address\": \"203.0.113.5\" }, { \"address\": \"somewhere\" } ] }", "bans[1].address is not a valid address (got 'somewhere')")]
        [TestCase("{ \"bans\": [ { \"productUserId\": \"abc\" } ] }", "bans[0].productUserId is not a valid product user id (got 'abc')")]
        [TestCase("{ \"bans\": [ { \"address\": 5 } ] }", "bans[0].address must be a string")]
        [TestCase("{ \"bans\": [ { \"address\": \"203.0.113.5\", \"note\": \"x\" } ] }", "bans[0] has an unknown item \"note\"")]
        [TestCase("{ \"bans\": [ { \"address\": \"203.0.113.5\", \"bannedAt\": \"yesterday\" } ] }", "bans[0].bannedAt must be a UTC time")]
        [TestCase("{ \"bans\": [", "is not valid JSON")]
        public void AnUnreadableFileNamesWhatIsWrong(string text, string expected)
        {
            var path = Write(text);

            Assert.That(ServerBanList.TryLoad(path, out var bans, out var error), Is.False);
            Assert.That(bans, Is.Null);
            Assert.That(error, Does.StartWith("ban list " + path));
            Assert.That(error, Does.Contain(expected));
        }

        [Test]
        public void ATooLongReasonInTheFileIsRefused()
        {
            var path = Write("{ \"bans\": [ { \"address\": \"203.0.113.5\", \"reason\": \"" + new string('x', ServerBanList.MaxReasonLength + 1) + "\" } ] }");

            Assert.That(ServerBanList.TryLoad(path, out _, out var error), Is.False);
            Assert.That(error, Does.Contain("bans[0].reason must be at most 200 characters"));
        }

        [Test]
        public void ControlCharactersInAReasonBecomeSpaces()
        {
            var entry = new ServerBanEntry(ServerBanKind.Address, "203.0.113.5", " line one\nline two ", null);

            Assert.That(entry.Reason, Is.EqualTo("line one line two"));
            Assert.That(new ServerBanEntry(ServerBanKind.Address, "203.0.113.5", "   ", null).Reason, Is.Null);
        }

        [TestCase(_id, ServerBanKind.ProductUserId, _id)]
        [TestCase("00020000000000000000000000000001", ServerBanKind.ProductUserId, "00020000000000000000000000000001")]
        [TestCase(" 203.0.113.5 ", ServerBanKind.Address, "203.0.113.5")]
        [TestCase("::ffff:203.0.113.5", ServerBanKind.Address, "203.0.113.5")]
        [TestCase("2001:DB8::1", ServerBanKind.Address, "2001:db8::1")]
        public void ATargetIsAnIdOrAnAddress(string text, ServerBanKind kind, string value)
        {
            Assert.That(ServerBanEntry.TryParseTarget(text, out var parsedKind, out var parsedValue), Is.True);
            Assert.That(parsedKind, Is.EqualTo(kind));
            Assert.That(parsedValue, Is.EqualTo(value));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("1")]
        [TestCase("localhost")]
        [TestCase("0002aaaabbbbccccddddeeeeffff000")]
        public void AnythingElseIsNotATarget(string text)
        {
            Assert.That(ServerBanEntry.TryParseTarget(text, out _, out _), Is.False);
            Assert.Throws<ArgumentException>(() => _ = new ServerBanEntry(ServerBanKind.Address, text, null, null));
        }

        [Test]
        public void WithoutAFileTheListLivesInMemory()
        {
            Assert.That(ServerBanList.TryLoad(null, out var bans, out var error), Is.True, error);
            Assert.That(bans.Path, Is.Null);
            bans.Add(new ServerBanEntry(ServerBanKind.Address, "203.0.113.5", null, null));

            Assert.That(bans.TrySave(out error), Is.True, error);
            Assert.That(ServerAdminLog.Loaded(ServerBanList.InMemory()), Is.EqualTo("[Admin] no ban list (--ban-file); bans last until the server stops"));
        }

        [Test]
        public void AFailedSaveSaysWhy()
        {
            var blocker = Path.Combine(_directory.Path, "file");
            File.WriteAllText(blocker, "");
            Assert.That(ServerBanList.TryLoad(Path.Combine(blocker, "bans.json"), out var bans, out var error), Is.True, error);
            bans.Add(new ServerBanEntry(ServerBanKind.Address, "203.0.113.5", null, null));

            Assert.That(bans.TrySave(out error), Is.False);
            Assert.That(error, Does.StartWith("cannot write the ban list " + bans.Path + ": "));
        }

        private ServerBanList Load(string text)
        {
            var path = Write(text);
            Assert.That(ServerBanList.TryLoad(path, out var bans, out var error), Is.True, error);
            return bans;
        }

        private string Write(string text)
        {
            var path = Path.Combine(_directory.Path, "bans.json");
            File.WriteAllText(path, text);
            return path;
        }
    }
}
