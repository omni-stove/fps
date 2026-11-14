using System.Collections.Generic;
using System.Text.RegularExpressions;
using Fps.Core;
using Fps.Editor;
using Fps.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Fps.Tests.EditMode
{
    // Edit Mode では EnemyAgent の Update が走らないので、判断 (ChargerEnemyBehavior) は知覚を作って直接進め、体を動かす EnemyCharger は Tick を呼ぶ。
    // NavMesh が無いので、EnemyCharger は Transform を直に動かす。
    public sealed class EnemyChargerTests
    {
        // 開いているシーンの物に当たらないよう、離れた所に置く。
        private static readonly Vector3 Origin = new Vector3(600f, 0f, 600f);

        private const float Tick = 0.05f;
        private const int MaxTicks = 60;

        private readonly List<Object> _created = new List<Object>();
        private WeaponDefinition _rifle;

        [SetUp]
        public void SetUp()
        {
            _rifle = ScriptableObject.CreateInstance<WeaponDefinition>();
            _rifle.name = "Rifle";
            WeaponAssets.Configure(_rifle, TestWeapons.RifleSpec());
            _created.Add(_rifle);
        }

        [TearDown]
        public void TearDown()
        {
            for (var i = _created.Count - 1; i >= 0; i--)
            {
                if (_created[i] != null)
                {
                    Object.DestroyImmediate(_created[i]);
                }
            }

            _created.Clear();
        }

        [Test]
        public void ItShowsTheWindupBeforeCharging()
        {
            var (agent, charger) = CreateCharger(EnemyArchetype.Charger);
            var target = CreateTarget(Origin + new Vector3(0f, 1f, 4f));
            agent.Target = target;
            charger.Tick(0f);
            Assert.That(charger.IsTellVisible, Is.False);

            LogAssert.Expect(LogType.Log, "[AI] id=charger-1 charger windup target=target seconds=0.90");
            Think(agent, charger, 0f);
            charger.Tick(Tick);

            Assert.That(charger.Behavior.Phase, Is.EqualTo(ChargerPhase.WindingUp));
            Assert.That(charger.IsTellVisible, Is.True);
            Assert.That(agent.transform.position, Is.EqualTo(Origin + Vector3.up), "it does not move while winding up");
        }

        [Test]
        public void TheChargeKnocksIntoTheTargetForTheImpactDamage()
        {
            var (agent, charger) = CreateCharger(EnemyArchetype.Charger);
            var target = CreateTarget(Origin + new Vector3(0f, 1f, 4f));
            var reports = new List<DamageReport>();
            target.Damaged += (_, report) => reports.Add(report);
            agent.Target = target;
            StartCharge(agent, charger);

            LogAssert.Expect(LogType.Log, new Regex(@"^\[AI\] id=charger-1 charger hit target=target dmg=35 travelled=\d+\.\d$"));
            var hit = RunCharge(charger);

            Assert.That(hit, Is.True);
            Assert.That(target.Health.Current, Is.EqualTo(100 - ChargerSpec.DefaultImpactDamage));
            Assert.That(reports.Count, Is.EqualTo(1));
            Assert.That(reports[0].Kind, Is.EqualTo(DamageKind.Charge), "the receiver flies off from a charge report");
            Assert.That(reports[0].Direction.Value.z, Is.GreaterThan(0.99f));
            Assert.That(charger.Behavior.Phase, Is.EqualTo(ChargerPhase.Recovering));
            Assert.That(charger.Behavior.LastEnd, Is.EqualTo(ChargeEnd.Hit));
            Assert.That(charger.IsTellVisible, Is.False);
        }

        [Test]
        public void AMissedChargeStopsAtTheWallAndIsStunned()
        {
            var (agent, charger) = CreateCharger(EnemyArchetype.Charger);
            var wall = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
            wall.name = "Wall";
            wall.transform.position = Origin + new Vector3(0f, 1f, 3f);
            wall.transform.localScale = new Vector3(4f, 2f, 0.5f);
            var target = CreateTarget(Origin + new Vector3(0f, 1f, 6f));
            agent.Target = target;
            Physics.SyncTransforms();
            StartCharge(agent, charger);

            LogAssert.Expect(LogType.Log, "[AI] id=charger-1 charger blocked by=Wall");
            LogAssert.Expect(LogType.Log, new Regex(@"^\[AI\] id=charger-1 charger missed cause=obstacle travelled=\d+\.\d stun=1\.80$"));
            var hit = RunCharge(charger);

            Assert.That(hit, Is.False);
            Assert.That(charger.Behavior.Phase, Is.EqualTo(ChargerPhase.Stunned));
            Assert.That(charger.Behavior.LastEnd, Is.EqualTo(ChargeEnd.Obstacle));
            Assert.That(target.Health.Current, Is.EqualTo(100));
            Assert.That(agent.transform.position.z, Is.GreaterThan(Origin.z + 1f), "it ran toward the wall");
            Assert.That(agent.transform.position.z + 0.5f, Is.LessThanOrEqualTo(Origin.z + 2.75f), "it stops before going through the wall");
            Assert.That(charger.Travelled, Is.EqualTo(agent.transform.position.z - Origin.z).Within(1e-3f));
        }

        [Test]
        public void ADodgedTargetBehindIsNotHit()
        {
            var (agent, charger) = CreateCharger(EnemyArchetype.Charger);
            var target = CreateTarget(Origin + new Vector3(0f, 1f, 4f));
            agent.Target = target;
            StartCharge(agent, charger);

            target.transform.position = Origin + new Vector3(0f, 1f, -1.2f);
            Physics.SyncTransforms();

            for (var i = 0; i < 5; i++)
            {
                Assert.That(charger.Tick(Tick), Is.False);
            }

            Assert.That(target.Health.Current, Is.EqualTo(100));
            Assert.That(charger.Behavior.IsCharging, Is.True);
        }

        [Test]
        public void AnInterruptCancelsTheCharge()
        {
            var (agent, charger) = CreateCharger(EnemyArchetype.Charger);
            agent.Target = CreateTarget(Origin + new Vector3(0f, 1f, 4f));
            StartCharge(agent, charger);
            agent.Interrupt(1f, "flinch");

            LogAssert.Expect(LogType.Log, "[AI] id=charger-1 charger cancelled reason=interrupt");
            charger.Tick(Tick);

            Assert.That(charger.Behavior.Phase, Is.EqualTo(ChargerPhase.Ready));
            Assert.That(charger.IsTellVisible, Is.False);
            Assert.That(agent.transform.position, Is.EqualTo(Origin + Vector3.up));
        }

        [Test]
        public void AnotherArchetypeDoesNotCharge()
        {
            var (agent, charger) = CreateCharger(EnemyArchetype.Rifleman);
            agent.Target = CreateTarget(Origin + new Vector3(0f, 1f, 4f));
            charger.Tick(0f);

            var overridden = charger.Behavior.TryOverride(agent.Brain, 0f, Perceive(agent), EnemyDecision.None, out _);
            charger.Tick(Tick);

            Assert.That(overridden, Is.False);
            Assert.That(charger.IsActive, Is.False);
            Assert.That(charger.IsAttached, Is.True);
            Assert.That(charger.Behavior.Phase, Is.EqualTo(ChargerPhase.Ready));
        }

        [Test]
        public void APlaceholderTellIsMadeWithoutACollider()
        {
            var (_, charger) = CreateCharger(EnemyArchetype.Charger);

            charger.Tick(0f);

            Assert.That(charger.Tell, Is.Not.Null);
            Assert.That(charger.Tell.name, Is.EqualTo(EnemyCharger.TellPlaceholderName));
            Assert.That(charger.Tell.parent, Is.SameAs(charger.transform));
            Assert.That(charger.Tell.GetComponent<Collider>(), Is.Null);
        }

        [Test]
        public void TheChargerAndBomberValuesRoundTripThroughTheAgent()
        {
            var host = Track(GameObject.CreatePrimitive(PrimitiveType.Capsule));
            var agent = host.AddComponent<EnemyAgent>();
            var charger = new ChargerSpec(5f, 0.5f, 9f, 9f, 1.2f, 20, 1f, 0.5f);
            var bomber = new BomberSpec(2f, 1f, 1.2f, new ExplosionDamage(50, 3f, 1f), new ExplosionDamage(10, 1.5f, 0.2f));

            agent.ApplySpec(new EnemySpec(20f, 10f, 1f, 2f, 0.5f, archetype: EnemyArchetype.Bomber, charger: charger, bomber: bomber));
            var spec = agent.CreateSpec();

            Assert.That(spec.Archetype, Is.EqualTo(EnemyArchetype.Bomber));
            Assert.That(spec.Charger.TriggerDistance, Is.EqualTo(5f));
            Assert.That(spec.Charger.WindupSeconds, Is.EqualTo(0.5f));
            Assert.That(spec.Charger.ChargeSpeed, Is.EqualTo(9f));
            Assert.That(spec.Charger.HitReach, Is.EqualTo(1.2f));
            Assert.That(spec.Charger.ImpactDamage, Is.EqualTo(20));
            Assert.That(spec.Charger.RecoverSeconds, Is.EqualTo(0.5f));
            Assert.That(spec.Bomber.TriggerDistance, Is.EqualTo(2f));
            Assert.That(spec.Bomber.RunSpeedMultiplier, Is.EqualTo(1.2f));
            Assert.That(spec.Bomber.Blast.MaxDamage, Is.EqualTo(50));
            Assert.That(spec.Bomber.Blast.FullDamageRadius, Is.EqualTo(1f));
            Assert.That(spec.Bomber.DeathBlast.Radius, Is.EqualTo(1.5f));
            Assert.That(spec.Bomber.DeathBlast.FullDamageRadius, Is.EqualTo(0.2f));
        }

        private static EnemyPerception Perceive(EnemyAgent agent)
        {
            var target = agent.Target != null ? agent.Target.GroundPoint.ToNumerics() : (System.Numerics.Vector3?)null;
            return new EnemyPerception(agent.GroundPosition.ToNumerics(), target, true, false, null);
        }

        private static void Think(EnemyAgent agent, EnemyCharger charger, float deltaSeconds)
        {
            charger.Behavior.TryOverride(agent.Brain, deltaSeconds, Perceive(agent), EnemyDecision.None, out _);
        }

        private static void StartCharge(EnemyAgent agent, EnemyCharger charger)
        {
            charger.Tick(0f);
            Think(agent, charger, 0f);
            Think(agent, charger, ChargerSpec.DefaultWindupSeconds);
            Assert.That(charger.Behavior.Phase, Is.EqualTo(ChargerPhase.Charging));
        }

        private static bool RunCharge(EnemyCharger charger)
        {
            for (var i = 0; i < MaxTicks && charger.Behavior.IsCharging; i++)
            {
                if (charger.Tick(Tick))
                {
                    return true;
                }
            }

            Assert.That(charger.Behavior.IsCharging, Is.False, "the charge ended");
            return false;
        }

        private T Track<T>(T created)
            where T : Object
        {
            _created.Add(created);
            return created;
        }

        private Damageable CreateTarget(Vector3 center)
        {
            var body = Track(GameObject.CreatePrimitive(PrimitiveType.Capsule));
            body.name = "Target";
            body.transform.position = center;
            var damageable = body.AddComponent<Damageable>();
            damageable.Initialize("target", 100);
            body.AddComponent<Hitbox>().Initialize(HitZone.Body, damageable);
            Physics.SyncTransforms();
            return damageable;
        }

        private (EnemyAgent Agent, EnemyCharger Charger) CreateCharger(EnemyArchetype archetype)
        {
            var host = Track(GameObject.CreatePrimitive(PrimitiveType.Capsule));
            host.name = "Charger";
            host.transform.SetPositionAndRotation(Origin + Vector3.up, Quaternion.identity);
            var agent = host.AddComponent<EnemyAgent>();
            agent.WeaponDefinition = _rifle;
            agent.Id = "charger-1";
            var damageable = host.GetComponent<Damageable>();
            damageable.Initialize("charger-1", 100);
            host.AddComponent<Hitbox>().Initialize(HitZone.Body, damageable);
            agent.ApplySpec(new EnemySpec(10f, 6f, 1f, 2f, 0.5f, archetype: archetype));
            Assert.That(agent.TryInitialize(), Is.True);

            var charger = host.AddComponent<EnemyCharger>();
            Physics.SyncTransforms();
            return (agent, charger);
        }
    }
}
