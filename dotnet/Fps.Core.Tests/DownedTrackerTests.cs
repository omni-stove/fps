using System;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    public sealed class DownedTrackerTests
    {
        // 定数配列を直接渡すと CA1861 になるので params で受ける
        private static int[] Ids(params int[] expected) => expected;

        private static DownedTracker Create(int health = 30, float bleedoutSeconds = 15f, float reviveSeconds = 3f)
        {
            return new DownedTracker(health, bleedoutSeconds, reviveSeconds);
        }

        [TestCase(100, 0.3f, 30)]
        [TestCase(10, 0.25f, 3)]
        [TestCase(7, 0.5f, 4)]
        [TestCase(1, 0.01f, 1)]
        [TestCase(100, 1f, 100)]
        public void HealthForRoundsUp(int maxHealth, float fraction, int expected)
        {
            Assert.That(DownedTracker.HealthFor(maxHealth, fraction), Is.EqualTo(expected));
        }

        [Test]
        public void HealthForRejectsInvalidArguments()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => DownedTracker.HealthFor(0, 0.3f));
            Assert.Throws<ArgumentOutOfRangeException>(() => DownedTracker.HealthFor(100, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => DownedTracker.HealthFor(100, 1.1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => DownedTracker.HealthFor(100, float.NaN));
        }

        [Test]
        public void RejectsInvalidConstruction()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new DownedTracker(0, 15f, 3f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new DownedTracker(30, 0f, 3f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new DownedTracker(30, float.PositiveInfinity, 3f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new DownedTracker(30, 15f, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new DownedTracker(30, 15f, float.NaN));
        }

        [Test]
        public void NotDownedPlayerHasNoState()
        {
            var tracker = Create();

            Assert.That(tracker.IsDowned(1), Is.False);
            Assert.That(tracker.GetHealth(1), Is.Zero);
            Assert.That(tracker.GetBleedoutRemaining(1), Is.Null);
            Assert.That(tracker.GetReviveProgress(1), Is.Zero);
            Assert.That(tracker.GetBleedoutCreditId(1), Is.Null);
            Assert.That(tracker.Remove(1), Is.False);
        }

        [Test]
        public void DownStartsWithDownedHealthAndFullBleedout()
        {
            var tracker = Create();

            tracker.Down(1, 2, byEnemy: true);

            Assert.That(tracker.IsDowned(1), Is.True);
            Assert.That(tracker.Count, Is.EqualTo(1));
            Assert.That(tracker.GetHealth(1), Is.EqualTo(30));
            Assert.That(tracker.GetBleedoutRemaining(1), Is.EqualTo(15f));
            Assert.That(tracker.GetDownerId(1), Is.EqualTo(2));
            Assert.That(tracker.GetLastEnemyId(1), Is.EqualTo(2));
            Assert.Throws<InvalidOperationException>(() => tracker.Down(1, 2, byEnemy: true));
        }

        [Test]
        public void TakeDamageReducesHealthAndRemembersTheLastEnemy()
        {
            var tracker = Create();
            tracker.Down(1, 2, byEnemy: true);

            Assert.That(tracker.TakeDamage(1, 4, byEnemy: true, 10), Is.EqualTo(20));
            Assert.That(tracker.GetLastEnemyId(1), Is.EqualTo(4));

            Assert.That(tracker.TakeDamage(1, 3, byEnemy: false, 5), Is.EqualTo(15));
            Assert.That(tracker.GetLastEnemyId(1), Is.EqualTo(4));

            Assert.That(tracker.TakeDamage(1, 4, byEnemy: true, 100), Is.Zero);
            Assert.Throws<InvalidOperationException>(() => tracker.TakeDamage(9, 4, byEnemy: true, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => tracker.TakeDamage(1, 4, byEnemy: true, -1));
        }

        [Test]
        public void BleedoutCreditGoesToTheEnemyWhoDowned()
        {
            var tracker = Create();
            tracker.Down(1, 2, byEnemy: true);
            tracker.TakeDamage(1, 4, byEnemy: true, 10);

            Assert.That(tracker.GetBleedoutCreditId(1), Is.EqualTo(2));
        }

        [Test]
        public void BleedoutCreditFallsBackToTheLastEnemyWhenAnAllyDowned()
        {
            var tracker = Create();
            tracker.Down(1, 3, byEnemy: false);

            Assert.That(tracker.GetBleedoutCreditId(1), Is.Null);
            Assert.That(tracker.GetDownerId(1), Is.EqualTo(3));

            tracker.TakeDamage(1, 4, byEnemy: true, 10);

            Assert.That(tracker.GetBleedoutCreditId(1), Is.EqualTo(4));
        }

        [Test]
        public void TickReturnsBledOutPlayersInIdOrderWithoutRemovingThem()
        {
            var tracker = Create(bleedoutSeconds: 5f);
            tracker.Down(3, 2, byEnemy: true);
            tracker.Down(1, 2, byEnemy: true);

            Assert.That(tracker.Tick(4f), Is.Empty);
            Assert.That(tracker.GetBleedoutRemaining(1), Is.EqualTo(1f));

            Assert.That(tracker.Tick(2f), Is.EqualTo(Ids(1, 3)));
            Assert.That(tracker.GetBleedoutRemaining(1), Is.Zero);
            Assert.That(tracker.IsDowned(1), Is.True);
            Assert.Throws<ArgumentOutOfRangeException>(() => tracker.Tick(-1f));
        }

        [Test]
        public void ReviveCompletesWhenTheTotalReachesReviveSeconds()
        {
            var tracker = Create(reviveSeconds: 3f);
            tracker.Down(1, 2, byEnemy: true);

            Assert.That(tracker.AddReviveProgress(1, 3, 1f), Is.EqualTo(ReviveStep.Progressed));
            Assert.That(tracker.GetReviveProgress(1), Is.EqualTo(1f / 3f).Within(1e-6f));
            tracker.Tick(1f);
            Assert.That(tracker.AddReviveProgress(1, 3, 1f), Is.EqualTo(ReviveStep.Progressed));
            tracker.Tick(1f);
            Assert.That(tracker.AddReviveProgress(1, 3, 1f), Is.EqualTo(ReviveStep.Completed));
        }

        [Test]
        public void ReviveResetsWhenATickPassesWithoutProgress()
        {
            var tracker = Create(reviveSeconds: 3f);
            tracker.Down(1, 2, byEnemy: true);

            tracker.AddReviveProgress(1, 3, 2f);
            tracker.Tick(1f);
            Assert.That(tracker.GetReviveProgress(1), Is.GreaterThan(0f));

            tracker.Tick(1f);

            Assert.That(tracker.GetReviveProgress(1), Is.Zero);
            Assert.That(tracker.AddReviveProgress(1, 3, 2f), Is.EqualTo(ReviveStep.Progressed));
        }

        [Test]
        public void OnlyOneReviverPerTick()
        {
            var tracker = Create(reviveSeconds: 3f);
            tracker.Down(1, 2, byEnemy: true);

            Assert.That(tracker.AddReviveProgress(1, 3, 1f), Is.EqualTo(ReviveStep.Progressed));
            Assert.That(tracker.AddReviveProgress(1, 5, 1f), Is.EqualTo(ReviveStep.Rejected));
            Assert.That(tracker.GetReviveProgress(1), Is.EqualTo(1f / 3f).Within(1e-6f));
        }

        [Test]
        public void ChangingReviverAcrossTicksStartsOver()
        {
            var tracker = Create(reviveSeconds: 3f);
            tracker.Down(1, 2, byEnemy: true);
            tracker.AddReviveProgress(1, 3, 2f);
            tracker.Tick(0f);

            Assert.That(tracker.AddReviveProgress(1, 5, 1f), Is.EqualTo(ReviveStep.Progressed));
            Assert.That(tracker.GetReviveProgress(1), Is.EqualTo(1f / 3f).Within(1e-6f));
        }

        [Test]
        public void ReviveIsRejectedForPlayersWhoAreNotDowned()
        {
            var tracker = Create();

            Assert.That(tracker.AddReviveProgress(1, 3, 1f), Is.EqualTo(ReviveStep.Rejected));
            Assert.Throws<ArgumentOutOfRangeException>(() => tracker.AddReviveProgress(1, 3, -1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => tracker.AddReviveProgress(1, 3, float.NaN));
        }

        [Test]
        public void RemovePlayerForgetsTheLeaverAsDownerEnemyAndReviver()
        {
            var tracker = Create(reviveSeconds: 3f);
            tracker.Down(1, 2, byEnemy: true);
            tracker.Down(5, 2, byEnemy: true);
            tracker.AddReviveProgress(5, 3, 1f);

            Assert.That(tracker.RemovePlayer(2), Is.False);
            Assert.That(tracker.GetDownerId(1), Is.Null);
            Assert.That(tracker.GetBleedoutCreditId(1), Is.Null);

            Assert.That(tracker.RemovePlayer(3), Is.False);
            Assert.That(tracker.GetReviveProgress(5), Is.Zero);
            Assert.That(tracker.AddReviveProgress(5, 7, 1f), Is.EqualTo(ReviveStep.Progressed));

            Assert.That(tracker.RemovePlayer(1), Is.True);
            Assert.That(tracker.IsDowned(1), Is.False);
        }

        [Test]
        public void ClearRemovesEveryone()
        {
            var tracker = Create();
            tracker.Down(1, 2, byEnemy: true);
            tracker.Down(3, 2, byEnemy: true);

            tracker.Clear();

            Assert.That(tracker.Count, Is.Zero);
            Assert.That(tracker.GetDownedIds(), Is.Empty);
        }
    }
}
