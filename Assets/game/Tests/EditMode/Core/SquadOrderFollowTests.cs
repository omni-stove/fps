using System;
using System.Numerics;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    // Unity の EditMode テストと dotnet/Fps.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class SquadOrderFollowTests
    {
        private static readonly SquadOrderSpec _spec = new SquadOrderSpec(followDistance: 3f, leashDistance: 10f);
        private static readonly Vector3 _self = Vector3.Zero;
        private static readonly Vector3 _enemy = new Vector3(0f, 1f, 8f);

        // 撃ち合っている判断: 敵を見て撃ち、その場に留まる (Attack)。
        private static readonly EnemyDecision _fighting = new EnemyDecision(null, _enemy, true, true);

        // 追っている判断: 敵の方へ動き、まだ撃たない (Chase)。
        private static readonly EnemyDecision _chasing = new EnemyDecision(_enemy, _enemy, false);

        // 巡回の判断: 巡回地点へ動く (Patrol)。
        private static readonly EnemyDecision _patrolling = new EnemyDecision(new Vector3(-5f, 0f, 0f), null, false);

        [Test]
        public void WithoutAnOrderTheDecisionIsKept()
        {
            var decision = BotDriver.FollowOrder(_chasing, EnemyState.Chase, BotErrand.None, _self, _spec, out var steered);

            Assert.That(steered, Is.False);
            Assert.That(decision.MoveTo, Is.EqualTo(_chasing.MoveTo));
            Assert.That(decision.Fire, Is.EqualTo(_chasing.Fire));

            var errand = new BotErrand(BotErrandKind.EnterZone, new Vector3(9f, 0f, 9f), -1, true);
            BotDriver.FollowOrder(_chasing, EnemyState.Chase, errand, _self, _spec, out steered);
            Assert.That(steered, Is.False, "a mode's errand is not a squad order");
        }

        [Test]
        public void HoldGoesToThePlaceEvenWhileFightingButKeepsShooting()
        {
            var place = new Vector3(6f, 0f, 0f);

            var decision = BotDriver.FollowOrder(_chasing, EnemyState.Chase, BotErrand.Hold(place), _self, _spec, out var steered);

            Assert.That(steered, Is.True);
            Assert.That(decision.MoveTo, Is.EqualTo(place), "the bot does not chase away from the order");
            Assert.That(decision.LookAt, Is.EqualTo(_enemy));

            decision = BotDriver.FollowOrder(_fighting, EnemyState.Attack, BotErrand.Hold(place), _self, _spec, out _);
            Assert.That(decision.MoveTo, Is.EqualTo(place));
            Assert.That(decision.Fire, Is.True, "it shoots back on the way");
            Assert.That(decision.Crouch, Is.False, "it does not crouch while walking");
        }

        [Test]
        public void HoldStaysAtThePlaceAndFightsBackFromThere()
        {
            var place = new Vector3(0.3f, 0f, 0f);

            var decision = BotDriver.FollowOrder(_fighting, EnemyState.Attack, BotErrand.Hold(place), _self, _spec, out var steered);

            Assert.That(steered, Is.True);
            Assert.That(decision.MoveTo, Is.Null, "arrived, so it stays");
            Assert.That(decision.Fire, Is.True);
            Assert.That(decision.Crouch, Is.True);

            decision = BotDriver.FollowOrder(_patrolling, EnemyState.Patrol, BotErrand.Hold(place), _self, _spec, out _);
            Assert.That(decision.MoveTo, Is.Null, "it does not wander off on its patrol");
        }

        [Test]
        public void AttackGoesToTheTargetUntilTheBotEngagesIt()
        {
            var target = new Vector3(20f, 0f, 0f);

            var decision = BotDriver.FollowOrder(_patrolling, EnemyState.Patrol, BotErrand.Attack(target), _self, _spec, out var steered);

            Assert.That(steered, Is.True);
            Assert.That(decision.MoveTo, Is.EqualTo(target));
            Assert.That(decision.LookAt, Is.EqualTo(target));
            Assert.That(decision.Fire, Is.False);

            decision = BotDriver.FollowOrder(_fighting, EnemyState.Attack, BotErrand.Attack(target), _self, _spec, out steered);
            Assert.That(steered, Is.False, "once it sees the target its own judgement fights it");
            Assert.That(decision.Fire, Is.True);
        }

        [Test]
        public void FollowStaysCloseAndComesBackFromAFightOnlyPastTheLeash()
        {
            var near = new Vector3(2f, 0f, 0f);
            var decision = BotDriver.FollowOrder(_patrolling, EnemyState.Patrol, BotErrand.Follow(near), _self, _spec, out var steered);
            Assert.That(steered, Is.True);
            Assert.That(decision.MoveTo, Is.Null, "within the follow distance it waits");

            var apart = new Vector3(5f, 0f, 0f);
            decision = BotDriver.FollowOrder(_patrolling, EnemyState.Patrol, BotErrand.Follow(apart), _self, _spec, out _);
            Assert.That(decision.MoveTo, Is.EqualTo(apart));

            decision = BotDriver.FollowOrder(_fighting, EnemyState.Attack, BotErrand.Follow(apart), _self, _spec, out steered);
            Assert.That(steered, Is.False, "inside the leash it keeps fighting");
            Assert.That(decision.Fire, Is.True);

            var far = new Vector3(0f, 0f, -12f);
            decision = BotDriver.FollowOrder(_fighting, EnemyState.Attack, BotErrand.Follow(far), _self, _spec, out steered);
            Assert.That(steered, Is.True);
            Assert.That(decision.MoveTo, Is.EqualTo(far), "past the leash it comes back");
            Assert.That(decision.Fire, Is.True, "and still shoots on the way");
        }

        [Test]
        public void DistanceIsMeasuredOnTheGround()
        {
            var above = new Vector3(0.2f, 5f, 0f);

            var decision = BotDriver.FollowOrder(_patrolling, EnemyState.Patrol, BotErrand.Hold(above), _self, _spec, out _);

            Assert.That(decision.MoveTo, Is.Null);
        }

        [Test]
        public void ADeadBotOrABadOrderIsHandled()
        {
            BotDriver.FollowOrder(EnemyDecision.None, EnemyState.Dead, BotErrand.Hold(new Vector3(9f, 0f, 0f)), _self, _spec, out var steered);
            Assert.That(steered, Is.False);

            var nan = BotErrand.Hold(new Vector3(float.NaN, 0f, 0f));
            Assert.Throws<ArgumentException>(() => BotDriver.FollowOrder(_patrolling, EnemyState.Patrol, nan, _self, _spec, out _));
            Assert.Throws<ArgumentNullException>(() => BotDriver.FollowOrder(_patrolling, EnemyState.Patrol, BotErrand.None, _self, null, out _));
        }

        [Test]
        public void OrdersAreTheThreeSquadKinds()
        {
            Assert.That(BotErrand.Hold(Vector3.Zero).IsOrder, Is.True);
            Assert.That(BotErrand.Attack(Vector3.Zero).IsOrder, Is.True);
            Assert.That(BotErrand.Follow(Vector3.Zero).IsOrder, Is.True);
            Assert.That(BotErrand.None.IsOrder, Is.False);
            Assert.That(new BotErrand(BotErrandKind.Collect, Vector3.Zero, 0, false).IsOrder, Is.False);

            var moved = BotErrand.Attack(Vector3.Zero).WithDestination(new Vector3(1f, 2f, 3f));
            Assert.That(moved.Kind, Is.EqualTo(BotErrandKind.Attack));
            Assert.That(moved.Destination, Is.EqualTo(new Vector3(1f, 2f, 3f)));
            Assert.That(moved.OverridesCombat, Is.True);
        }
    }
}
