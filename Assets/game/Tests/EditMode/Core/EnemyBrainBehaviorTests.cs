using System;
using System.Collections.Generic;
using System.Numerics;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    // Unity の EditMode テストと dotnet/Fps.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class EnemyBrainBehaviorTests
    {
        private static EnemySpec Spec() => new EnemySpec(20f, 10f, 1f, 2f, 0.5f, 6f, 0f, 2);

        private static readonly Vector3 _self = Vector3.Zero;
        private static readonly Vector3 _near = new Vector3(0f, 0f, 5f);
        private static readonly Vector3 _far = new Vector3(0f, 0f, 15f);
        private static readonly Vector3 _noise = new Vector3(-4f, 0f, 3f);

        private readonly struct Step
        {
            public Step(float deltaSeconds, Vector3? target, bool lineOfSight, Vector3? noise, bool wasHit, float health)
            {
                DeltaSeconds = deltaSeconds;
                Target = target;
                LineOfSight = lineOfSight;
                Noise = noise;
                WasHit = wasHit;
                Health = health;
            }

            public float DeltaSeconds { get; }
            public Vector3? Target { get; }
            public bool LineOfSight { get; }
            public Vector3? Noise { get; }
            public bool WasHit { get; }
            public float Health { get; }
        }

        // 物音 → 捜索 → 遠くに見える (追う) → 近づく (撃つまで待って撃つ) → 撃たれる → 見失う → 捜索 → 待機、をひと通り通す。
        private static IEnumerable<Step> Scenario()
        {
            yield return new Step(0.1f, null, false, null, false, 1f);
            yield return new Step(0.1f, null, false, _noise, false, 1f);
            yield return new Step(0.5f, null, false, null, false, 1f);
            yield return new Step(0.1f, _far, true, null, false, 1f);
            yield return new Step(0.1f, _far, false, null, false, 1f);
            yield return new Step(0.1f, _near, true, null, false, 1f);
            yield return new Step(0.3f, _near, true, null, false, 1f);
            yield return new Step(0.3f, _near, true, null, true, 0.6f);
            yield return new Step(0.3f, _near, true, null, false, 0.6f);
            yield return new Step(0.5f, null, false, null, false, 0.6f);
            yield return new Step(0.6f, null, false, null, false, 0.6f);
            yield return new Step(1f, null, false, null, false, 0.6f);
            yield return new Step(1.5f, null, false, null, false, 0.6f);
        }

        private static EnemyPerception Legacy(Step s) =>
            new EnemyPerception(_self, s.Target, s.LineOfSight, s.Noise.HasValue, s.Noise, s.WasHit, s.WasHit ? _far : (Vector3?)null, s.Health);

        private static EnemyPerception WithAwareness(Step s, bool suppressed) =>
            new EnemyPerception(
                _self,
                s.Target,
                s.LineOfSight,
                s.Noise.HasValue,
                s.Noise,
                s.WasHit,
                s.WasHit ? _far : (Vector3?)null,
                s.Health,
                EnemyPerception.AwarenessFromSight(s.LineOfSight),
                suppressed);

        private static List<(EnemyState, Vector3?, Vector3?, bool, bool)> Run(EnemyBrain brain, Func<Step, EnemyPerception> perceive)
        {
            var log = new List<(EnemyState, Vector3?, Vector3?, bool, bool)>();
            foreach (var step in Scenario())
            {
                var d = brain.Tick(step.DeltaSeconds, perceive(step));
                log.Add((brain.State, d.MoveTo, d.LookAt, d.Fire, d.Crouch));
            }

            return log;
        }

        [Test]
        public void OldConstructorsSetAwarenessFromLineOfSight()
        {
            Assert.That(new EnemyPerception(_self, _near, true, false, null).Awareness, Is.EqualTo(1f));
            Assert.That(new EnemyPerception(_self, _near, false, false, null).Awareness, Is.EqualTo(0f));
            Assert.That(new EnemyPerception(_self, _near, true, false, null, false, null, 1f).IsSuppressed, Is.False);
        }

        [Test]
        public void AwarenessOutsideZeroToOneIsRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => _ = new EnemyPerception(_self, _near, true, false, null, false, null, 1f, 1.5f, false));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => _ = new EnemyPerception(_self, _near, true, false, null, false, null, 1f, -0.1f, false));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => _ = new EnemyPerception(_self, _near, true, false, null, false, null, 1f, float.NaN, false));
        }

        [Test]
        public void DecisionsAreTheSameWithAwarenessSuppressionAndAPassiveBehavior()
        {
            var expected = Run(new EnemyBrain(Spec()), Legacy);

            Assert.That(Run(new EnemyBrain(Spec()), s => WithAwareness(s, false)), Is.EqualTo(expected));
            Assert.That(Run(new EnemyBrain(Spec()), s => WithAwareness(s, true)), Is.EqualTo(expected), "suppression is only an input for now");

            var passive = new EnemyBrain(Spec());
            var behavior = new RecordingBehavior(null);
            passive.AddBehavior(behavior);
            Assert.That(Run(passive, Legacy), Is.EqualTo(expected));
            Assert.That(behavior.Calls, Is.EqualTo(expected.Count));
        }

        [Test]
        public void ScenarioWalksThroughTheUsualStates()
        {
            var states = Run(new EnemyBrain(Spec()), Legacy).ConvertAll(entry => entry.Item1);

            Assert.That(states, Is.EqualTo(new[]
            {
                EnemyState.Idle,
                EnemyState.Search,
                EnemyState.Search,
                EnemyState.Chase,
                EnemyState.Chase,
                EnemyState.Attack,
                EnemyState.Attack,
                EnemyState.Attack,
                EnemyState.Attack,
                EnemyState.Chase,
                EnemyState.Search,
                EnemyState.Search,
                EnemyState.Idle,
            }));
        }

        [Test]
        public void SeeingWithoutFullAwarenessDoesNotEngage()
        {
            var brain = new EnemyBrain(Spec());

            brain.Tick(0.1f, new EnemyPerception(_self, _near, true, false, null, false, null, 1f, 0.5f, false));

            Assert.That(brain.State, Is.EqualTo(EnemyState.Idle));
        }

        [Test]
        public void BehaviorReplacesTheDecisionAfterTheStateMachineMoves()
        {
            var brain = new EnemyBrain(Spec());
            var charge = new EnemyDecision(_near, _near, false);
            var behavior = new RecordingBehavior(charge);
            brain.AddBehavior(behavior);

            var decision = brain.Tick(0.1f, new EnemyPerception(_self, _near, true, false, null));

            Assert.That(brain.State, Is.EqualTo(EnemyState.Attack), "the state machine still runs");
            Assert.That(behavior.LastSeenState, Is.EqualTo(EnemyState.Attack));
            Assert.That(behavior.LastPlanned.MoveTo, Is.Null, "the behavior receives the brain's own decision");
            Assert.That(decision.MoveTo, Is.EqualTo(_near));
        }

        [Test]
        public void BehaviorsRunInOrderAndSeeTheEarlierOverride()
        {
            var brain = new EnemyBrain(Spec());
            var first = new RecordingBehavior(new EnemyDecision(_far, null, false));
            var second = new RecordingBehavior(null);
            brain.AddBehavior(first);
            brain.AddBehavior(second);

            var decision = brain.Tick(0.1f, new EnemyPerception(_self, null, false, false, null));

            Assert.That(second.LastPlanned.MoveTo, Is.EqualTo(_far));
            Assert.That(decision.MoveTo, Is.EqualTo(_far));
            Assert.That(brain.Behaviors, Is.EqualTo(new IEnemyBehavior[] { first, second }));
        }

        [Test]
        public void RemovedBehaviorIsNoLongerCalled()
        {
            var brain = new EnemyBrain(Spec());
            var behavior = new RecordingBehavior(null);
            brain.AddBehavior(behavior);

            Assert.That(brain.RemoveBehavior(behavior), Is.True);
            Assert.That(brain.RemoveBehavior(behavior), Is.False);
            brain.Tick(0.1f, new EnemyPerception(_self, null, false, false, null));

            Assert.That(behavior.Calls, Is.Zero);
        }

        [Test]
        public void AddingTheSameBehaviorTwiceOrNullIsRejected()
        {
            var brain = new EnemyBrain(Spec());
            var behavior = new RecordingBehavior(null);
            brain.AddBehavior(behavior);

            Assert.Throws<ArgumentException>(() => brain.AddBehavior(behavior));
            Assert.Throws<ArgumentNullException>(() => brain.AddBehavior(null));
            Assert.Throws<ArgumentNullException>(() => brain.RemoveBehavior(null));
        }

        [Test]
        public void BehaviorThatKillsTheBrainLeavesNoDecision()
        {
            var brain = new EnemyBrain(Spec());
            brain.AddBehavior(new KillingBehavior());

            var decision = brain.Tick(0.1f, new EnemyPerception(_self, _near, true, false, null));

            Assert.That(brain.IsDead, Is.True);
            Assert.That(decision.LookAt, Is.Null);
            Assert.That(decision.Fire, Is.False);
        }

        [Test]
        public void DeadBrainDoesNotCallBehaviors()
        {
            var brain = new EnemyBrain(Spec());
            var behavior = new RecordingBehavior(null);
            brain.AddBehavior(behavior);
            brain.Kill();

            brain.Tick(0.1f, new EnemyPerception(_self, _near, true, false, null));

            Assert.That(behavior.Calls, Is.Zero);
        }

        private sealed class RecordingBehavior : IEnemyBehavior
        {
            private readonly EnemyDecision? _replacement;

            public RecordingBehavior(EnemyDecision? replacement)
            {
                _replacement = replacement;
            }

            public int Calls { get; private set; }
            public EnemyState LastSeenState { get; private set; }
            public EnemyDecision LastPlanned { get; private set; }

            public bool TryOverride(EnemyBrain brain, float deltaSeconds, EnemyPerception perception, EnemyDecision planned, out EnemyDecision decision)
            {
                Calls++;
                LastSeenState = brain.State;
                LastPlanned = planned;
                decision = _replacement ?? planned;
                return _replacement.HasValue;
            }
        }

        private sealed class KillingBehavior : IEnemyBehavior
        {
            public bool TryOverride(EnemyBrain brain, float deltaSeconds, EnemyPerception perception, EnemyDecision planned, out EnemyDecision decision)
            {
                brain.Kill();
                decision = planned;
                return false;
            }
        }
    }
}
