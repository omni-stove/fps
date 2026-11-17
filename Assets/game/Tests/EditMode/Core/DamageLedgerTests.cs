using System;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class DamageLedgerTests
    {
        private const int _victim = 1;
        private const int _killer = 2;
        private const int _helper = 3;
        private const int _other = 4;

        private static DamageLedger NewLedger()
        {
            return new DamageLedger(new AssistSpec(windowSeconds: 10f, minDamage: 20));
        }

        [Test]
        public void SomeoneWhoDealtEnoughInsideTheWindowAssists()
        {
            var ledger = NewLedger();
            ledger.Record(_victim, _helper, 30, friendly: false, time: 100.0);
            ledger.Record(_victim, _killer, 70, friendly: false, time: 105.0);

            Assert.That(ledger.TakeAssisters(_victim, _killer, 105.0), Is.EqualTo(new[] { _helper }));
        }

        [Test]
        public void DamageOlderThanTheWindowDoesNotCount()
        {
            var ledger = NewLedger();
            ledger.Record(_victim, _helper, 50, friendly: false, time: 100.0);
            ledger.Record(_victim, _other, 50, friendly: false, time: 100.0);
            ledger.Record(_victim, _other, 5, friendly: false, time: 109.0);

            // Helper の 50 は 10.5 秒前で外れる。Other は 10.5 秒前の 50 が外れ、残る 5 では足りない。
            Assert.That(ledger.TakeAssisters(_victim, _killer, 110.5), Is.Empty);
        }

        [Test]
        public void DamageExactlyAtTheEdgeOfTheWindowCounts()
        {
            var ledger = NewLedger();
            ledger.Record(_victim, _helper, 20, friendly: false, time: 100.0);

            Assert.That(ledger.TakeAssisters(_victim, _killer, 110.0), Is.EqualTo(new[] { _helper }));
        }

        [Test]
        public void TheKillerIsNotAnAssister()
        {
            var ledger = NewLedger();
            ledger.Record(_victim, _killer, 80, friendly: false, time: 100.0);
            ledger.Record(_victim, _helper, 25, friendly: false, time: 101.0);

            Assert.That(ledger.TakeAssisters(_victim, _killer, 102.0), Is.EqualTo(new[] { _helper }));
        }

        [Test]
        public void DamageFromAnAllyIsNotCounted()
        {
            var ledger = NewLedger();
            ledger.Record(_victim, _helper, 60, friendly: true, time: 100.0);

            Assert.That(ledger.TakeAssisters(_victim, _killer, 101.0), Is.Empty);
        }

        [Test]
        public void DamageToYourselfIsNotCounted()
        {
            var ledger = NewLedger();
            ledger.Record(_victim, _victim, 60, friendly: false, time: 100.0);

            Assert.That(ledger.TakeAssisters(_victim, _killer, 101.0), Is.Empty);
        }

        [Test]
        public void SomeoneShortOfTheMinimumIsLeftOut()
        {
            var ledger = NewLedger();
            ledger.Record(_victim, _helper, 19, friendly: false, time: 100.0);
            ledger.Record(_victim, _other, 10, friendly: false, time: 100.0);
            ledger.Record(_victim, _other, 10, friendly: false, time: 101.0);

            // Helper は 19 で 1 足りない。Other は 2 回の合計で 20 に届く。
            Assert.That(ledger.TakeAssisters(_victim, _killer, 102.0), Is.EqualTo(new[] { _other }));
        }

        [Test]
        public void AssistersComeInOrderOfDamageThenOfFirstHit()
        {
            var ledger = NewLedger();
            ledger.Record(_victim, _helper, 20, friendly: false, time: 100.0);
            ledger.Record(_victim, _other, 40, friendly: false, time: 101.0);
            ledger.Record(_victim, 5, 20, friendly: false, time: 102.0);

            Assert.That(ledger.TakeAssisters(_victim, _killer, 103.0), Is.EqualTo(new[] { _other, _helper, 5 }));
        }

        [Test]
        public void TakingTheAssistersClearsThatVictim()
        {
            var ledger = NewLedger();
            ledger.Record(_victim, _helper, 30, friendly: false, time: 100.0);
            ledger.Record(_other, _helper, 30, friendly: false, time: 100.0);

            Assert.That(ledger.TakeAssisters(_victim, _killer, 101.0), Is.EqualTo(new[] { _helper }));
            Assert.That(ledger.TakeAssisters(_victim, _killer, 101.0), Is.Empty);
            Assert.That(ledger.TakeAssisters(_other, _killer, 101.0), Is.EqualTo(new[] { _helper }), "other victims are kept");
        }

        [Test]
        public void ClearingOnRespawnForgetsOnlyThatVictim()
        {
            var ledger = NewLedger();
            ledger.Record(_victim, _helper, 30, friendly: false, time: 100.0);
            ledger.Record(_other, _helper, 30, friendly: false, time: 100.0);

            ledger.Clear(_victim);

            Assert.That(ledger.TakeAssisters(_victim, _killer, 101.0), Is.Empty);
            Assert.That(ledger.TakeAssisters(_other, _killer, 101.0), Is.EqualTo(new[] { _helper }));
        }

        [Test]
        public void ResetForgetsEveryone()
        {
            var ledger = NewLedger();
            ledger.Record(_victim, _helper, 30, friendly: false, time: 100.0);
            ledger.Record(_other, _helper, 30, friendly: false, time: 100.0);

            ledger.Reset();

            Assert.That(ledger.TakeAssisters(_victim, _killer, 101.0), Is.Empty);
            Assert.That(ledger.TakeAssisters(_other, _killer, 101.0), Is.Empty);
        }

        [Test]
        public void ZeroOrNegativeAmountsAreIgnored()
        {
            var ledger = NewLedger();
            ledger.Record(_victim, _helper, 0, friendly: false, time: 100.0);
            ledger.Record(_victim, _helper, -30, friendly: false, time: 100.0);
            ledger.Record(_victim, _helper, 20, friendly: false, time: 100.0);

            Assert.That(ledger.TakeAssisters(_victim, _killer, 101.0), Is.EqualTo(new[] { _helper }));
        }

        [Test]
        public void ASuicideStillCreditsTheOthers()
        {
            var ledger = NewLedger();
            ledger.Record(_victim, _helper, 40, friendly: false, time: 100.0);

            Assert.That(ledger.TakeAssisters(_victim, _victim, 101.0), Is.EqualTo(new[] { _helper }));
        }

        [Test]
        public void AnUnknownVictimHasNoAssisters()
        {
            Assert.That(NewLedger().TakeAssisters(_victim, _killer, 0.0), Is.Empty);
        }

        [Test]
        public void ANonFiniteTimeOrMissingSpecIsRejected()
        {
            var ledger = NewLedger();

            Assert.Throws<ArgumentOutOfRangeException>(() => ledger.Record(_victim, _helper, 10, false, double.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => ledger.TakeAssisters(_victim, _killer, double.PositiveInfinity));
            Assert.Throws<ArgumentNullException>(() => new DamageLedger(null));
        }
    }
}
