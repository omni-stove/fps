using System;
using System.Numerics;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class StealthHearingTests
    {
        private static readonly Vector3 _self = Vector3.Zero;
        private static readonly Vector3 _behind = new Vector3(0f, 0f, -6f);
        private static readonly Vector3 _ahead = new Vector3(0f, 0f, 15f);

        private static EnemySpec BrainSpec() => new EnemySpec(20f, 10f, 1f, 2f, 0.5f, 6f, 0f, 2);

        private static EnemyPerception Nothing() => new EnemyPerception(_self, null, false, false, null, false, null, 1f);

        private static EnemyPerception Sees(Vector3 target) => new EnemyPerception(_self, target, true, false, null, false, null, 1f);

        private static SoundStimulus Clatter(Vector3 at, float radius = 10f) => new SoundStimulus(SoundCueKind.Clatter, at, radius, true);

        private static (EnemyBrain Brain, StealthAwareness Awareness) Create(Stealth rule)
        {
            var brain = new EnemyBrain(BrainSpec());
            var awareness = new StealthAwareness(AwarenessSpec.Default) { Rule = rule };
            awareness.Observe(Vector3.UnitZ, AwarenessPosture.Standing, 1f, 0f);
            brain.AddPerceptionContributor(awareness);
            brain.AddBehavior(awareness);
            return (brain, awareness);
        }

        [Test]
        public void AClatterBehindAnUnawareEnemySendsItToInvestigate()
        {
            var (brain, awareness) = Create(Stealth.On);

            Assert.That(awareness.Hear(brain, _self, Clatter(_behind)), Is.EqualTo(HearingOutcome.Investigating));

            Assert.That(awareness.Stage, Is.EqualTo(AwarenessStage.Alerted));
            Assert.That(awareness.Level, Is.EqualTo(AwarenessSpec.DefaultInvestigateLevel));
            Assert.That(awareness.LastSeen, Is.EqualTo(_behind));

            var decision = brain.Tick(0.05f, Nothing());
            Assert.That(decision.MoveTo, Is.EqualTo(_behind), "walks to where the object landed");
            Assert.That(decision.Fire, Is.False);
        }

        [Test]
        public void TheInvestigationLastsUntilTheEnemyCouldHaveWalkedThere()
        {
            var (brain, awareness) = Create(Stealth.On);
            awareness.Hear(brain, _self, Clatter(_behind));

            for (var i = 0; i < 60; i++)
            {
                brain.Tick(0.05f, Nothing());
            }

            Assert.That(awareness.Stage, Is.EqualTo(AwarenessStage.Alerted), "3 s after the clatter it is still alerted");
        }

        [Test]
        public void ASoundOutOfRangeIsNotHeard()
        {
            var (brain, awareness) = Create(Stealth.On);

            Assert.That(awareness.Hear(brain, _self, Clatter(_behind, 5f)), Is.EqualTo(HearingOutcome.NotHeard));
            Assert.That(awareness.Stage, Is.EqualTo(AwarenessStage.Unaware));
            Assert.That(awareness.LastSeen, Is.Null);
        }

        [Test]
        public void AnEngagedEnemyDoesNotReact()
        {
            var (brain, awareness) = Create(Stealth.On);
            for (var i = 0; i < 1000 && awareness.Stage != AwarenessStage.Engaged; i++)
            {
                brain.Tick(0.05f, Sees(_ahead));
            }

            Assume.That(StealthAwareness.IsFighting(brain.State), Is.True);

            Assert.That(awareness.Hear(brain, _self, Clatter(_behind)), Is.EqualTo(HearingOutcome.Engaged));
            Assert.That(awareness.LastSeen, Is.EqualTo(_ahead));
            Assert.That(brain.Tick(0.05f, Sees(_ahead)).LookAt, Is.EqualTo(_ahead), "keeps fighting the target");
        }

        [Test]
        public void AnEnemyAlarmedByAnAllyDoesNotReact()
        {
            var (brain, awareness) = Create(Stealth.On);
            awareness.Alarm(_ahead);

            Assert.That(awareness.Hear(brain, _self, Clatter(_behind)), Is.EqualTo(HearingOutcome.Engaged));
            Assert.That(awareness.LastSeen, Is.EqualTo(_ahead));
        }

        [Test]
        public void WithStealthOffTheRuleIsReportedAndNothingChanges()
        {
            var (brain, awareness) = Create(Stealth.Off);

            Assert.That(awareness.Hear(brain, _self, Clatter(_behind)), Is.EqualTo(HearingOutcome.RuleOff));
            Assert.That(awareness.Level, Is.EqualTo(0f));
            Assert.That(awareness.LastSeen, Is.Null);
        }

        [Test]
        public void TheDeadDoNotHear()
        {
            var (brain, awareness) = Create(Stealth.On);
            brain.Kill();

            Assert.That(awareness.Hear(brain, _self, Clatter(_behind)), Is.EqualTo(HearingOutcome.NotHeard));
        }

        [Test]
        public void GunshotsRaiseTheGaugeByLoudnessButNeverToEngaged()
        {
            var (brain, awareness) = Create(Stealth.On);
            var shot = new SoundStimulus(SoundCueKind.Gunshot, _behind, 12f, false);

            Assert.That(awareness.Hear(brain, _self, shot), Is.EqualTo(HearingOutcome.Noticed));
            Assert.That(awareness.Level, Is.EqualTo(AwarenessSpec.DefaultGunshotGain * 0.5f).Within(1e-5f));
            Assert.That(awareness.LastSeen, Is.EqualTo(_behind));

            for (var i = 0; i < 20; i++)
            {
                awareness.Hear(brain, _self, shot);
            }

            Assert.That(awareness.Stage, Is.EqualTo(AwarenessStage.Alerted));
            Assert.That(awareness.Level, Is.EqualTo(AwarenessSpec.DefaultInvestigateLevel));
        }

        [Test]
        public void AFootstepRaisesTheGaugeLessThanAGunshot()
        {
            var (brain, awareness) = Create(Stealth.On);

            Assert.That(awareness.Hear(brain, _self, new SoundStimulus(SoundCueKind.Footstep, _behind, 12f, false)), Is.EqualTo(HearingOutcome.Raised));
            Assert.That(awareness.Stage, Is.EqualTo(AwarenessStage.Unaware));
            Assert.That(awareness.Level, Is.EqualTo(AwarenessSpec.DefaultFootstepGain * 0.5f).Within(1e-5f));
        }

        [Test]
        public void SoundsWithoutAGainAreIgnored()
        {
            var (brain, awareness) = Create(Stealth.On);

            Assert.That(awareness.Hear(brain, _self, new SoundStimulus(SoundCueKind.Reload, _behind, 12f, false)), Is.EqualTo(HearingOutcome.Ignored));
            Assert.That(awareness.Level, Is.EqualTo(0f));
            Assert.That(awareness.LastSeen, Is.Null);
        }

        [Test]
        public void HearingNeedsABrain()
        {
            var awareness = new StealthAwareness(AwarenessSpec.Default);

            Assert.Throws<ArgumentNullException>(() => awareness.Hear(null, _self, Clatter(_behind)));
        }

        [Test]
        public void LoudnessFallsLinearlyToTheRadius()
        {
            var sound = new SoundStimulus(SoundCueKind.Gunshot, Vector3.Zero, 10f, false);

            Assert.That(sound.LoudnessAt(Vector3.Zero), Is.EqualTo(1f));
            Assert.That(sound.LoudnessAt(new Vector3(0f, 0f, 2.5f)), Is.EqualTo(0.75f).Within(1e-6f));
            Assert.That(sound.LoudnessAt(new Vector3(10f, 0f, 0f)), Is.EqualTo(0f));
            Assert.That(sound.LoudnessAt(new Vector3(10.1f, 0f, 0f)), Is.EqualTo(0f));
            Assert.That(sound.LoudnessAt(new Vector3(float.NaN, 0f, 0f)), Is.EqualTo(0f));
            Assert.That(new SoundStimulus(SoundCueKind.Gunshot, Vector3.Zero, 0f, false).LoudnessAt(Vector3.Zero), Is.EqualTo(0f), "a silenced sound reaches no one");
        }

        [Test]
        public void InvalidSoundsAreRejected()
        {
            Assert.That(
                Assert.Throws<ArgumentOutOfRangeException>(() => new SoundStimulus((SoundCueKind)99, Vector3.Zero, 1f, false)).ParamName,
                Is.EqualTo("kind"));
            Assert.That(
                Assert.Throws<ArgumentOutOfRangeException>(() => new SoundStimulus(SoundCueKind.Gunshot, new Vector3(float.NaN, 0f, 0f), 1f, false)).ParamName,
                Is.EqualTo("position"));
            Assert.That(
                Assert.Throws<ArgumentOutOfRangeException>(() => new SoundStimulus(SoundCueKind.Gunshot, Vector3.Zero, -1f, false)).ParamName,
                Is.EqualTo("radius"));
            Assert.That(
                Assert.Throws<ArgumentOutOfRangeException>(() => new SoundStimulus(SoundCueKind.Gunshot, Vector3.Zero, float.PositiveInfinity, false)).ParamName,
                Is.EqualTo("radius"));
        }

        [Test]
        public void TheSpecKeepsTheHearingValuesAndRejectsBadOnes()
        {
            var spec = new AwarenessSpec(gunshotGain: 0.3f, footstepGain: 0.05f, investigateLevel: 0.7f);

            Assert.That(spec.HearingGain(SoundCueKind.Gunshot), Is.EqualTo(0.3f));
            Assert.That(spec.HearingGain(SoundCueKind.Footstep), Is.EqualTo(0.05f));
            Assert.That(spec.HearingGain(SoundCueKind.Explosion), Is.EqualTo(0f));
            Assert.That(spec.HearingGain(SoundCueKind.Clatter), Is.EqualTo(0f));
            Assert.That(spec.InvestigateLevel, Is.EqualTo(0.7f));

            Assert.That(Assert.Throws<ArgumentOutOfRangeException>(() => new AwarenessSpec(gunshotGain: 1.5f)).ParamName, Is.EqualTo("gunshotGain"));
            Assert.That(Assert.Throws<ArgumentOutOfRangeException>(() => new AwarenessSpec(footstepGain: -0.1f)).ParamName, Is.EqualTo("footstepGain"));
            Assert.That(
                Assert.Throws<ArgumentOutOfRangeException>(() => new AwarenessSpec(investigateLevel: AwarenessSpec.DefaultAlertThreshold - 0.01f)).ParamName,
                Is.EqualTo("investigateLevel"));
            Assert.That(Assert.Throws<ArgumentOutOfRangeException>(() => new AwarenessSpec(investigateLevel: 1f)).ParamName, Is.EqualTo("investigateLevel"));
        }

        [Test]
        public void AddingStopsAtTheCeilingAndNeverLowers()
        {
            var meter = new AwarenessMeter(AwarenessSpec.Default);

            meter.Add(0.3f, 0.5f);
            Assert.That(meter.Level, Is.EqualTo(0.3f).Within(1e-6f));

            meter.Add(0.3f, 0.5f);
            Assert.That(meter.Level, Is.EqualTo(0.5f));

            meter.Raise(0.9f);
            meter.Add(0.3f, 0.5f);
            Assert.That(meter.Level, Is.EqualTo(0.9f));

            Assert.Throws<ArgumentOutOfRangeException>(() => meter.Add(-0.1f, 0.5f));
            Assert.Throws<ArgumentOutOfRangeException>(() => meter.Add(0.1f, 1.1f));
        }
    }
}
