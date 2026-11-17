using System;
using System.Numerics;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class MountedGunEnemyBehaviorTests
    {
        private static readonly Vector3 _self = Vector3.Zero;
        private static readonly Vector3 _target = new Vector3(0f, 0f, 15f);
        private static readonly Vector3 _gun = new Vector3(3f, 0f, 4f);
        private static readonly EnemyDecision _planned = new EnemyDecision(new Vector3(0f, 0f, 9f), _target, true, true);

        // 乗りに行く距離 6m・あきらめるまで 2 秒・また乗りに行けるまで 4 秒。
        private static MountedGunUseSpec Spec() => new MountedGunUseSpec(6f, 2f, 4f);

        private static EnemyPerception See(Vector3 target) => new EnemyPerception(_self, target, true, false, null, false, null, 1f);

        private static EnemyPerception Nothing() => new EnemyPerception(_self, null, false, false, null, false, null, 1f);

        private static EnemyBrain Engaged()
        {
            var brain = new EnemyBrain(new EnemySpec(40f, 10f, 2f, 6f, 0.5f));
            brain.Tick(0.1f, See(_target));
            Assert.That(brain.State, Is.EqualTo(EnemyState.Chase).Or.EqualTo(EnemyState.Attack));
            return brain;
        }

        [Test]
        public void AnEngagedEnemyWalksToANearbyFreeGunAndKeepsShooting()
        {
            var behavior = new MountedGunEnemyBehavior(Spec());
            behavior.SetCandidate(_gun);

            Assert.That(behavior.TryOverride(Engaged(), 0.1f, See(_target), _planned, out var decision), Is.True);
            Assert.That(behavior.IsSeeking, Is.True);
            Assert.That(decision.MoveTo, Is.EqualTo(_gun));
            Assert.That(decision.LookAt, Is.EqualTo(_target));
            Assert.That(decision.Fire, Is.True);
            Assert.That(decision.Crouch, Is.False, "it stands up to walk to the gun");
        }

        [Test]
        public void AGunBeyondTheSeekRadiusIsLeftAlone()
        {
            var behavior = new MountedGunEnemyBehavior(Spec());
            behavior.SetCandidate(new Vector3(0f, 5f, 6.5f));

            Assert.That(behavior.TryOverride(Engaged(), 0.1f, See(_target), _planned, out var decision), Is.False);
            Assert.That(behavior.IsSeeking, Is.False);
            Assert.That(decision.MoveTo, Is.EqualTo(_planned.MoveTo));

            behavior.SetCandidate(new Vector3(0f, 5f, 5.9f));
            Assert.That(behavior.TryOverride(Engaged(), 0.1f, See(_target), _planned, out _), Is.True, "the radius is measured on the ground");
        }

        [Test]
        public void ItDoesNotSeekWithoutAFightOrAGun()
        {
            var behavior = new MountedGunEnemyBehavior(Spec());

            Assert.That(behavior.TryOverride(Engaged(), 0.1f, See(_target), _planned, out _), Is.False, "no free gun");

            behavior.SetCandidate(_gun);
            var calm = new EnemyBrain(new EnemySpec(40f, 10f, 2f, 6f, 0.5f));
            calm.Tick(0.1f, Nothing());
            Assert.That(calm.State, Is.EqualTo(EnemyState.Idle));
            Assert.That(behavior.TryOverride(calm, 0.1f, Nothing(), EnemyDecision.None, out _), Is.False, "an idle enemy stays put");

            behavior.Enabled = false;
            Assert.That(behavior.TryOverride(Engaged(), 0.1f, See(_target), _planned, out _), Is.False);

            behavior.Enabled = true;
            behavior.ApplySpec(new MountedGunUseSpec(0f, 2f, 4f));
            Assert.That(behavior.TryOverride(Engaged(), 0.1f, See(_target), _planned, out _), Is.False, "a zero radius never seeks");
        }

        [Test]
        public void ADeadEnemyDoesNotSeek()
        {
            var behavior = new MountedGunEnemyBehavior(Spec());
            behavior.SetCandidate(_gun);
            var brain = Engaged();
            brain.Kill();

            Assert.That(behavior.TryOverride(brain, 0.1f, See(_target), _planned, out _), Is.False);
        }

        [Test]
        public void SeatedItFiresOnlyWhenTheGunPointsAtAVisibleTarget()
        {
            var behavior = new MountedGunEnemyBehavior(Spec());

            var turning = behavior.Man(0.1f, true, true, false);
            Assert.That(turning.Fire, Is.False, "still turning the gun");
            Assert.That(turning.Leave.HasValue, Is.False);

            var onTarget = behavior.Man(0.1f, true, true, true);
            Assert.That(onTarget.Fire, Is.True);
            Assert.That(onTarget.Leave.HasValue, Is.False);
        }

        [Test]
        public void ItLeavesAfterLosingTheTargetForTheGiveUpTime()
        {
            var behavior = new MountedGunEnemyBehavior(Spec());

            Assert.That(behavior.Man(1.5f, false, true, true).Leave.HasValue, Is.False);
            Assert.That(behavior.Man(0.4f, false, true, true).Leave.HasValue, Is.False);
            Assert.That(behavior.UnusableSeconds, Is.EqualTo(1.9f).Within(1e-5f));

            var order = behavior.Man(0.1f, false, true, true);
            Assert.That(order.Fire, Is.False);
            Assert.That(order.Leave, Is.EqualTo(MountedGunLeaveCause.TargetLost));
        }

        [Test]
        public void ATargetOutsideTheArcAlsoMakesItLeave()
        {
            var behavior = new MountedGunEnemyBehavior(Spec());

            behavior.Man(1f, true, false, false);
            Assert.That(behavior.Man(1f, true, false, false).Leave, Is.EqualTo(MountedGunLeaveCause.OutOfArc));

            var hiddenBehind = new MountedGunEnemyBehavior(Spec());
            Assert.That(
                hiddenBehind.Man(2f, false, false, false).Leave,
                Is.EqualTo(MountedGunLeaveCause.OutOfArc),
                "a target the gun cannot turn to is out of its arc even when unseen");
        }

        [Test]
        public void SeeingTheTargetAgainRestartsTheGiveUpTime()
        {
            var behavior = new MountedGunEnemyBehavior(Spec());

            behavior.Man(1.5f, false, true, true);
            behavior.Man(0.1f, true, true, true);
            Assert.That(behavior.UnusableSeconds, Is.EqualTo(0f));
            Assert.That(behavior.Man(1.5f, false, true, true).Leave.HasValue, Is.False);
        }

        [Test]
        public void AfterLeavingItWaitsBeforeSeekingAgain()
        {
            var behavior = new MountedGunEnemyBehavior(Spec());
            behavior.SetCandidate(_gun);
            var brain = Engaged();

            behavior.NoteLeft();
            Assert.That(behavior.RemountRemainingSeconds, Is.EqualTo(4f));
            Assert.That(behavior.TryOverride(brain, 3f, See(_target), _planned, out _), Is.False);
            Assert.That(behavior.RemountRemainingSeconds, Is.EqualTo(1f));
            Assert.That(behavior.TryOverride(brain, 1f, See(_target), _planned, out _), Is.True);
        }

        [Test]
        public void ResetForgetsTheGunAndTheWait()
        {
            var behavior = new MountedGunEnemyBehavior(Spec());
            behavior.SetCandidate(_gun);
            behavior.NoteLeft();

            behavior.Reset();

            Assert.That(behavior.Candidate.HasValue, Is.False);
            Assert.That(behavior.RemountRemainingSeconds, Is.EqualTo(0f));
        }

        [Test]
        public void InvalidInputsAreRejected()
        {
            var behavior = new MountedGunEnemyBehavior(Spec());

            Assert.Throws<ArgumentNullException>(() => new MountedGunEnemyBehavior(null));
            Assert.Throws<ArgumentNullException>(() => behavior.ApplySpec(null));
            Assert.Throws<ArgumentNullException>(() => behavior.TryOverride(null, 0.1f, Nothing(), _planned, out _));
            Assert.Throws<ArgumentOutOfRangeException>(() => behavior.Man(-1f, true, true, true));
            Assert.That(new MountedGunEnemyBehavior().Spec, Is.SameAs(MountedGunUseSpec.Default));
        }
    }
}
