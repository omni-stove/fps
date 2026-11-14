using System;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    // Unity の EditMode テストと dotnet/Fps.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class StaggerJudgeTests
    {
        private static StaggerSpec Spec(
            float headDamage = 20f,
            float limbDamage = 10f,
            float windowSeconds = 1f,
            float immunitySeconds = 2f,
            float kneelSeconds = 1.5f)
        {
            return new StaggerSpec(0.5f, kneelSeconds, 1f, 0.8f, headDamage, limbDamage, 1f, 1f, windowSeconds, immunitySeconds);
        }

        [Test]
        public void ClassifiesByZoneAndKind()
        {
            Assert.That(StaggerJudge.Classify(HitZone.Head, DamageKind.Bullet), Is.EqualTo(StaggerKind.Flinch));
            Assert.That(StaggerJudge.Classify(HitZone.Limb, DamageKind.Bullet), Is.EqualTo(StaggerKind.Kneel));
            Assert.That(StaggerJudge.Classify(HitZone.Body, DamageKind.Bullet), Is.EqualTo(StaggerKind.None));
            Assert.That(StaggerJudge.Classify(null, DamageKind.Bullet), Is.EqualTo(StaggerKind.None));
            Assert.That(StaggerJudge.Classify(null, DamageKind.Other), Is.EqualTo(StaggerKind.None));
            Assert.That(StaggerJudge.Classify(null, DamageKind.Melee), Is.EqualTo(StaggerKind.Stumble));
            Assert.That(StaggerJudge.Classify(HitZone.Head, DamageKind.Melee), Is.EqualTo(StaggerKind.Stumble), "the kind wins over the zone");
            Assert.That(StaggerJudge.Classify(null, DamageKind.Explosion), Is.EqualTo(StaggerKind.Blast));
        }

        [Test]
        public void ClassifyRejectsUndefinedValues()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => StaggerJudge.Classify(null, (DamageKind)99));
            Assert.Throws<ArgumentOutOfRangeException>(() => StaggerJudge.Classify((HitZone)99, DamageKind.Bullet));
        }

        [Test]
        public void ALegShotAboveTheThresholdKneels()
        {
            var judge = new StaggerJudge(Spec());

            var verdict = judge.Hit(HitZone.Limb, DamageKind.Bullet, 19, 0f);

            Assert.That(verdict.Outcome, Is.EqualTo(StaggerOutcome.Staggered));
            Assert.That(verdict.Kind, Is.EqualTo(StaggerKind.Kneel));
            Assert.That(verdict.Seconds, Is.EqualTo(1.5f));
            Assert.That(judge.Current, Is.EqualTo(StaggerKind.Kneel));
            Assert.That(judge.SecondsRemaining, Is.EqualTo(1.5f));
            Assert.That(judge.AccumulatedDamage, Is.Zero, "staggering starts the count over");
        }

        [Test]
        public void BodyShotsDoNotStaggerButAddUpForTheNextHeadShot()
        {
            var judge = new StaggerJudge(Spec(headDamage: 30f));

            Assert.That(judge.Hit(HitZone.Body, DamageKind.Bullet, 25, 0f).Outcome, Is.EqualTo(StaggerOutcome.NoReaction));
            Assert.That(judge.IsStaggered, Is.False);

            var verdict = judge.Hit(HitZone.Head, DamageKind.Bullet, 10, 0f);

            Assert.That(verdict.Outcome, Is.EqualTo(StaggerOutcome.Staggered), "25 + 10 reaches 30 within the window");
            Assert.That(verdict.Kind, Is.EqualTo(StaggerKind.Flinch));
            Assert.That(verdict.AccumulatedDamage, Is.EqualTo(35f));
        }

        [Test]
        public void DamageOutsideTheWindowIsForgotten()
        {
            var judge = new StaggerJudge(Spec(headDamage: 30f, windowSeconds: 1f));

            judge.Hit(HitZone.Body, DamageKind.Bullet, 25, 0f);
            judge.Tick(1f);
            Assert.That(judge.AccumulatedDamage, Is.Zero);

            var verdict = judge.Hit(HitZone.Head, DamageKind.Bullet, 10, 0f);
            Assert.That(verdict.Outcome, Is.EqualTo(StaggerOutcome.BelowThreshold));
            Assert.That(verdict.Threshold, Is.EqualTo(30f));
        }

        [Test]
        public void EndsAfterTheDurationAndReportsTheKindOnce()
        {
            var judge = new StaggerJudge(Spec());
            judge.Hit(HitZone.Limb, DamageKind.Bullet, 19, 0f);

            Assert.That(judge.Tick(1f), Is.EqualTo(StaggerKind.None));
            Assert.That(judge.IsStaggered, Is.True);
            Assert.That(judge.Tick(0.5f), Is.EqualTo(StaggerKind.Kneel));
            Assert.That(judge.IsStaggered, Is.False);
            Assert.That(judge.Tick(0.5f), Is.EqualTo(StaggerKind.None));
        }

        [Test]
        public void DoesNotStaggerAgainUntilTheImmunityRunsOut()
        {
            var judge = new StaggerJudge(Spec(kneelSeconds: 1.5f, immunitySeconds: 2f));
            judge.Hit(HitZone.Limb, DamageKind.Bullet, 19, 0f);

            var during = judge.Hit(HitZone.Limb, DamageKind.Bullet, 19, 0f);
            Assert.That(during.Outcome, Is.EqualTo(StaggerOutcome.Immune));
            Assert.That(during.Kind, Is.EqualTo(StaggerKind.Kneel));
            Assert.That(judge.SecondsRemaining, Is.EqualTo(1.5f), "a hit while staggered does not lengthen it");

            judge.Tick(1.5f);
            Assert.That(judge.IsStaggered, Is.False);
            Assert.That(judge.IsImmune, Is.True);
            Assert.That(judge.Hit(HitZone.Limb, DamageKind.Bullet, 19, 0f).Outcome, Is.EqualTo(StaggerOutcome.Immune), "right after recovering");
            Assert.That(judge.AccumulatedDamage, Is.Zero, "damage while immune is not saved up");

            judge.Tick(2f);
            Assert.That(judge.IsImmune, Is.False);
            Assert.That(judge.Hit(HitZone.Limb, DamageKind.Bullet, 19, 0f).Outcome, Is.EqualTo(StaggerOutcome.Staggered));
        }

        [Test]
        public void ResistanceNeedsMoreDamageAndShortensTheStagger()
        {
            var judge = new StaggerJudge(Spec(limbDamage: 10f, kneelSeconds: 1.5f));

            Assert.That(judge.Hit(HitZone.Limb, DamageKind.Bullet, 19, 1f).Outcome, Is.EqualTo(StaggerOutcome.BelowThreshold), "needs 20");

            var verdict = judge.Hit(HitZone.Limb, DamageKind.Bullet, 1, 1f);
            Assert.That(verdict.Outcome, Is.EqualTo(StaggerOutcome.Staggered));
            Assert.That(verdict.Seconds, Is.EqualTo(0.75f));
            Assert.That(verdict.Threshold, Is.EqualTo(20f));
        }

        [Test]
        public void AZeroDurationKindNeverStaggers()
        {
            var judge = new StaggerJudge(StaggerSpec.PlayerDefault);

            Assert.That(judge.Hit(HitZone.Head, DamageKind.Bullet, 100, 0f).Outcome, Is.EqualTo(StaggerOutcome.NoReaction));
            Assert.That(judge.Hit(HitZone.Limb, DamageKind.Bullet, 100, 0f).Outcome, Is.EqualTo(StaggerOutcome.NoReaction));

            var melee = judge.Hit(null, DamageKind.Melee, 1, 0f);
            Assert.That(melee.Outcome, Is.EqualTo(StaggerOutcome.Staggered));
            Assert.That(melee.Kind, Is.EqualTo(StaggerKind.Stumble));
            Assert.That(melee.Seconds, Is.EqualTo(StaggerSpec.DefaultPlayerStumbleSeconds));
        }

        [Test]
        public void ExplosionsStaggerTheEnemyByDefault()
        {
            var verdict = new StaggerJudge(StaggerSpec.EnemyDefault).Hit(null, DamageKind.Explosion, 30, 0f);

            Assert.That(verdict.Kind, Is.EqualTo(StaggerKind.Blast));
            Assert.That(verdict.Seconds, Is.EqualTo(StaggerSpec.DefaultEnemyBlastSeconds));
        }

        [Test]
        public void AZeroDamageHitDoesNotStagger()
        {
            var judge = new StaggerJudge(Spec());

            Assert.That(judge.Hit(null, DamageKind.Melee, 0, 0f).Outcome, Is.EqualTo(StaggerOutcome.BelowThreshold));
            Assert.That(judge.IsStaggered, Is.False);
        }

        [Test]
        public void ResetClearsEverything()
        {
            var judge = new StaggerJudge(Spec());
            judge.Hit(HitZone.Limb, DamageKind.Bullet, 19, 0f);

            judge.Reset();

            Assert.That(judge.IsStaggered, Is.False);
            Assert.That(judge.IsImmune, Is.False);
            Assert.That(judge.SecondsRemaining, Is.Zero);
            Assert.That(judge.Hit(HitZone.Limb, DamageKind.Bullet, 19, 0f).Outcome, Is.EqualTo(StaggerOutcome.Staggered));
        }

        [Test]
        public void RejectsInvalidArguments()
        {
            Assert.Throws<ArgumentNullException>(() => _ = new StaggerJudge(null));

            var judge = new StaggerJudge(Spec());
            Assert.Throws<ArgumentOutOfRangeException>(() => judge.Hit(HitZone.Limb, DamageKind.Bullet, -1, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => judge.Hit(HitZone.Limb, DamageKind.Bullet, 1, -0.1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => judge.Hit(HitZone.Limb, DamageKind.Bullet, 1, float.PositiveInfinity));
            Assert.Throws<ArgumentOutOfRangeException>(() => judge.Hit(HitZone.Limb, DamageKind.Bullet, 1, float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => judge.Tick(-1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => judge.Tick(float.NaN));
            Assert.That(judge.IsStaggered, Is.False);
        }
    }
}
