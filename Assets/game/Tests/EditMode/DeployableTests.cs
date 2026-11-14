using System.Collections.Generic;
using Fps.Core;
using Fps.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Fps.Tests.EditMode
{
    // Edit Mode でも Physics の問い合わせは効くので、作った後に SyncTransforms してから撃つ。
    public sealed class DeployableTests
    {
        private const string Kind = "shield";
        private const int MaxHealth = 50;

        private static readonly Vector3 _shooterPosition = new Vector3(0f, 0.7f, 5f);

        private readonly List<Object> _created = new List<Object>();
        private ShieldDeployDefinition _definition;
        private DeploySpec _spec;
        private GameObject _owner;

        [SetUp]
        public void SetUp()
        {
            _definition = ScriptableObject.CreateInstance<ShieldDeployDefinition>();
            _definition.Id = Kind;
            _definition.MaxHealth = MaxHealth;
            _created.Add(_definition);
            _spec = _definition.ToDeploySpec();

            _owner = new GameObject("Owner");
            _owner.transform.position = new Vector3(0f, 1f, -2f);
            _created.Add(_owner);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var deployable in Object.FindObjectsByType<Deployable>(FindObjectsInactive.Include))
            {
                Object.DestroyImmediate(deployable.gameObject);
            }

            foreach (var created in _created)
            {
                if (created != null)
                {
                    Object.DestroyImmediate(created);
                }
            }

            _created.Clear();
        }

        [Test]
        public void TheShieldIsInitializedWithItsOwnerAndHealth()
        {
            var shield = PlaceShield(null);

            Assert.That(shield.Owner, Is.SameAs(_owner));
            Assert.That(shield.KindId, Is.EqualTo(Kind));
            Assert.That(shield.Serial, Is.EqualTo(1));
            Assert.That(shield.Damageable.Health.Current, Is.EqualTo(MaxHealth));
            Assert.That(shield.Damageable.ReportsKills, Is.False);
            Assert.That(shield.GetComponent<UnityEngine.AI.NavMeshObstacle>(), Is.Not.Null, "enemies walk around it");
            Assert.That(shield.GetComponentInChildren<CoverPoint>(), Is.Null, "enemies do not take cover behind it");
            Assert.Throws<System.InvalidOperationException>(() => shield.Initialize(_owner, "Owner", Kind, 2, null, MaxHealth));
        }

        [Test]
        public void EnemyBulletsStopAtTheShieldAndHurtIt()
        {
            var shield = PlaceShield(null);
            var shooter = CreateShooter("Shooter", null);
            var victim = CreateVictim(new Vector3(0f, 0.7f, -1f));
            Physics.SyncTransforms();

            LogAssert.Expect(LogType.Log, DeployLog.Blocked("Owner", Kind, 1, 10, MaxHealth - 10, "Shooter"));
            var result = Fire(shooter, 10);

            Assert.That(result.Hit, Is.True);
            Assert.That(Deployable.Of(result.RaycastHit.collider), Is.SameAs(shield));
            Assert.That(result.Hitbox, Is.Null);
            Assert.That(result.Damage, Is.Zero);
            Assert.That(shield.Damageable.Health.Current, Is.EqualTo(MaxHealth - 10));
            Assert.That(victim.Health.Current, Is.EqualTo(victim.MaxHealth), "nothing gets through");
        }

        [Test]
        public void TheOwnersBulletsGoThrough()
        {
            var shield = PlaceShield(null);
            var victim = CreateVictim(new Vector3(0f, 0.7f, 3f));
            Physics.SyncTransforms();

            var result = Hitscan.Fire(_owner.transform.position, Vector3.forward, 20f, 10, DamageFalloff.None, _owner.transform);

            Assert.That(result.Hitbox, Is.Not.Null);
            Assert.That(result.Hitbox.Owner, Is.SameAs(victim));
            Assert.That(shield.Damageable.Health.Current, Is.EqualTo(MaxHealth));
            Assert.That(victim.Health.Current, Is.EqualTo(victim.MaxHealth - 10));
        }

        [Test]
        public void AlliesBulletsGoThroughAndEnemiesBulletsDoNot()
        {
            _owner.AddComponent<SideMember>().Side = Side.Alpha;
            var shield = PlaceShield(Side.Alpha);
            var ally = CreateShooter("Ally", Side.Alpha);
            var enemy = CreateShooter("Enemy", Side.Bravo);
            CreateVictim(new Vector3(0f, 0.7f, -1f));
            Physics.SyncTransforms();

            Assert.That(shield.LetsThrough(ally), Is.True);
            Assert.That(shield.LetsThrough(enemy), Is.False);
            Assert.That(shield.LetsThrough(null), Is.False);

            var allyShot = Fire(ally, 10);
            Assert.That(allyShot.Hitbox, Is.Not.Null, "the ally's bullet reaches whoever is behind");
            Assert.That(shield.Damageable.Health.Current, Is.EqualTo(MaxHealth));

            LogAssert.Expect(LogType.Log, DeployLog.Blocked("Owner", Kind, 1, 10, MaxHealth - 10, "Enemy"));
            var enemyShot = Fire(enemy, 10);
            Assert.That(enemyShot.Hitbox, Is.Null);
            Assert.That(shield.Damageable.Health.Current, Is.EqualTo(MaxHealth - 10));
        }

        [Test]
        public void TheShieldBreaksAndIsRemovedWithoutAKill()
        {
            var shield = PlaceShield(null);
            var owner = DeployOwner.GetOrAdd(_owner);
            owner.Add(Kind, _spec, shield);
            var shooter = CreateShooter("Shooter", null);
            Physics.SyncTransforms();

            var kills = 0;
            void CountKill(KillFeedEntry _) => kills++;
            CombatEvents.Killed += CountKill;
            DeployRemoval? removedWith = null;
            shield.Removed += (_, reason) => removedWith = reason;
            try
            {
                LogAssert.Expect(LogType.Log, DeployLog.Blocked("Owner", Kind, 1, 30, 20, "Shooter"));
                Fire(shooter, 30);
                LogAssert.Expect(LogType.Log, DeployLog.Blocked("Owner", Kind, 1, 30, 0, "Shooter"));
                LogAssert.Expect(LogType.Log, DeployLog.Removed("Owner", Kind, 1, DeployRemoval.Destroyed));
                Fire(shooter, 30);
            }
            finally
            {
                CombatEvents.Killed -= CountKill;
            }

            Assert.That(removedWith, Is.EqualTo(DeployRemoval.Destroyed));
            Assert.That(shield == null, Is.True, "the broken shield is destroyed");
            Assert.That(owner.ActiveCount(Kind), Is.Zero);
            Assert.That(kills, Is.Zero, "a broken shield is not a kill");
        }

        [Test]
        public void SightLinesIgnoreOnlyTheViewersOwnShields()
        {
            PlaceShield(null);
            var target = CreateVictim(new Vector3(0f, 0.7f, -1f));
            var stranger = CreateShooter("Stranger", null);
            Physics.SyncTransforms();

            var fromOwner = new Vector3(0f, 0.7f, 2f);
            _owner.transform.position = fromOwner;
            Assert.That(Hitscan.HasLineOfSight(fromOwner, target, 20f, _owner.transform), Is.True);
            Assert.That(Hitscan.HasLineOfSight(_shooterPosition, target, 20f, stranger.transform), Is.False);
        }

        [Test]
        public void NonDeployableCollidersAreLeftAlone()
        {
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _created.Add(wall);

            Assert.That(Deployable.Of(wall.GetComponent<Collider>()), Is.Null);
            Assert.That(Deployable.LetsThrough(wall.GetComponent<Collider>(), _owner), Is.False);
            Assert.That(
                Deployable.TryStopBullet(wall.GetComponent<Collider>(), 10, 1f, DamageFalloff.None, null, null, _owner),
                Is.False);
            Assert.Throws<System.ArgumentNullException>(() => Deployable.Of(null));
        }

        private Deployable PlaceShield(Side? side)
        {
            var shield = _definition.CreateDeployed(new Pose(Vector3.zero, Quaternion.identity), _spec);
            shield.Initialize(_owner, "Owner", Kind, 1, side, _spec.MaxHealth);
            return shield;
        }

        private GameObject CreateShooter(string name, Side? side)
        {
            var shooter = new GameObject(name);
            shooter.transform.SetPositionAndRotation(_shooterPosition, Quaternion.Euler(0f, 180f, 0f));
            if (side.HasValue)
            {
                shooter.AddComponent<SideMember>().Side = side.Value;
            }

            _created.Add(shooter);
            return shooter;
        }

        private Damageable CreateVictim(Vector3 position)
        {
            var victim = GameObject.CreatePrimitive(PrimitiveType.Cube);
            victim.name = "Victim";
            victim.transform.position = position;
            var damageable = victim.AddComponent<Damageable>();
            victim.AddComponent<Hitbox>().Initialize(HitZone.Body, damageable);
            _created.Add(victim);
            return damageable;
        }

        private static HitscanResult Fire(GameObject shooter, int damage)
        {
            return Hitscan.Fire(shooter.transform.position, Vector3.back, 20f, damage, DamageFalloff.None, shooter.transform);
        }
    }
}
