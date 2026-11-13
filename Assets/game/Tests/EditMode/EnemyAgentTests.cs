using System.Text.RegularExpressions;
using Fps.Core;
using Fps.Editor;
using Fps.Runtime;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Fps.Tests.EditMode
{
    /// <summary>
    /// EnemyAgent の組み立て (部品の解決・設定の検査・ログ・死亡の伝達) を確かめる。
    /// NavMesh 上を歩く部分は Play で確かめる (手順は PR 本文)。
    /// </summary>
    public sealed class EnemyAgentTests
    {
        private GameObject _host;
        private EnemyAgent _agent;
        private WeaponDefinition _rifle;

        [SetUp]
        public void SetUp()
        {
            _rifle = ScriptableObject.CreateInstance<WeaponDefinition>();
            _rifle.name = "Rifle";
            WeaponAssetBuilder.Configure(_rifle, WeaponAssetBuilder.RifleSpec());

            _host = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            _host.name = "Enemy";
            _agent = _host.AddComponent<EnemyAgent>();
            _agent.WeaponDefinition = _rifle;
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_host);
            Object.DestroyImmediate(_rifle);
        }

        [Test]
        public void RequiresNavMeshAgentAndDamageable()
        {
            Assert.That(_host.GetComponent<NavMeshAgent>(), Is.Not.Null);
            Assert.That(_host.GetComponent<Damageable>(), Is.Not.Null);
        }

        [Test]
        public void InitializeBuildsTheBrainFromTheDefaults()
        {
            Assert.That(_agent.TryInitialize(), Is.True);

            Assert.That(_agent.IsInitialized, Is.True);
            Assert.That(_agent.Brain.Spec.SightRange, Is.EqualTo(EnemyAgent.DefaultSightRange));
            Assert.That(_agent.Brain.Spec.AttackRange, Is.EqualTo(EnemyAgent.DefaultAttackRange));
            Assert.That(_agent.Brain.Spec.ReactionSeconds, Is.EqualTo(EnemyAgent.DefaultReactionSeconds));
            Assert.That(_agent.State, Is.EqualTo(EnemyState.Idle));
            Assert.That(_agent.Weapon, Is.Not.Null);
            Assert.That(_agent.TryInitialize(), Is.True, "a second call is a no-op");
        }

        [Test]
        public void InvalidSettingsAreReportedAndRejected()
        {
            var serialized = new SerializedObject(_agent);
            serialized.FindProperty("_attackRange").floatValue = EnemyAgent.DefaultSightRange + 1f;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            LogAssert.Expect(LogType.Error, new Regex(@"^\[AI\] id=.* の設定が不正です"));
            Assert.That(_agent.TryInitialize(), Is.False);
            Assert.That(_agent.IsInitialized, Is.False);
        }

        [Test]
        public void WeaponIsBuiltFromTheDefinition()
        {
            Assert.That(_agent.TryInitialize(), Is.True);

            var expected = WeaponAssetBuilder.RifleSpec();
            var spec = _agent.Weapon.Spec;
            Assert.That(spec.Id, Is.EqualTo(expected.Id));
            Assert.That(spec.MagazineSize, Is.EqualTo(expected.MagazineSize));
            Assert.That(spec.ReserveAmmo, Is.EqualTo(expected.ReserveAmmo));
            Assert.That(spec.FireInterval, Is.EqualTo(expected.FireInterval));
            Assert.That(spec.ReloadSeconds, Is.EqualTo(expected.ReloadSeconds));
            Assert.That(spec.Damage, Is.EqualTo(expected.Damage));
            Assert.That(spec.Range, Is.EqualTo(expected.Range));
            Assert.That(spec.Falloff.StartDistance, Is.EqualTo(expected.Falloff.StartDistance));
            Assert.That(spec.Falloff.EndDistance, Is.EqualTo(expected.Falloff.EndDistance));
            Assert.That(spec.Falloff.MinMultiplier, Is.EqualTo(expected.Falloff.MinMultiplier));
            Assert.That(_agent.Weapon.AmmoInMagazine, Is.EqualTo(expected.MagazineSize));
        }

        [Test]
        public void MissingWeaponDefinitionIsReportedAndRejected()
        {
            _agent.Id = "enemy-1";
            _agent.WeaponDefinition = null;

            LogAssert.Expect(LogType.Error, new Regex(@"^\[AI\] id=enemy-1 に WeaponDefinition が設定されていません"));
            Assert.That(_agent.TryInitialize(), Is.False);
            Assert.That(_agent.IsInitialized, Is.False);
            Assert.That(_agent.Weapon, Is.Null);
        }

        [Test]
        public void InvalidWeaponDefinitionIsReportedAndRejected()
        {
            _agent.Id = "enemy-1";
            _rifle.MagazineSize = 0;

            LogAssert.Expect(LogType.Error, new Regex(@"^\[AI\] id=enemy-1 の武器定義が不正です: weapon 'Rifle'"));
            Assert.That(_agent.TryInitialize(), Is.False);
            Assert.That(_agent.IsInitialized, Is.False);
            Assert.That(_agent.Weapon, Is.Null);
        }

        [Test]
        public void IdFallsBackToTheDamageable()
        {
            _host.GetComponent<Damageable>().Initialize("grunt", 50);

            Assert.That(_agent.Id, Is.EqualTo("grunt"));

            _agent.Id = "enemy-7";
            Assert.That(_agent.Id, Is.EqualTo("enemy-7"));
        }

        [Test]
        public void StateChangesAreLoggedWithTheId()
        {
            _agent.Id = "enemy-1";
            Assert.That(_agent.TryInitialize(), Is.True);

            // NavMesh が無いので巡回地点はそのまま使われる (警告が出る)。
            LogAssert.Expect(LogType.Log, "[AI] id=enemy-1 state=Patrol prev=Idle");
            _agent.SetPatrolRoute(new[] { new Vector3(0f, 0f, 5f), new Vector3(5f, 0f, 5f) });

            Assert.That(_agent.State, Is.EqualTo(EnemyState.Patrol));
            Assert.That(_agent.PatrolRoute.Count, Is.EqualTo(2));
        }

        [Test]
        public void PatrolRouteSetBeforeInitializeIsKept()
        {
            _agent.SetPatrolRoute(new[] { new Vector3(1f, 0f, 2f) });

            Assert.That(_agent.PatrolRoute.Count, Is.EqualTo(1));
            Assert.That(_agent.PatrolRoute[0], Is.EqualTo(new Vector3(1f, 0f, 2f)));
            Assert.That(_agent.State, Is.EqualTo(EnemyState.Idle), "the brain receives the route on Start");
        }

        [Test]
        public void DeathKillsTheBrain()
        {
            _agent.Id = "enemy-1";
            var damageable = _host.GetComponent<Damageable>();
            damageable.Initialize("enemy-1", 10);
            Assert.That(_agent.TryInitialize(), Is.True);

            LogAssert.Expect(LogType.Log, "[Combat] target died id=enemy-1");
            LogAssert.Expect(LogType.Log, "[AI] id=enemy-1 state=Dead prev=Idle");
            damageable.TakeDamage(10);

            Assert.That(_agent.State, Is.EqualTo(EnemyState.Dead));
            Assert.That(_host.GetComponent<NavMeshAgent>().enabled, Is.False);
        }

        [Test]
        public void ShotFromBehindWhilePatrollingSearchesTowardsTheShooter()
        {
            var damageable = PrepareShootableEnemy(Damageable.DefaultMaxHealth);
            _agent.SetPatrolRoute(new[] { new Vector3(0f, 0f, 5f), new Vector3(5f, 0f, 5f) });
            Assert.That(_agent.State, Is.EqualTo(EnemyState.Patrol));

            // 敵は +z を向いて巡回しているので、-z 側は視界の外 (目標もいない)。
            var shooter = new GameObject("Shooter");
            try
            {
                shooter.transform.position = new Vector3(0f, 0f, -10f);

                var result = Hitscan.Fire(new Vector3(0f, 1f, -10f), Vector3.forward, 50f, 10, DamageFalloff.None, shooter.transform);
                Assert.That(result.Damage, Is.EqualTo(10));
                Assert.That(damageable.Health.Current, Is.EqualTo(Damageable.DefaultMaxHealth - 10));

                var perception = _agent.Perceive();
                Assert.That(perception.HeardNoise, Is.True);
                Assert.That(perception.NoisePosition, Is.EqualTo(shooter.transform.position.ToNumerics()));

                LogAssert.Expect(LogType.Log, "[AI] id=enemy-1 state=Search prev=Patrol");
                var decision = _agent.Brain.Tick(0.1f, perception);

                Assert.That(_agent.State, Is.EqualTo(EnemyState.Search));
                Assert.That(decision.MoveTo, Is.EqualTo(shooter.transform.position.ToNumerics()));
                Assert.That(decision.MoveTo.Value.Z, Is.LessThan(_host.transform.position.z), "heads behind itself, towards the shooter");

                Assert.That(_agent.Perceive().HeardNoise, Is.False, "the hit is used for one perception only");
            }
            finally
            {
                Object.DestroyImmediate(shooter);
            }
        }

        [Test]
        public void DamageWithoutAShooterPositionIsNotHeard()
        {
            var damageable = PrepareShootableEnemy(Damageable.DefaultMaxHealth);

            damageable.TakeDamage(10);

            Assert.That(_agent.Perceive().HeardNoise, Is.False);
        }

        [Test]
        public void DeadEnemyDoesNotReactToBeingShot()
        {
            var damageable = PrepareShootableEnemy(10);
            _agent.SetPatrolRoute(new[] { new Vector3(0f, 0f, 5f) });

            LogAssert.Expect(LogType.Log, "[Combat] target died id=enemy-1");
            LogAssert.Expect(LogType.Log, "[AI] id=enemy-1 state=Dead prev=Patrol");
            damageable.TakeDamage(10, new Vector3(0f, 0f, -10f));
            damageable.TakeDamage(5, new Vector3(0f, 0f, -10f));

            Assert.That(_agent.State, Is.EqualTo(EnemyState.Dead));
            Assert.That(_agent.Perceive().HeardNoise, Is.False);

            var decision = _agent.Brain.Tick(0.1f, _agent.Perceive());
            Assert.That(decision.MoveTo.HasValue, Is.False);
            Assert.That(decision.LookAt.HasValue, Is.False);
            Assert.That(decision.Fire, Is.False);
            Assert.That(_agent.State, Is.EqualTo(EnemyState.Dead));
        }

        [Test]
        public void PerceiveRequiresInitialization()
        {
            Assert.Throws<System.InvalidOperationException>(() => _agent.Perceive());
        }

        [Test]
        public void GroundAndEyePositionsUseTheBaseOffset()
        {
            _host.GetComponent<NavMeshAgent>().baseOffset = 1f;
            _host.transform.position = new Vector3(2f, 1f, 3f);
            Assert.That(_agent.TryInitialize(), Is.True);

            Assert.That(_agent.GroundPosition, Is.EqualTo(new Vector3(2f, 0f, 3f)));
            Assert.That(_agent.EyePosition, Is.EqualTo(new Vector3(2f, EnemyAgent.DefaultEyeHeight, 3f)));
        }

        [Test]
        public void FormatsStateLog()
        {
            Assert.That(
                EnemyAgent.FormatStateLog("enemy-2", EnemyState.Chase, EnemyState.Search),
                Is.EqualTo("[AI] id=enemy-2 state=Search prev=Chase"));
        }

        // 原点に立つ (足元 y=0) 撃てる敵を用意して初期化する。胴の Hitbox を付けて Raycast が当たるようにする。
        private Damageable PrepareShootableEnemy(int maxHealth)
        {
            _agent.Id = "enemy-1";
            _host.transform.SetPositionAndRotation(new Vector3(0f, 1f, 0f), Quaternion.identity);
            _host.GetComponent<NavMeshAgent>().baseOffset = 1f;
            var damageable = _host.GetComponent<Damageable>();
            damageable.Initialize("enemy-1", maxHealth);
            _host.AddComponent<Hitbox>().Initialize(HitZone.Body, damageable);
            Physics.SyncTransforms();
            Assert.That(_agent.TryInitialize(), Is.True);
            return damageable;
        }
    }
}
