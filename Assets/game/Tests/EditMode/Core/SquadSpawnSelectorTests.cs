using System;
using System.Numerics;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class SquadSpawnSelectorTests
    {
        private const float _tolerance = 1e-4f;

        private static readonly Side _mine = new Side(0);
        private static readonly Side _theirs = new Side(1);
        private static readonly Vector3[] _noEnemies = Array.Empty<Vector3>();

        private static readonly SquadSpawnSelector _selector = new SquadSpawnSelector(SquadSpawnSpec.Default);

        private static SquadSpawnMate Mate(float x, float z, float secondsSinceDamaged = SquadSpawnMate.NeverDamaged)
        {
            return new SquadSpawnMate(_mine, new Vector3(x, 0f, z), true, secondsSinceDamaged);
        }

        private static void AssertAt(Vector3? actual, float x, float y, float z)
        {
            Assert.That(actual.HasValue, Is.True);
            Assert.That(actual.Value.X, Is.EqualTo(x).Within(_tolerance));
            Assert.That(actual.Value.Y, Is.EqualTo(y).Within(_tolerance));
            Assert.That(actual.Value.Z, Is.EqualTo(z).Within(_tolerance));
        }

        [Test]
        public void DefaultSpecValues()
        {
            var spec = SquadSpawnSpec.Default;

            Assert.That(spec.MinEnemyDistance, Is.EqualTo(20f));
            Assert.That(spec.DamageCooldownSeconds, Is.EqualTo(5f));
            Assert.That(spec.MateDistance, Is.EqualTo(2f));
            Assert.That(spec.Clearance, Is.EqualTo(1f));
            Assert.That(spec.SlotCount, Is.EqualTo(8));
        }

        [TestCase(-1f, 5f, 2f, 1f, 8)]
        [TestCase(float.NaN, 5f, 2f, 1f, 8)]
        [TestCase(20f, -1f, 2f, 1f, 8)]
        [TestCase(20f, float.PositiveInfinity, 2f, 1f, 8)]
        [TestCase(20f, 5f, 0f, 1f, 8)]
        [TestCase(20f, 5f, 2f, -1f, 8)]
        [TestCase(20f, 5f, 1f, 1.5f, 8)]
        [TestCase(20f, 5f, 2f, 1f, 0)]
        public void RejectsInvalidSpec(float minEnemyDistance, float damageCooldownSeconds, float mateDistance, float clearance, int slotCount)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new SquadSpawnSpec(minEnemyDistance, damageCooldownSeconds, mateDistance, clearance, slotCount));
        }

        [Test]
        public void RejectsInvalidMate()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new SquadSpawnMate(_mine, new Vector3(float.NaN, 0f, 0f), true));
            Assert.Throws<ArgumentOutOfRangeException>(() => new SquadSpawnMate(_mine, Vector3.Zero, true, -1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new SquadSpawnMate(_mine, Vector3.Zero, true, float.NaN));
        }

        [Test]
        public void RejectsNullArguments()
        {
            Assert.Throws<ArgumentNullException>(() => new SquadSpawnSelector(null));
            Assert.Throws<ArgumentNullException>(() => _selector.Select(_mine, null, _noEnemies));
            Assert.Throws<ArgumentNullException>(() => _selector.Select(_mine, new[] { Mate(0f, 0f) }, null));
        }

        [Test]
        public void PicksTheSafeMateAndSpawnsBesideIt()
        {
            var mates = new[] { Mate(0f, 0f), Mate(100f, 0f) };
            var enemies = new[] { new Vector3(10f, 0f, 0f) };

            var position = _selector.Select(_mine, mates, enemies);

            AssertAt(position, 102f, 0f, 0f);
        }

        [Test]
        public void PrefersTheMateFarthestFromEnemiesAndTheSideAwayFromThem()
        {
            var mates = new[] { Mate(0f, 0f), Mate(0f, 100f) };
            var enemies = new[] { new Vector3(0f, 0f, -30f) };

            var position = _selector.Select(_mine, mates, enemies);

            AssertAt(position, 0f, 0f, 102f);
        }

        [Test]
        public void AvoidsAMateShotRecently()
        {
            var mates = new[] { Mate(0f, 0f, 1f), Mate(0f, 50f) };

            var position = _selector.Select(_mine, mates, _noEnemies);

            AssertAt(position, 2f, 0f, 50f);
        }

        [Test]
        public void AMateShotExactlyTheCooldownAgoIsUsable()
        {
            var mates = new[] { Mate(0f, 0f, 5f) };

            var position = _selector.Select(_mine, mates, _noEnemies);

            AssertAt(position, 2f, 0f, 0f);
        }

        [Test]
        public void ReturnsNullWhenEveryMateWasShotRecently()
        {
            var mates = new[] { Mate(0f, 0f, 0f), Mate(0f, 50f, 4.9f) };

            Assert.That(_selector.Select(_mine, mates, _noEnemies), Is.Null);
        }

        [Test]
        public void AvoidsAMateWithAnEnemyNearby()
        {
            var mates = new[] { Mate(0f, 0f) };
            var enemies = new[] { new Vector3(19f, 0f, 0f) };

            Assert.That(_selector.Select(_mine, mates, enemies), Is.Null);
        }

        [Test]
        public void AMateExactlyAtTheMinimumDistanceIsUsableButNotTheSlotTowardTheEnemy()
        {
            var mates = new[] { Mate(0f, 0f) };
            var enemies = new[] { new Vector3(-20f, 0f, 0f) };

            var position = _selector.Select(_mine, mates, enemies);

            AssertAt(position, 2f, 0f, 0f);
        }

        [Test]
        public void ReturnsNullWhenNoMateCanBeChosen()
        {
            Assert.That(_selector.Select(_mine, Array.Empty<SquadSpawnMate>(), _noEnemies), Is.Null, "no mates");
            Assert.That(
                _selector.Select(_mine, new[] { new SquadSpawnMate(_mine, Vector3.Zero, false) }, _noEnemies),
                Is.Null,
                "a dead mate is not a spawn point");
            Assert.That(
                _selector.Select(_mine, new[] { new SquadSpawnMate(_theirs, Vector3.Zero, true) }, _noEnemies),
                Is.Null,
                "another squad's player is not a spawn point");
        }

        [Test]
        public void SkipsASlotTakenByAnotherBody()
        {
            var mates = new[] { Mate(0f, 0f), new SquadSpawnMate(_theirs, new Vector3(2f, 0f, 0f), true) };

            var position = _selector.Select(_mine, mates, _noEnemies);

            AssertAt(position, 1.4142135f, 0f, 1.4142135f);
        }

        [Test]
        public void ADeadBodyDoesNotTakeASlot()
        {
            var mates = new[] { Mate(0f, 0f), new SquadSpawnMate(_mine, new Vector3(2f, 0f, 0f), false) };

            var position = _selector.Select(_mine, mates, _noEnemies);

            AssertAt(position, 2f, 0f, 0f);
        }

        [Test]
        public void AsksTheCallerWhetherASlotIsClear()
        {
            var mates = new[] { Mate(0f, 0f) };

            var position = _selector.Select(_mine, mates, _noEnemies, p => p.X < 0.5f);

            AssertAt(position, 0f, 0f, 2f);
        }

        [Test]
        public void FallsBackToTheNextMateWhenNoSlotIsClear()
        {
            var mates = new[] { Mate(0f, 0f), Mate(0f, 50f) };

            var position = _selector.Select(_mine, mates, _noEnemies, p => p.Z > 25f);

            AssertAt(position, 2f, 0f, 50f);
        }

        [Test]
        public void ReturnsNullWhenNoSlotIsClear()
        {
            var mates = new[] { Mate(0f, 0f) };

            Assert.That(_selector.Select(_mine, mates, _noEnemies, _ => false), Is.Null);
        }

        [Test]
        public void UsesTheConfiguredSpec()
        {
            var selector = new SquadSpawnSelector(new SquadSpawnSpec(minEnemyDistance: 5f, damageCooldownSeconds: 0f, mateDistance: 3f, slotCount: 4));
            var mates = new[] { Mate(0f, 0f, 0f) };
            var enemies = new[] { new Vector3(-10f, 0f, 0f) };

            var position = selector.Select(_mine, mates, enemies);

            AssertAt(position, 3f, 0f, 0f);
        }
    }
}
