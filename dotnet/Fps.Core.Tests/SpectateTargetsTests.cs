using System.Collections.Generic;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    public sealed class SpectateTargetsTests
    {
        private const ulong _viewer = 2;

        // 見ている本人 (2) は Alpha。Alpha の生きた味方は 1・4・7、5 は倒れている、3・6 は敵
        private static readonly SpectateCandidate[] _match =
        {
            new SpectateCandidate(7, Team.Alpha, true),
            new SpectateCandidate(3, Team.Bravo, true),
            new SpectateCandidate(_viewer, Team.Alpha, false),
            new SpectateCandidate(4, Team.Alpha, true),
            new SpectateCandidate(5, Team.Alpha, false),
            new SpectateCandidate(1, Team.Alpha, true),
            new SpectateCandidate(6, Team.Bravo, true),
        };

        [Test]
        public void StartsFromTheLowestIdAlly()
        {
            Assert.That(SpectateTargets.Pick(_match, _viewer, Team.Alpha, null, 0), Is.EqualTo(1UL));
        }

        [Test]
        public void KeepsTheCurrentAllyWithoutInput()
        {
            Assert.That(SpectateTargets.Pick(_match, _viewer, Team.Alpha, 4, 0), Is.EqualTo(4UL));
        }

        [TestCase(1UL, 4UL)]
        [TestCase(4UL, 7UL)]
        [TestCase(7UL, 1UL)]
        public void NextMovesToTheFollowingAllyAndWraps(ulong current, ulong expected)
        {
            Assert.That(SpectateTargets.Pick(_match, _viewer, Team.Alpha, current, 1), Is.EqualTo(expected));
        }

        [TestCase(7UL, 4UL)]
        [TestCase(4UL, 1UL)]
        [TestCase(1UL, 7UL)]
        public void PreviousMovesToThePrecedingAllyAndWraps(ulong current, ulong expected)
        {
            Assert.That(SpectateTargets.Pick(_match, _viewer, Team.Alpha, current, -1), Is.EqualTo(expected));
        }

        [Test]
        public void SeveralPressesInOneFrameAllCount()
        {
            Assert.That(SpectateTargets.Pick(_match, _viewer, Team.Alpha, 1, 2), Is.EqualTo(7UL));
            Assert.That(SpectateTargets.Pick(_match, _viewer, Team.Alpha, 1, -4), Is.EqualTo(7UL));
        }

        [Test]
        public void NeverPicksTheViewerEnemiesOrTheDead()
        {
            var seen = new HashSet<ulong>();
            ulong? current = null;
            for (var i = 0; i < 10; i++)
            {
                current = SpectateTargets.Pick(_match, _viewer, Team.Alpha, current, 1);
                seen.Add(current.Value);
            }

            Assert.That(seen, Is.EquivalentTo(new ulong[] { 1, 4, 7 }));
        }

        [Test]
        public void WhenTheWatchedAllyFallsMovesToTheNextOne()
        {
            // 5 は倒れている (映していた味方が倒れた)
            Assert.That(SpectateTargets.Pick(_match, _viewer, Team.Alpha, 5, 0), Is.EqualTo(7UL));
        }

        [Test]
        public void WhenTheWatchedAllyIsGoneNextAndPreviousStepFromWhereItWas()
        {
            // 5 はいない扱い (倒れている)。Next は 5 の次の 7、Previous は 5 の前の 4
            Assert.That(SpectateTargets.Pick(_match, _viewer, Team.Alpha, 5, 1), Is.EqualTo(7UL));
            Assert.That(SpectateTargets.Pick(_match, _viewer, Team.Alpha, 5, -1), Is.EqualTo(4UL));
        }

        [Test]
        public void WhenTheGoneAllyHadTheHighestIdWrapsToTheFirst()
        {
            Assert.That(SpectateTargets.Pick(_match, _viewer, Team.Alpha, 9, 0), Is.EqualTo(1UL));
            Assert.That(SpectateTargets.Pick(_match, _viewer, Team.Alpha, 9, 1), Is.EqualTo(1UL));
            Assert.That(SpectateTargets.Pick(_match, _viewer, Team.Alpha, 0, -1), Is.EqualTo(7UL));
        }

        [Test]
        public void PreviousWithoutACurrentAllyStartsFromTheLast()
        {
            Assert.That(SpectateTargets.Pick(_match, _viewer, Team.Alpha, null, -1), Is.EqualTo(7UL));
            Assert.That(SpectateTargets.Pick(_match, _viewer, Team.Alpha, null, 1), Is.EqualTo(1UL));
        }

        [Test]
        public void TheOtherTeamSeesItsOwnAllies()
        {
            Assert.That(SpectateTargets.Pick(_match, 3, Team.Bravo, null, 0), Is.EqualTo(6UL));
            Assert.That(SpectateTargets.Pick(_match, 3, Team.Bravo, 6, 1), Is.EqualTo(6UL), "a single ally stays put");
        }

        [Test]
        public void DownedAlliesCanBeWatched()
        {
            // ダウン中は IsAlive のまま届く
            var match = new[] { new SpectateCandidate(8, Team.Alpha, true) };

            Assert.That(SpectateTargets.Pick(match, _viewer, Team.Alpha, null, 0), Is.EqualTo(8UL));
        }

        [Test]
        public void NoAllyMeansNoTarget()
        {
            var alone = new[]
            {
                new SpectateCandidate(_viewer, Team.Alpha, false),
                new SpectateCandidate(3, Team.Bravo, true),
                new SpectateCandidate(5, Team.Alpha, false),
            };

            Assert.That(SpectateTargets.Pick(alone, _viewer, Team.Alpha, null, 1), Is.Null);
            Assert.That(SpectateTargets.Pick(alone, _viewer, Team.Alpha, 5, 0), Is.Null);
            Assert.That(SpectateTargets.Pick(null, _viewer, Team.Alpha, null, 0), Is.Null);
        }

        [Test]
        public void DuplicateRowsCountOnce()
        {
            var match = new[]
            {
                new SpectateCandidate(4, Team.Alpha, true),
                new SpectateCandidate(4, Team.Alpha, true),
                new SpectateCandidate(7, Team.Alpha, true),
            };

            Assert.That(SpectateTargets.Pick(match, _viewer, Team.Alpha, 4, 1), Is.EqualTo(7UL));
            Assert.That(SpectateTargets.Pick(match, _viewer, Team.Alpha, 7, 1), Is.EqualTo(4UL));
        }

        [TestCase(DeathCamera.Ally, true, DeathCamera.Ally)]
        [TestCase(DeathCamera.Ally, false, DeathCamera.Free)]
        [TestCase(DeathCamera.Free, true, DeathCamera.Free)]
        [TestCase(DeathCamera.Killer, false, DeathCamera.Killer)]
        [TestCase(DeathCamera.None, false, DeathCamera.None)]
        public void AllyWithoutAnyoneToWatchFallsBackToFree(DeathCamera rule, bool hasAlly, DeathCamera expected)
        {
            Assert.That(SpectateTargets.ResolveMode(rule, hasAlly), Is.EqualTo(expected));
        }
    }
}
