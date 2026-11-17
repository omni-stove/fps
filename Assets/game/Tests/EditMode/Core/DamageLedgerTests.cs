using System;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class DamageLedgerTests
    {
        private const int Victim = 1;
        private const int Killer = 2;
        private const int Helper = 3;
        private const int Other = 4;

        private static DamageLedger NewLedger()
        {
            return new DamageLedger(new AssistSpec(windowSeconds: 10f, minDamage: 20));
        }

        [Test]
        public void SomeoneWhoDealtEnoughInsideTheWindowAssists()
        {
            var ledger = NewLedger();
            ledger.Record(Victim, Helper, 30, friendly: false, time: 100.0);
            ledger.Record(Victim, Killer, 70, friendly: false, time: 105.0);

            Assert.That(ledger.TakeAssisters(Victim, Killer, 105.0), Is.EqualTo(new[] { Helper }));
        }

        [Test]
        public void DamageOlderThanTheWindowDoesNotCount()
        {
            var ledger = NewLedger();
            ledger.Record(Victim, Helper, 50, friendly: false, time: 100.0);
            ledger.Record(Victim, Other, 50, friendly: false, time: 100.0);
            ledger.Record(Victim, Other, 5, friendly: false, time: 109.0);

            // Helper の 50 は 10.5 秒前で外れる。Other は 10.5 秒前の 50 が外れ、残る 5 では足りない。
            Assert.That(ledger.TakeAssisters(Victim, Killer, 110.5), Is.Empty);
        }

        [Test]
        public void DamageExactlyAtTheEdgeOfTheWindowCounts()
        {
            var ledger = NewLedger();
            ledger.Record(Victim, Helper, 20, friendly: false, time: 100.0);

            Assert.That(ledger.TakeAssisters(Victim, Killer, 110.0), Is.EqualTo(new[] { Helper }));
        }

        [Test]
        public void TheKillerIsNotAnAssister()
        {
            var ledger = NewLedger();
            ledger.Record(Victim, Killer, 80, friendly: false, time: 100.0);
            ledger.Record(Victim, Helper, 25, friendly: false, time: 101.0);

            Assert.That(ledger.TakeAssisters(Victim, Killer, 102.0), Is.EqualTo(new[] { Helper }));
        }

        [Test]
        public void DamageFromAnAllyIsNotCounted()
        {
            var ledger = NewLedger();
            ledger.Record(Victim, Helper, 60, friendly: true, time: 100.0);

            Assert.That(ledger.TakeAssisters(Victim, Killer, 101.0), Is.Empty);
        }

        [Test]
        public void DamageToYourselfIsNotCounted()
        {
            var ledger = NewLedger();
            ledger.Record(Victim, Victim, 60, friendly: false, time: 100.0);

            Assert.That(ledger.TakeAssisters(Victim, Killer, 101.0), Is.Empty);
        }

        [Test]
        public void SomeoneShortOfTheMinimumIsLeftOut()
        {
            var ledger = NewLedger();
            ledger.Record(Victim, Helper, 19, friendly: false, time: 100.0);
            ledger.Record(Victim, Other, 10, friendly: false, time: 100.0);
            ledger.Record(Victim, Other, 10, friendly: false, time: 101.0);

            // Helper は 19 で 1 足りない。Other は 2 回の合計で 20 に届く。
            Assert.That(ledger.TakeAssisters(Victim, Killer, 102.0), Is.EqualTo(new[] { Other }));
        }

        [Test]
        public void AssistersComeInOrderOfDamageThenOfFirstHit()
        {
            var ledger = NewLedger();
            ledger.Record(Victim, Helper, 20, friendly: false, time: 100.0);
            ledger.Record(Victim, Other, 40, friendly: false, time: 101.0);
            ledger.Record(Victim, 5, 20, friendly: false, time: 102.0);

            Assert.That(ledger.TakeAssisters(Victim, Killer, 103.0), Is.EqualTo(new[] { Other, Helper, 5 }));
        }

        [Test]
        public void TakingTheAssistersClearsThatVictim()
        {
            var ledger = NewLedger();
            ledger.Record(Victim, Helper, 30, friendly: false, time: 100.0);
            ledger.Record(Other, Helper, 30, friendly: false, time: 100.0);

            Assert.That(ledger.TakeAssisters(Victim, Killer, 101.0), Is.EqualTo(new[] { Helper }));
            Assert.That(ledger.TakeAssisters(Victim, Killer, 101.0), Is.Empty);
            Assert.That(ledger.TakeAssisters(Other, Killer, 101.0), Is.EqualTo(new[] { Helper }), "other victims are kept");
        }

        [Test]
        public void ClearingOnRespawnForgetsOnlyThatVictim()
        {
            var ledger = NewLedger();
            ledger.Record(Victim, Helper, 30, friendly: false, time: 100.0);
            ledger.Record(Other, Helper, 30, friendly: false, time: 100.0);

            ledger.Clear(Victim);

            Assert.That(ledger.TakeAssisters(Victim, Killer, 101.0), Is.Empty);
            Assert.That(ledger.TakeAssisters(Other, Killer, 101.0), Is.EqualTo(new[] { Helper }));
        }

        [Test]
        public void ResetForgetsEveryone()
        {
            var ledger = NewLedger();
            ledger.Record(Victim, Helper, 30, friendly: false, time: 100.0);
            ledger.Record(Other, Helper, 30, friendly: false, time: 100.0);

            ledger.Reset();

            Assert.That(ledger.TakeAssisters(Victim, Killer, 101.0), Is.Empty);
            Assert.That(ledger.TakeAssisters(Other, Killer, 101.0), Is.Empty);
        }

        [Test]
        public void ZeroOrNegativeAmountsAreIgnored()
        {
            var ledger = NewLedger();
            ledger.Record(Victim, Helper, 0, friendly: false, time: 100.0);
            ledger.Record(Victim, Helper, -30, friendly: false, time: 100.0);
            ledger.Record(Victim, Helper, 20, friendly: false, time: 100.0);

            Assert.That(ledger.TakeAssisters(Victim, Killer, 101.0), Is.EqualTo(new[] { Helper }));
        }

        [Test]
        public void ASuicideStillCreditsTheOthers()
        {
            var ledger = NewLedger();
            ledger.Record(Victim, Helper, 40, friendly: false, time: 100.0);

            Assert.That(ledger.TakeAssisters(Victim, Victim, 101.0), Is.EqualTo(new[] { Helper }));
        }

        [Test]
        public void AnUnknownVictimHasNoAssisters()
        {
            Assert.That(NewLedger().TakeAssisters(Victim, Killer, 0.0), Is.Empty);
        }

        [Test]
        public void ANonFiniteTimeOrMissingSpecIsRejected()
        {
            var ledger = NewLedger();

            Assert.Throws<ArgumentOutOfRangeException>(() => ledger.Record(Victim, Helper, 10, false, double.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => ledger.TakeAssisters(Victim, Killer, double.PositiveInfinity));
            Assert.Throws<ArgumentNullException>(() => new DamageLedger(null));
        }
    }
}
