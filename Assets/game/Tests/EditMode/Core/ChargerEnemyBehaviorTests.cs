using System;
using System.Collections.Generic;
using System.Numerics;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    // Unity の EditMode テストと dotnet/Fps.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class ChargerEnemyBehaviorTests
    {
        private static readonly Vector3 _self = Vector3.Zero;
        private static readonly EnemyDecision _planned = new EnemyDecision(new Vector3(9f, 0f, 9f), new Vector3(0f, 0f, 9f), true, true);

        // 構えの距離 6m・構え 1 秒・速さ 10m/s・距離 10m (1 秒)・届く 1m・ダメージ 30・よろめき 2 秒・立て直し 0.5 秒。
        private static readonly ChargerSpec _spec = new ChargerSpec(6f, 1f, 10f, 10f, 1f, 30, 2f, 0.5f);

        private static EnemyBrain Brain(EnemyArchetype archetype = EnemyArchetype.Charger) =>
            new EnemyBrain(new EnemySpec(12f, 8f, 1f, 2f, 0.5f, archetype: archetype, charger: _spec));

        private static EnemyPerception See(Vector3 target, bool lineOfSight = true) =>
            new EnemyPerception(_self, target, lineOfSight, false, null, false, null, 1f);

        private static EnemyDecision Step(ChargerEnemyBehavior behavior, EnemyBrain brain, float deltaSeconds, Vector3 target, bool lineOfSight = true)
        {
            behavior.TryOverride(brain, deltaSeconds, See(target, lineOfSight), _planned, out var decision);
            return decision;
        }

        private static (ChargerEnemyBehavior Behavior, EnemyBrain Brain) Charging(Vector3 target)
        {
            var behavior = new ChargerEnemyBehavior();
            var brain = Brain();
            Step(behavior, brain, 0f, target);
            Step(behavior, brain, 1f, target);
            Assert.That(behavior.Phase, Is.EqualTo(ChargerPhase.Charging));
            return (behavior, brain);
        }

        [Test]
        public void AFarTargetIsChasedWithoutShooting()
        {
            var behavior = new ChargerEnemyBehavior();
            var target = new Vector3(0f, 0f, 10f);

            var decision = Step(behavior, Brain(), 0.1f, target);

            Assert.That(behavior.Phase, Is.EqualTo(ChargerPhase.Ready));
            Assert.That(decision.MoveTo, Is.EqualTo(target));
            Assert.That(decision.LookAt, Is.EqualTo(target));
            Assert.That(decision.Fire, Is.False);
            Assert.That(decision.Crouch, Is.False);
        }

        [Test]
        public void ANearTargetIsWoundUpForStandingStill()
        {
            var behavior = new ChargerEnemyBehavior();
            var phases = new List<ChargerPhase>();
            behavior.PhaseChanged += (_, next) => phases.Add(next);
            var target = new Vector3(0f, 0f, 5f);

            var decision = Step(behavior, Brain(), 0.1f, target);

            Assert.That(behavior.Phase, Is.EqualTo(ChargerPhase.WindingUp));
            Assert.That(phases, Is.EqualTo(new[] { ChargerPhase.WindingUp }));
            Assert.That(behavior.WindupTarget, Is.EqualTo(target));
            Assert.That(decision.MoveTo.HasValue, Is.False, "it stands still to show the windup");
            Assert.That(decision.LookAt, Is.EqualTo(target));
            Assert.That(decision.Fire, Is.False);
        }

        [Test]
        public void TheChargeStartsAfterTheWindupStraightAtWhereTheTargetWas()
        {
            var behavior = new ChargerEnemyBehavior();
            var brain = Brain();
            Step(behavior, brain, 0f, new Vector3(0f, 0f, 5f));

            Step(behavior, brain, 0.5f, new Vector3(0f, 0f, 5f));
            Assert.That(behavior.Phase, Is.EqualTo(ChargerPhase.WindingUp), "it is still winding up");

            var decision = Step(behavior, brain, 0.5f, new Vector3(3f, 0f, 4f));

            Assert.That(behavior.Phase, Is.EqualTo(ChargerPhase.Charging));
            Assert.That(behavior.ChargeOrigin, Is.EqualTo(_self));
            Assert.That(behavior.ChargeDirection.Value.X, Is.EqualTo(0.6f).Within(1e-5f));
            Assert.That(behavior.ChargeDirection.Value.Z, Is.EqualTo(0.8f).Within(1e-5f));
            Assert.That(decision.MoveTo.HasValue, Is.False, "the body is moved by the runtime, not the path finder");
            Assert.That(decision.Fire, Is.False);
        }

        [Test]
        public void TheChargeDoesNotSteerAfterTheTarget()
        {
            var (behavior, brain) = Charging(new Vector3(0f, 0f, 5f));

            var decision = Step(behavior, brain, 0.1f, new Vector3(5f, 0f, 0f));

            Assert.That(behavior.ChargeDirection, Is.EqualTo(Vector3.UnitZ));
            Assert.That(decision.LookAt.Value.X, Is.EqualTo(0f));
            Assert.That(decision.LookAt.Value.Z, Is.GreaterThan(0f));
        }

        [Test]
        public void LosingSightDuringTheWindupCancelsIt()
        {
            var behavior = new ChargerEnemyBehavior();
            var brain = Brain();
            Step(behavior, brain, 0f, new Vector3(0f, 0f, 5f));

            var decision = Step(behavior, brain, 0.5f, new Vector3(0f, 0f, 5f), lineOfSight: false);

            Assert.That(behavior.Phase, Is.EqualTo(ChargerPhase.Ready));
            Assert.That(behavior.LastEnd, Is.EqualTo(ChargeEnd.Cancelled));
            Assert.That(decision.Fire, Is.False);
        }

        [Test]
        public void AHitRecoversBrieflyAndThenGetsReady()
        {
            var target = new Vector3(0f, 0f, 5f);
            var (behavior, brain) = Charging(target);

            Assert.That(behavior.EndCharge(ChargeEnd.Hit), Is.True);
            Assert.That(behavior.Phase, Is.EqualTo(ChargerPhase.Recovering));
            Assert.That(behavior.LastEnd, Is.EqualTo(ChargeEnd.Hit));

            Step(behavior, brain, 0.25f, target);
            Assert.That(behavior.Phase, Is.EqualTo(ChargerPhase.Recovering));
            Assert.That(Step(behavior, brain, 0.25f, target).MoveTo.HasValue, Is.False);
            Assert.That(behavior.Phase, Is.EqualTo(ChargerPhase.Ready));
        }

        [Test]
        public void HittingAWallStunsItForTheStunTime()
        {
            var target = new Vector3(0f, 0f, 5f);
            var (behavior, brain) = Charging(target);

            Assert.That(behavior.EndCharge(ChargeEnd.Obstacle), Is.True);
            Assert.That(behavior.Phase, Is.EqualTo(ChargerPhase.Stunned));

            var decision = Step(behavior, brain, 1.5f, target);
            Assert.That(behavior.Phase, Is.EqualTo(ChargerPhase.Stunned));
            Assert.That(decision.MoveTo.HasValue, Is.False, "it stays where it hit the wall");
            Assert.That(decision.Fire, Is.False);

            Step(behavior, brain, 0.5f, target);
            Assert.That(behavior.Phase, Is.EqualTo(ChargerPhase.Ready));
        }

        [Test]
        public void RunningTheWholeDistanceWithoutAHitCountsAsAMiss()
        {
            var target = new Vector3(0f, 0f, 5f);
            var (behavior, brain) = Charging(target);

            Step(behavior, brain, 0.5f, target);
            Assert.That(behavior.Phase, Is.EqualTo(ChargerPhase.Charging));

            Step(behavior, brain, 0.5f, target);
            Assert.That(behavior.Phase, Is.EqualTo(ChargerPhase.Stunned));
            Assert.That(behavior.LastEnd, Is.EqualTo(ChargeEnd.Spent));
        }

        [Test]
        public void EndingIsOnlyForACharge()
        {
            var behavior = new ChargerEnemyBehavior();

            Assert.That(behavior.EndCharge(ChargeEnd.Hit), Is.False);
            Assert.Throws<ArgumentOutOfRangeException>(() => behavior.EndCharge(ChargeEnd.None));
            Assert.Throws<ArgumentOutOfRangeException>(() => behavior.EndCharge(ChargeEnd.Cancelled));
        }

        [Test]
        public void CancellingAChargeGetsReadyAgain()
        {
            var (behavior, _) = Charging(new Vector3(0f, 0f, 5f));

            Assert.That(behavior.Cancel(), Is.True);

            Assert.That(behavior.Phase, Is.EqualTo(ChargerPhase.Ready));
            Assert.That(behavior.ChargeDirection.HasValue, Is.False);
            Assert.That(behavior.Cancel(), Is.False);
        }

        [Test]
        public void AnotherArchetypeIsLeftAlone()
        {
            var behavior = new ChargerEnemyBehavior();

            var overridden = behavior.TryOverride(Brain(EnemyArchetype.Rifleman), 0.1f, See(new Vector3(0f, 0f, 5f)), _planned, out var decision);

            Assert.That(overridden, Is.False);
            Assert.That(decision, Is.EqualTo(_planned));
            Assert.That(behavior.Phase, Is.EqualTo(ChargerPhase.Ready));
        }

        [Test]
        public void AnUnseenTargetFollowsThePlannedSearchWithoutShooting()
        {
            var behavior = new ChargerEnemyBehavior();

            var decision = Step(behavior, Brain(), 0.1f, new Vector3(0f, 0f, 5f), lineOfSight: false);

            Assert.That(decision.MoveTo, Is.EqualTo(_planned.MoveTo));
            Assert.That(decision.Fire, Is.False);
            Assert.That(decision.Crouch, Is.False);
            Assert.That(behavior.Phase, Is.EqualTo(ChargerPhase.Ready));
        }
    }
}
