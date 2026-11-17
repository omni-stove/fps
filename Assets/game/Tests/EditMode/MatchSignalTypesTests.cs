using System;
using System.Collections.Generic;
using System.Numerics;
using Shooter.Core;
using NUnit.Framework;

namespace Shooter.Tests.EditMode
{
    public sealed class MatchSignalTypesTests
    {
        [TestCase(0f, 0f)]
        [TestCase(90f, 90f)]
        [TestCase(360f, 0f)]
        [TestCase(-90f, 270f)]
        [TestCase(725f, 5f)]
        [TestCase(-0f, 0f)]
        public void AMinimapContactWrapsItsHeadingIntoOneTurn(float heading, float expected)
        {
            var contact = new MinimapContact(MinimapContactKind.Enemy, Vector3.Zero, heading, Team.Bravo);

            Assert.That(contact.HeadingDegrees, Is.EqualTo(expected).Within(1e-3f));
            Assert.That(contact.HeadingDegrees, Is.GreaterThanOrEqualTo(0f).And.LessThan(360f));
        }

        [Test]
        public void AMinimapContactKeepsWhatItWasGiven()
        {
            var contact = new MinimapContact(MinimapContactKind.Objective, new Vector3(1f, 2f, 3f), 45f, null);

            Assert.That(contact.Kind, Is.EqualTo(MinimapContactKind.Objective));
            Assert.That(contact.Position, Is.EqualTo(new Vector3(1f, 2f, 3f)));
            Assert.That(contact.Team, Is.Null);
            Assert.That(contact, Is.EqualTo(new MinimapContact(MinimapContactKind.Objective, new Vector3(1f, 2f, 3f), 405f, null)));
            Assert.That(contact, Is.Not.EqualTo(new MinimapContact(MinimapContactKind.Objective, new Vector3(1f, 2f, 3f), 45f, Team.Alpha)));
        }

