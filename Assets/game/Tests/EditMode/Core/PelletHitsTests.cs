using System;
using System.Numerics;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class PelletHitsTests
    {
        [Test]
        public void PelletsOnTheSameTargetBecomeOneHit()
        {
            var hits = new PelletHits<int>();

            hits.Add(2, 12, HitZone.Body, DamageOutcome.Damaged);
            hits.Add(2, 9, HitZone.Limb, DamageOutcome.Damaged);
            hits.Add(2, 12, HitZone.Body, DamageOutcome.Damaged);

            Assert.That(hits.Count, Is.EqualTo(1));
            Assert.That(hits[0].Target, Is.EqualTo(2));
            Assert.That(hits[0].Damage, Is.EqualTo(33));
            Assert.That(hits[0].Pellets, Is.EqualTo(3));
            Assert.That(hits[0].ToHitConfirm(), Is.EqualTo(new HitConfirm(33, HitZone.Body, killed: false, downed: false)));
        }

        [Test]
        public void TheMergedHitKeepsTheFirstPelletPointItKnows()
        {
            var hits = new PelletHits<int>();

            hits.Add(2, 10, HitZone.Body, DamageOutcome.Damaged);
            hits.Add(2, 10, HitZone.Body, DamageOutcome.Damaged, new Vector3(1f, 2f, 3f));
            hits.Add(2, 10, HitZone.Body, DamageOutcome.Damaged, new Vector3(4f, 5f, 6f));

            Assert.That(hits[0].Point, Is.EqualTo(new Vector3(1f, 2f, 3f)));
            Assert.That(hits[0].ToHitConfirm().Point, Is.EqualTo(new Vector3(1f, 2f, 3f)));
        }

        [Test]
        public void AHitWithoutAPointHasNone()
        {
            var hits = new PelletHits<int>();

            hits.Add(2, 10, HitZone.Body, DamageOutcome.Damaged);

            Assert.That(hits[0].ToHitConfirm().Point, Is.Null);
        }

        [Test]
        public void EachTargetGetsItsOwnHitInTheOrderItWasFirstHit()
        {
            var hits = new PelletHits<int>();

            hits.Add(5, 10, HitZone.Body, DamageOutcome.Damaged);
            hits.Add(3, 10, HitZone.Body, DamageOutcome.Damaged);
            hits.Add(5, 10, HitZone.Body, DamageOutcome.Damaged);

            Assert.That(hits.Count, Is.EqualTo(2));
            Assert.That(hits[0].Target, Is.EqualTo(5));
            Assert.That(hits[0].Damage, Is.EqualTo(20));
            Assert.That(hits[1].Target, Is.EqualTo(3));
            Assert.That(hits[1].Damage, Is.EqualTo(10));
        }

        [Test]
        public void TheMergedHitShowsTheMostNotableZoneAndOutcome()
        {
            var hits = new PelletHits<int>();

            hits.Add(1, 10, HitZone.Limb, DamageOutcome.Damaged);
            hits.Add(1, 20, HitZone.Head, DamageOutcome.Downed);
            hits.Add(1, 10, HitZone.Body, DamageOutcome.Killed);
            hits.Add(1, 10, HitZone.Body, DamageOutcome.Damaged);

            Assert.That(hits[0].Zone, Is.EqualTo(HitZone.Head));
            Assert.That(hits[0].Outcome, Is.EqualTo(DamageOutcome.Killed));
            Assert.That(hits[0].ToHitConfirm(), Is.EqualTo(new HitConfirm(50, HitZone.Head, killed: true, downed: false)));

            hits.Add(1, 1, HitZone.WeakPoint, DamageOutcome.Damaged);
            Assert.That(hits[0].Zone, Is.EqualTo(HitZone.WeakPoint));
        }

        [Test]
        public void ADownWithoutAKillIsReportedAsDowned()
        {
            var hits = new PelletHits<int>();

            hits.Add(1, 10, HitZone.Body, DamageOutcome.Damaged);
            hits.Add(1, 10, HitZone.Body, DamageOutcome.Downed);

            Assert.That(hits[0].ToHitConfirm(), Is.EqualTo(new HitConfirm(20, HitZone.Body, killed: false, downed: true)));
        }

        [Test]
        public void IgnoredPelletsAreNotReported()
        {
            var hits = new PelletHits<int>();

            hits.Add(1, 10, HitZone.Head, DamageOutcome.Ignored);
            Assert.That(hits.Count, Is.EqualTo(0));

            hits.Add(1, 10, HitZone.Body, DamageOutcome.Damaged);
            hits.Add(1, 10, HitZone.Head, DamageOutcome.Ignored);
            Assert.That(hits[0].Damage, Is.EqualTo(10));
            Assert.That(hits[0].Zone, Is.EqualTo(HitZone.Body));
        }

        [Test]
        public void TheTotalDamageStopsAtTheLargestInt()
        {
            var hits = new PelletHits<int>();

            hits.Add(1, int.MaxValue, HitZone.Body, DamageOutcome.Damaged);
            hits.Add(1, 10, HitZone.Body, DamageOutcome.Damaged);

            Assert.That(hits[0].Damage, Is.EqualTo(int.MaxValue));
        }

        [Test]
        public void ClearForgetsTheShot()
        {
            var hits = new PelletHits<int>();
            hits.Add(1, 10, HitZone.Body, DamageOutcome.Damaged);

            hits.Clear();

            Assert.That(hits.Count, Is.EqualTo(0));
        }

        [Test]
        public void RejectsInvalidInput()
        {
            var hits = new PelletHits<string>();

            Assert.Throws<ArgumentNullException>(() => hits.Add(null, 1, HitZone.Body, DamageOutcome.Damaged));
            Assert.Throws<ArgumentOutOfRangeException>(() => hits.Add("a", -1, HitZone.Body, DamageOutcome.Damaged));
            Assert.Throws<ArgumentOutOfRangeException>(() => hits.Add("a", 1, (HitZone)99, DamageOutcome.Damaged));
            Assert.Throws<ArgumentOutOfRangeException>(() => hits.Add("a", 1, HitZone.Body, (DamageOutcome)99));
        }
    }
}
