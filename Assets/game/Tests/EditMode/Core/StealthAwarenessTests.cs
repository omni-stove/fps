using System;
using System.Collections.Generic;
using System.Numerics;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    // Unity の EditMode テストと dotnet/Fps.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class StealthAwarenessTests
    {
        private static readonly Vector3 _self = Vector3.Zero;
        private static readonly Vector3 _ahead = new Vector3(0f, 0f, 15f);
        private static readonly Vector3 _elsewhere = new Vector3(8f, 0f, 15f);
        private static readonly float[] _fullAwareness = { 1f };

        private static EnemySpec BrainSpec() => new EnemySpec(20f, 10f, 1f, 2f, 0.5f, 6f, 0f, 2);

        private static EnemyPerception Sees(Vector3 target, Vector3? self = null) =>
            new EnemyPerception(self ?? _self, target, true, false, null, false, null, 1f);

        private static EnemyPerception Hidden(Vector3 target, Vector3? self = null) =>
            new EnemyPerception(self ?? _self, target, false, false, null, false, null, 1f);

        private static (EnemyBrain Brain, StealthAwareness Awareness, List<AwarenessStage> Stages, Recorder Recorder) Create(
            Stealth rule, AwarenessSpec spec = null)
        {
            var brain = new EnemyBrain(BrainSpec());
            var awareness = new StealthAwareness(spec ?? AwarenessSpec.Default) { Rule = rule };
            awareness.Observe(Vector3.UnitZ, AwarenessPosture.Standing, 1f, 0f);
            var stages = new List<AwarenessStage>();
            awareness.StageChanged += (_, next) => stages.Add(next);
            var recorder = new Recorder();
            brain.AddPerceptionContributor(awareness);
            brain.AddPerceptionContributor(recorder);
            brain.AddBehavior(awareness);
            return (brain, awareness, stages, recorder);
        }

        private static EnemyDecision TickUntil(EnemyBrain brain, StealthAwareness awareness, AwarenessStage stage, EnemyPerception perception)
        {
            var decision = EnemyDecision.None;
            for (var i = 0; i < 1000 && awareness.Stage != stage; i++)
            {
                decision = brain.Tick(0.05f, perception);
            }

            Assert.That(awareness.Stage, Is.EqualTo(stage));
            return decision;
        }

        [Test]
        public void OffEngagesAsSoonAsTheTargetIsSeen()
        {
            var (brain, awareness, stages, recorder) = Create(Stealth.Off);

            brain.Tick(0.1f, Sees(_ahead));

            Assert.That(brain.State, Is.EqualTo(EnemyState.Chase));
            Assert.That(awareness.Stage, Is.EqualTo(AwarenessStage.Engaged));
            Assert.That(stages, Is.EqualTo(new[] { AwarenessStage.Engaged }));
            Assert.That(recorder.Awareness, Is.EqualTo(_fullAwareness), "the perception is passed on unchanged");
        }

        [Test]
        public void OffFallsBackToUnawareWhenTheFightIsOver()
        {
            var (brain, awareness, stages, _) = Create(Stealth.Off);
            brain.Tick(0.1f, Sees(_ahead));

            for (var i = 0; i < 100 && StealthAwareness.IsFighting(brain.State); i++)
            {
                brain.Tick(0.1f, Hidden(_ahead));
            }

            Assert.That(awareness.Stage, Is.EqualTo(AwarenessStage.Unaware));
            Assert.That(awareness.Level, Is.EqualTo(0f));
            Assert.That(stages, Is.EqualTo(new[] { AwarenessStage.Engaged, AwarenessStage.Unaware }));
        }

        [Test]
        public void OnWaitsForTheMeterThenGoesThroughEveryStage()
        {
            var (brain, awareness, stages, recorder) = Create(Stealth.On);

            brain.Tick(0.1f, Sees(_ahead));
            Assert.That(brain.State, Is.EqualTo(EnemyState.Idle), "seeing is not enough to fight yet");
            Assert.That(recorder.Awareness[0], Is.GreaterThan(0f).And.LessThan(1f));

            TickUntil(brain, awareness, AwarenessStage.Engaged, Sees(_ahead));

            Assert.That(brain.State, Is.EqualTo(EnemyState.Chase));
            Assert.That(stages, Is.EqualTo(new[] { AwarenessStage.Noticed, AwarenessStage.Alerted, AwarenessStage.Engaged }));
            Assert.That(recorder.Awareness[recorder.Awareness.Count - 1], Is.EqualTo(1f));
            Assert.That(awareness.RaisedByAlarm, Is.False);
        }

        [Test]
        public void NoticedStopsAndLooksAtTheLastSeenSpot()
        {
            var (brain, awareness, _, _) = Create(Stealth.On);

            var decision = TickUntil(brain, awareness, AwarenessStage.Noticed, Sees(_ahead));

            Assert.That(decision.MoveTo, Is.Null);
            Assert.That(decision.LookAt, Is.EqualTo(_ahead));
            Assert.That(decision.Fire, Is.False);
            Assert.That(awareness.LastSeen, Is.EqualTo(_ahead));
        }

        [Test]
        public void AlertedGoesToWhereTheTargetWasLastSeenWhileTheMeterDrains()
        {
            var (brain, awareness, _, _) = Create(Stealth.On);
            TickUntil(brain, awareness, AwarenessStage.Alerted, Sees(_ahead));
            var level = awareness.Level;

            var decision = brain.Tick(0.1f, Hidden(_elsewhere));

            Assert.That(decision.MoveTo, Is.EqualTo(_ahead), "the last place it was seen, not where it is now");
            Assert.That(decision.LookAt, Is.EqualTo(_ahead));
            Assert.That(decision.Fire, Is.False);

            var arrived = brain.Tick(0f, Hidden(_elsewhere, _ahead));
            Assert.That(awareness.Stage, Is.EqualTo(AwarenessStage.Alerted));
            Assert.That(arrived.MoveTo, Is.Null, "already there");

            brain.Tick(AwarenessSpec.DefaultDecayDelaySeconds + 0.5f, Hidden(_elsewhere));
            Assert.That(awareness.Level, Is.LessThan(level));
        }

        [Test]
        public void NothingFillsBehindTheEnemysBack()
        {
            var (brain, awareness, _, _) = Create(Stealth.On);
            awareness.Observe(-Vector3.UnitZ, AwarenessPosture.Standing, 1f, 6f);

            for (var i = 0; i < 40; i++)
            {
                brain.Tick(0.05f, Sees(_ahead));
            }

            Assert.That(awareness.Level, Is.EqualTo(0f));
            Assert.That(awareness.Stage, Is.EqualTo(AwarenessStage.Unaware));
            Assert.That(brain.State, Is.EqualTo(EnemyState.Idle));
        }

        [Test]
        public void SightingUsesTheObservedFacingPostureLightAndSpeed()
        {
            var awareness = new StealthAwareness(AwarenessSpec.Default);
            awareness.Observe(Vector3.UnitX, AwarenessPosture.Crouching, 0.5f, 2f);

            var sighting = awareness.SightingOf(Sees(_ahead));

            Assert.That(sighting.HasLineOfSight, Is.True);
            Assert.That(sighting.Distance, Is.EqualTo(15f).Within(1e-4f));
            Assert.That(sighting.AngleDegrees, Is.EqualTo(90f).Within(1e-3f));
            Assert.That(sighting.Posture, Is.EqualTo(AwarenessPosture.Crouching));
            Assert.That(sighting.LightFactor, Is.EqualTo(0.5f));
            Assert.That(sighting.Speed, Is.EqualTo(2f));
            Assert.That(awareness.SightingOf(new EnemyPerception(_self, null, false, false, null)).HasLineOfSight, Is.False);

            awareness.Observe(Vector3.UnitY, AwarenessPosture.Standing, 1f, 0f);
            Assert.That(awareness.SightingOf(Sees(_ahead)).AngleDegrees, Is.EqualTo(90f).Within(1e-3f), "straight up keeps the last facing");
        }

        [Test]
        public void BeingShotEngagesAtOnceTowardsTheShooter()
        {
            var (brain, awareness, stages, _) = Create(Stealth.On);
            var shooter = new Vector3(-5f, 0f, 2f);

            brain.Tick(0.1f, new EnemyPerception(_self, null, false, true, shooter, true, shooter, 0.9f));

            Assert.That(awareness.Stage, Is.EqualTo(AwarenessStage.Engaged));
            Assert.That(awareness.LastSeen, Is.EqualTo(shooter));
            Assert.That(stages, Is.EqualTo(new[] { AwarenessStage.Engaged }));
        }

        [Test]
        public void AnAlarmEngagesOnceAndIsMarkedAsPassedOn()
        {
            var (_, awareness, stages, _) = Create(Stealth.On);
            var spot = new Vector3(3f, 0f, 4f);

            Assert.That(awareness.Alarm(spot), Is.True);
            Assert.That(awareness.Stage, Is.EqualTo(AwarenessStage.Engaged));
            Assert.That(awareness.RaisedByAlarm, Is.True);
            Assert.That(awareness.LastSeen, Is.EqualTo(spot));
            Assert.That(awareness.Alarm(Vector3.Zero), Is.False, "already engaged");
            Assert.That(stages, Is.EqualTo(new[] { AwarenessStage.Engaged }));

            var off = Create(Stealth.Off).Awareness;
            Assert.That(off.Alarm(spot), Is.False);
            Assert.That(off.Stage, Is.EqualTo(AwarenessStage.Unaware));
        }

        [Test]
        public void SeeingTheTargetAfterAnAlarmMakesTheFightOurOwn()
        {
            var (brain, awareness, _, _) = Create(Stealth.On);
            awareness.Alarm(_elsewhere);

            brain.Tick(0.1f, Sees(_ahead));

            Assert.That(brain.State, Is.EqualTo(EnemyState.Chase));
            Assert.That(awareness.RaisedByAlarm, Is.False);
            Assert.That(awareness.LastSeen, Is.EqualTo(_ahead));
        }

        [Test]
        public void ResetForgetsEverything()
        {
            var (brain, awareness, stages, _) = Create(Stealth.On);
            TickUntil(brain, awareness, AwarenessStage.Alerted, Sees(_ahead));

            awareness.Reset();

            Assert.That(awareness.Stage, Is.EqualTo(AwarenessStage.Unaware));
            Assert.That(awareness.Level, Is.EqualTo(0f));
            Assert.That(awareness.LastSeen, Is.Null);
            Assert.That(stages[stages.Count - 1], Is.EqualTo(AwarenessStage.Unaware));
        }

        [Test]
        public void FightingStatesAreChaseAttackAndTakeCover()
        {
            Assert.That(StealthAwareness.IsFighting(EnemyState.Chase), Is.True);
            Assert.That(StealthAwareness.IsFighting(EnemyState.Attack), Is.True);
            Assert.That(StealthAwareness.IsFighting(EnemyState.TakeCover), Is.True);
            Assert.That(StealthAwareness.IsFighting(EnemyState.Search), Is.False);
            Assert.That(StealthAwareness.IsFighting(EnemyState.Patrol), Is.False);
            Assert.That(StealthAwareness.IsFighting(EnemyState.Idle), Is.False);
            Assert.That(StealthAwareness.IsFighting(EnemyState.Dead), Is.False);
        }

        [Test]
        public void InvalidInputsAreRejected()
        {
            var awareness = new StealthAwareness(AwarenessSpec.Default);

            Assert.Throws<ArgumentNullException>(() => _ = new StealthAwareness(null));
            Assert.Throws<ArgumentOutOfRangeException>(() => awareness.Rule = (Stealth)7);
            Assert.Throws<ArgumentOutOfRangeException>(() => awareness.Observe(Vector3.UnitZ, (AwarenessPosture)7, 1f, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => awareness.Observe(Vector3.UnitZ, AwarenessPosture.Standing, -0.1f, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => awareness.Observe(Vector3.UnitZ, AwarenessPosture.Standing, 1f, float.PositiveInfinity));
            Assert.Throws<ArgumentNullException>(() => awareness.Contribute(null, 0.1f, Sees(_ahead)));
            Assert.Throws<ArgumentNullException>(() => awareness.TryOverride(null, 0.1f, Sees(_ahead), EnemyDecision.None, out _));
        }

        private sealed class Recorder : IPerceptionContributor
        {
            public List<float> Awareness { get; } = new List<float>();

            public EnemyPerception Contribute(EnemyBrain brain, float deltaSeconds, EnemyPerception perception)
            {
                Awareness.Add(perception.Awareness);
                return perception;
            }
        }
    }
}
