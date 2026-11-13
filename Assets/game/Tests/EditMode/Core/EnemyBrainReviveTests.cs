using System.Collections.Generic;
using System.Numerics;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    // Unity の EditMode テストと dotnet/Fps.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class EnemyBrainReviveTests
    {
        private static readonly Vector3 _first = new Vector3(0f, 0f, 10f);
        private static readonly Vector3 _second = new Vector3(10f, 0f, 10f);

        private static EnemyBrain Brain() => new EnemyBrain(new EnemySpec(20f, 10f, 1f, 2f, 0.5f));

        private static EnemyPerception Sees(Vector3 self, Vector3 target) =>
            new EnemyPerception(self, target, true, false, null);

        private static EnemyPerception Nothing(Vector3 self) =>
            new EnemyPerception(self, null, false, false, null);

        [Test]
        public void ReviveWithoutRouteReturnsToIdle()
        {
            var brain = Brain();
            var changes = new List<(EnemyState, EnemyState)>();
            brain.Kill();
            brain.StateChanged += (previous, next) => changes.Add((previous, next));

            brain.Revive();

            Assert.That(brain.State, Is.EqualTo(EnemyState.Idle));
            Assert.That(brain.IsDead, Is.False);
            Assert.That(changes, Is.EqualTo(new[] { (EnemyState.Dead, EnemyState.Idle) }));
        }

        [Test]
        public void ReviveRestartsThePatrolFromTheFirstPoint()
        {
            var brain = Brain();
            brain.SetPatrolRoute(new[] { _first, _second });
            brain.Tick(0.1f, Nothing(_first));
            Assert.That(brain.PatrolIndex, Is.EqualTo(1));
            brain.Kill();

            brain.Revive();
            var decision = brain.Tick(0.1f, Nothing(Vector3.Zero));

            Assert.That(brain.State, Is.EqualTo(EnemyState.Patrol));
            Assert.That(decision.MoveTo, Is.EqualTo(_first));
        }

        [Test]
        public void ReviveForgetsTheReactionAndSpreadOfTheLastFight()
        {
            var brain = Brain();
            var target = new Vector3(0f, 0f, 5f);
            brain.Tick(0.1f, Sees(Vector3.Zero, target));
            brain.Tick(1f, Sees(Vector3.Zero, target));
            brain.RecordShot();
            Assert.That(brain.State, Is.EqualTo(EnemyState.Attack));
            brain.Kill();

            brain.Revive();

            Assert.That(brain.ShotsInAttack, Is.Zero);
            Assert.That(brain.CoverPhase, Is.EqualTo(EnemyCoverPhase.None));
            Assert.That(brain.CoverPointIndex, Is.EqualTo(-1));

            // 生き返った直後に見えても、反応の秒数が過ぎるまでは撃たない。
            var decision = brain.Tick(0.1f, Sees(Vector3.Zero, target));
            Assert.That(brain.State, Is.EqualTo(EnemyState.Attack));
            Assert.That(decision.Fire, Is.False);
        }

        [Test]
        public void ReviveOnALivingBrainDoesNothing()
        {
            var brain = Brain();
            brain.SetPatrolRoute(new[] { _first, _second });
            brain.Tick(0.1f, Nothing(_first));
            var changes = 0;
            brain.StateChanged += (_, _) => changes++;

            brain.Revive();

            Assert.That(brain.State, Is.EqualTo(EnemyState.Patrol));
            Assert.That(brain.PatrolIndex, Is.EqualTo(1));
            Assert.That(changes, Is.Zero);
        }
    }
}
