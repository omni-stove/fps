using System;
using System.Collections.Generic;
using Fps.Core;
using Fps.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Fps.Tests.EditMode
{
    /// <summary>
    /// Raycast → Hitbox → DamageCalculator → Damageable の流れと、視線判定・知覚の組み立てを確かめる。
    /// Edit Mode でも Physics.Raycast はコライダーに当たるので、生成後に SyncTransforms してから撃つ。
    /// </summary>
    public sealed class HitscanTests
    {
        private static readonly Vector3 _eye = new Vector3(0f, 1.6f, 0f);

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
        public void HeadHitAppliesTheZoneMultiplierToTheOwner()
        {
            var target = CreateTarget("dummy", new Vector3(0f, 1f, 10f), HitZone.Head);

            LogAssert.Expect(LogType.Log, "[Combat] hit zone=Head dmg=20 id=dummy hp=80");
            var result = Hitscan.Fire(_eye, Vector3.forward, 50f, 10, DamageFalloff.None, null);

            Assert.That(result.Hit, Is.True);
            Assert.That(result.Hitbox, Is.Not.Null);
            Assert.That(result.Hitbox.Zone, Is.EqualTo(HitZone.Head));
            Assert.That(result.Damage, Is.EqualTo(20));
            Assert.That(target.Health.Current, Is.EqualTo(80));
        }

        [Test]
        public void DistanceFalloffUsesTheRaycastDistance()
        {
            var target = CreateTarget("far", new Vector3(0f, 1f, 20.5f), HitZone.Body);
            var falloff = new DamageFalloff(0f, 40f, 0.5f);

            // カプセルの円柱部分の高さから撃ち、表面 (z = 20) までの 20m で倍率 0.75。
            var result = Hitscan.Fire(new Vector3(0f, 1f, 0f), Vector3.forward, 50f, 100, falloff, null);

            Assert.That(result.Damage, Is.EqualTo(75));
            Assert.That(target.Health.Current, Is.EqualTo(25));
        }

        [Test]
        public void WallStopsTheShot()
        {
            var target = CreateTarget("hidden", new Vector3(0f, 1f, 10f), HitZone.Body);
            var wall = CreateWall(new Vector3(0f, 1.5f, 5f));

            var result = Hitscan.Fire(_eye, Vector3.forward, 50f, 10, DamageFalloff.None, null);

            Assert.That(result.Hit, Is.True);
            Assert.That(result.Hitbox, Is.Null);
            Assert.That(result.RaycastHit.collider.gameObject, Is.SameAs(wall));
            Assert.That(result.Damage, Is.Zero);
            Assert.That(target.Health.Current, Is.EqualTo(target.Health.Max));
        }

        [Test]
        public void ShooterDoesNotHitItself()
        {
            var shooter = new GameObject("Shooter");
            _created.Add(shooter);
            var ownCollider = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ownCollider.transform.SetParent(shooter.transform, false);
            ownCollider.transform.position = new Vector3(0f, 1.6f, 1f);
            var target = CreateTarget("dummy", new Vector3(0f, 1f, 10f), HitZone.Body);

            var result = Hitscan.Fire(_eye, Vector3.forward, 50f, 10, DamageFalloff.None, shooter.transform);

            Assert.That(result.Hitbox, Is.Not.Null);
            Assert.That(result.Hitbox.Owner, Is.SameAs(target));
        }

        [Test]
        public void MissReturnsNoHit()
        {
            var result = Hitscan.Fire(_eye, Vector3.back, 50f, 10, DamageFalloff.None, null);

            Assert.That(result.Hit, Is.False);
            Assert.That(result.Damage, Is.Zero);
        }

        [Test]
        public void DeadOwnerTakesNoMoreHits()
        {
            var target = CreateTarget("dummy", new Vector3(0f, 1f, 10f), HitZone.Body);
            target.Initialize("dummy", 5);

            LogAssert.Expect(LogType.Log, "[Combat] target died id=dummy");
            Hitscan.Fire(_eye, Vector3.forward, 50f, 10, DamageFalloff.None, null);
            var second = Hitscan.Fire(_eye, Vector3.forward, 50f, 10, DamageFalloff.None, null);

            Assert.That(second.Damage, Is.Zero);
        }

        [Test]
        public void HitboxFindsItsOwnerInTheParents()
        {
            var root = new GameObject("Root");
            _created.Add(root);
            var owner = root.AddComponent<Damageable>();
            var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            head.transform.SetParent(root.transform, false);
            var hitbox = head.AddComponent<Hitbox>();

            Assert.That(Hitbox.FindFor(head.GetComponent<Collider>()), Is.SameAs(hitbox));
            Assert.That(hitbox.Owner, Is.SameAs(owner));
        }

        [Test]
        public void HitboxWithoutOwnerIgnoresTheHit()
        {
            var orphan = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _created.Add(orphan);
            orphan.transform.position = new Vector3(0f, 1.6f, 5f);
            var hitbox = orphan.AddComponent<Hitbox>();
            Physics.SyncTransforms();

            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("has no Damageable owner"));
            var result = Hitscan.Fire(_eye, Vector3.forward, 50f, 10, DamageFalloff.None, null);

            Assert.That(result.Hitbox, Is.SameAs(hitbox));
            Assert.That(result.Damage, Is.Zero);
        }

        [Test]
        public void HitboxInitializeRejectsMissingOwner()
        {
            var host = new GameObject("Host");
            _created.Add(host);
            var hitbox = host.AddComponent<Hitbox>();

            Assert.Throws<ArgumentNullException>(() => hitbox.Initialize(HitZone.Body, null));
        }

        [Test]
        public void RejectsInvalidRaycastArguments()
        {
            Assert.Throws<ArgumentException>(() => Hitscan.TryFirstHit(_eye, Vector3.zero, 10f, null, out _));
            Assert.Throws<ArgumentOutOfRangeException>(() => Hitscan.TryFirstHit(_eye, Vector3.forward, 0f, null, out _));
            Assert.Throws<ArgumentOutOfRangeException>(() => Hitscan.TryFirstHit(_eye, Vector3.forward, float.PositiveInfinity, null, out _));
            Assert.Throws<ArgumentNullException>(() => Hitscan.Fire(_eye, Vector3.forward, 10f, 1, null, null));
        }

        [Test]
        public void LineOfSightIsClearWithNothingInBetween()
        {
            var target = CreateTarget("player", new Vector3(3f, 1f, 10f), HitZone.Body);

            Assert.That(Hitscan.HasLineOfSight(_eye, target, 20f, null), Is.True);
        }

        [Test]
        public void WallBlocksTheLineOfSight()
        {
            var target = CreateTarget("player", new Vector3(0f, 1f, 10f), HitZone.Body);
            CreateWall(new Vector3(0f, 1.5f, 5f));

            Assert.That(Hitscan.HasLineOfSight(_eye, target, 20f, null), Is.False);
        }

        [Test]
        public void TargetBeyondRangeOrDeadIsNotSeen()
        {
            var target = CreateTarget("player", new Vector3(0f, 1f, 10f), HitZone.Body);

            Assert.That(Hitscan.HasLineOfSight(_eye, target, 5f, null), Is.False);
            Assert.That(Hitscan.HasLineOfSight(_eye, null, 20f, null), Is.False);

            target.Initialize("player", 1);
            LogAssert.Expect(LogType.Log, "[Combat] target died id=player");
            target.TakeDamage(1);

            Assert.That(Hitscan.HasLineOfSight(_eye, target, 20f, null), Is.False);
        }

        [Test]
        public void PerceptionReportsGroundPositionsAndSight()
        {
            var target = CreateTarget("player", new Vector3(0f, 1f, 10f), HitZone.Body);

            var perception = EnemySenses.Perceive(Vector3.zero, _eye, target, 20f, null);

            Assert.That(perception.Self, Is.EqualTo(System.Numerics.Vector3.Zero));
            Assert.That(perception.Target.HasValue, Is.True);
            Assert.That(System.Numerics.Vector3.Distance(perception.Target.Value, new System.Numerics.Vector3(0f, 0f, 10f)), Is.LessThan(1e-3f));
            Assert.That(perception.HasLineOfSight, Is.True);
            Assert.That(perception.HeardNoise, Is.False);
        }

        [Test]
        public void PerceptionHasNoSightOutsideTheSightRangeOrBehindAWall()
        {
            var target = CreateTarget("player", new Vector3(0f, 1f, 10f), HitZone.Body);

            Assert.That(EnemySenses.Perceive(Vector3.zero, _eye, target, 8f, null).HasLineOfSight, Is.False);

            CreateWall(new Vector3(0f, 1.5f, 5f));
            Physics.SyncTransforms();
            var blocked = EnemySenses.Perceive(Vector3.zero, _eye, target, 20f, null);

            Assert.That(blocked.HasLineOfSight, Is.False);
            Assert.That(blocked.Target.HasValue, Is.True, "the brain still knows where the target is, it just cannot see it");
        }

        [Test]
        public void PerceptionWithoutTargetHasNoTarget()
        {
            var perception = EnemySenses.Perceive(Vector3.zero, _eye, null, 20f, null);

            Assert.That(perception.Target.HasValue, Is.False);
            Assert.That(perception.HasLineOfSight, Is.False);
            Assert.Throws<ArgumentOutOfRangeException>(() => EnemySenses.Perceive(Vector3.zero, _eye, null, 0f, null));
        }

        [Test]
        public void VectorConversionsRoundTrip()
        {
            var unity = new Vector3(1.5f, -2f, 3.25f);

            var numerics = unity.ToNumerics();

            Assert.That(numerics, Is.EqualTo(new System.Numerics.Vector3(1.5f, -2f, 3.25f)));
            Assert.That(numerics.ToUnity(), Is.EqualTo(unity));
        }

        private Damageable CreateTarget(string id, Vector3 position, HitZone zone)
        {
            var target = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            _created.Add(target);
            target.name = id;
            target.transform.position = position;
            var damageable = target.AddComponent<Damageable>();
            damageable.Initialize(id, 100);
            target.AddComponent<Hitbox>().Initialize(zone, damageable);
            Physics.SyncTransforms();
            return damageable;
        }

        private GameObject CreateWall(Vector3 center)
        {
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _created.Add(wall);
            wall.name = "Wall";
            wall.transform.position = center;
            wall.transform.localScale = new Vector3(4f, 3f, 0.5f);
            Physics.SyncTransforms();
            return wall;
        }
    }
}
