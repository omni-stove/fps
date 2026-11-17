using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class PartyLobbyTests
    {
        private const string _a = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        private const string _b = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
        private const string _c = "cccccccccccccccccccccccccccccccc";

        private static LobbyInfo Party(string owner, params string[] members)
        {
            return new LobbyInfo("party-1", owner, PartyLobby.DefaultMaxMembers, members);
        }

        private static Dictionary<string, LobbyAttribute> ByKey(IReadOnlyList<LobbyAttribute> attributes)
        {
            var byKey = new Dictionary<string, LobbyAttribute>();
            foreach (var attribute in attributes)
            {
                byKey.Add(attribute.Key, attribute);
            }

            return byKey;
        }

        [Test]
        public void TheBucketIsSeparateFromTheServers()
        {
            Assert.That(PartyLobby.BucketId("0.3.0"), Is.EqualTo("shooter-party:0.3.0"));
            Assert.That(PartyLobby.BucketId(" "), Is.EqualTo("shooter-party:0"));
            Assert.That(PartyLobby.BucketId("0.3.0"), Is.Not.EqualTo(ServerLobby.BucketId("0.3.0")));
        }

        [Test]
        public void TheSizeLimitIsTwoToSixteen()
        {
            Assert.That(PartyLobby.DefaultMaxMembers, Is.EqualTo(4));
            Assert.That(PartyLobby.IsValidMaxMembers(1), Is.False);
            Assert.That(PartyLobby.IsValidMaxMembers(2), Is.True);
            Assert.That(PartyLobby.IsValidMaxMembers(PartyLobby.DefaultMaxMembers), Is.True);
            Assert.That(PartyLobby.IsValidMaxMembers(16), Is.True);
            Assert.That(PartyLobby.IsValidMaxMembers(17), Is.False);
        }

        [Test]
        public void ThePartyCarriesItsVersion()
        {
            var attributes = PartyLobby.LobbyAttributes("0.3.0");
            var same = new LobbyInfo("party-1", _a, 4, new[] { _a }, attributes);
            var old = new LobbyInfo("party-2", _a, 4, new[] { _a }, new[] { LobbyAttribute.OfText("version", "0.2.0") });
            var bare = Party(_a, _a);

            Assert.That(attributes.Count, Is.EqualTo(1));
            Assert.That(ByKey(attributes)["version"].Text, Is.EqualTo("0.3.0"));
            Assert.That(PartyLobby.IsSameVersion(same, "0.3.0"), Is.True);
            Assert.That(PartyLobby.IsSameVersion(old, "0.3.0"), Is.False);
            Assert.That(PartyLobby.IsSameVersion(bare, "0.3.0"), Is.True, "a service that returns no attributes is trusted");
            Assert.Throws<ArgumentNullException>(() => PartyLobby.IsSameVersion(null, "0.3.0"));
        }

        [Test]
        public void TheOwnerLeadsWhileStillAMember()
        {
            Assert.That(PartyLobby.Leader(Party(_b, _a, _b, _c)), Is.EqualTo(_b));
            Assert.That(PartyLobby.Leader(Party(_b.ToUpperInvariant(), _a, _b)), Is.EqualTo(_b.ToUpperInvariant()));
        }

        [Test]
        public void TheFirstRemainingMemberLeadsWhenTheOwnerHasLeft()
        {
            Assert.That(PartyLobby.Leader(Party(_a, _b, _c)), Is.EqualTo(_b));
            Assert.That(PartyLobby.Leader(Party(_a)), Is.Null);
            Assert.Throws<ArgumentNullException>(() => PartyLobby.Leader(null));
        }

        [Test]
        public void TheLeaderWritesTheServerOnlyInAMatch()
        {
            var idle = ByKey(PartyLobby.LeaderAttributes(PartyState.Idle, null, 3));
            var searching = ByKey(PartyLobby.LeaderAttributes(PartyState.Searching, "203.0.113.2:7790", 3));
            var match = ByKey(PartyLobby.LeaderAttributes(PartyState.InMatch, "203.0.113.2:7790", 3));

            Assert.That(idle.Count, Is.EqualTo(1));
            Assert.That(idle["party_state"].Text, Is.EqualTo("idle"));
            Assert.That(searching.Count, Is.EqualTo(1));
            Assert.That(searching["party_state"].Text, Is.EqualTo("searching"));
            Assert.That(match.Count, Is.EqualTo(3));
            Assert.That(match["party_state"].Text, Is.EqualTo("match"));
            Assert.That(match["party_server"].Text, Is.EqualTo("203.0.113.2:7790"));
            Assert.That(match["party_match"].IsText, Is.False);
            Assert.That(match["party_match"].Number, Is.EqualTo(3));
            Assert.Throws<ArgumentException>(() => PartyLobby.LeaderAttributes(PartyState.InMatch, "lobby.example:7777", 1));
            Assert.Throws<ArgumentException>(() => PartyLobby.LeaderAttributes(PartyState.InMatch, null, 1));
        }

        [Test]
        public void TheLeaderAttributesReadBack()
        {
            Assert.That(
                PartyLobby.TryReadLeader(PartyLobby.LeaderAttributes(PartyState.InMatch, "203.0.113.2:7790", 7), out var state, out var endpoint, out var match),
                Is.True);
            Assert.That(state, Is.EqualTo(PartyState.InMatch));
            Assert.That(endpoint, Is.EqualTo("203.0.113.2:7790"));
            Assert.That(match, Is.EqualTo(7));

            Assert.That(PartyLobby.TryReadLeader(PartyLobby.LeaderAttributes(PartyState.Searching, null, 7), out state, out endpoint, out match), Is.True);
            Assert.That(state, Is.EqualTo(PartyState.Searching));
            Assert.That(endpoint, Is.Null);
            Assert.That(match, Is.Zero);
        }

        [Test]
        public void ALeaderWithoutAttributesIsIdle()
        {
            Assert.That(PartyLobby.TryReadLeader(Array.Empty<LobbyAttribute>(), out var state, out var endpoint, out _), Is.True);
            Assert.That(state, Is.EqualTo(PartyState.Idle));
            Assert.That(endpoint, Is.Null);
            Assert.Throws<ArgumentNullException>(() => PartyLobby.TryReadLeader(null, out _, out _, out _));
        }

        [Test]
        public void UnreadableLeaderAttributesAreRejectedAsIdle()
        {
            var unknown = new[] { LobbyAttribute.OfText("party_state", "dancing") };
            var numeric = new[] { LobbyAttribute.OfNumber("party_state", 2) };
            var noServer = new[] { LobbyAttribute.OfText("party_state", "match"), LobbyAttribute.OfNumber("party_match", 1) };
            var badServer = new[]
            {
                LobbyAttribute.OfText("party_state", "match"),
                LobbyAttribute.OfText("party_server", "0.0.0.0:7777"),
                LobbyAttribute.OfNumber("party_match", 1),
            };
            var noMatch = new[] { LobbyAttribute.OfText("party_state", "match"), LobbyAttribute.OfText("party_server", "203.0.113.2:7790") };

            foreach (var attributes in new[] { unknown, numeric, noServer, badServer, noMatch })
            {
                Assert.That(PartyLobby.TryReadLeader(attributes, out var state, out var endpoint, out var match), Is.False);
                Assert.That(state, Is.EqualTo(PartyState.Idle));
                Assert.That(endpoint, Is.Null);
                Assert.That(match, Is.Zero);
            }
        }

        [Test]
        public void MembersConnectToTheLeadersServerOverUdp()
        {
            var options = PartyLobby.ConnectOptions("203.0.113.2:7790");

            Assert.That(options.Mode, Is.EqualTo(NetLaunchMode.Client));
            Assert.That(options.Transport, Is.EqualTo(NetTransportKind.Udp));
            Assert.That(options.Address, Is.EqualTo("203.0.113.2"));
            Assert.That(options.Port, Is.EqualTo(7790));
            Assert.Throws<ArgumentException>(() => PartyLobby.ConnectOptions("not-an-endpoint"));
        }

        [Test]
        public void TheStateTextsRoundTrip()
        {
            // Unity の .NET には Enum.GetValues<TEnum>() が無いので、dotnet でだけ出る CA2263 を止める
#pragma warning disable CA2263
            foreach (PartyState state in Enum.GetValues(typeof(PartyState)))
#pragma warning restore CA2263
            {
                Assert.That(PartyLobby.TryParseState(PartyLobby.StateText(state), out var parsed), Is.True);
                Assert.That(parsed, Is.EqualTo(state));
            }

            Assert.That(PartyLobby.TryParseState(null, out _), Is.False);
            Assert.That(PartyLobby.TryParseState("MATCH", out _), Is.False);
        }

        [Test]
        public void EachMemberCarriesItsSkill()
        {
            var attributes = PartyLobby.SkillAttributes(1350);

            Assert.That(attributes.Count, Is.EqualTo(1));
            Assert.That(ByKey(attributes)["party_skill"].IsText, Is.False);
            Assert.That(ByKey(attributes)["party_skill"].Number, Is.EqualTo(1350));
            Assert.That(PartyLobby.ReadSkill(attributes), Is.EqualTo(1350));
            Assert.That(PartyLobby.ReadSkill(PartyLobby.SkillAttributes(-20)), Is.EqualTo(-20));
            Assert.That(PartyLobby.ReadSkill(PartyLobby.LeaderAttributes(PartyState.Idle, null, 0)), Is.Null);
            Assert.Throws<ArgumentNullException>(() => PartyLobby.ReadSkill(null));
        }

        [Test]
        public void AnUnreadableSkillCountsAsNone()
        {
            Assert.That(PartyLobby.ReadSkill(System.Array.Empty<LobbyAttribute>()), Is.Null);
            Assert.That(PartyLobby.ReadSkill(new[] { LobbyAttribute.OfText("party_skill", "1350") }), Is.Null);
            Assert.That(PartyLobby.ReadSkill(new[] { LobbyAttribute.OfNumber("party_skill", 3000000000L) }), Is.Null);
            Assert.That(PartyLobby.ReadSkill(new[] { LobbyAttribute.OfNumber("party_skill", -3000000000L) }), Is.Null);
        }
    }
}
