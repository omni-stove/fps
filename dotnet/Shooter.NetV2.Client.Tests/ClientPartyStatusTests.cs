using System;
using NUnit.Framework;
using Shooter.Core;
using CorePartyStatus = Shooter.Core.PartyStatus;

namespace Shooter.NetV2.Tests
{
    public sealed class ClientPartyStatusTests
    {
        private const string _self = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        private const string _friend = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";

        [Test]
        public void OutsideAPartyNothingIsShown()
        {
            var shown = ClientPartyStatus.ToShown(PartyStatus.None);

            Assert.That(shown, Is.EqualTo(new CorePartyStatus(PartyStage.None, null, null, false)));
        }

        [TestCase(PartyPhase.Joining)]
        [TestCase(PartyPhase.Leaving)]
        public void JoiningAndLeavingAreShownAsWaiting(PartyPhase phase)
        {
            var shown = ClientPartyStatus.ToShown(Status(phase, _self, PartyState.Idle, _self));

            Assert.That(shown.Stage, Is.EqualTo(PartyStage.Joining));
            Assert.That(shown.Members, Is.Empty);
            Assert.That(shown.HasInviteCode, Is.False);
        }

        [Test]
        public void InAPartyTheMembersLeaderAndInviteCodeAreShown()
        {
            var shown = ClientPartyStatus.ToShown(Status(PartyPhase.InParty, _self, PartyState.Idle, _self, _friend));

            Assert.That(shown.Stage, Is.EqualTo(PartyStage.InParty));
            Assert.That(shown.Members, Is.EqualTo(new[] { new PartyMember(_self, true), new PartyMember(_friend, false) }));
            Assert.That(shown.InviteCode, Is.EqualTo("party-1"));
            Assert.That(shown.IsLocalLeader, Is.True);
        }

        [Test]
        public void AFollowerIsNotTheLocalLeader()
        {
            var shown = ClientPartyStatus.ToShown(Status(PartyPhase.InParty, _friend, PartyState.Idle, _self, _friend));

            Assert.That(shown.IsLocalLeader, Is.False);
            Assert.That(shown.Members[0].IsLeader, Is.False);
            Assert.That(shown.Members[1].IsLeader, Is.True);
        }

        [Test]
        public void TheLeaderSearchingIsShownAsSearching()
        {
            var shown = ClientPartyStatus.ToShown(Status(PartyPhase.InParty, _self, PartyState.Searching, _self));

            Assert.That(shown.Stage, Is.EqualTo(PartyStage.Searching));
        }

        [Test]
        public void ALeaderNotYetInTheMemberListIsNotShownAsLeading()
        {
            var status = new PartyStatus(
                PartyPhase.InParty, "party-1", _self, 4, new[] { _friend }, _self, PartyState.Idle, PartyError.None);

            var shown = ClientPartyStatus.ToShown(status);

            Assert.That(shown.IsLocalLeader, Is.False);
            Assert.That(shown.Members, Is.EqualTo(new[] { new PartyMember(_friend, false) }));
        }

        [Test]
        public void ANullStatusIsRefused()
        {
            Assert.Throws<ArgumentNullException>(() => ClientPartyStatus.ToShown(null));
        }

        private static PartyStatus Status(PartyPhase phase, string leader, PartyState state, params string[] members)
        {
            return new PartyStatus(phase, "party-1", _self, 4, members, leader, state, PartyError.None);
        }
    }
}
