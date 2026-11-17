using System.Collections.Generic;
using System.Numerics;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class EnemyBrainStandDownTests
    {
        // 視認 20m、攻撃 10m、見失って 1 秒で Search、Search 2 秒、反応 0.5 秒、撃ち始めの 2 発は散らばる。
        private static EnemySpec Spec() => new EnemySpec(20f, 10f, 1f, 2f, 0.5f, 6f, 0f, 2);

        private static readonly Vector3 _first = new Vector3(0f, 0f, 10f);
        private static readonly Vector3 _second = new Vector3(10f, 0f, 10f);
        private static readonly Vector3 _target = new Vector3(0f, 0f, 5f);

        private static EnemyPerception Nothing(Vector3 self) => new EnemyPerception(self, null, false, false, null);

        private static EnemyPerception Sees(Vector3 self, Vector3 target) => new EnemyPerception(self, target, true, false, null);

        private static EnemyPerception Hears(Vector3 self, Vector3 noise) => new EnemyPerception(self, null, false, true, noise);

        [Test]
        public void StandingDownFromAttackWithoutRouteIsIdleAndDoesNotRushToTheBody()
        {
            var brain = new EnemyBrain(Spec());
            brain.Tick(0.1f, Sees(Vector3.Zero, _target));
            Assert.That(brain.State, Is.EqualTo(EnemyState.Attack));

            var changes = new List<(EnemyState, EnemyState)>();
            brain.StateChanged += (previous, next) => changes.Add((previous, next));

            Assert.That(brain.StandDown(), Is.True);
            Assert.That(brain.State, Is.EqualTo(EnemyState.Idle));
            Assert.That(changes, Is.EqualTo(new[] { (EnemyState.Attack, EnemyState.Idle) }));

            var decision = brain.Tick(0.1f, Nothing(Vector3.Zero));
            Assert.That(brain.State, Is.EqualTo(EnemyState.Idle));
            Assert.That(decision.MoveTo, Is.Null, "it does not head to where the fallen target was last seen");
            Assert.That(decision.LookAt, Is.Null);
            Assert.That(decision.Fire, Is.False);
        }

        [Test]
        public void StandingDownFromChaseResumesThePatrolWhereItLeftOff()
        {
            var brain = new EnemyBrain(Spec());
            brain.SetPatrolRoute(new[] { _first, _second });
            brain.Tick(0.1f, Nothing(_first));
            Assert.That(brain.PatrolIndex, Is.EqualTo(1));

            var far = new Vector3(0f, 0f, 25f);
            brain.Tick(0.1f, Sees(Vector3.Zero, new Vector3(0f, 0f, 15f)));
            brain.Tick(0.1f, Nothing(Vector3.Zero));
            Assert.That(brain.State, Is.EqualTo(EnemyState.Chase), "lost sight, so it heads to the last seen spot");

            Assert.That(brain.StandDown(), Is.True);
            Assert.That(brain.State, Is.EqualTo(EnemyState.Patrol));
            Assert.That(brain.PatrolIndex, Is.EqualTo(1));

            var decision = brain.Tick(0.1f, Nothing(far));
            Assert.That(decision.MoveTo, Is.EqualTo(_second));
        }

        [Test]
        public void StandingDownFromSearchStopsSearching()
        {
            var brain = new EnemyBrain(Spec());
            brain.Tick(0.1f, Sees(Vector3.Zero, _target));
            brain.Tick(1.1f, Nothing(Vector3.Zero));
            brain.Tick(0.1f, Nothing(Vector3.Zero));
            Assert.That(brain.State, Is.EqualTo(EnemyState.Search));

            Assert.That(brain.StandDown(), Is.True);

            var decision = brain.Tick(0.1f, Nothing(Vector3.Zero));
            Assert.That(brain.State, Is.EqualTo(EnemyState.Idle));
            Assert.That(decision.MoveTo, Is.Null);
        }

        [Test]
        public void StandingDownWithoutAFightDoesNothing()
        {
            var brain = new EnemyBrain(Spec());
            Assert.That(brain.StandDown(), Is.False);
            Assert.That(brain.State, Is.EqualTo(EnemyState.Idle));

            brain.SetPatrolRoute(new[] { _first, _second });
            Assert.That(brain.StandDown(), Is.False);
            Assert.That(brain.State, Is.EqualTo(EnemyState.Patrol));

            brain.Kill();
            Assert.That(brain.StandDown(), Is.False);
            Assert.That(brain.State, Is.EqualTo(EnemyState.Dead));
        }

        [Test]
        public void StoodDownBrainReactsAgainFromScratch()
        {
            var brain = new EnemyBrain(Spec());
            brain.Tick(0.1f, Sees(Vector3.Zero, _target));
            brain.Tick(0.6f, Sees(Vector3.Zero, _target));
            brain.RecordShot();
            Assert.That(brain.ShotsInAttack, Is.EqualTo(1));

            brain.StandDown();
            Assert.That(brain.ShotsInAttack, Is.Zero);
            Assert.That(brain.NextShotSpreadDegrees, Is.EqualTo(6f));

            var seen = brain.Tick(0.1f, Sees(Vector3.Zero, _target));
            Assert.That(brain.State, Is.EqualTo(EnemyState.Attack));
            Assert.That(seen.Fire, Is.False, "it waits ReactionSeconds again before firing at a new target");
        }

        [Test]
        public void StoodDownBrainStillSearchesANoise()
        {
            var brain = new EnemyBrain(Spec());
            brain.Tick(0.1f, Sees(Vector3.Zero, _target));
            brain.StandDown();

            var noise = new Vector3(3f, 0f, -4f);
            var decision = brain.Tick(0.1f, Hears(Vector3.Zero, noise));
            Assert.That(brain.State, Is.EqualTo(EnemyState.Search));
            Assert.That(decision.MoveTo, Is.EqualTo(noise));
        }
    }
}
