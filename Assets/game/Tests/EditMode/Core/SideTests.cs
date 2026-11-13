using System;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    // Unity の EditMode テストと dotnet/Fps.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class SideTests
    {
        [Test]
        public void TeamsMapToTheFirstTwoIds()
        {
            Assert.That(Side.FromTeam(Team.Alpha), Is.EqualTo(Side.Alpha));
            Assert.That(Side.FromTeam(Team.Bravo), Is.EqualTo(Side.Bravo));
            Assert.That(Side.Alpha.Id, Is.EqualTo(0));
            Assert.That(Side.Bravo.Id, Is.EqualTo(1));
            Assert.That(Side.Alpha.ToTeam(), Is.EqualTo(Team.Alpha));
            Assert.That(Side.Bravo.ToTeam(), Is.EqualTo(Team.Bravo));
        }

        [Test]
        public void NullableTeamsConvertBothWays()
        {
            Assert.That(Side.FromTeam((Team?)null), Is.Null);
            Assert.That(Side.FromTeam((Team?)Team.Bravo), Is.EqualTo((Side?)Side.Bravo));
            Assert.That(Side.ToTeam(null), Is.Null);
            Assert.That(Side.ToTeam(Side.Alpha), Is.EqualTo((Team?)Team.Alpha));
        }

        [Test]
        public void OtherSidesAreNotTeams()
        {
            var side = new Side(2);

            Assert.That(side.IsTeam, Is.False);
            Assert.That(side.TryGetTeam(out _), Is.False);
            Assert.Throws<InvalidOperationException>(() => side.ToTeam());
            Assert.Throws<InvalidOperationException>(() => Side.ToTeam(side));
        }

        [Test]
        public void EqualityAndOrderFollowTheId()
        {
            Assert.That(new Side(3) == new Side(3), Is.True);
            Assert.That(new Side(3) != new Side(4), Is.True);
            var first = new Side(3);
            var second = new Side(3);
            Assert.That(first.GetHashCode(), Is.EqualTo(second.GetHashCode()));
            Assert.That(new Side(3).CompareTo(new Side(4)), Is.LessThan(0));
            Assert.That(new Side(5).Equals((object)new Side(5)), Is.True);
        }

        [Test]
        public void RejectsInvalidValues()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new Side(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => Side.FromTeam((Team)7));
        }
    }
}
