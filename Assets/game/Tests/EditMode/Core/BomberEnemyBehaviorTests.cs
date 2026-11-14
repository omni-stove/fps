using System;
using System.Collections.Generic;
using System.Numerics;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    // Unity の EditMode テストと dotnet/Fps.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class BomberEnemyBehaviorTests
    {
        private static readonly Vector3 _self = Vector3.Zero;
        private static readonly EnemyDecision _planned = new EnemyDecision(new Vector3(9f, 0f, 9f), new Vector3(0f, 0f, 9f), true, true);

        // 合図の距離 3m・合図 2 秒・走る倍率 1.5。
        private static readonly BomberSpec _spec = new BomberSpec(3f, 2f, 1.5f, new ExplosionDamage(80, 5f, 1f), new ExplosionDamage(20, 2f));

        private static EnemyBrain Brain(EnemyArchetype archetype = EnemyArchetype.Bomber) =>
            new EnemyBrain(new EnemySpec(12f, 8f, 1f, 2f, 0.5f, archetype: archetype, bomber: _spec));

        private static EnemyDecision Step(BomberEnemyBehavior behavior, EnemyBrain brain, float deltaSeconds, Vector3 target, bool lineOfSight = true)
        {
            behavior.TryOverride(brain, deltaSeconds, new EnemyPerception(_self, target, lineOfSight, false, null, false, null, 1f), _planned, out var decision);
            return decision;
        }

        [Test]
        public void ItRunsAtASeenTargetWithoutShooting()
        {
            var behavior = new BomberEnemyBehavior();
            var target = new Vector3(0f, 0f, 8f);

            var decision = Step(behavior, Brain(), 0.1f, target);

            Assert.That(behavior.Phase, Is.EqualTo(BomberPhase.Approaching));
            Assert.That(decision.MoveTo, Is.EqualTo(target));
            Assert.That(decision.LookAt, Is.EqualTo(target));
            Assert.That(decision.Fire, Is.False);
            Assert.That(decision.Crouch, Is.False);
        }

        [Test]
        public void CloseByItLightsTheFuseAndKeepsComing()
        {
            var behavior = new BomberEnemyBehavior();
            var phases = new List<BomberPhase>();
            behavior.PhaseChanged += (_, next) => phases.Add(next);
            var target = new Vector3(0f, 0f, 2.5f);

            var decision = Step(behavior, Brain(), 0.1f, target);

            Assert.That(behavior.Phase, Is.EqualTo(BomberPhase.Fusing));
            Assert.That(phases, Is.EqualTo(new[] { BomberPhase.Fusing }));
            Assert.That(behavior.FuseElapsed, Is.EqualTo(0f));
            Assert.That(decision.MoveTo, Is.EqualTo(target));
            Assert.That(decision.Fire, Is.False);
        }

        [Test]
        public void TheFuseBurnsOutAfterTheFuseTime()
        {
            var behavior = new BomberEnemyBehavior();
            var brain = Brain();
            var target = new Vector3(0f, 0f, 2.5f);
            Step(behavior, brain, 0f, target);

            Step(behavior, brain, 1.5f, target);
            Assert.That(behavior.IsFuseBurnt, Is.False);

            Step(behavior, brain, 0.5f, target);
            Assert.That(behavior.IsFuseBurnt, Is.True);
            Assert.That(behavior.Phase, Is.EqualTo(BomberPhase.Fusing), "the runtime detonates it");
        }

        [Test]
        public void HidingDoesNotPutOutTheFuse()
        {
            var behavior = new BomberEnemyBehavior();
            var brain = Brain();
            var seenAt = new Vector3(0f, 0f, 2.5f);
            Step(behavior, brain, 0f, seenAt);

            var decision = Step(behavior, brain, 1f, new Vector3(4f, 0f, 4f), lineOfSight: false);

            Assert.That(behavior.Phase, Is.EqualTo(BomberPhase.Fusing));
            Assert.That(behavior.FuseElapsed, Is.EqualTo(1f));
            Assert.That(decision.MoveTo, Is.EqualTo(seenAt), "it runs to where it last saw the target");
        }

        [Test]
        public void TheRuntimeCanBurnTheFuseWhileTheBrainIsHeld()
        {
            var behavior = new BomberEnemyBehavior();
            Assert.That(behavior.AdvanceFuse(5f), Is.False, "nothing burns before the fuse is lit");

            Step(behavior, Brain(), 0f, new Vector3(0f, 0f, 2.5f));

            Assert.That(behavior.AdvanceFuse(1f), Is.False);
            Assert.That(behavior.AdvanceFuse(1f), Is.True);
            Assert.Throws<ArgumentOutOfRangeException>(() => behavior.AdvanceFuse(-1f));
        }

        [Test]
        public void DetonatingOnlyOnceReportsThePhaseItWasIn()
        {
            var behavior = new BomberEnemyBehavior();

            Assert.That(behavior.Detonate(), Is.EqualTo(BomberPhase.Approaching));
            Assert.That(behavior.Phase, Is.EqualTo(BomberPhase.Exploded));
            Assert.That(behavior.Detonate(), Is.EqualTo(BomberPhase.Exploded));

            var decision = Step(behavior, Brain(), 0.1f, new Vector3(0f, 0f, 2.5f));
            Assert.That(decision.MoveTo.HasValue, Is.False);
            Assert.That(decision.Fire, Is.False);
        }

        [Test]
        public void ResetStartsOver()
        {
            var behavior = new BomberEnemyBehavior();
            Step(behavior, Brain(), 0f, new Vector3(0f, 0f, 2.5f));
            behavior.Detonate();

            behavior.Reset();

            Assert.That(behavior.Phase, Is.EqualTo(BomberPhase.Approaching));
            Assert.That(behavior.FuseElapsed, Is.EqualTo(0f));
            Assert.That(behavior.LastKnownTarget.HasValue, Is.False);
        }

        [Test]
        public void AnotherArchetypeIsLeftAlone()
        {
            var behavior = new BomberEnemyBehavior();

            var overridden = behavior.TryOverride(
                Brain(EnemyArchetype.Rifleman), 0.1f, new EnemyPerception(_self, new Vector3(0f, 0f, 2f), true, false, null), _planned, out var decision);

            Assert.That(overridden, Is.False);
            Assert.That(decision, Is.EqualTo(_planned));
            Assert.That(behavior.Phase, Is.EqualTo(BomberPhase.Approaching));
        }
    }
}
