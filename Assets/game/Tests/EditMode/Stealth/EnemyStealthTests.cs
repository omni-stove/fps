using System.Text.RegularExpressions;
using Shooter.Core;
using Shooter.Editor;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    // Update・OnEnable は Edit Mode では走らないので、ゲージのたまり方は Core のテストに、仲間を探して伝えるところと頭上の印は射撃場の smoke に任せる。
    public sealed class EnemyStealthTests
    {
        private GameObject _host;
        private EnemyAgent _agent;
        private EnemyStealth _stealth;
        private WeaponDefinition _rifle;

        [SetUp]
        public void SetUp()
        {
            _rifle = ScriptableObject.CreateInstance<WeaponDefinition>();
            _rifle.name = "Rifle";
            WeaponAssets.Configure(_rifle, TestWeapons.RifleSpec());
            (_host, _agent, _stealth) = CreateEnemy("enemy-1");
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_host);
            Object.DestroyImmediate(_rifle);
        }

        [Test]
        public void InitializingHooksTheAwarenessIntoTheBrainWithStealthOff()
        {
            Assert.That(_stealth.TryInitialize(), Is.True);
            Assert.That(_stealth.TryInitialize(), Is.True, "a second call does nothing");

            Assert.That(_agent.Brain.PerceptionContributors, Is.EqualTo(new[] { _stealth.Awareness }));
            Assert.That(_agent.Brain.Behaviors, Is.EqualTo(new[] { _stealth.Awareness }));
            Assert.That(_stealth.Rule, Is.EqualTo(Stealth.Off));
            Assert.That(_stealth.Stage, Is.EqualTo(AwarenessStage.Unaware));
            Assert.That(_stealth.Id, Is.EqualTo("enemy-1"));
        }

        [Test]
        public void AnInvalidSpecIsReportedAndNothingIsHooked()
        {
            var serialized = new SerializedObject(_stealth);
            serialized.FindProperty("_settings._alertThreshold").floatValue = 0.9f;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            LogAssert.Expect(LogType.Error, new Regex(@"^\[Stealth\] id=enemy-1 の設定が不正です: "));
            Assert.That(_stealth.TryInitialize(), Is.False);
            Assert.That(_stealth.Awareness, Is.Null);
        }

        [Test]
        public void TheSpecRoundTripsUntilInitialized()
        {
            var spec = new AwarenessSpec(
                fieldOfViewDegrees: 90f,
                gainPerSecond: 2f,
                crouchGainFactor: 0.3f,
                decayDelaySeconds: 1f,
                noticeThreshold: 0.1f,
                alertThreshold: 0.5f,
                alarmRadius: 8f,
                engagedHoldThreshold: 0.7f);

            _stealth.ApplySpec(spec);
            var created = _stealth.CreateSpec();

            Assert.That(created.FieldOfViewDegrees, Is.EqualTo(90f));
            Assert.That(created.GainPerSecond, Is.EqualTo(2f));
            Assert.That(created.CrouchGainFactor, Is.EqualTo(0.3f));
            Assert.That(created.DecayDelaySeconds, Is.EqualTo(1f));
            Assert.That(created.NoticeThreshold, Is.EqualTo(0.1f));
            Assert.That(created.AlertThreshold, Is.EqualTo(0.5f));
            Assert.That(created.AlarmRadius, Is.EqualTo(8f));
            Assert.That(created.EngagedHoldThreshold, Is.EqualTo(0.7f));

            Assert.That(_stealth.TryInitialize(), Is.True);
            Assert.That(_stealth.Awareness.Spec.AlarmRadius, Is.EqualTo(8f));
            Assert.Throws<System.InvalidOperationException>(() => _stealth.ApplySpec(AwarenessSpec.Default));
        }

        [Test]
        public void AnAlarmFromAnAllyEngagesOnlyWithStealthOn()
        {
            Assert.That(_stealth.TryInitialize(), Is.True);
            var spot = new Vector3(1f, 0f, 2f);

            Assert.That(_stealth.ReceiveAlarm(null, spot), Is.False, "stealth off keeps the old behavior");
            Assert.That(_stealth.Stage, Is.EqualTo(AwarenessStage.Unaware));

            _stealth.Awareness.Rule = Stealth.On;
            LogAssert.Expect(LogType.Log, EnemyStealth.FormatStageLog("enemy-1", AwarenessStage.Unaware, AwarenessStage.Engaged, 1f, Stealth.On, spot.ToNumerics()));
            LogAssert.Expect(LogType.Log, EnemyStealth.FormatAlarmLog("enemy-1", "unknown", spot));
            Assert.That(_stealth.ReceiveAlarm(null, spot), Is.True);

            Assert.That(_stealth.Stage, Is.EqualTo(AwarenessStage.Engaged));
            Assert.That(_stealth.Awareness.RaisedByAlarm, Is.True);
            Assert.That(_stealth.ReceiveAlarm(null, spot), Is.False, "already engaged");
        }

        [Test]
        public void DyingForgetsTheAwareness()
        {
            Assert.That(_stealth.TryInitialize(), Is.True);
            _stealth.Awareness.Rule = Stealth.On;
            _stealth.ReceiveAlarm(null, Vector3.zero);

            _host.GetComponent<Damageable>().TakeDamage(10);

            Assert.That(_stealth.Stage, Is.EqualTo(AwarenessStage.Unaware));
            Assert.That(_stealth.Awareness.LastSeen, Is.Null);
            Assert.That(_stealth.ReceiveAlarm(null, Vector3.zero), Is.False, "the dead do not answer");
        }

        [Test]
        public void AlliesShareASideOrBothHaveNone()
        {
            var (otherHost, _, other) = CreateEnemy("enemy-2");
            try
            {
                Assert.That(_stealth.IsAllyOf(other), Is.True, "campaign enemies have no side and are all allies");
                Assert.That(_stealth.IsAllyOf(_stealth), Is.False);
                Assert.That(_stealth.IsAllyOf(null), Is.False);

                var mine = _host.AddComponent<SideMember>();
                mine.Side = new Side(1);
                Assert.That(_stealth.IsAllyOf(other), Is.False, "only one has a side");

                var theirs = otherHost.AddComponent<SideMember>();
                theirs.Side = new Side(1);
                Assert.That(_stealth.IsAllyOf(other), Is.True);

                theirs.Side = new Side(0);
                Assert.That(_stealth.IsAllyOf(other), Is.False);
            }
            finally
            {
                Object.DestroyImmediate(otherHost);
            }
        }

        [Test]
        public void LogsHaveAStableShape()
        {
            Assert.That(
                EnemyStealth.FormatStageLog("enemy-1", AwarenessStage.Noticed, AwarenessStage.Alerted, 0.6f, Stealth.On, new System.Numerics.Vector3(1f, 0f, -2.5f)),
                Is.EqualTo("[Stealth] id=enemy-1 stage=Alerted prev=Noticed level=0.60 rule=On last=(1.0, 0.0, -2.5)"));
            Assert.That(
                EnemyStealth.FormatStageLog("enemy-1", AwarenessStage.Unaware, AwarenessStage.Engaged, 1f, Stealth.Off, null),
                Is.EqualTo("[Stealth] id=enemy-1 stage=Engaged prev=Unaware level=1.00 rule=Off last=none"));
            Assert.That(EnemyStealth.StageLogPrefix("enemy-1"), Is.EqualTo("[Stealth] id=enemy-1 stage="));
            Assert.That(
                EnemyStealth.FormatAlarmLog("enemy-1", "enemy-2", new Vector3(3f, 0f, 4f)),
                Is.EqualTo("[Stealth] id=enemy-1 alarmed by=enemy-2 at=(3.0, 0.0, 4.0)"));
        }

        [Test]
        public void AClatterFromThePlayerSendsTheEnemyToInvestigate()
        {
            Assert.That(_stealth.TryInitialize(), Is.True);
            _stealth.Awareness.Rule = Stealth.On;
            var player = new GameObject("Player");
            try
            {
                var clatter = Clatter(new Vector3(0f, 0f, -4f));
                LogAssert.Expect(LogType.Log, new Regex(@"^\[Stealth\] id=enemy-1 stage=Alerted prev=Unaware "));
                LogAssert.Expect(LogType.Log, EnemyStealth.FormatHeardLog("enemy-1", clatter, HearingOutcome.Investigating, Stealth.On));

                Assert.That(_stealth.Hear(player, clatter), Is.EqualTo(HearingOutcome.Investigating));
                Assert.That(_stealth.Stage, Is.EqualTo(AwarenessStage.Alerted));
                Assert.That(_stealth.Awareness.LastSeen, Is.EqualTo(clatter.Position));
            }
            finally
            {
                Object.DestroyImmediate(player);
            }
        }

        [Test]
        public void AnEngagedEnemyIgnoresTheClatter()
        {
            Assert.That(_stealth.TryInitialize(), Is.True);
            _stealth.Awareness.Rule = Stealth.On;
            _stealth.ReceiveAlarm(null, Vector3.zero);
            var clatter = Clatter(new Vector3(0f, 0f, -4f));
            LogAssert.Expect(LogType.Log, EnemyStealth.FormatHeardLog("enemy-1", clatter, HearingOutcome.Engaged, Stealth.On));

            Assert.That(_stealth.Hear(null, clatter), Is.EqualTo(HearingOutcome.Engaged));
            Assert.That(_stealth.Awareness.LastSeen, Is.EqualTo(System.Numerics.Vector3.Zero));
        }

        [Test]
        public void WithStealthOffAClatterStillSendsTheEnemyToSearch()
        {
            Assert.That(_stealth.TryInitialize(), Is.True);
            var clatter = Clatter(new Vector3(0f, 0f, -4f));
            LogAssert.Expect(LogType.Log, EnemyStealth.FormatHeardLog("enemy-1", clatter, HearingOutcome.Investigating, Stealth.Off));

            Assert.That(_stealth.Hear(null, clatter), Is.EqualTo(HearingOutcome.Investigating));
            Assert.That(_stealth.Stage, Is.EqualTo(AwarenessStage.Unaware), "the gauge is not used with stealth off");
            Assert.That(_agent.Perceive().NoisePosition, Is.EqualTo(clatter.Position), "the brain searches where the object landed");
        }

        [Test]
        public void SoundsFromItselfOrAnAllyAreNotHeard()
        {
            Assert.That(_stealth.TryInitialize(), Is.True);
            _stealth.Awareness.Rule = Stealth.On;
            var (allyHost, _, _) = CreateEnemy("enemy-2");
            try
            {
                var shot = new SoundStimulus(SoundCueKind.Gunshot, System.Numerics.Vector3.Zero, 30f, false);

                Assert.That(_stealth.Hear(_host, shot), Is.EqualTo(HearingOutcome.NotHeard));
                Assert.That(_stealth.Hear(allyHost, shot), Is.EqualTo(HearingOutcome.NotHeard));
                Assert.That(_stealth.IsHostileSource(allyHost), Is.False);
                Assert.That(_stealth.Level, Is.EqualTo(0f));

                _host.AddComponent<SideMember>().Side = new Side(1);
                allyHost.AddComponent<SideMember>().Side = new Side(0);
                Assert.That(_stealth.IsHostileSource(allyHost), Is.True, "an enemy of another side is heard");
                Assert.That(_stealth.Hear(allyHost, shot), Is.EqualTo(HearingOutcome.Noticed));
            }
            finally
            {
                Object.DestroyImmediate(allyHost);
            }
        }

        [Test]
        public void TheNoiseReachesEveryListenerAndCountsThoseWhoReacted()
        {
            Assert.That(_stealth.TryInitialize(), Is.True);
            _stealth.Awareness.Rule = Stealth.On;
            var (farHost, _, far) = CreateEnemy("enemy-far");
            try
            {
                farHost.transform.position = new Vector3(100f, 0f, 0f);
                Assert.That(far.TryInitialize(), Is.True);
                far.Awareness.Rule = Stealth.On;
                LogAssert.Expect(LogType.Log, new Regex(@"^\[Stealth\] id=enemy-1 stage=Alerted "));
                LogAssert.Expect(LogType.Log, new Regex(@"^\[Stealth\] id=enemy-1 heard kind=Clatter "));

                var reacted = StealthNoise.Emit(null, Clatter(new Vector3(0f, 0f, -4f)), new[] { _stealth, null, far });

                Assert.That(reacted, Is.EqualTo(1));
                Assert.That(far.Stage, Is.EqualTo(AwarenessStage.Unaware), "too far to hear it");
            }
            finally
            {
                Object.DestroyImmediate(farHost);
            }
        }

        [Test]
        public void TheHearingValuesRoundTripThroughTheSpec()
        {
            _stealth.ApplySpec(new AwarenessSpec(gunshotGain: 0.2f, footstepGain: 0.05f, investigateLevel: 0.7f));
            var created = _stealth.CreateSpec();

            Assert.That(created.GunshotGain, Is.EqualTo(0.2f));
            Assert.That(created.FootstepGain, Is.EqualTo(0.05f));
            Assert.That(created.InvestigateLevel, Is.EqualTo(0.7f));
        }

        [Test]
        public void ANewStealthHasTheAwarenessDefaults()
        {
            var spec = _stealth.CreateSpec();
            var expected = AwarenessSpec.Default;

            Assert.That(spec.FieldOfViewDegrees, Is.EqualTo(expected.FieldOfViewDegrees));
            Assert.That(spec.GainPerSecond, Is.EqualTo(expected.GainPerSecond));
            Assert.That(spec.FullGainDistance, Is.EqualTo(expected.FullGainDistance));
            Assert.That(spec.FarGainFactor, Is.EqualTo(expected.FarGainFactor));
            Assert.That(spec.PeripheralGainFactor, Is.EqualTo(expected.PeripheralGainFactor));
            Assert.That(spec.CrouchGainFactor, Is.EqualTo(expected.CrouchGainFactor));
            Assert.That(spec.ProneGainFactor, Is.EqualTo(expected.ProneGainFactor));
            Assert.That(spec.StillGainFactor, Is.EqualTo(expected.StillGainFactor));
            Assert.That(spec.FullMotionSpeed, Is.EqualTo(expected.FullMotionSpeed));
            Assert.That(spec.DecayDelaySeconds, Is.EqualTo(expected.DecayDelaySeconds));
            Assert.That(spec.DecayPerSecond, Is.EqualTo(expected.DecayPerSecond));
            Assert.That(spec.NoticeThreshold, Is.EqualTo(expected.NoticeThreshold));
            Assert.That(spec.AlertThreshold, Is.EqualTo(expected.AlertThreshold));
            Assert.That(spec.AlarmRadius, Is.EqualTo(expected.AlarmRadius));
            Assert.That(spec.EngagedHoldThreshold, Is.EqualTo(expected.EngagedHoldThreshold));
            Assert.That(spec.GunshotGain, Is.EqualTo(expected.GunshotGain));
            Assert.That(spec.FootstepGain, Is.EqualTo(expected.FootstepGain));
            Assert.That(spec.InvestigateLevel, Is.EqualTo(expected.InvestigateLevel));
        }

        [Test]
        public void AwarenessWrittenInTheSettingsReachesTheSpec()
        {
            _stealth.Settings.CopyFrom(new AwarenessSpec(
                fieldOfViewDegrees: 90f,
                gainPerSecond: 2f,
                noticeThreshold: 0.3f,
                alertThreshold: 0.7f,
                engagedHoldThreshold: 0.9f,
                alarmRadius: 8f,
                investigateLevel: 0.8f));

            var spec = _stealth.CreateSpec();

            Assert.That(spec.FieldOfViewDegrees, Is.EqualTo(90f));
            Assert.That(spec.GainPerSecond, Is.EqualTo(2f));
            Assert.That(spec.NoticeThreshold, Is.EqualTo(0.3f));
            Assert.That(spec.AlertThreshold, Is.EqualTo(0.7f));
            Assert.That(spec.EngagedHoldThreshold, Is.EqualTo(0.9f));
            Assert.That(spec.AlarmRadius, Is.EqualTo(8f));
            Assert.That(spec.InvestigateLevel, Is.EqualTo(0.8f));
            Assert.That(spec.FootstepGain, Is.EqualTo(AwarenessSpec.DefaultFootstepGain));
        }

        [Test]
        public void AnInvalidNoticeThresholdNamesTheValue()
        {
            var serialized = new SerializedObject(_stealth);
            serialized.FindProperty("_settings._noticeThreshold").floatValue = 0.9f;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            LogAssert.Expect(LogType.Error, new Regex(@"^\[Stealth\] id=enemy-1 の設定が不正です: .*noticeThreshold"));
            Assert.That(_stealth.TryInitialize(), Is.False);
            Assert.That(_stealth.Awareness, Is.Null);
        }

        [Test]
        public void AsAMeleeWitnessItReportsItsStage()
        {
            IMeleeWitness witness = _stealth;
            Assert.That(witness.IsUnaware, Is.True, "not initialized yet");
            Assert.That(witness.StageName, Is.EqualTo("Unaware"));

            Assert.That(_stealth.TryInitialize(), Is.True);
            _stealth.Awareness.Rule = Stealth.On;
            _stealth.Awareness.Meter.Raise(_stealth.Awareness.Spec.NoticeThreshold);

            Assert.That(witness.Id, Is.EqualTo("enemy-1"));
            Assert.That(witness.gameObject, Is.SameAs(_host));
            Assert.That(witness.IsUnaware, Is.False);
            Assert.That(witness.StageName, Is.EqualTo(AwarenessStage.Noticed.ToString()));
        }

        [Test]
        public void WitnessingRaisesOnlyAnEnemyLookingAtTheAttackerWithStealthOn()
        {
            Assert.That(_stealth.TryInitialize(), Is.True);
            _stealth.Awareness.Rule = Stealth.On;
            var attackerHost = new GameObject("Attacker");
            try
            {
                var attacker = attackerHost.AddComponent<Damageable>();
                attacker.Initialize("attacker", 100);

                _agent.Target = attacker;
                Assert.That(_stealth.Witness(attacker, 1f), Is.False, "not looking");
                Assert.That(_stealth.Stage, Is.EqualTo(AwarenessStage.Unaware));

                _agent.Target = null;
                SeeTheTarget();
                var level = _stealth.Level;
                Assert.That(_stealth.Witness(attacker, 1f), Is.False, "looking at someone else");
                Assert.That(_stealth.Level, Is.EqualTo(level));

                _agent.Target = attacker;
                _stealth.Awareness.Rule = Stealth.Off;
                _stealth.Witness(attacker, 1f);
                Assert.That(_stealth.Stage, Is.EqualTo(AwarenessStage.Unaware), "stealth off keeps the old behavior");

                _stealth.Awareness.Rule = Stealth.On;
                LogAssert.Expect(LogType.Log, new Regex(@"^\[Stealth\] id=enemy-1 stage=Engaged "));
                Assert.That(_stealth.Witness(attacker, 1f), Is.True);
                Assert.That(_stealth.Stage, Is.EqualTo(AwarenessStage.Engaged));
                Assert.That(_stealth.Awareness.RaisedByAlarm, Is.False);
                Assert.That(_stealth.Awareness.LastSeen, Is.EqualTo(attacker.GroundPoint.ToNumerics()));
            }
            finally
            {
                Object.DestroyImmediate(attackerHost);
            }
        }

        private void SeeTheTarget()
        {
            _stealth.Awareness.Meter.Tick(0f, EnemySpec.DefaultSightRange, new AwarenessSighting(true, 4f, 0f, AwarenessPosture.Standing, 1f, 0f));
            Assert.That(_stealth.Awareness.Meter.IsSeeing, Is.True);
        }

        private static SoundStimulus Clatter(Vector3 at)
        {
            return new SoundStimulus(SoundCueKind.Clatter, at.ToNumerics(), 10f, true);
        }

        private (GameObject Host, EnemyAgent Agent, EnemyStealth Stealth) CreateEnemy(string id)
        {
            var host = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            host.name = id;
            var agent = host.AddComponent<EnemyAgent>();
            agent.WeaponDefinition = _rifle;
            agent.Id = id;
            host.GetComponent<Damageable>().Initialize(id, 10);
            var stealth = host.AddComponent<EnemyStealth>();
            return (host, agent, stealth);
        }
    }
}
