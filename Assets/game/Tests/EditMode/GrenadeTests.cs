using System.Collections.Generic;
using Fps.Core;
using Fps.Runtime;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Fps.Tests.EditMode
{
    /// <summary>
    /// グレネードを投げる (<see cref="GrenadeThrower"/>) ことと、爆発 (<see cref="ThrownGrenade.Explode"/>) が半径内の
    /// <see cref="Damageable"/> と建造物に当たり、壁の向こうには当たらないことを確かめる。
    /// Edit Mode でも Physics の問い合わせはコライダーを見つけるので、生成後に SyncTransforms してから呼ぶ。
    /// </summary>
    public sealed class GrenadeTests
    {
        private static readonly GameRules _twoGrenades =
            GameRules.Default.WithActions(ActionRules.Default.WithGrenade(Grenade.On).WithGrenadeCount(2));

        private readonly List<GameObject> _created = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
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
        public void ExplosionKillsANearbyTarget()
        {
            var target = CreateTarget("Near", new Vector3(0f, 1f, 1.5f));
            var grenade = SpawnGrenade(new Vector3(0f, 0.1f, 0f));

            var result = grenade.Explode();

            Assert.That(target.IsDead, Is.True);
            Assert.That(result.Damaged, Is.EqualTo(1));
            Assert.That(result.Killed, Is.EqualTo(1));
        }

        [Test]
        public void ExplosionDamagesLessFartherAway()
        {
            var target = CreateTarget("Mid", new Vector3(0f, 1f, 4.5f));
            var grenade = SpawnGrenade(new Vector3(0f, 1f, 0f));

            grenade.Explode();

            Assert.That(target.IsDead, Is.False);
            Assert.That(target.Health.Current, Is.LessThan(target.Health.Max));
        }

        [Test]
        public void ExplosionDoesNotReachBeyondTheRadius()
        {
            var target = CreateTarget("Far", new Vector3(0f, 1f, GrenadeThrower.DamageRadius + 2f));
            var grenade = SpawnGrenade(new Vector3(0f, 1f, 0f));

            var result = grenade.Explode();

            Assert.That(target.Health.Current, Is.EqualTo(target.Health.Max));
            Assert.That(result.Damaged, Is.Zero);
        }

        [Test]
        public void ExplosionDoesNotHitATargetBehindAWall()
        {
            var target = CreateTarget("Hidden", new Vector3(0f, 1f, 2.5f));
            CreateBox("Wall", new Vector3(0f, 1.5f, 1.2f), new Vector3(4f, 3f, 0.3f));
            var grenade = SpawnGrenade(new Vector3(0f, 0.5f, 0f));

            var result = grenade.Explode();

            Assert.That(target.Health.Current, Is.EqualTo(target.Health.Max));
            Assert.That(result.Damaged, Is.Zero);
        }

        [Test]
        public void AnotherTargetInFrontDoesNotShield()
        {
            var front = CreateTarget("Front", new Vector3(0f, 1f, 1f));
            var back = CreateTarget("Back", new Vector3(0f, 1f, 2.5f));
            var grenade = SpawnGrenade(new Vector3(0f, 1f, 0f));

            grenade.Explode();

            Assert.That(front.Health.Current, Is.LessThan(front.Health.Max));
            Assert.That(back.Health.Current, Is.LessThan(back.Health.Max));
        }

        [Test]
        public void ExplosionHitsStructuresInTheRadius()
        {
            var structure = CreateBox("Hut", new Vector3(0f, 0.5f, 2f), Vector3.one).AddComponent<StructureHitRecorder>();
            var grenade = SpawnGrenade(new Vector3(0f, 0.5f, 0f));

            var result = grenade.Explode();

            Assert.That(structure.Calls, Has.Count.EqualTo(1));
            Assert.That(structure.Calls[0].Damage, Is.GreaterThan(0));
            Assert.That(result.StructureHits, Is.EqualTo(1));
        }

        [Test]
        public void ExplodesWhenTheFuseRunsOutAndOnlyOnce()
        {
            var target = CreateTarget("Near", new Vector3(0f, 1f, 1f));
            var grenade = SpawnGrenade(new Vector3(0f, 1f, 0f));
            var explosions = 0;
            grenade.Exploded += (exploded, result) => explosions++;

            Assert.That(grenade.Tick(GrenadeThrower.FuseSeconds - 0.1f), Is.False);
            Assert.That(target.IsDead, Is.False);
            Assert.That(grenade.Tick(0.2f), Is.True);

            Assert.That(explosions, Is.EqualTo(1));
            Assert.That(target.IsDead, Is.True);
            Assert.That(grenade == null, Is.True, "the grenade is removed after exploding");
        }

        [Test]
        public void ThrowerThrowsOnlyAsManyAsTheRulesAllow()
        {
            var thrower = CreateThrower(_twoGrenades);

            Assert.That(thrower.TryThrow(), Is.True);
            thrower.Tick(GrenadeThrower.ThrowSeconds);
            Assert.That(thrower.TryThrow(), Is.True);
            thrower.Tick(GrenadeThrower.ThrowSeconds);

            Assert.That(thrower.TryThrow(), Is.False, "a third grenade cannot be thrown");
            Assert.That(thrower.Remaining, Is.Zero);
        }

        [Test]
        public void ThrowerCannotThrowAgainWhileThrowing()
        {
            var thrower = CreateThrower(_twoGrenades);

            thrower.TryThrow();

            Assert.That(thrower.IsThrowing, Is.True);
            Assert.That(thrower.TryThrow(), Is.False);
            thrower.Tick(GrenadeThrower.ThrowSeconds);
            Assert.That(thrower.IsThrowing, Is.False);
        }

        [Test]
        public void ThrowerThrowsNothingWhenGrenadesAreOff()
        {
            var thrower = CreateThrower(GameRules.Default.WithActions(ActionRules.Default.WithGrenadeCount(3)));

            Assert.That(thrower.UsesGrenades, Is.False);
            Assert.That(thrower.TryThrow(), Is.False);
        }

        [Test]
        public void ThrowerThrowsNothingBeforeItIsConfigured()
        {
            var body = CreateBox("Unconfigured", new Vector3(0f, 1f, 0f), Vector3.one);
            var thrower = body.AddComponent<GrenadeThrower>();

            Assert.That(thrower.TryThrow(), Is.False);
        }

        [Test]
        public void ThrowerSendsTheGrenadeForwardFromInFrontOfTheBody()
        {
            var thrower = CreateThrower(_twoGrenades);
            ThrownGrenade thrown = null;
            thrower.Thrown += grenade => thrown = grenade;

            thrower.TryThrow();

            Assert.That(thrown, Is.Not.Null);
            Assert.That(thrown.Thrower, Is.SameAs(thrower.gameObject));
            Assert.That(thrown.transform.position.z, Is.GreaterThan(thrower.transform.position.z));
            Assert.That(thrown.GetComponent<Rigidbody>().linearVelocity.z, Is.GreaterThan(0f));
            Assert.That(thrown.RemainingSeconds, Is.EqualTo(GrenadeThrower.FuseSeconds));
        }

        [Test]
        public void ThrowerRefillsWhenTheBodyRespawns()
        {
            var thrower = CreateThrower(_twoGrenades);
            thrower.TryThrow();

            thrower.GetComponent<Damageable>().ResetHealth();

            Assert.That(thrower.Remaining, Is.EqualTo(2));
            Assert.That(thrower.IsThrowing, Is.False);
        }

        [Test]
        public void ThrowerDoesNotThrowWhileDeadOrBlocked()
        {
            var thrower = CreateThrower(_twoGrenades);
            var blocked = true;
            thrower.ThrowBlocker = () => blocked;

            Assert.That(thrower.TryThrow(), Is.False);
            blocked = false;
            thrower.GetComponent<Damageable>().Kill();
            Assert.That(thrower.TryThrow(), Is.False);
            Assert.That(thrower.Remaining, Is.EqualTo(2));
        }

        [Test]
        public void ThrowerReportsChangesToTheCount()
        {
            var thrower = CreateThrower(_twoGrenades);
            var changes = 0;
            thrower.Changed += _ => changes++;

            thrower.TryThrow();
            thrower.Refill();

            Assert.That(changes, Is.EqualTo(2));
        }

        private GrenadeThrower CreateThrower(GameRules rules)
        {
            var body = CreateBox("Thrower", new Vector3(0f, 1f, 0f), new Vector3(1f, 2f, 1f));
            body.AddComponent<Damageable>().Initialize("Thrower", 100);
            var thrower = body.AddComponent<GrenadeThrower>();
            thrower.Configure(rules);
            thrower.Thrown += grenade => _created.Add(grenade.gameObject);
            return thrower;
        }

        private ThrownGrenade SpawnGrenade(Vector3 position)
        {
            var grenade = ThrownGrenade.Spawn(
                null, position, Vector3.zero, GrenadeThrower.FuseSeconds, GrenadeThrower.Explosion, GrenadeThrower.StructureDamageAtCenter);
            _created.Add(grenade.gameObject);
            Physics.SyncTransforms();
            return grenade;
        }

        private Damageable CreateTarget(string name, Vector3 position)
        {
            var body = CreateBox(name, position, new Vector3(0.6f, 2f, 0.6f));
            var damageable = body.AddComponent<Damageable>();
            damageable.Initialize(name, 100);
            body.AddComponent<Hitbox>().Initialize(HitZone.Body, damageable);
            return damageable;
        }

        private GameObject CreateBox(string name, Vector3 position, Vector3 size)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _created.Add(box);
            box.name = name;
            box.transform.position = position;
            box.transform.localScale = size;
            Physics.SyncTransforms();
            return box;
        }
    }
}
