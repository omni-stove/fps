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
    // Edit Mode でも Physics の問い合わせはコライダーを見つけるので、置いたら SyncTransforms してから撃つ。Update は走らないので Tick を直接呼ぶ。
    public sealed class EnemyShieldTests
    {
        // 開いているシーンの物に当たらないよう、離れた所に置く。
        private static readonly Vector3 Origin = new Vector3(500f, 0f, 500f);

        private readonly List<Object> _created = new List<Object>();
        private WeaponDefinition _rifle;

        [SetUp]
        public void SetUp()
        {
            _rifle = ScriptableObject.CreateInstance<WeaponDefinition>();
            _rifle.name = "Rifle";
            WeaponAssetBuilder.Configure(_rifle, WeaponAssetBuilder.RifleSpec());
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
        public void ABulletFromTheFrontIsStoppedByTheShield()
        {
            var (_, damageable, _) = CreateShieldedBody();

            LogAssert.Expect(LogType.Log, "[AI] id=Shield shield blocked dmg=25 by=unknown");
            var result = FireFromTheFront(25);

            Assert.That(result.Hit, Is.True);
            Assert.That(result.Hitbox, Is.Null);
            Assert.That(result.Damage, Is.Zero);
            Assert.That(damageable.Health.Current, Is.EqualTo(100));
        }

        [Test]
        public void ABulletFromBehindHitsTheBody()
        {
            var (_, damageable, _) = CreateShieldedBody();

            var result = Hitscan.Fire(Origin + new Vector3(0f, 1f, -5f), Vector3.forward, 20f, 25, DamageFalloff.None, null);

            Assert.That(result.Hitbox, Is.Not.Null);
            Assert.That(result.Damage, Is.EqualTo(25));
            Assert.That(damageable.Health.Current, Is.EqualTo(75));
        }

        [Test]
        public void ALoweredShieldLetsBulletsThroughAndRaisingItRestoresIt()
        {
            var (_, damageable, plate) = CreateShieldedBody();
            var raisedPosition = plate.transform.localPosition;

            plate.SetRaised(false);
            Physics.SyncTransforms();

            Assert.That(plate.IsRaised, Is.False);
            Assert.That(plate.Collider.enabled, Is.False);
            Assert.That(plate.transform.localPosition, Is.Not.EqualTo(raisedPosition));
            Assert.That(FireFromTheFront(25).Damage, Is.EqualTo(25));
            Assert.That(damageable.Health.Current, Is.EqualTo(75));

            plate.SetRaised(true);
            Physics.SyncTransforms();

            Assert.That(plate.Collider.enabled, Is.True);
            Assert.That(plate.transform.localPosition, Is.EqualTo(raisedPosition));
            LogAssert.Expect(LogType.Log, "[AI] id=Shield shield blocked dmg=25 by=unknown");
            Assert.That(FireFromTheFront(25).Damage, Is.Zero);
        }

        [Test]
        public void AMeleeStrikeReachesTheBodyThroughTheShield()
        {
            var (_, damageable, _) = CreateShieldedBody();
            var attacker = Track(new GameObject("Attacker"));
            attacker.transform.SetPositionAndRotation(Origin + new Vector3(0f, 1f, 1.6f), Quaternion.LookRotation(Vector3.back));
            Physics.SyncTransforms();

            var found = MeleeStrike.TryFind(
                attacker.transform, attacker.transform.position, attacker.transform.forward, MeleeSpec.Default, Physics.DefaultRaycastLayers, out var hit);

            Assert.That(found, Is.True);
            Assert.That(hit.Target, Is.SameAs(damageable));
        }

        [Test]
        public void TheShieldEnemyRaisesItsShieldAndWalksSlowly()
        {
            var (agent, shield) = CreateShieldEnemy(EnemyArchetype.Shield);

            shield.Tick(0.1f);

            Assert.That(shield.IsActive, Is.True);
            Assert.That(shield.IsAttached, Is.True);
            Assert.That(agent.Brain.Behaviors, Has.Member(shield.Behavior));
            Assert.That(shield.Plate, Is.Not.Null, "a placeholder shield is made when none is wired");
            Assert.That(shield.Plate.transform.parent, Is.SameAs(agent.transform));
            Assert.That(shield.Plate.IsRaised, Is.True);
            Assert.That(agent.SprintAllowed, Is.False);
            Assert.That(agent.GaitSpeedMultiplier, Is.EqualTo(ShieldSpec.Default.ApproachSpeedMultiplier));
        }

        [Test]
        public void TheShieldEnemyUsesThePlatePrefabWhenOneIsSet()
        {
            var (agent, shield) = CreateShieldEnemy(EnemyArchetype.Shield);
            var prefab = CreatePlateTemplate("CustomPlate");
            shield.PlatePrefab = prefab;

            shield.Tick(0.1f);
            Physics.SyncTransforms();

            Assert.That(shield.Plate, Is.Not.Null);
            Assert.That(shield.Plate.gameObject, Is.Not.SameAs(prefab));
            Assert.That(shield.Plate.name, Is.EqualTo("CustomPlate"));
            Assert.That(shield.Plate.transform.parent, Is.SameAs(agent.transform));
            Assert.That(shield.Plate.transform.localPosition, Is.EqualTo(prefab.transform.localPosition));
            Assert.That(shield.Plate.transform.localScale, Is.EqualTo(prefab.transform.localScale));
            Assert.That(agent.transform.Find(BulletShield.PlaceholderName), Is.Null, "no placeholder is made beside the prefab");
            LogAssert.Expect(LogType.Log, "[AI] id=shield-1 shield blocked dmg=25 by=unknown");
            Assert.That(FireFromTheFront(25).Damage, Is.Zero);
        }

        [Test]
        public void APlatePrefabWithoutARootColliderFallsBackToThePlaceholder()
        {
            var (agent, shield) = CreateShieldEnemy(EnemyArchetype.Shield);
            var prefab = Track(new GameObject("NoCollider"));
            shield.PlatePrefab = prefab;

            LogAssert.Expect(LogType.Error, new Regex("shield prefab 'NoCollider' has no collider on its root.*using the placeholder shield", RegexOptions.Singleline));
            shield.Tick(0.1f);

            Assert.That(shield.Plate, Is.Not.Null);
            Assert.That(shield.Plate.name, Is.EqualTo(BulletShield.PlaceholderName));
            Assert.That(shield.Plate.transform.parent, Is.SameAs(agent.transform));
        }

        [Test]
        public void CreatingFromAPrefabKeepsItsOwnBulletShield()
        {
            var owner = Track(new GameObject("Owner"));
            var prefab = CreatePlateTemplate("Plate");
            prefab.AddComponent<BulletShield>();

            var plate = BulletShield.Create(owner.transform, prefab);

            Assert.That(plate.gameObject, Is.Not.SameAs(prefab));
            Assert.That(plate.GetComponents<BulletShield>(), Has.Length.EqualTo(1));
            Assert.That(plate.Collider, Is.Not.Null);
            Assert.That(BulletShield.Create(owner.transform, null).name, Is.EqualTo(BulletShield.PlaceholderName));
            Assert.Throws<System.ArgumentNullException>(() => BulletShield.Create(null, prefab));
            Assert.Throws<System.ArgumentException>(() => BulletShield.Create(owner.transform, Track(new GameObject("Bare"))));
        }

        [Test]
        public void AMeleeHitLowersTheShieldUntilItIsRaisedAgain()
        {
            var (agent, shield) = CreateShieldEnemy(EnemyArchetype.Shield);
            shield.Tick(0.1f);
            var damageable = agent.GetComponent<Damageable>();

            LogAssert.Expect(LogType.Log, "[AI] id=shield-1 shield lowered cause=melee seconds=1.50");
            damageable.TakeDamage(10, null, null, DamageKind.Melee);
            Physics.SyncTransforms();

            Assert.That(shield.Guard.IsRaised, Is.False);
            Assert.That(shield.Plate.Collider.enabled, Is.False);
            Assert.That(agent.GaitSpeedMultiplier, Is.EqualTo(1f));
            Assert.That(FireFromTheFront(25).Damage, Is.EqualTo(25), "the body is open while the shield is down");

            LogAssert.Expect(LogType.Log, "[AI] id=shield-1 shield raised");
            shield.Tick(ShieldSpec.DefaultMeleeLowerSeconds);
            Physics.SyncTransforms();

            Assert.That(shield.Plate.IsRaised, Is.True);
            LogAssert.Expect(LogType.Log, "[AI] id=shield-1 shield blocked dmg=25 by=unknown");
            Assert.That(FireFromTheFront(25).Damage, Is.Zero);
        }

        [Test]
        public void AnExplosionLowersTheShieldForLonger()
        {
            var (agent, shield) = CreateShieldEnemy(EnemyArchetype.Shield);
            shield.Tick(0.1f);

            LogAssert.Expect(LogType.Log, "[AI] id=shield-1 shield lowered cause=explosion seconds=2.50");
            agent.GetComponent<Damageable>().TakeDamage(10, null, null, DamageKind.Explosion);

            Assert.That(shield.Guard.SecondsRemaining, Is.EqualTo(ShieldSpec.DefaultExplosionLowerSeconds));
        }

        [Test]
        public void BulletDamageDoesNotLowerTheShield()
        {
            var (agent, shield) = CreateShieldEnemy(EnemyArchetype.Shield);
            shield.Tick(0.1f);

            agent.GetComponent<Damageable>().TakeDamage(10, null, null, DamageKind.Bullet);

            Assert.That(shield.Guard.IsRaised, Is.True);
            Assert.That(shield.Plate.IsRaised, Is.True);
        }

        [Test]
        public void AnInterruptLowersTheShield()
        {
            var (agent, shield) = CreateShieldEnemy(EnemyArchetype.Shield);
            shield.Tick(0.1f);
            agent.Interrupt(1f, "flinch");

            LogAssert.Expect(LogType.Log, "[AI] id=shield-1 shield lowered cause=interrupt seconds=1.00");
            shield.Tick(0f);

            Assert.That(shield.Guard.IsRaised, Is.False);
            Assert.That(shield.Plate.IsRaised, Is.False);
        }

        [Test]
        public void AnotherArchetypeDoesNotHoldUpTheShield()
        {
            var (agent, shield) = CreateShieldEnemy(EnemyArchetype.Rifleman);

            shield.Tick(0.1f);

            Assert.That(shield.IsActive, Is.False);
            Assert.That(shield.Plate.IsRaised, Is.False);
            Assert.That(agent.SprintAllowed, Is.True);
            Assert.That(agent.GaitSpeedMultiplier, Is.EqualTo(1f));
        }

        [Test]
        public void DyingDropsTheShield()
        {
            var (agent, shield) = CreateShieldEnemy(EnemyArchetype.Shield);
            shield.Tick(0.1f);

            agent.GetComponent<Damageable>().TakeDamage(100);
            shield.Tick(0.1f);

            Assert.That(shield.IsActive, Is.False);
            Assert.That(shield.Plate.IsRaised, Is.False);
            Assert.That(agent.SprintAllowed, Is.True);
        }

        [Test]
        public void TheSniperShotIsScaledFromTheWeaponDamage()
        {
            Assert.That(EnemyAgent.ScaleDamage(25, 3f), Is.EqualTo(75));
            Assert.That(EnemyAgent.ScaleDamage(25, 1f), Is.EqualTo(25));
            Assert.That(EnemyAgent.ScaleDamage(25, 0.5f), Is.EqualTo(13));
            Assert.That(EnemyAgent.ScaleDamage(int.MaxValue, 2f), Is.EqualTo(int.MaxValue));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => EnemyAgent.ScaleDamage(-1, 1f));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => EnemyAgent.ScaleDamage(1, 0f));
        }

        [Test]
        public void TheArchetypeValuesRoundTripThroughTheAgent()
        {
            var host = Track(GameObject.CreatePrimitive(PrimitiveType.Capsule));
            var agent = host.AddComponent<EnemyAgent>();
            var shield = new ShieldSpec(0.3f, 1f, 2f, 3f, new MeleeSpec(20, 1.5f, 80f, 0.5f, 1f, 0.2f, 0f));
            var sniper = new SniperSpec(1.5f, 4f, 0.5f, 6f);

            agent.ApplySpec(new EnemySpec(20f, 10f, 1f, 2f, 0.5f, archetype: EnemyArchetype.Sniper, shield: shield, sniper: sniper));
            var spec = agent.CreateSpec();

            Assert.That(spec.Archetype, Is.EqualTo(EnemyArchetype.Sniper));
            Assert.That(spec.Shield.ApproachSpeedMultiplier, Is.EqualTo(0.3f));
            Assert.That(spec.Shield.HoldDistance, Is.EqualTo(1f));
            Assert.That(spec.Shield.ExplosionLowerSeconds, Is.EqualTo(3f));
            Assert.That(spec.Shield.Bash.Damage, Is.EqualTo(20));
            Assert.That(spec.Shield.Bash.LungeDistance, Is.EqualTo(0f));
            Assert.That(spec.Sniper.AimSeconds, Is.EqualTo(1.5f));
            Assert.That(spec.Sniper.ShotDamageMultiplier, Is.EqualTo(4f));
            Assert.That(spec.Sniper.RelocateGiveUpSeconds, Is.EqualTo(6f));
        }

        private T Track<T>(T created)
            where T : Object
        {
            _created.Add(created);
            return created;
        }

        // 盾の位置に置くと敵の前を塞ぐ大きさにし、見分けられるよう仮の板と違う大きさにする。原点から離れた所に置いて撃つ線に掛からないようにする。
        private GameObject CreatePlateTemplate(string name)
        {
            var template = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
            template.name = name;
            template.transform.localPosition = new Vector3(0f, 0.2f, 0.7f);
            template.transform.localScale = new Vector3(1.3f, 1.8f, 0.2f);
            return template;
        }

        private static HitscanResult FireFromTheFront(int damage)
        {
            return Hitscan.Fire(Origin + new Vector3(0f, 1f, 5f), Vector3.back, 20f, damage, DamageFalloff.None, null);
        }

        private (GameObject Body, Damageable Damageable, BulletShield Plate) CreateShieldedBody()
        {
            var body = Track(GameObject.CreatePrimitive(PrimitiveType.Capsule));
            body.name = "Body";
            body.transform.position = Origin + Vector3.up;
            var damageable = body.AddComponent<Damageable>();
            damageable.Initialize("target", 100);
            body.AddComponent<Hitbox>().Initialize(HitZone.Body, damageable);
            var plate = BulletShield.CreatePlaceholder(body.transform);
            Physics.SyncTransforms();
            return (body, damageable, plate);
        }

        private (EnemyAgent Agent, EnemyShield Shield) CreateShieldEnemy(EnemyArchetype archetype)
        {
            var host = Track(GameObject.CreatePrimitive(PrimitiveType.Capsule));
            host.name = "ShieldEnemy";
            host.transform.position = Origin + Vector3.up;
            var agent = host.AddComponent<EnemyAgent>();
            agent.WeaponDefinition = _rifle;
            agent.Id = "shield-1";
            var damageable = host.GetComponent<Damageable>();
            damageable.Initialize("shield-1", 100);
            host.AddComponent<Hitbox>().Initialize(HitZone.Body, damageable);
            agent.ApplySpec(new EnemySpec(10f, 2f, 1f, 2f, 0.5f, archetype: archetype));
            Assert.That(agent.TryInitialize(), Is.True);

            var shield = host.AddComponent<EnemyShield>();
            Physics.SyncTransforms();
            return (agent, shield);
        }
    }
}