        [Test]
        public void AMinimapContactRejectsOutOfRangeValues()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new MinimapContact((MinimapContactKind)99, Vector3.Zero, 0f, null));
            Assert.Throws<ArgumentOutOfRangeException>(() => new MinimapContact(MinimapContactKind.Ally, Vector3.Zero, 0f, (Team)7));
            Assert.Throws<ArgumentOutOfRangeException>(() => new MinimapContact(MinimapContactKind.Ally, new Vector3(float.NaN, 0f, 0f), 0f, null));
            Assert.Throws<ArgumentOutOfRangeException>(() => new MinimapContact(MinimapContactKind.Ally, new Vector3(0f, 0f, float.PositiveInfinity), 0f, null));
            Assert.Throws<ArgumentOutOfRangeException>(() => new MinimapContact(MinimapContactKind.Ally, Vector3.Zero, float.NaN, null));
            Assert.Throws<ArgumentOutOfRangeException>(() => new MinimapContact(MinimapContactKind.Ally, Vector3.Zero, float.NegativeInfinity, null));
        }

        [Test]
        public void AMinimapStatusCopiesItsContacts()
        {
            var contacts = new List<MinimapContact> { new MinimapContact(MinimapContactKind.Ally, Vector3.One, 10f, Team.Alpha) };
            var status = new MinimapStatus(new Vector3(5f, 0f, 5f), -45f, contacts);
            contacts.Add(new MinimapContact(MinimapContactKind.Ping, Vector3.Zero, 0f, null));

            Assert.That(status.Contacts, Has.Count.EqualTo(1));
            Assert.That(status.SelfHeadingDegrees, Is.EqualTo(315f).Within(1e-3f));
            Assert.That(status, Is.EqualTo(new MinimapStatus(new Vector3(5f, 0f, 5f), 315f, new[] { contacts[0] })));
            Assert.That(new MinimapStatus(Vector3.Zero, 0f, null).Contacts, Is.Empty);
            Assert.That(default(MinimapStatus).Contacts, Is.Empty);
        }

        [Test]
        public void AMinimapStatusRejectsANonFiniteSelf()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new MinimapStatus(new Vector3(float.NaN, 0f, 0f), 0f, null));
            Assert.Throws<ArgumentOutOfRangeException>(() => new MinimapStatus(Vector3.Zero, float.PositiveInfinity, null));
        }

        [Test]
        public void AChatLineKeepsItsSenderChannelTextAndTime()
        {
            var line = new ChatLine(3, ChatChannel.Team, "rotate A", 42.25d);

            Assert.That(line.SenderId, Is.EqualTo(3));
            Assert.That(line.Channel, Is.EqualTo(ChatChannel.Team));
            Assert.That(line.Text, Is.EqualTo("rotate A"));
            Assert.That(line.SentAtSeconds, Is.EqualTo(42.25d));
            Assert.That(line, Is.EqualTo(new ChatLine(3, ChatChannel.Team, "rotate A", 42.25d)));
            Assert.That(line, Is.Not.EqualTo(new ChatLine(3, ChatChannel.All, "rotate A", 42.25d)));
            Assert.That(default(ChatLine).Text, Is.Empty);
        }

        [Test]
        public void AChatLineDoesNotPutTheTextInItsDescription()
        {
            Assert.That(new ChatLine(3, ChatChannel.All, "secret", 1d).ToString(), Does.Not.Contain("secret").And.Contain("length=6"));
        }

        [Test]
        public void AChatLineRejectsOutOfRangeValues()
        {
            Assert.Throws<ArgumentNullException>(() => new ChatLine(1, ChatChannel.All, null, 0d));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ChatLine(1, (ChatChannel)5, "hi", 0d));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ChatLine(1, ChatChannel.All, "hi", -1d));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ChatLine(1, ChatChannel.All, "hi", double.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ChatLine(1, ChatChannel.All, "hi", double.PositiveInfinity));
        }

        [Test]
        public void AVoiceStatusCopiesTheSpeakers()
        {
            var speaking = new List<int> { 2, 5 };
            var status = new VoiceStatus(true, speaking, true);
            speaking.Add(9);

            Assert.That(status.IsLocalSpeaking, Is.True);
            Assert.That(status.MicrophoneAvailable, Is.True);
            Assert.That(status.SpeakingIds, Is.EqualTo(new[] { 2, 5 }));
            Assert.That(status, Is.EqualTo(new VoiceStatus(true, new[] { 2, 5 }, true)));
            Assert.That(status, Is.Not.EqualTo(new VoiceStatus(true, new[] { 5, 2 }, true)));
            Assert.That(new VoiceStatus(false, null, false).SpeakingIds, Is.Empty);
            Assert.That(default(VoiceStatus).SpeakingIds, Is.Empty);
        }

        [Test]
        public void AVoiceStatusRejectsARepeatedSpeakerAndSpeakingWithoutAMicrophone()
        {
            Assert.Throws<ArgumentException>(() => new VoiceStatus(false, new[] { 3, 3 }, true));
            Assert.Throws<ArgumentException>(() => new VoiceStatus(true, null, false));
        }

        [Test]
        public void AMapVoteStatusKeepsTheCandidatesTheVoteAndTheDecision()
        {
            var candidates = new List<MapVoteCandidate> { new MapVoteCandidate("dust", 2), new MapVoteCandidate("port", 1) };
            var status = new MapVoteStatus(candidates, "port", 7.5f, "dust");
            candidates.Clear();

            Assert.That(status.Candidates, Is.EqualTo(new[] { new MapVoteCandidate("dust", 2), new MapVoteCandidate("port", 1) }));
            Assert.That(status.MyVote, Is.EqualTo("port"));
            Assert.That(status.HasVoted, Is.True);
            Assert.That(status.RemainingSeconds, Is.EqualTo(7.5f));
            Assert.That(status.DecidedMapId, Is.EqualTo("dust"));
            Assert.That(status.IsDecided, Is.True);
        }

        [Test]
        public void AMapVoteStatusWithoutAVoteOrDecisionReadsEmpty()
        {
            var status = new MapVoteStatus(new[] { new MapVoteCandidate("dust", 0) }, null, 30f, string.Empty);

            Assert.That(status.MyVote, Is.Empty);
            Assert.That(status.HasVoted, Is.False);
            Assert.That(status.DecidedMapId, Is.Empty);
            Assert.That(status.IsDecided, Is.False);
            Assert.That(status, Is.EqualTo(new MapVoteStatus(new[] { new MapVoteCandidate("dust", 0) }, string.Empty, 30f, null)));
            Assert.That(default(MapVoteStatus).Candidates, Is.Empty);
            Assert.That(default(MapVoteStatus).MyVote, Is.Empty);
        }

        [Test]
        public void AMapVoteRejectsOutOfRangeValues()
        {
            var dust = new[] { new MapVoteCandidate("dust", 0) };

            Assert.Throws<ArgumentException>(() => new MapVoteCandidate(null, 0));
            Assert.Throws<ArgumentException>(() => new MapVoteCandidate(" ", 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new MapVoteCandidate("dust", -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new MapVoteStatus(dust, null, -0.1f, null));
            Assert.Throws<ArgumentOutOfRangeException>(() => new MapVoteStatus(dust, null, float.NaN, null));
            Assert.Throws<ArgumentOutOfRangeException>(() => new MapVoteStatus(dust, null, float.PositiveInfinity, null));
            Assert.Throws<ArgumentException>(() => new MapVoteStatus(new[] { new MapVoteCandidate("dust", 0), new MapVoteCandidate("dust", 1) }, null, 1f, null));
            Assert.Throws<ArgumentException>(() => new MapVoteStatus(new[] { default(MapVoteCandidate) }, null, 1f, null));
            Assert.Throws<ArgumentException>(() => new MapVoteStatus(dust, "port", 1f, null));
            Assert.Throws<ArgumentException>(() => new MapVoteStatus(dust, null, 1f, "port"));
        }

        [Test]
        public void ASpectateStatusKeepsTheTargetCountAndReason()
        {
            var status = new SpectateStatus(7, 3, SpectateReason.Dead);

            Assert.That(status.TargetId, Is.EqualTo(7));
            Assert.That(status.CandidateCount, Is.EqualTo(3));
            Assert.That(status.Reason, Is.EqualTo(SpectateReason.Dead));
            Assert.That(status, Is.EqualTo(new SpectateStatus(7, 3, SpectateReason.Dead)));
            Assert.That(status, Is.Not.EqualTo(new SpectateStatus(7, 3, SpectateReason.BetweenRounds)));
            Assert.That(new SpectateStatus(null, 0, SpectateReason.Dead).TargetId, Is.Null, "nobody left to watch");
        }

        [Test]
        public void ASpectateStatusRejectsOutOfRangeValues()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new SpectateStatus(null, -1, SpectateReason.Dead));
            Assert.Throws<ArgumentOutOfRangeException>(() => new SpectateStatus(4, 0, SpectateReason.Dead));
            Assert.Throws<ArgumentOutOfRangeException>(() => new SpectateStatus(4, 1, (SpectateReason)9));
        }

        [Test]
        public void APartyStatusCopiesItsMembersAndKeepsTheCodeAndLeader()
        {
            var members = new List<PartyMember> { new PartyMember("Ann", true), new PartyMember("Bo", false) };
            var status = new PartyStatus(PartyStage.Searching, members, "lobby_7-A", true);
            members.Add(new PartyMember("Cy", false));

            Assert.That(status.Stage, Is.EqualTo(PartyStage.Searching));
            Assert.That(status.Members, Is.EqualTo(new[] { new PartyMember("Ann", true), new PartyMember("Bo", false) }));
            Assert.That(status.Members[1].Name, Is.EqualTo("Bo"));
            Assert.That(status.Members[1].IsLeader, Is.False);
            Assert.That(status.InviteCode, Is.EqualTo("lobby_7-A"));
            Assert.That(status.HasInviteCode, Is.True);
            Assert.That(status.IsLocalLeader, Is.True);
            Assert.That(status, Is.EqualTo(new PartyStatus(PartyStage.Searching, new[] { new PartyMember("Ann", true), new PartyMember("Bo", false) }, "lobby_7-A", true)));
            Assert.That(status, Is.Not.EqualTo(new PartyStatus(PartyStage.InParty, new[] { new PartyMember("Ann", true), new PartyMember("Bo", false) }, "lobby_7-A", true)));
            Assert.That(status, Is.Not.EqualTo(new PartyStatus(PartyStage.Searching, new[] { new PartyMember("Bo", false), new PartyMember("Ann", true) }, "lobby_7-A", true)));
        }

        [Test]
        public void APartyStatusOutsideAPartyReadsEmpty()
        {
            var status = new PartyStatus(PartyStage.None, null, null, false);

            Assert.That(status.Members, Is.Empty);
            Assert.That(status.InviteCode, Is.Empty);
            Assert.That(status.HasInviteCode, Is.False);
            Assert.That(status, Is.EqualTo(new PartyStatus(PartyStage.None, Array.Empty<PartyMember>(), string.Empty, false)));
            Assert.That(status, Is.EqualTo(default(PartyStatus)));
            Assert.That(default(PartyStatus).Members, Is.Empty);
            Assert.That(default(PartyMember).Name, Is.Empty);
            Assert.That(new PartyStatus(PartyStage.Joining, null, null, false).Members, Is.Empty, "the lobby is not read yet while joining");
        }

        [Test]
        public void APartyStatusRejectsOutOfRangeValues()
        {
            var leader = new[] { new PartyMember("Ann", true) };

            Assert.Throws<ArgumentNullException>(() => new PartyMember(null, false));
            Assert.Throws<ArgumentException>(() => new PartyMember(" ", false));
            Assert.Throws<ArgumentOutOfRangeException>(() => new PartyStatus((PartyStage)9, null, null, false));
            Assert.Throws<ArgumentOutOfRangeException>(() => new PartyStatus((PartyStage)(-1), leader, "lobby", false));
            Assert.Throws<ArgumentException>(() => new PartyStatus(PartyStage.InParty, new[] { default(PartyMember) }, null, false));
            Assert.Throws<ArgumentException>(() => new PartyStatus(PartyStage.InParty, new[] { new PartyMember("Ann", true), new PartyMember("Bo", true) }, null, false));
            Assert.Throws<ArgumentException>(() => new PartyStatus(PartyStage.InParty, leader, "has space", false));
            Assert.Throws<ArgumentException>(() => new PartyStatus(PartyStage.InParty, leader, new string('a', ConnectionExtras.MaxPartyIdLength + 1), false));
            Assert.Throws<ArgumentException>(() => new PartyStatus(PartyStage.InParty, new[] { new PartyMember("Bo", false) }, null, true));
            Assert.Throws<ArgumentException>(() => new PartyStatus(PartyStage.None, leader, null, false));
            Assert.Throws<ArgumentException>(() => new PartyStatus(PartyStage.None, null, "lobby", false));
            Assert.Throws<ArgumentException>(() => new PartyStatus(PartyStage.None, null, null, true));
            Assert.DoesNotThrow(() => new PartyStatus(PartyStage.InParty, leader, new string('a', ConnectionExtras.MaxPartyIdLength), false));
        }

        [Test]
        public void TheJoinCodeOutcomesAreTheFiveTheUiShows()
        {
            Assert.That(
                Enum.GetValues(typeof(JoinCodeOutcome)),
                Is.EqualTo(new[] { JoinCodeOutcome.Joined, JoinCodeOutcome.NotFound, JoinCodeOutcome.Full, JoinCodeOutcome.VersionMismatch, JoinCodeOutcome.Failed }));
            Assert.That(Enum.IsDefined(typeof(JoinCodeOutcome), (JoinCodeOutcome)5), Is.False);
        }
    }
}
