using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Fps.Core;
using Fps.Editor;
using Fps.Runtime;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Fps.Tests.EditMode
{
    public sealed class BossControllerTests
    {
        private const int MaxHealth = 100;
        private const int BaseDamage = 30;

        private static readonly ZoneDamageMultipliers Multipliers = new ZoneDamageMultipliers(0.25, 0.2, 0.1, 3.0);

        private GameObject _root;
        private Damageable _damageable;
        private Hitbox _body;
        private Hitbox _weakHitbox;
        private BossWeakPoint _weakPoint;
        private BossController _boss;
        private readonly List<BossPhaseChangedInfo> _events = new List<BossPhaseChangedInfo>();

        [SetUp]
        public void SetUp()
        {
            CombatEvents.ClearSubscribers();
            CombatEvents.BossPhaseChanged += _events.Add;

            _root = new GameObject("Boss");
            _damageable = _root.AddComponent<Damageable>();
            _damageable.Initialize("Boss", MaxHealth);
            _body = CreatePart("Body", HitZone.Body);
            _weakHitbox = CreatePart("WeakPoint", HitZone.WeakPoint);
            _weakPoint = _weakHitbox.gameObject.AddComponent<BossWeakPoint>();
            _weakPoint.Initialize("Core");

            _boss = _root.AddComponent<BossController>();
            _boss.Configure(
                new[]
                {
                    new BossPhaseEntry("Guarded", 1f, 1f, Array.Empty<string>()),
                    new BossPhaseEntry("Exposed", 0.5f, 2.5f, new[] { "Core" }),
                },
                Multipliers);
            Assert.That(_boss.TryInitialize(), Is.True);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_root);
            CombatEvents.ClearSubscribers();
            _events.Clear();
        }

        [Test]
        public void StartsInTheFirstPhaseWithTheWeakPointHiddenAndTheZoneMultipliersSet()
        {
            Assert.That(_boss.CurrentPhase.Id, Is.EqualTo("Guarded"));
            Assert.That(_boss.MoveSpeedMultiplier, Is.EqualTo(1f));
            Assert.That(_weakPoint.IsExposed, Is.False);
            Assert.That(_boss.WeakPoints, Is.EqualTo(new[] { _weakPoint }));
            Assert.That(_damageable.ZoneMultipliers.Body, Is.EqualTo(0.2).Within(1e-6));
            Assert.That(_damageable.ZoneMultipliers.WeakPoint, Is.EqualTo(3.0).Within(1e-6));
            Assert.That(_events, Is.Empty, "starting is not a change");
        }

        [Test]
        public void HitsOutsideTheWeakPointDealLittleAndAreLogged()
        {
            LogAssert.Expect(LogType.Log, "[Boss] id=Boss hit zone=Body damage=6 health=94/100");

            var damage = _body.ApplyHit(BaseDamage, 0f, DamageFalloff.None);

            Assert.That(damage, Is.EqualTo(6));
            Assert.That(_damageable.Health.Current, Is.EqualTo(94));
        }

        [Test]
        public void HalfHealthMovesToTheNextPhaseExposesTheWeakPointAndRaisesTheEvent()
        {
            _damageable.TakeDamage(45);
            Assert.That(_boss.CurrentPhase.Id, Is.EqualTo("Guarded"));

            LogAssert.Expect(LogType.Log, "[Boss] id=Boss hit zone=Body damage=6 health=49/100");
            LogAssert.Expect(LogType.Log, "[Boss] id=Boss weakpoint=Core exposed=True");
            LogAssert.Expect(
                LogType.Log,
                new Regex(@"^\[Boss\] id=Boss phase=0->1 from=Guarded to=Exposed reason=health<=0\.5 health=0\.49 speed=2\.5 exposed=Core$"));

            _body.ApplyHit(BaseDamage, 0f, DamageFalloff.None);

            Assert.That(_boss.CurrentPhase.Id, Is.EqualTo("Exposed"));
            Assert.That(_boss.MoveSpeedMultiplier, Is.EqualTo(2.5f));
            Assert.That(_weakPoint.IsExposed, Is.True);
            Assert.That(_events, Has.Count.EqualTo(1));
            var info = _events[0];
            Assert.That(info.Boss, Is.SameAs(_root));
            Assert.That(info.Damageable, Is.SameAs(_damageable));
            Assert.That(info.BossId, Is.EqualTo("Boss"));
            Assert.That(info.PreviousIndex, Is.Zero);
            Assert.That(info.Index, Is.EqualTo(1));
            Assert.That(info.PreviousPhaseId, Is.EqualTo("Guarded"));
            Assert.That(info.PhaseId, Is.EqualTo("Exposed"));
            Assert.That(info.HealthFraction, Is.EqualTo(0.49f).Within(1e-6f));
            Assert.That(info.IsReset, Is.False);
        }

        [Test]
        public void TheExposedWeakPointTakesTheBigMultiplier()
        {
            _damageable.TakeDamage(50);
            Assert.That(_weakPoint.IsExposed, Is.True);

            var damage = _weakHitbox.ApplyHit(10, 0f, DamageFalloff.None);

            Assert.That(damage, Is.EqualTo(30));
            Assert.That(_damageable.Health.Current, Is.EqualTo(20));
        }

        [Test]
        public void RefillingHealthGoesBackToTheFirstPhaseAndHidesTheWeakPoint()
        {
            _damageable.TakeDamage(60);
            _events.Clear();
            LogAssert.Expect(LogType.Log, "[Boss] id=Boss weakpoint=Core exposed=False");
            LogAssert.Expect(LogType.Log, new Regex(@"^\[Boss\] id=Boss phase=1->0 from=Exposed to=Guarded reason=reset "));

            _damageable.ResetHealth();

            Assert.That(_boss.CurrentPhase.Id, Is.EqualTo("Guarded"));
            Assert.That(_weakPoint.IsExposed, Is.False);
            Assert.That(_events, Has.Count.EqualTo(1));
            Assert.That(_events[0].IsReset, Is.True);
        }

        [Test]
        public void RestoringHealthBelowTheThresholdCatchesUpOnTheNextTick()
        {
            _damageable.RestoreHealth(30);
            Assert.That(_boss.CurrentPhase.Id, Is.EqualTo("Guarded"), "RestoreHealth does not raise Damaged");

            _boss.Tick(0f);

            Assert.That(_boss.CurrentPhase.Id, Is.EqualTo("Exposed"));
        }

        [Test]
        public void TheKillingHitDoesNotChangeThePhase()
        {
            _damageable.TakeDamage(MaxHealth);

            Assert.That(_damageable.IsDead, Is.True);
            Assert.That(_boss.CurrentPhase.Id, Is.EqualTo("Guarded"));
            Assert.That(_events, Is.Empty);
        }

        [Test]
        public void ACueCanAdvanceThePhaseOrToggleAWeakPoint()
        {
            LogAssert.Expect(LogType.Log, "[Boss] id=Boss weakpoint=Core exposed=True");
            Assert.That(_boss.SetWeakPointExposed("Core", true), Is.True);
            Assert.That(_weakPoint.IsExposed, Is.True);
            Assert.That(_boss.CurrentPhase.Id, Is.EqualTo("Guarded"), "toggling a weak point is not a phase change");

            LogAssert.Expect(LogType.Warning, "[Boss] id=Boss has no weak point 'Tail'");
            Assert.That(_boss.SetWeakPointExposed("Tail", true), Is.False);

            LogAssert.Expect(LogType.Log, new Regex(@"^\[Boss\] id=Boss phase=0->1 from=Guarded to=Exposed reason=cue "));
            Assert.That(_boss.Advance(), Is.True);
            Assert.That(_events, Has.Count.EqualTo(1));
            Assert.That(_boss.Advance(), Is.False, "already in the last phase");
        }

        [Test]
        public void CodeCanGivePhasesWithCustomTriggers()
        {
            _boss.UsePhases(new BossPhases(new[]
            {
                new BossPhase("Intro", null),
                new BossPhase("Fight", BossPhaseTrigger.When("after 2s", c => c.SecondsInPhase >= 2f), exposedWeakPoints: new[] { "Core" }),
            }));

            _boss.Tick(1f);
            Assert.That(_boss.CurrentPhase.Id, Is.EqualTo("Intro"));

            _boss.Tick(1f);

            Assert.That(_boss.CurrentPhase.Id, Is.EqualTo("Fight"));
            Assert.That(_weakPoint.IsExposed, Is.True);
            Assert.That(_events, Has.Count.EqualTo(1));
            Assert.That(_events[0].PhaseId, Is.EqualTo("Fight"));
        }

        [Test]
        public void AThrowingTriggerIsLoggedAndTheBossKeepsItsPhase()
        {
            // UsePhases がその場で 1 回、Tick でもう 1 回、条件を見る。
            LogAssert.Expect(LogType.Exception, new Regex("boom"));
            LogAssert.Expect(LogType.Exception, new Regex("boom"));

            _boss.UsePhases(new BossPhases(new[]
            {
                new BossPhase("A", null),
                new BossPhase("B", BossPhaseTrigger.When("broken", _ => throw new InvalidOperationException("boom"))),
            }));
            _boss.Tick(0.1f);

            Assert.That(_boss.CurrentPhase.Id, Is.EqualTo("A"));
        }

        [Test]
        public void ConfigureRefusesBadPhasesAndKeepsThePreviousOnes()
        {
            var before = _boss.PhaseEntries;

            Assert.Throws<ArgumentException>(() => _boss.Configure(Array.Empty<BossPhaseEntry>(), Multipliers));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => _boss.Configure(new[] { new BossPhaseEntry("A", 1f, 1f, null), new BossPhaseEntry("B", 0f, 1f, null) }, Multipliers));
            Assert.Throws<ArgumentNullException>(() => _boss.Configure(null, Multipliers));
            Assert.Throws<ArgumentNullException>(() => _boss.Configure(before, null));

            Assert.That(_boss.PhaseEntries, Is.EqualTo(before));
        }

        [Test]
        public void StrafingSpeedsUpWithThePhase()
        {
            var strafe = _root.AddComponent<BossStrafeMotion>();
            strafe.Configure(1f, 10f);

            strafe.Tick(1f);
            Assert.That(strafe.Offset, Is.EqualTo(1f).Within(1e-4f));
            Assert.That(_root.transform.position.x, Is.EqualTo(1f).Within(1e-4f));

            _damageable.TakeDamage(50);
            strafe.Tick(1f);

            Assert.That(strafe.Offset, Is.EqualTo(3.5f).Within(1e-4f));
        }

        [Test]
        public void StrafingTurnsBackAtTheEdges()
        {
            var strafe = _root.AddComponent<BossStrafeMotion>();
            strafe.Configure(1f, 2f);

            strafe.Tick(3f);
            Assert.That(strafe.Offset, Is.EqualTo(1f).Within(1e-4f), "2m out to the edge, then 1m back");

            strafe.Tick(4f);
            Assert.That(strafe.Offset, Is.EqualTo(-1f).Within(1e-4f));
        }

        [Test]
        public void StrafingStopsWhileDead()
        {
            var strafe = _root.AddComponent<BossStrafeMotion>();
            strafe.Configure(1f, 10f);
            _damageable.Kill();

            strafe.Tick(1f);

            Assert.That(strafe.Offset, Is.Zero);
        }


        private Hitbox CreatePart(string partName, HitZone zone)
        {
            var part = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            part.name = partName;
            part.transform.SetParent(_root.transform, false);
            var hitbox = part.AddComponent<Hitbox>();
            hitbox.Initialize(zone, _damageable);
            return hitbox;
        }
    }
}
