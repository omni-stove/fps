using System.Text.RegularExpressions;
using Fps.Core;
using Fps.Editor;
using Fps.Runtime;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Fps.Tests.EditMode
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
            WeaponAssetBuilder.Configure(_rifle, WeaponAssetBuilder.RifleSpec());
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
            serialized.FindProperty("_alertThreshold").floatValue = 0.9f;
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
