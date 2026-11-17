using System;
using System.Numerics;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class EnemyArrivalAndGaitTests
    {
        private static readonly Vector3[] _route =
        {
            new Vector3(0f, 0f, 10f),
            new Vector3(10f, 0f, 10f),
        };

        private static readonly Vector3[] _perches =
        {
            new Vector3(0f, 4f, 0f),
            new Vector3(5f, 4f, 0f),
        };

        private static readonly Vector3 _sniperTarget = new Vector3(0f, 0f, 20f);

        private static EnemySpec Spec(float arrivalDistance) =>
            new EnemySpec(20f, 10f, 1f, 2f, 0.5f, arrivalDistance: arrivalDistance);

        private static EnemyDecision Patrol(EnemySpec spec, Vector3 self)
        {
            var brain = new EnemyBrain(spec);
            brain.SetPatrolRoute(_route);
            return brain.Tick(0.1f, new EnemyPerception(self, null, false, false, null));
        }

        [Test]
        public void AnEnemyWithArrivalDistanceTwoMovesToTheNextPointTwoMetresShort()
        {
            var decision = Patrol(Spec(2f), new Vector3(0f, 0f, 8f));

            Assert.That(decision.MoveTo, Is.EqualTo(_route[1]));
        }

        [Test]
        public void AnEnemyWithArrivalDistanceTwoKeepsGoingWhenFartherThanTwoMetres()
        {
            var decision = Patrol(Spec(2f), new Vector3(0f, 0f, 7.9f));

            Assert.That(decision.MoveTo, Is.EqualTo(_route[0]));
        }

        [Test]
        public void TheDefaultArrivalDistanceStillWalksOnTwoMetresShort()
        {
            var decision = Patrol(EnemySpec.Default, new Vector3(0f, 0f, 8f));

            Assert.That(decision.MoveTo, Is.EqualTo(_route[0]));
        }

        [Test]
        public void ASniperReachesItsNextPerchWithinItsArrivalDistance()
        {
            Assert.That(Relocate(2f), Is.EqualTo(SniperPhase.Holding));
            Assert.That(Relocate(EnemySpec.DefaultArrivalDistance), Is.EqualTo(SniperPhase.Relocating));
        }

        // 1 つ目の足場から撃って 2 つ目へ移る途中、足場の 1.5 m 手前で 1 tick 進めた後の段階。
        private static SniperPhase Relocate(float arrivalDistance)
        {
            var brain = new EnemyBrain(new EnemySpec(
                30f, 30f, 1f, 2f, 0.5f, archetype: EnemyArchetype.Sniper, sniper: new SniperSpec(2f, 3f, 1f, 5f), arrivalDistance: arrivalDistance));
            var behavior = new SniperEnemyBehavior();
            behavior.SetPerches(_perches);
            var planned = new EnemyDecision(_sniperTarget, _sniperTarget, true, false);
            var seen = new EnemyPerception(_perches[0], _sniperTarget, true, false, null, false, null, 1f);
            behavior.TryOverride(brain, 0f, seen, planned, out _);
            for (var i = 0; i < 4; i++)
            {
                behavior.TryOverride(brain, 0.5f, seen, planned, out _);
            }

            Assert.That(behavior.ConfirmShot(_perches[0]), Is.True);
            Assert.That(behavior.Phase, Is.EqualTo(SniperPhase.Relocating));
            var onTheWay = new EnemyPerception(new Vector3(3.5f, 4f, 0f), _sniperTarget, true, false, null, false, null, 1f);
            behavior.TryOverride(brain, 0.1f, onTheWay, planned, out _);
            return behavior.Phase;
        }

        [Test]
        public void ChangingTheSprintRatioChangesTheSprintSpeed()
        {
            Assert.That(EnemyGait.Speed(4f, sprinting: true, crouching: false, 1.6f, 0.5f), Is.EqualTo(6.4f).Within(1e-5f));
            Assert.That(EnemyGait.Speed(4f, sprinting: true, crouching: false, 2.5f, 0.5f), Is.EqualTo(10f).Within(1e-5f));
        }

        [Test]
        public void TheSprintRatioDoesNotChangeTheWalkOrCrouchSpeed()
        {
            Assert.That(EnemyGait.Speed(4f, sprinting: false, crouching: false, 2.5f, 0.5f), Is.EqualTo(4f));
            Assert.That(EnemyGait.Speed(4f, sprinting: true, crouching: true, 2.5f, 0.25f), Is.EqualTo(1f).Within(1e-5f));
        }

        [TestCase(0f, 0.5f)]
        [TestCase(-1f, 0.5f)]
        [TestCase(float.NaN, 0.5f)]
        [TestCase(float.PositiveInfinity, 0.5f)]
        [TestCase(1.6f, 0f)]
        [TestCase(1.6f, float.NaN)]
        public void SpeedRejectsRatiosThatAreNotPositiveAndFinite(float sprintRatio, float crouchRatio)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => EnemyGait.Speed(4f, true, false, sprintRatio, crouchRatio));
        }
    }
}
