using System;
using System.Numerics;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    public sealed class SpawnSelectorTests
    {
        private static readonly Vector3[] None = Array.Empty<Vector3>();

        private static Vector3 At(float x)
        {
            return new Vector3(x, 0f, 0f);
        }

        [Test]
        public void DefaultMinEnemyDistanceIs15()
        {
            Assert.That(new SpawnSelector().MinEnemyDistance, Is.EqualTo(15f));
        }

        [TestCase(-1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void RejectsInvalidMinEnemyDistance(float minEnemyDistance)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new SpawnSelector(minEnemyDistance));
        }

        [Test]
        public void RejectsNonFinitePosition()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new SpawnPoint(new Vector3(float.NaN, 0f, 0f)));
        }

        [Test]
        public void ThrowsWhenNoSpawnPoints()
        {
            var selector = new SpawnSelector();

            Assert.Throws<InvalidOperationException>(() => selector.Select(Array.Empty<SpawnPoint>(), None, None, Team.Alpha));
        }

        [Test]
        public void ThrowsWhenNoSpawnPointsForTeam()
        {
            var selector = new SpawnSelector();
            var points = new[] { new SpawnPoint(At(0f), Team.Bravo) };

            Assert.Throws<InvalidOperationException>(() => selector.Select(points, None, None, Team.Alpha));
        }

        [Test]
        public void RejectsNullArguments()
        {
            var selector = new SpawnSelector();
            var points = new[] { new SpawnPoint(At(0f)) };

            Assert.Throws<ArgumentNullException>(() => selector.Select(null, None, None, Team.Alpha));
            Assert.Throws<ArgumentNullException>(() => selector.Select(points, null, None, Team.Alpha));
            Assert.Throws<ArgumentNullException>(() => selector.Select(points, None, null, Team.Alpha));
            Assert.Throws<ArgumentException>(() => selector.Select(new SpawnPoint[] { null }, None, None, Team.Alpha));
            Assert.Throws<ArgumentOutOfRangeException>(() => selector.Select(points, None, None, (Team)99));
        }

        [Test]
        public void FiltersByTeam()
        {
            var selector = new SpawnSelector();
            var bravo = new SpawnPoint(At(100f), Team.Bravo);
            var alpha = new SpawnPoint(At(30f), Team.Alpha);
            var shared = new SpawnPoint(At(20f));
            var points = new[] { bravo, alpha, shared };
            var enemies = new[] { At(0f) };

            Assert.That(selector.Select(points, enemies, None, Team.Alpha), Is.SameAs(alpha));
            Assert.That(selector.Select(points, enemies, None, Team.Bravo), Is.SameAs(bravo));
        }

        [Test]
        public void SharedPointIsUsableByBothTeams()
        {
            var selector = new SpawnSelector();
            var shared = new SpawnPoint(At(0f));
            var points = new[] { shared };

            Assert.That(selector.Select(points, None, None, Team.Alpha), Is.SameAs(shared));
            Assert.That(selector.Select(points, None, None, Team.Bravo), Is.SameAs(shared));
        }

        [Test]
        public void ExcludesPointsWithEnemyWithinMinDistance()
        {
            // near は敵まで 10m で除外され、far (20m) が選ばれる。
            var selector = new SpawnSelector();
            var near = new SpawnPoint(At(10f));
            var far = new SpawnPoint(At(-20f));
            var points = new[] { near, far };
            var enemies = new[] { At(0f) };

            Assert.That(selector.Select(points, enemies, None, Team.Alpha), Is.SameAs(far));
        }

        [Test]
        public void EnemyExactlyAtMinDistanceIsExcluded()
        {
            var selector = new SpawnSelector(15f);
            var atLimit = new SpawnPoint(At(15f));
            var outside = new SpawnPoint(At(-16f));
            var points = new[] { atLimit, outside };
            var enemies = new[] { At(0f) };

            Assert.That(selector.Select(points, enemies, None, Team.Alpha), Is.SameAs(outside));
        }

        [Test]
        public void SelectsPointFarthestFromNearestEnemy()
        {
            var selector = new SpawnSelector();
            var a = new SpawnPoint(At(20f));
            var b = new SpawnPoint(At(50f));
            var c = new SpawnPoint(At(35f));
            var points = new[] { a, b, c };
            // b は敵 2 に近いので、いちばん近い敵までの距離は a=20, b=10 (除外), c=25。
            var enemies = new[] { At(0f), At(60f) };

            Assert.That(selector.Select(points, enemies, None, Team.Alpha), Is.SameAs(c));
        }

        [Test]
        public void TieIsBrokenByNearestAlly()
        {
            var selector = new SpawnSelector();
            var left = new SpawnPoint(At(-20f));
            var right = new SpawnPoint(At(20f));
            var points = new[] { left, right };
            var enemies = new[] { At(0f) };
            var allies = new[] { At(25f) };

            Assert.That(selector.Select(points, enemies, allies, Team.Alpha), Is.SameAs(right));
        }

        [Test]
        public void FullTieSelectsFirstInList()
        {
            var selector = new SpawnSelector();
            var left = new SpawnPoint(At(-20f));
            var right = new SpawnPoint(At(20f));
            var enemies = new[] { At(0f) };
            var allies = new[] { new Vector3(0f, 0f, 5f) };

            Assert.That(selector.Select(new[] { left, right }, enemies, allies, Team.Alpha), Is.SameAs(left));
            Assert.That(selector.Select(new[] { right, left }, enemies, allies, Team.Alpha), Is.SameAs(right));
        }

        [Test]
        public void FallsBackToFarthestWhenAllExcluded()
        {
            var selector = new SpawnSelector();
            var bravo = new SpawnPoint(At(100f), Team.Bravo);
            var near = new SpawnPoint(At(3f), Team.Alpha);
            var lessNear = new SpawnPoint(At(10f));
            var points = new[] { bravo, near, lessNear };
            var enemies = new[] { At(0f) };

            // 他チームの地点 (敵から 100m) はフォールバックでも選ばない。
            Assert.That(selector.Select(points, enemies, None, Team.Alpha), Is.SameAs(lessNear));
        }

        [Test]
        public void WithoutEnemiesSelectsFirstPoint()
        {
            var selector = new SpawnSelector();
            var bravo = new SpawnPoint(At(0f), Team.Bravo);
            var first = new SpawnPoint(At(10f), Team.Alpha);
            var second = new SpawnPoint(At(20f));
            var points = new[] { bravo, first, second };

            Assert.That(selector.Select(points, None, None, Team.Alpha), Is.SameAs(first));
        }

        [Test]
        public void SameInputGivesSameResult()
        {
            var selector = new SpawnSelector();
            var points = new[] { new SpawnPoint(At(-20f)), new SpawnPoint(At(20f)), new SpawnPoint(At(40f)) };
            var enemies = new[] { At(0f) };
            var allies = new[] { At(-30f) };

            var first = selector.Select(points, enemies, allies, Team.Bravo);

            for (var i = 0; i < 10; i++)
            {
                Assert.That(selector.Select(points, enemies, allies, Team.Bravo), Is.SameAs(first));
            }
        }
    }
}
