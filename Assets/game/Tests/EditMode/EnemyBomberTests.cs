using System.Collections.Generic;
using Fps.Core;
using Fps.Editor;
using Fps.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Fps.Tests.EditMode
{
    // Edit Mode では EnemyAgent の Update が走らないので、判断 (BomberEnemyBehavior) は知覚を作って直接進め、爆発させる EnemyBomber は Tick を呼ぶ。
    public sealed class EnemyBomberTests
    {
        // 開いているシーンの物に当たらないよう、離れた所に置く。
        private static readonly Vector3 Origin = new Vector3(700f, 0f, 700f);

        // 相手の体の手前の面が爆発の中心から 1.5m (本来の爆発の減らさない半径) になる位置。
        private static readonly Vector3 TargetCenter = Origin + new Vector3(0f, 1f, 2f);

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
        public void ItRunsWithoutSprintingAtItsOwnPace()
        {
            var (agent, bomber) = CreateBomber(EnemyArchetype.Bomber);

            bomber.Tick(0f);

            Assert.That(bomber.IsActive, Is.True);
            Assert.That(bomber.IsAttached, Is.True);
            Assert.That(agent.Brain.Behaviors, Has.Member(bomber.Behavior));
            Assert.That(agent.SprintAllowed, Is.False);
            Assert.That(agent.GaitSpeedMultiplier, Is.EqualTo(BomberSpec.DefaultRunSpeedMultiplier));
            Assert.That(bomber.IsSignalVisible, Is.False);
            Assert.That(bomber.Signal.GetComponent<Collider>(), Is.Null);
        }

        [Test]
        public void CloseByItSignalsAndThenExplodes()
        {
            var (agent, bomber) = CreateBomber(EnemyArchetype.Bomber);
            var target = CreateTarget();
            agent.Target = target;
            bomber.Tick(0f);

            LogAssert.Expect(LogType.Log, "[AI] id=bomber-1 bomber fuse target=target seconds=1.50");
            Think(agent, bomber, 0f);
            bomber.Tick(0.01f);
            Assert.That(bomber.Behavior.Phase, Is.EqualTo(BomberPhase.Fusing));
            Assert.That(bomber.IsSignalVisible, Is.True);
            Assert.That(target.Health.Current, Is.EqualTo(100), "nothing explodes during the signal");

            Think(agent, bomber, BomberSpec.DefaultFuseSeconds);
            LogAssert.Expect(LogType.Log, "[AI] id=bomber-1 bomber hit target=target dmg=80 distance=1.5");
            LogAssert.Expect(LogType.Log, "[AI] id=bomber-1 bomber exploded cause=fuse radius=4.5 damaged=1 killed=0");
            var exploded = bomber.Tick(0f);

            Assert.That(exploded, Is.True);
            Assert.That(target.Health.Current, Is.EqualTo(100 - BomberSpec.DefaultBlastDamage));
            Assert.That(agent.GetComponent<Damageable>().IsDead, Is.True, "it goes up with its own blast");
            Assert.That(bomber.Behavior.Phase, Is.EqualTo(BomberPhase.Exploded));
            Assert.That(bomber.IsSignalVisible, Is.False);
        }

        [Test]
        public void KilledBeforeTheSignalItExplodesSmallWhereItFalls()
        {
            var (agent, bomber) = CreateBomber(EnemyArchetype.Bomber);
            var target = CreateTarget();
            agent.Target = target;
            bomber.Tick(0f);

            LogAssert.Expect(LogType.Log, "[AI] id=bomber-1 bomber exploded cause=killed radius=2.5 damaged=1 killed=0");
            agent.GetComponent<Damageable>().TakeDamage(1000);

            // 小さな爆発は 0.5m から 2.5m まで減るので、1.5m では半分 (25 の半分を切り上げ)。
            Assert.That(target.Health.Current, Is.EqualTo(100 - 13));
            Assert.That(bomber.Behavior.Phase, Is.EqualTo(BomberPhase.Exploded));
        }

        [Test]
        public void KilledDuringTheSignalTheFullBlastGoesOff()
        {
            var (agent, bomber) = CreateBomber(EnemyArchetype.Bomber);
            var target = CreateTarget();
            agent.Target = target;
            bomber.Tick(0f);
            Think(agent, bomber, 0f);

            LogAssert.Expect(LogType.Log, "[AI] id=bomber-1 bomber exploded cause=killed-armed radius=4.5 damaged=1 killed=0");
            agent.GetComponent<Damageable>().TakeDamage(1000);

            Assert.That(target.Health.Current, Is.EqualTo(100 - BomberSpec.DefaultBlastDamage));
        }

        [Test]
        public void ItExplodesOnlyOnce()
        {
            var (agent, bomber) = CreateBomber(EnemyArchetype.Bomber);
            var target = CreateTarget();
            agent.Target = target;
            bomber.Tick(0f);
            Think(agent, bomber, 0f);
            Think(agent, bomber, BomberSpec.DefaultFuseSeconds);
            bomber.Tick(0f);

            Assert.That(bomber.Tick(1f), Is.False);
            Assert.That(target.Health.Current, Is.EqualTo(100 - BomberSpec.DefaultBlastDamage));
        }

        [Test]
        public void TheFuseKeepsBurningWhileItIsInterrupted()
        {
            var (agent, bomber) = CreateBomber(EnemyArchetype.Bomber);
            agent.Target = CreateTarget();
            bomber.Tick(0f);
            Think(agent, bomber, 0f);
            agent.Interrupt(5f, "flinch");

            Assert.That(bomber.Tick(BomberSpec.DefaultFuseSeconds), Is.True);
            Assert.That(bomber.Behavior.Phase, Is.EqualTo(BomberPhase.Exploded));
        }

        [Test]
        public void AnotherArchetypeDoesNotExplodeWhenKilled()
        {
            var (agent, bomber) = CreateBomber(EnemyArchetype.Rifleman);
            var target = CreateTarget();
            agent.Target = target;
            bomber.Tick(0f);

            agent.GetComponent<Damageable>().TakeDamage(1000);

            Assert.That(bomber.IsActive, Is.False);
            Assert.That(target.Health.Current, Is.EqualTo(100));
            Assert.That(agent.SprintAllowed, Is.True);
            Assert.That(agent.GaitSpeedMultiplier, Is.EqualTo(1f));
        }

        [Test]
        public void ARevivedBomberStartsOverWithoutItsFuse()
        {
            var (agent, bomber) = CreateBomber(EnemyArchetype.Bomber);
            agent.Target = CreateTarget();
            bomber.Tick(0f);
            Think(agent, bomber, 0f);
            agent.GetComponent<Damageable>().TakeDamage(1000);
            Assert.That(bomber.Behavior.Phase, Is.EqualTo(BomberPhase.Exploded));

            // EnemyAgent.Respawn は NavMesh に載せ直すので、Edit Mode では判断だけを生き返らせる (Respawn の Restart も、同じ Dead から抜ける状態の通知で初めからにする)。
            Assert.That(agent.Brain.Revive(), Is.True);

            Assert.That(bomber.Behavior.Phase, Is.EqualTo(BomberPhase.Approaching));
            Assert.That(bomber.Behavior.FuseElapsed, Is.EqualTo(0f));
        }

        private static void Think(EnemyAgent agent, EnemyBomber bomber, float deltaSeconds)
        {
            var target = agent.Target != null ? agent.Target.GroundPoint.ToNumerics() : (System.Numerics.Vector3?)null;
            var perception = new EnemyPerception(agent.GroundPosition.ToNumerics(), target, true, false, null);
            bomber.Behavior.TryOverride(agent.Brain, deltaSeconds, perception, EnemyDecision.None, out _);
        }

        private T Track<T>(T created)
            where T : Object
        {
            _created.Add(created);
            return created;
        }

        private Damageable CreateTarget()
        {
            var body = Track(GameObject.CreatePrimitive(PrimitiveType.Capsule));
            body.name = "Target";
            body.transform.position = TargetCenter;
            var damageable = body.AddComponent<Damageable>();
            damageable.Initialize("target", 100);
            body.AddComponent<Hitbox>().Initialize(HitZone.Body, damageable);
            Physics.SyncTransforms();
            return damageable;
        }

        private (EnemyAgent Agent, EnemyBomber Bomber) CreateBomber(EnemyArchetype archetype)
        {
            var host = Track(GameObject.CreatePrimitive(PrimitiveType.Capsule));
            host.name = "Bomber";
            host.transform.SetPositionAndRotation(Origin + Vector3.up, Quaternion.identity);
            var agent = host.AddComponent<EnemyAgent>();
            agent.WeaponDefinition = _rifle;
            agent.Id = "bomber-1";
            var damageable = host.GetComponent<Damageable>();
            damageable.Initialize("bomber-1", 100);
            host.AddComponent<Hitbox>().Initialize(HitZone.Body, damageable);
            agent.ApplySpec(new EnemySpec(10f, 6f, 1f, 2f, 0.5f, archetype: archetype));
            Assert.That(agent.TryInitialize(), Is.True);

            var bomber = host.AddComponent<EnemyBomber>();
            Physics.SyncTransforms();
            return (agent, bomber);
        }
    }
}
