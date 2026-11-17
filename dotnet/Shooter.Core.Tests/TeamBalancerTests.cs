using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    public sealed class TeamBalancerTests
    {
        // 定数配列を直接渡すと CA1861 になるので params で受ける
        private static int[] Expected(params int[] playerIds) => playerIds;

        [Test]
        public void MovesNobodyWhileTheDifferenceIsWithinTheLimit()
        {
            Assert.That(TeamBalancer.Plan(Solo(1, 2, 3), Solo(4, 5), 1), Is.Empty);
            Assert.That(TeamBalancer.Plan(Solo(1, 2, 3, 4), Solo(5, 6), 2), Is.Empty);
            Assert.That(TeamBalancer.Plan(Solo(), Solo(), 1), Is.Empty);
        }

        [Test]
        public void MovesFromTheLargerTeamUntilTheDifferenceFits()
        {
            var moves = TeamBalancer.Plan(Solo(1, 2, 3, 4, 5), Solo(6), 1);

            Assert.That(Ids(moves), Is.EqualTo(Expected(5, 4)));
            Assert.That(moves.All(m => m.From == Team.Alpha && m.To == Team.Bravo), Is.True);
        }

        [Test]
        public void StopsAsSoonAsTheDifferenceFitsAWiderLimit()
        {
            // 5 対 0 で上限 2: 1 人移すと 4 対 1 (差 3)、2 人で 3 対 2 (差 1)
            var moves = TeamBalancer.Plan(Solo(1, 2, 3, 4, 5), Solo(), 2);

            Assert.That(Ids(moves), Is.EqualTo(Expected(5, 4)));
        }

        [Test]
        public void MovesFromBravoToAlphaWhenBravoIsLarger()
        {
            var moves = TeamBalancer.Plan(Solo(1), Solo(2, 3, 4, 5), 1);

            Assert.That(Ids(moves), Is.EqualTo(Expected(5)));
            Assert.That(moves[0].From, Is.EqualTo(Team.Bravo));
            Assert.That(moves[0].To, Is.EqualTo(Team.Alpha));
        }

        [Test]
        public void MovesTheMostRecentlyJoinedFirstRegardlessOfPlayerId()
        {
            // 並びが入った順。Id の大小ではなく、後ろにいる人から移す
            var moves = TeamBalancer.Plan(Solo(), Solo(10, 3, 8, 1), 1);

            Assert.That(Ids(moves), Is.EqualTo(Expected(1, 8)));
        }

        [Test]
        public void MovesAPartyTogether()
        {
            var alpha = new List<TeamBalanceMember>
            {
                new TeamBalanceMember(1),
                new TeamBalanceMember(2),
                new TeamBalanceMember(3, "p"),
                new TeamBalanceMember(4, "p"),
            };

            var moves = TeamBalancer.Plan(alpha, Solo(), 1);

            Assert.That(Ids(moves), Is.EqualTo(Expected(4, 3)));
        }

        [Test]
        public void KeepsAPartyThatWouldFlipTheDifferenceAndMovesSomeoneOlderInstead()
        {
            // 5 対 1。5 を移した後 (4 対 2) にパーティ (2・4) を移すと 2 対 4 で差が縮まらないので、より古い 3 を移して 3 対 3 にする
            var alpha = new List<TeamBalanceMember>
            {
                new TeamBalanceMember(1),
                new TeamBalanceMember(2, "p"),
                new TeamBalanceMember(3),
                new TeamBalanceMember(4, "p"),
                new TeamBalanceMember(5),
            };

            var moves = TeamBalancer.Plan(alpha, Solo(6), 1);

            Assert.That(Ids(moves), Is.EqualTo(Expected(5, 3)));
        }

        [Test]
        public void SkipsANewPartyThatWouldOvershootForAnOlderOneThatFits()
        {
            // 7 対 0。新しいパーティ p (5 人) を移すと 2 対 5 で逆側が上限を超えるので、移しても上限の内側に寄る古いパーティ q を先に移す。
            // その後 (5 対 2) は p を移すと 0 対 7 で差が縮まらないので止める
            var alpha = new List<TeamBalanceMember>
            {
                new TeamBalanceMember(1, "q"),
                new TeamBalanceMember(2, "q"),
                new TeamBalanceMember(3, "p"),
                new TeamBalanceMember(4, "p"),
                new TeamBalanceMember(5, "p"),
                new TeamBalanceMember(6, "p"),
                new TeamBalanceMember(7, "p"),
            };

            var moves = TeamBalancer.Plan(alpha, Solo(), 1);

            Assert.That(Ids(moves), Is.EqualTo(Expected(2, 1)));
        }

        [Test]
        public void MovesAPartyThatShrinksTheDifferenceEvenIfItCannotFit()
        {
            // 8 対 3 (差 5)。どちらのパーティを移しても 4 対 7 (差 3) で上限 1 に収まらないが、差は縮むので新しい r を移す
            var alpha = new List<TeamBalanceMember>
            {
                new TeamBalanceMember(1, "p"),
                new TeamBalanceMember(2, "p"),
                new TeamBalanceMember(3, "p"),
                new TeamBalanceMember(4, "p"),
                new TeamBalanceMember(5, "r"),
                new TeamBalanceMember(6, "r"),
                new TeamBalanceMember(7, "r"),
                new TeamBalanceMember(8, "r"),
            };

            var moves = TeamBalancer.Plan(alpha, Solo(9, 10, 11), 1);

            Assert.That(Ids(moves), Is.EqualTo(Expected(8, 7, 6, 5)));
        }

        [Test]
        public void DoesNotMoveAPartyThatWouldOnlySwapWhichTeamIsLarger()
        {
            var alpha = new List<TeamBalanceMember>
            {
                new TeamBalanceMember(1, "p"),
                new TeamBalanceMember(2, "p"),
                new TeamBalanceMember(3, "p"),
            };

            Assert.That(TeamBalancer.Plan(alpha, Solo(), 1), Is.Empty);
        }

        [Test]
        public void ALimitOfZeroMovesNobody()
        {
            Assert.That(TeamBalancer.Plan(Solo(1, 2, 3, 4), Solo(), 0), Is.Empty);
        }

        [Test]
        public void AnEmptyPartyIdIsNoParty()
        {
            Assert.That(new TeamBalanceMember(1, "").PartyId, Is.Null);
            var alpha = new List<TeamBalanceMember> { new TeamBalanceMember(1, ""), new TeamBalanceMember(2, ""), new TeamBalanceMember(3, "") };

            Assert.That(Ids(TeamBalancer.Plan(alpha, Solo(), 1)), Is.EqualTo(Expected(3)));
        }

        [Test]
        public void InvalidInputIsRefused()
        {
            Assert.That(() => TeamBalancer.Plan(null, Solo(), 1), Throws.ArgumentNullException);
            Assert.That(() => TeamBalancer.Plan(Solo(), null, 1), Throws.ArgumentNullException);
            Assert.That(() => TeamBalancer.Plan(Solo(1), Solo(2), -1), Throws.InstanceOf<ArgumentOutOfRangeException>());
            Assert.That(() => TeamBalancer.Plan(Solo(1, 2), Solo(2), 1), Throws.ArgumentException);
        }

        private static List<TeamBalanceMember> Solo(params int[] playerIds)
        {
            return playerIds.Select(id => new TeamBalanceMember(id)).ToList();
        }

        private static int[] Ids(IReadOnlyList<TeamMove> moves)
        {
            return moves.Select(m => m.PlayerId).ToArray();
        }
    }
}
