using System;
using System.Numerics;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    // Unity の EditMode テストと dotnet/Fps.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class MeleeEnemyBehaviorTests
    {
        private static readonly Vector3 _self = Vector3.Zero;
        private static readonly EnemyDecision _planned = new EnemyDecision(new Vector3(5f, 0f, 5f), new Vector3(0f, 0f, 9f), true);

        private static EnemyBrain Brain() => new EnemyBrain(new EnemySpec(20f, 10f, 1f, 2f, 0.5f, 6f, 0f, 2));

        private static MeleeSwing Swing() => new MeleeSwing(new MeleeSpec(50, 2f, 90f, 0.5f, 0.4f, 0.2f, 0.75f));

        private static EnemyPerception See(Vector3 target, bool lineOfSight = true) =>
            new EnemyPerception(_self, target, lineOfSight, false, null, false, null, 1f);

        [Test]
        public void ATargetWithinReachIsHitInsteadOfShot()
        {
            var behavior = new MeleeEnemyBehavior(Swing());
            var target = new Vector3(0f, 0f, 1.5f);

            Assert.That(behavior.TryOverride(Brain(), 0.1f, See(target), _planned, out var decision), Is.True);
            Assert.That(behavior.WantsSwing, Is.True);
            Assert.That(decision.Fire, Is.False);
            Assert.That(decision.MoveTo.HasValue, Is.False);
            Assert.That(decision.LookAt, Is.EqualTo(target));
        }

        [Test]
        public void ATargetOutOfReachKeepsThePlan()
        {
            var behavior = new MeleeEnemyBehavior(Swing());
            Assert.That(behavior.TryOverride(Brain(), 0.1f, See(new Vector3(0f, 0f, 3f)), _planned, out var decision), Is.False);
            Assert.That(behavior.WantsSwing, Is.False);
            Assert.That(decision.Fire, Is.True);
        }

        [Test]
        public void AnUnseenTargetIsNotHit()
        {
            var behavior = new MeleeEnemyBehavior(Swing());
            Assert.That(behavior.TryOverride(Brain(), 0.1f, See(new Vector3(0f, 0f, 1f), lineOfSight: false), _planned, out _), Is.False);
            Assert.That(behavior.TryOverride(Brain(), 0.1f, new EnemyPerception(_self, null, false, false, null), _planned, out _), Is.False);
            Assert.That(behavior.WantsSwing, Is.False);
        }

        [Test]
        public void ADisabledBehaviorKeepsThePlan()
        {
            var behavior = new MeleeEnemyBehavior(Swing()) { Enabled = false };
            Assert.That(behavior.TryOverride(Brain(), 0.1f, See(new Vector3(0f, 0f, 1f)), _planned, out var decision), Is.False);
            Assert.That(decision.Fire, Is.True);
        }

        [Test]
        public void WhileSwingingTheEnemyHoldsAndDoesNotShoot()
        {
            var swing = Swing();
            swing.TryStart();
            var behavior = new MeleeEnemyBehavior(swing);

            Assert.That(behavior.TryOverride(Brain(), 0.1f, See(new Vector3(0f, 0f, 6f)), _planned, out var decision), Is.True);
            Assert.That(decision.Fire, Is.False);
            Assert.That(decision.MoveTo.HasValue, Is.False);
            Assert.That(behavior.WantsSwing, Is.False, "it does not start another swing");
        }

        [Test]
        public void TheBrainRunsTheBehavior()
        {
            var brain = Brain();
            var behavior = new MeleeEnemyBehavior(Swing());
            brain.AddBehavior(behavior);

            var decision = brain.Tick(0.1f, See(new Vector3(0f, 0f, 1f)));
            Assert.That(behavior.WantsSwing, Is.True);
            Assert.That(decision.Fire, Is.False);
        }

        [Test]
        public void NullArgumentsAreRejected()
        {
            Assert.Throws<ArgumentNullException>(() => new MeleeEnemyBehavior(null));
            var behavior = new MeleeEnemyBehavior(Swing());
            Assert.Throws<ArgumentNullException>(() => behavior.TryOverride(null, 0.1f, See(Vector3.Zero), _planned, out _));
        }
    }
}
