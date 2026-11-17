using System.Numerics;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class SquadOrderArrivalTests
    {
        private static readonly SquadOrderSpec _spec = new SquadOrderSpec(followDistance: 3f, leashDistance: 10f);
        private static readonly Vector3 _self = Vector3.Zero;

        // 行き先まで水平に 1.5m。既定の到着距離 (0.5m) では遠く、2m では着いている。
        private static readonly Vector3 _place = new Vector3(1.5f, 0f, 0f);

        private static readonly EnemyDecision _patrolling = new EnemyDecision(new Vector3(-5f, 0f, 0f), null, false);

        private static BotInputSpec Input(float arrivalDistance)
        {
            return new BotInputSpec(
                BotInputSpec.DefaultMaxTurnDegreesPerSecond,
                BotInputSpec.DefaultAimErrorPerMeter,
                BotInputSpec.DefaultAimErrorRefreshSeconds,
                BotInputSpec.DefaultFireToleranceDegrees,
                arrivalDistance,
                BotInputSpec.DefaultSprintDistance,
                BotInputSpec.DefaultSprintMaxAngleDegrees,
                false);
        }

        [Test]
        public void HoldArrivesWithinTheConfiguredArrivalDistance()
        {
            var decision = BotDriver.FollowOrder(
                _patrolling, EnemyState.Patrol, BotErrand.Hold(_place), _self, _spec, Input(2f), out var steered);

            Assert.That(steered, Is.True);
            Assert.That(decision.MoveTo, Is.Null, "1.5m is inside the 2m arrival distance, so it stays");
        }

        [Test]
        public void HoldKeepsGoingOutsideTheConfiguredArrivalDistance()
        {
            var decision = BotDriver.FollowOrder(
                _patrolling, EnemyState.Patrol, BotErrand.Hold(_place), _self, _spec, Input(1f), out var steered);

            Assert.That(steered, Is.True);
            Assert.That(decision.MoveTo, Is.EqualTo(_place));
        }

        [Test]
        public void AttackStopsWithinTheConfiguredArrivalDistance()
        {
            var near = BotDriver.FollowOrder(
                _patrolling, EnemyState.Patrol, BotErrand.Attack(_place), _self, _spec, Input(2f), out var steered);
            Assert.That(steered, Is.True);
            Assert.That(near.MoveTo, Is.Null);
            Assert.That(near.LookAt, Is.EqualTo(_place));

            var far = BotDriver.FollowOrder(
                _patrolling, EnemyState.Patrol, BotErrand.Attack(_place), _self, _spec, Input(1f), out _);
            Assert.That(far.MoveTo, Is.EqualTo(_place));
        }
    }
}
