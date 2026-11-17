using System;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    public sealed class TeamAssignerTests
    {
        [Test]
        public void StartsWithAlphaWhenEverythingIsEven()
        {
            Assert.That(TeamAssigner.Choose(0, 0, 0, 0), Is.EqualTo(Team.Alpha));
            Assert.That(TeamAssigner.Choose(2, 2, 3, 3), Is.EqualTo(Team.Alpha));
        }

        [Test]
        public void PicksTheTeamWithFewerPlayers()
        {
            Assert.That(TeamAssigner.Choose(1, 0, 0, 0), Is.EqualTo(Team.Bravo));
            Assert.That(TeamAssigner.Choose(1, 2, 0, 0), Is.EqualTo(Team.Alpha));
        }

        [Test]
        public void PlayerCountWinsOverScore()
        {
            // 点の低いほうより人数の少ないほうを優先する
            Assert.That(TeamAssigner.Choose(1, 2, 9, 0), Is.EqualTo(Team.Alpha));
            Assert.That(TeamAssigner.Choose(2, 1, 0, 9), Is.EqualTo(Team.Bravo));
        }

        [Test]
        public void EvenPlayersGoToTheLowerScore()
        {
            Assert.That(TeamAssigner.Choose(2, 2, 5, 3), Is.EqualTo(Team.Bravo));
            Assert.That(TeamAssigner.Choose(2, 2, 3, 5), Is.EqualTo(Team.Alpha));
        }

        [Test]
        public void RejectsNegativeCounts()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => TeamAssigner.Choose(-1, 0, 0, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => TeamAssigner.Choose(0, -1, 0, 0));
        }

        [Test]
        public void APartyMemberJoinsTheTeamOfTheParty()
        {
            // 人数でも点数でも Bravo になるところを、同じパーティの人がいる Alpha に入れる
            Assert.That(TeamAssigner.Choose(2, 1, 5, 0, 1, 0, 2, 1, 4), Is.EqualTo(Team.Alpha));
            Assert.That(TeamAssigner.Choose(1, 2, 0, 5, 0, 1, 1, 2, 4), Is.EqualTo(Team.Bravo));
        }

        [Test]
        public void APartyMemberGoesToTheSmallerTeamWhenThePartyTeamIsFull()
        {
            Assert.That(TeamAssigner.Choose(4, 3, 0, 0, 2, 0, 4, 3, 4), Is.EqualTo(Team.Bravo));
            Assert.That(TeamAssigner.Choose(3, 4, 0, 0, 0, 1, 3, 4, 4), Is.EqualTo(Team.Alpha));
        }

        [Test]
        public void BotsDoNotFillThePartyTeam()
        {
            // Alpha は Bot を入れて 4 人だが、人は 1 人なので定員 2 に届いていない
            Assert.That(TeamAssigner.Choose(4, 1, 0, 0, 1, 0, 1, 1, 2), Is.EqualTo(Team.Alpha));
        }

        [Test]
        public void WithoutPartyMembersItChoosesLikeEveryoneElse()
        {
            Assert.That(TeamAssigner.Choose(1, 0, 0, 0, 0, 0, 1, 0, 4), Is.EqualTo(Team.Bravo));
            Assert.That(TeamAssigner.Choose(2, 2, 5, 3, 0, 0, 2, 2, 4), Is.EqualTo(Team.Bravo));
        }

        [Test]
        public void ASplitPartyFollowsTheTeamWithMoreOfIt()
        {
            Assert.That(TeamAssigner.PartyTeam(2, 1), Is.EqualTo(Team.Alpha));
            Assert.That(TeamAssigner.PartyTeam(0, 1), Is.EqualTo(Team.Bravo));
            Assert.That(TeamAssigner.PartyTeam(0, 0), Is.Null);
            Assert.That(TeamAssigner.PartyTeam(1, 1), Is.Null);

            // 同じ数ずつ分かれていれば、パーティを見ずに少ない方へ入れる
            Assert.That(TeamAssigner.Choose(2, 1, 0, 0, 1, 1, 2, 1, 4), Is.EqualTo(Team.Bravo));
        }

        [Test]
        public void TeamCapacityIsHalfTheMatchRoundedUp()
        {
            Assert.That(TeamAssigner.TeamCapacity(1), Is.EqualTo(1));
            Assert.That(TeamAssigner.TeamCapacity(7), Is.EqualTo(4));
            Assert.That(TeamAssigner.TeamCapacity(8), Is.EqualTo(4));
            Assert.Throws<ArgumentOutOfRangeException>(() => TeamAssigner.TeamCapacity(0));
        }

        [Test]
        public void RejectsBadPartyArguments()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => TeamAssigner.PartyTeam(-1, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => TeamAssigner.PartyTeam(0, -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => TeamAssigner.Choose(0, 0, 0, 0, 0, 0, -1, 0, 4));
            Assert.Throws<ArgumentOutOfRangeException>(() => TeamAssigner.Choose(0, 0, 0, 0, 0, 0, 0, -1, 4));
            Assert.Throws<ArgumentOutOfRangeException>(() => TeamAssigner.Choose(0, 0, 0, 0, 0, 0, 0, 0, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => TeamAssigner.Choose(-1, 0, 0, 0, 1, 0, 0, 0, 4));
        }
    }
}
