using System.Collections.Generic;
using System.Numerics;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    // Unity の EditMode テストと dotnet/Fps.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class EnemyBrainReviveTests
    {
        // 視認 20m、攻撃 10m、見失って 1 秒で Search、Search 2 秒、反応 0.5 秒、撃ち始めの 2 発は散らばる。
        private static EnemySpec Spec() => new EnemySpec(20f, 10f, 1f, 2f, 0.5f, 6f, 0f, 2);

        private static readonly Vector3 _first = new Vector3(0f, 0f, 10f);
        private static readonly Vector3 _second = new Vector3(10f, 0f, 10f);

        private static EnemyPerception Nothing(Vector3 self) => new EnemyPerception(self, null, false, false, null);

        private static EnemyPerception Sees(Vector3 self, Vector3 target) => new EnemyPerception(self, target, true, false, null);

        [Test]
        public void ReviveDoesNothingWhileAlive()
        {
            var brain = new EnemyBrain(Spec());

            Assert.That(brain.Revive(), Is.False);
            Assert.That(brain.State, Is.EqualTo(EnemyState.Idle));
        }

        [Test]
        public void RevivedBrainPatrolsFromTheFirstPoint()
        {
            var brain = new EnemyBrain(Spec());
            brain.SetPatrolRoute(new[] { _first, _second });
            brain.Tick(0.1f, Nothing(_first));
            Assert.That(brain.PatrolIndex, Is.EqualTo(1), "arriving at the first point moves on to the second");

            brain.Kill();
            var changes = new List<(EnemyState, EnemyState)>();
            brain.StateChanged += (previous, next) => changes.Add((previous, next));

            Assert.That(brain.Revive(), Is.True);

            Assert.That(brain.IsDead, Is.False);
            Assert.That(brain.State, Is.EqualTo(EnemyState.Patrol));
            Assert.That(brain.PatrolIndex, Is.EqualTo(0));
            Assert.That(changes, Is.EqualTo(new[] { (EnemyState.Dead, EnemyState.Patrol) }));

            var decision = brain.Tick(0.1f, Nothing(Vector3.Zero));
            Assert.That(decision.MoveTo, Is.EqualTo(_first));
        }

        [Test]
        public void RevivedBrainWithoutRouteIsIdle()
        {
            var brain = new EnemyBrain(Spec());
            brain.Kill();

            Assert.That(brain.Revive(), Is.True);
            Assert.That(brain.State, Is.EqualTo(EnemyState.Idle));
        }

        [Test]
        public void RevivedBrainForgetsTheLastFightAndWaitsToReactAgain()
        {
            var brain = new EnemyBrain(Spec());
            var target = new Vector3(0f, 0f, 5f);
            brain.Tick(0.1f, Sees(Vector3.Zero, target));
            brain.Tick(0.6f, Sees(Vector3.Zero, target));
            brain.RecordShot();
            Assert.That(brain.ShotsInAttack, Is.EqualTo(1));

            brain.Kill();
            brain.Revive();

            Assert.That(brain.ShotsInAttack, Is.Zero);
            Assert.That(brain.NextShotSpreadDegrees, Is.EqualTo(6f));

            var unseen = brain.Tick(0.1f, Nothing(Vector3.Zero));
            Assert.That(brain.State, Is.EqualTo(EnemyState.Idle));
            Assert.That(unseen.MoveTo, Is.Null, "it does not chase where it last saw the target before dying");

            var seen = brain.Tick(0.1f, Sees(Vector3.Zero, target));
            Assert.That(brain.State, Is.EqualTo(EnemyState.Attack));
            Assert.That(seen.Fire, Is.False, "the reaction delay starts over after reviving");
        }
    }
}
