using System;
using System.Collections.Generic;
using System.Numerics;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    // Unity の EditMode テストと dotnet/Fps.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class SniperEnemyBehaviorTests
    {
        private static readonly Vector3 _target = new Vector3(0f, 0f, 20f);
        private static readonly Vector3[] _perches =
        {
            new Vector3(0f, 4f, 0f),
            new Vector3(5f, 4f, 0f),
            new Vector3(10f, 4f, 0f),
        };

        // 照準 2 秒・ダメージ 3 倍・狙い直しまで 1 秒・移るのを諦めるまで 5 秒。
        private static readonly EnemyDecision _planned = new EnemyDecision(_target, new Vector3(0f, 0f, 9f), true, true);

        private static EnemyBrain Brain(EnemyArchetype archetype = EnemyArchetype.Sniper) =>
            new EnemyBrain(new EnemySpec(30f, 30f, 1f, 2f, 0.5f, archetype: archetype, sniper: new SniperSpec(2f, 3f, 1f, 5f)));

        private static SniperEnemyBehavior Behavior(bool withPerches = true)
        {
            var behavior = new SniperEnemyBehavior();
            if (withPerches)
            {
                behavior.SetPerches(_perches);
            }

            return behavior;
        }

        private static EnemyPerception See(Vector3 self, bool lineOfSight = true) =>
            new EnemyPerception(self, _target, lineOfSight, false, null, false, null, 1f);

        private static EnemyDecision Step(SniperEnemyBehavior behavior, EnemyBrain brain, float deltaSeconds, Vector3 self, bool lineOfSight = true)
        {
            behavior.TryOverride(brain, deltaSeconds, See(self, lineOfSight), _planned, out var decision);
            return decision;
        }

        private static void AimUntilFiring(SniperEnemyBehavior behavior, EnemyBrain brain, Vector3 self)
        {
            Step(behavior, brain, 0f, self);
            for (var i = 0; i < 4; i++)
            {
                Step(behavior, brain, 0.5f, self);
            }
        }

        [Test]
        public void ASeenTargetIsAimedAtFromWhereItStands()
        {
            var behavior = Behavior();

            var decision = Step(behavior, Brain(), 0.1f, _perches[0]);

            Assert.That(behavior.Phase, Is.EqualTo(SniperPhase.Aiming));
            Assert.That(behavior.AimTarget, Is.EqualTo(_target));
            Assert.That(decision.MoveTo.HasValue, Is.False, "it does not chase down from its perch");
            Assert.That(decision.LookAt, Is.EqualTo(_target));
            Assert.That(decision.Fire, Is.False);
            Assert.That(decision.Crouch, Is.False);
        }

        [Test]
        public void ItShootsOnlyAfterShowingTheAimForTheAimTime()
        {
            var behavior = Behavior();
            var brain = Brain();
            Step(behavior, brain, 0f, _perches[0]);

            for (var i = 0; i < 3; i++)
            {
                Assert.That(Step(behavior, brain, 0.5f, _perches[0]).Fire, Is.False, $"step {i}");
            }

            Assert.That(behavior.AimElapsed, Is.EqualTo(1.5f));
            var decision = Step(behavior, brain, 0.5f, _perches[0]);
            Assert.That(behavior.Phase, Is.EqualTo(SniperPhase.Firing));
            Assert.That(decision.Fire, Is.True);
            Assert.That(decision.MoveTo.HasValue, Is.False);

            Assert.That(Step(behavior, brain, 0.1f, _perches[0]).Fire, Is.True, "it keeps the trigger until the shot is confirmed");
        }

        [Test]
        public void LosingSightCancelsTheAimWithoutShooting()
        {
            var behavior = Behavior();
            var brain = Brain();
            Step(behavior, brain, 0f, _perches[0]);
            Step(behavior, brain, 1.5f, _perches[0]);

            var decision = Step(behavior, brain, 0.5f, _perches[0], lineOfSight: false);

            Assert.That(behavior.Phase, Is.EqualTo(SniperPhase.Holding));
            Assert.That(decision.Fire, Is.False);
            Assert.That(behavior.AimTarget.HasValue, Is.False);

            Step(behavior, brain, 0.1f, _perches[0]);
            Assert.That(behavior.Phase, Is.EqualTo(SniperPhase.Aiming));
            Assert.That(behavior.AimElapsed, Is.EqualTo(0f), "the next aim starts over");
        }

        [Test]
        public void ATargetBeyondTheAttackRangeIsNotAimedAt()
        {
            var behavior = Behavior();

            behavior.TryOverride(Brain(), 0.1f, new EnemyPerception(_perches[0], new Vector3(0f, 0f, 40f), true, false, null), _planned, out var decision);

            Assert.That(behavior.Phase, Is.EqualTo(SniperPhase.Holding));
            Assert.That(decision.MoveTo.HasValue, Is.False, "it stays on its perch instead of chasing");
            Assert.That(decision.Fire, Is.False);
        }

        [Test]
        public void AfterTheShotItMovesToTheNextPerch()
        {
            var behavior = Behavior();
            var brain = Brain();
            AimUntilFiring(behavior, brain, _perches[0]);

            Assert.That(behavior.ConfirmShot(_perches[0]), Is.True);
            Assert.That(behavior.Phase, Is.EqualTo(SniperPhase.Relocating));
            Assert.That(behavior.PerchIndex, Is.EqualTo(1));

            var decision = Step(behavior, brain, 0.1f, _perches[0]);
            Assert.That(decision.MoveTo, Is.EqualTo(_perches[1]));
            Assert.That(decision.Fire, Is.False);
            Assert.That(behavior.ConfirmShot(_perches[0]), Is.False, "only a pending shot can be confirmed");
        }

        [Test]
        public void ThePerchesAreVisitedInTurnAndWrapAround()
        {
            var behavior = Behavior();
            var brain = Brain();
            AimUntilFiring(behavior, brain, _perches[2]);

            behavior.ConfirmShot(_perches[2] + new Vector3(0.2f, 0f, 0f));

            Assert.That(behavior.PerchIndex, Is.EqualTo(0));
        }

        [Test]
        public void ArrivingWaitsBeforeAimingAgain()
        {
            var behavior = Behavior();
            var brain = Brain();
            AimUntilFiring(behavior, brain, _perches[0]);
            behavior.ConfirmShot(_perches[0]);

            var arrived = Step(behavior, brain, 0.1f, _perches[1]);
            Assert.That(behavior.Phase, Is.EqualTo(SniperPhase.Holding));
            Assert.That(behavior.LastRelocationGaveUp, Is.False);
            Assert.That(arrived.MoveTo.HasValue, Is.False);
            Assert.That(behavior.ReaimRemaining, Is.EqualTo(1f));

            Step(behavior, brain, 0.5f, _perches[1]);
            Assert.That(behavior.Phase, Is.EqualTo(SniperPhase.Holding));
            Step(behavior, brain, 0.5f, _perches[1]);
            Assert.That(behavior.Phase, Is.EqualTo(SniperPhase.Aiming));
        }

        [Test]
        public void ItGivesUpAPerchItCannotReach()
        {
            var behavior = Behavior();
            var brain = Brain();
            AimUntilFiring(behavior, brain, _perches[0]);
            behavior.ConfirmShot(_perches[0]);

            Step(behavior, brain, 2.5f, _perches[0]);
            Assert.That(behavior.Phase, Is.EqualTo(SniperPhase.Relocating));
            var decision = Step(behavior, brain, 2.5f, _perches[0]);

            Assert.That(behavior.Phase, Is.EqualTo(SniperPhase.Holding));
            Assert.That(behavior.LastRelocationGaveUp, Is.True);
            Assert.That(decision.MoveTo.HasValue, Is.False);
        }

        [Test]
        public void WithoutAnotherPerchItStaysAndAimsAgainLater()
        {
            var behavior = Behavior(withPerches: false);
            var brain = Brain();
            AimUntilFiring(behavior, brain, _perches[0]);

            Assert.That(behavior.ConfirmShot(_perches[0]), Is.True);
            Assert.That(behavior.Phase, Is.EqualTo(SniperPhase.Holding));
            Assert.That(behavior.ReaimRemaining, Is.EqualTo(1f));

            var single = Behavior(withPerches: false);
            single.SetPerches(new[] { _perches[0] });
            AimUntilFiring(single, brain, _perches[0]);
            single.ConfirmShot(_perches[0]);
            Assert.That(single.Phase, Is.EqualTo(SniperPhase.Holding), "it is already on its only perch");
        }

        [Test]
        public void EachStepIsReported()
        {
            var behavior = Behavior();
            var brain = Brain();
            var steps = new List<SniperPhase>();
            behavior.PhaseChanged += (_, next) => steps.Add(next);

            AimUntilFiring(behavior, brain, _perches[0]);
            behavior.ConfirmShot(_perches[0]);
            Step(behavior, brain, 0.1f, _perches[1]);

            Assert.That(steps, Is.EqualTo(new[] { SniperPhase.Aiming, SniperPhase.Firing, SniperPhase.Relocating, SniperPhase.Holding }));
        }

        [Test]
        public void CancelAimStopsAnAimInProgress()
        {
            var behavior = Behavior();
            Assert.That(behavior.CancelAim(), Is.False);

            Step(behavior, Brain(), 0f, _perches[0]);
            Assert.That(behavior.CancelAim(), Is.True);
            Assert.That(behavior.Phase, Is.EqualTo(SniperPhase.Holding));
        }

        [Test]
        public void OtherArchetypesAndADisabledBehaviorKeepThePlan()
        {
            var behavior = Behavior();
            Assert.That(behavior.TryOverride(Brain(EnemyArchetype.Rifleman), 0.1f, See(_perches[0]), _planned, out var rifleman), Is.False);
            Assert.That(rifleman.Fire, Is.True);
            Assert.That(behavior.Phase, Is.EqualTo(SniperPhase.Holding));

            var brain = Brain();
            Step(behavior, brain, 0f, _perches[0]);
            behavior.Enabled = false;
            Assert.That(behavior.TryOverride(brain, 0.1f, See(_perches[0]), _planned, out _), Is.False);
            Assert.That(behavior.Phase, Is.EqualTo(SniperPhase.Holding), "turning it off drops the aim");
        }

        [Test]
        public void TheBrainRunsTheBehavior()
        {
            var brain = Brain();
            var behavior = Behavior();
            brain.AddBehavior(behavior);

            brain.Tick(0.1f, See(_perches[0]));
            var decision = brain.Tick(1f, See(_perches[0]));

            Assert.That(brain.State, Is.EqualTo(EnemyState.Attack));
            Assert.That(decision.Fire, Is.False, "the brain would shoot after its reaction time");
            Assert.That(behavior.Phase, Is.EqualTo(SniperPhase.Aiming));
        }

        [Test]
        public void ResetForgetsTheAimAndThePerch()
        {
            var behavior = Behavior();
            var brain = Brain();
            AimUntilFiring(behavior, brain, _perches[0]);
            behavior.ConfirmShot(_perches[0]);

            behavior.Reset();

            Assert.That(behavior.Phase, Is.EqualTo(SniperPhase.Holding));
            Assert.That(behavior.PerchIndex, Is.EqualTo(-1));
            Assert.That(behavior.ReaimRemaining, Is.EqualTo(0f));
        }

        [Test]
        public void NullArgumentsAreRejected()
        {
            Assert.Throws<ArgumentNullException>(() => Behavior().SetPerches(null));
            Assert.Throws<ArgumentNullException>(() => Behavior().TryOverride(null, 0.1f, See(Vector3.Zero), _planned, out _));
        }
    }
}
