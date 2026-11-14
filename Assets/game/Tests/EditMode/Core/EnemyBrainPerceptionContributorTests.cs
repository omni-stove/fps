using System;
using System.Collections.Generic;
using System.Numerics;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    // Unity の EditMode テストと dotnet/Fps.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class EnemyBrainPerceptionContributorTests
    {
        private static readonly Vector3 _near = new Vector3(0f, 0f, 5f);
        private static readonly string[] _orderedCalls = { "first:1", "second:0.5", "behavior:0.5" };

        private static EnemySpec Spec() => new EnemySpec(20f, 10f, 1f, 2f, 0.5f, 6f, 0f, 2);

        private static EnemyPerception Sees() => new EnemyPerception(Vector3.Zero, _near, true, false, null, false, null, 1f);

        [Test]
        public void ContributorsRunInOrderBeforeTheDecisionAndBehaviorsSeeTheResult()
        {
            var brain = new EnemyBrain(Spec());
            var calls = new List<string>();
            brain.AddPerceptionContributor(new Contributor("first", calls, 0.5f));
            brain.AddPerceptionContributor(new Contributor("second", calls, null));
            var behavior = new Behavior(calls);
            brain.AddBehavior(behavior);

            brain.Tick(0.1f, Sees());

            Assert.That(calls, Is.EqualTo(_orderedCalls));
            Assert.That(brain.State, Is.EqualTo(EnemyState.Idle), "the lowered awareness kept the brain from attacking");
        }

        [Test]
        public void WithoutContributorsTheBrainDecidesAsBefore()
        {
            var brain = new EnemyBrain(Spec());
            var contributor = new Contributor("pass", new List<string>(), null);
            brain.AddPerceptionContributor(contributor);
            Assert.That(brain.RemovePerceptionContributor(contributor), Is.True);
            Assert.That(brain.RemovePerceptionContributor(contributor), Is.False);
            Assert.That(brain.PerceptionContributors, Is.Empty);

            brain.Tick(0.1f, Sees());

            Assert.That(brain.State, Is.EqualTo(EnemyState.Attack));
        }

        [Test]
        public void AddingTheSameContributorTwiceOrNullIsRejected()
        {
            var brain = new EnemyBrain(Spec());
            var contributor = new Contributor("pass", new List<string>(), null);
            brain.AddPerceptionContributor(contributor);

            Assert.Throws<ArgumentException>(() => brain.AddPerceptionContributor(contributor));
            Assert.Throws<ArgumentNullException>(() => brain.AddPerceptionContributor(null));
            Assert.Throws<ArgumentNullException>(() => brain.RemovePerceptionContributor(null));
            Assert.That(brain.PerceptionContributors, Has.Count.EqualTo(1));
        }

        [Test]
        public void AContributorThatKillsTheBrainLeavesNoDecision()
        {
            var brain = new EnemyBrain(Spec());
            brain.AddPerceptionContributor(new Killer());

            var decision = brain.Tick(0.1f, Sees());

            Assert.That(brain.IsDead, Is.True);
            Assert.That(decision.LookAt, Is.Null);
            Assert.That(decision.Fire, Is.False);
        }

        private sealed class Contributor : IBrainPerceptionContributor
        {
            private readonly string _name;
            private readonly List<string> _calls;
            private readonly float? _awareness;

            public Contributor(string name, List<string> calls, float? awareness)
            {
                _name = name;
                _calls = calls;
                _awareness = awareness;
            }

            public EnemyPerception Contribute(EnemyBrain brain, float deltaSeconds, EnemyPerception p)
            {
                _calls.Add(_name + ":" + p.Awareness.ToString(System.Globalization.CultureInfo.InvariantCulture));
                if (!_awareness.HasValue)
                {
                    return p;
                }

                return new EnemyPerception(
                    p.Self, p.Target, p.HasLineOfSight, p.HeardNoise, p.NoisePosition, p.WasHit, p.HitSource, p.HealthFraction, _awareness.Value, p.IsSuppressed);
            }
        }

        private sealed class Behavior : IEnemyBehavior
        {
            private readonly List<string> _calls;

            public Behavior(List<string> calls)
            {
                _calls = calls;
            }

            public bool TryOverride(EnemyBrain brain, float deltaSeconds, EnemyPerception perception, EnemyDecision planned, out EnemyDecision decision)
            {
                _calls.Add("behavior:" + perception.Awareness.ToString(System.Globalization.CultureInfo.InvariantCulture));
                decision = planned;
                return false;
            }
        }

        private sealed class Killer : IBrainPerceptionContributor
        {
            public EnemyPerception Contribute(EnemyBrain brain, float deltaSeconds, EnemyPerception perception)
            {
                brain.Kill();
                return perception;
            }
        }
    }
}
