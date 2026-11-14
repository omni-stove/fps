using System;
using System.Numerics;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    // Unity の EditMode テストと dotnet/Fps.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class ShieldEnemyBehaviorTests
    {
        private static readonly Vector3 _self = Vector3.Zero;
        private static readonly EnemyDecision _planned = new EnemyDecision(new Vector3(5f, 0f, 5f), new Vector3(0f, 0f, 9f), true, true);

        // 止まる距離 1.2m・届く距離 1.8m。
        private static ShieldSpec Shield() => new ShieldSpec(0.5f, 1.2f, 1.5f, 2.5f, new MeleeSpec(30, 1.8f, 90f, 0.6f, 1f, 0.3f, 0.5f));

        private static EnemyBrain Brain(EnemyArchetype archetype = EnemyArchetype.Shield) =>
            new EnemyBrain(new EnemySpec(20f, 10f, 1f, 2f, 0.5f, archetype: archetype, shield: Shield()));

        private static ShieldEnemyBehavior Behavior(ShieldGuard guard = null, MeleeSwing bash = null) =>
            new ShieldEnemyBehavior(guard ?? new ShieldGuard(), bash ?? new MeleeSwing(Shield().Bash));

        private static EnemyPerception See(Vector3 target, bool lineOfSight = true) =>
            new EnemyPerception(_self, target, lineOfSight, false, null, false, null, 1f);

        [Test]
        public void ASeenTargetIsApproachedWithoutShooting()
        {
            var behavior = Behavior();
            var target = new Vector3(0f, 0f, 6f);

            Assert.That(behavior.TryOverride(Brain(), 0.1f, See(target), _planned, out var decision), Is.True);
            Assert.That(decision.MoveTo, Is.EqualTo(target));
            Assert.That(decision.LookAt, Is.EqualTo(target));
            Assert.That(decision.Fire, Is.False);
            Assert.That(decision.Crouch, Is.False);
            Assert.That(behavior.WantsBash, Is.False);
        }

        [Test]
        public void ItStopsAtTheHoldDistanceAndBashes()
        {
            var behavior = Behavior();
            var target = new Vector3(0f, 0f, 1f);

            behavior.TryOverride(Brain(), 0.1f, See(target), _planned, out var decision);

            Assert.That(decision.MoveTo.HasValue, Is.False);
            Assert.That(decision.LookAt, Is.EqualTo(target));
            Assert.That(behavior.WantsBash, Is.True);
        }

        [Test]
        public void WithinReachButBeyondTheHoldDistanceItBashesWhileClosingIn()
        {
            var behavior = Behavior();
            var target = new Vector3(0f, 0f, 1.5f);

            behavior.TryOverride(Brain(), 0.1f, See(target), _planned, out var decision);

            Assert.That(decision.MoveTo, Is.EqualTo(target));
            Assert.That(behavior.WantsBash, Is.True);
        }

        [Test]
        public void ReachIsMeasuredOnTheGround()
        {
            var behavior = Behavior();

            behavior.TryOverride(Brain(), 0.1f, See(new Vector3(0f, 3f, 1f)), _planned, out _);

            Assert.That(behavior.WantsBash, Is.True, "a target standing on a step is still in front of the shield");
        }

        [Test]
        public void WhileTheShieldIsDownItStaggersInPlace()
        {
            var guard = new ShieldGuard();
            guard.Lower(1f, "melee");
            var behavior = Behavior(guard);

            Assert.That(behavior.TryOverride(Brain(), 0.1f, See(new Vector3(0f, 0f, 1f)), _planned, out var decision), Is.True);
            Assert.That(decision.MoveTo.HasValue, Is.False);
            Assert.That(decision.LookAt, Is.EqualTo(_planned.LookAt));
            Assert.That(decision.Fire, Is.False);
            Assert.That(behavior.WantsBash, Is.False);
        }

        [Test]
        public void WhileBashingItHoldsAndFacesTheTarget()
        {
            var bash = new MeleeSwing(Shield().Bash);
            bash.TryStart();
            var behavior = Behavior(bash: bash);
            var target = new Vector3(0f, 0f, 6f);

            behavior.TryOverride(Brain(), 0.1f, See(target), _planned, out var decision);

            Assert.That(decision.MoveTo.HasValue, Is.False);
            Assert.That(decision.LookAt, Is.EqualTo(target));
            Assert.That(behavior.WantsBash, Is.False, "it does not start another bash");
        }

        [Test]
        public void AnUnseenTargetKeepsThePlannedRouteButNeverShootsOrCrouches()
        {
            var behavior = Behavior();

            Assert.That(behavior.TryOverride(Brain(), 0.1f, See(new Vector3(0f, 0f, 1f), lineOfSight: false), _planned, out var decision), Is.True);
            Assert.That(decision.MoveTo, Is.EqualTo(_planned.MoveTo));
            Assert.That(decision.LookAt, Is.EqualTo(_planned.LookAt));
            Assert.That(decision.Fire, Is.False);
            Assert.That(decision.Crouch, Is.False);
            Assert.That(behavior.WantsBash, Is.False);
        }

        [Test]
        public void ATargetBeyondSightIsNotApproached()
        {
            var behavior = Behavior();

            behavior.TryOverride(Brain(), 0.1f, See(new Vector3(0f, 0f, 25f)), _planned, out var decision);

            Assert.That(decision.MoveTo, Is.EqualTo(_planned.MoveTo));
            Assert.That(decision.Fire, Is.False);
        }

        [Test]
        public void OtherArchetypesAndADisabledBehaviorKeepThePlan()
        {
            Assert.That(Behavior().TryOverride(Brain(EnemyArchetype.Rifleman), 0.1f, See(new Vector3(0f, 0f, 1f)), _planned, out var rifleman), Is.False);
            Assert.That(rifleman.Fire, Is.True);

            var disabled = Behavior();
            disabled.Enabled = false;
            Assert.That(disabled.TryOverride(Brain(), 0.1f, See(new Vector3(0f, 0f, 1f)), _planned, out var decision), Is.False);
            Assert.That(decision.Fire, Is.True);
            Assert.That(disabled.WantsBash, Is.False);
        }

        [Test]
        public void TheBrainRunsTheBehavior()
        {
            var brain = Brain();
            var behavior = Behavior();
            brain.AddBehavior(behavior);
            var target = new Vector3(0f, 0f, 6f);

            brain.Tick(0.1f, See(target));
            var decision = brain.Tick(1f, See(target));

            Assert.That(brain.State, Is.EqualTo(EnemyState.Attack));
            Assert.That(decision.Fire, Is.False, "the brain would shoot after its reaction time");
            Assert.That(decision.MoveTo, Is.EqualTo(target));
        }

        [Test]
        public void ChangingTheSpecChangesTheHoldDistance()
        {
            var brain = Brain();
            var behavior = Behavior();
            var target = new Vector3(0f, 0f, 1.5f);

            behavior.TryOverride(brain, 0.1f, See(target), _planned, out var before);
            brain.ChangeSpec(new EnemySpec(
                20f, 10f, 1f, 2f, 0.5f, archetype: EnemyArchetype.Shield, shield: new ShieldSpec(0.5f, 1.6f, 1f, 1f, Shield().Bash)));
            behavior.TryOverride(brain, 0.1f, See(target), _planned, out var after);

            Assert.That(before.MoveTo.HasValue, Is.True);
            Assert.That(after.MoveTo.HasValue, Is.False);
        }

        [Test]
        public void NullArgumentsAreRejected()
        {
            Assert.Throws<ArgumentNullException>(() => _ = new ShieldEnemyBehavior(null, new MeleeSwing(Shield().Bash)));
            Assert.Throws<ArgumentNullException>(() => _ = new ShieldEnemyBehavior(new ShieldGuard(), null));
            Assert.Throws<ArgumentNullException>(() => Behavior().TryOverride(null, 0.1f, See(Vector3.Zero), _planned, out _));
        }
    }
}
