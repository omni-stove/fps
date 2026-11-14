using System.Collections.Generic;
using Fps.Core;
using Fps.Runtime;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Fps.Tests.EditMode
{
    public sealed class DamageReportTests
    {
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
        public void PlainDamageHasNoZoneNoDirectionAndIsOther()
        {
            var target = CreateTarget("dummy", Vector3.zero, HitZone.Body, out var reports);

            target.TakeDamage(5, new Vector3(1f, 0f, 0f));

            Assert.That(reports, Has.Count.EqualTo(1));
            Assert.That(reports[0].Zone.HasValue, Is.False);
            Assert.That(reports[0].Direction.HasValue, Is.False);
            Assert.That(reports[0].Kind, Is.EqualTo(DamageKind.Other));
        }

        [Test]
        public void ApplyHitReportsTheZoneTheNormalizedDirectionAndBullet()
        {
            var target = CreateTarget("dummy", Vector3.zero, HitZone.Body, out var reports);

            target.ApplyHit(10, HitZone.Head, 0f, DamageFalloff.None, Vector3.back * 5f, new Vector3(0f, 0f, 4f));

            Assert.That(reports[0].Zone, Is.EqualTo(HitZone.Head));
            Assert.That(reports[0].Direction.HasValue, Is.True);
            Assert.That(Vector3.Distance(reports[0].Direction.Value, Vector3.forward), Is.LessThan(1e-5f));
            Assert.That(reports[0].Kind, Is.EqualTo(DamageKind.Bullet));
        }

        [Test]
        public void ApplyHitWithoutADirectionStillReportsTheZone()
        {
            var target = CreateTarget("dummy", Vector3.zero, HitZone.Body, out var reports);

            target.ApplyHit(10, HitZone.Limb, 0f, DamageFalloff.None);

            Assert.That(reports[0].Zone, Is.EqualTo(HitZone.Limb));
            Assert.That(reports[0].Direction.HasValue, Is.False);
            Assert.That(reports[0].Kind, Is.EqualTo(DamageKind.Bullet));
        }

        [Test]
        public void TakeDamageCarriesTheGivenKindAndDirection()
        {
            var target = CreateTarget("dummy", Vector3.zero, HitZone.Body, out var reports);

            target.TakeDamage(20, Vector3.zero, new Vector3(3f, 0f, 0f), DamageKind.Explosion);

            Assert.That(reports[0].Zone.HasValue, Is.False);
            Assert.That(Vector3.Distance(reports[0].Direction.Value, Vector3.right), Is.LessThan(1e-5f));
            Assert.That(reports[0].Kind, Is.EqualTo(DamageKind.Explosion));
        }

        [Test]
        public void ZeroLengthDirectionIsDropped()
        {
            var report = new DamageReport(1, null, HitZone.Body, Vector3.zero, DamageKind.Melee);

            Assert.That(report.Direction.HasValue, Is.False);
            Assert.That(report.Kind, Is.EqualTo(DamageKind.Melee));
        }

        [Test]
        public void UndefinedKindIsRejectedWithoutTakingHealth()
        {
            var target = CreateTarget("dummy", Vector3.zero, HitZone.Body, out _);

            Assert.Throws<System.ArgumentOutOfRangeException>(() => target.TakeDamage(5, null, null, (DamageKind)99));
            Assert.That(target.Health.Current, Is.EqualTo(target.Health.Max));
        }

        [Test]
        public void HitscanFillsTheZoneTheShotDirectionAndBullet()
        {
            var target = CreateTarget("dummy", new Vector3(0f, 1f, 10f), HitZone.Head, out var reports);

            Hitscan.Fire(new Vector3(0f, 1.6f, 0f), new Vector3(0f, 0f, 2f), 50f, 10, DamageFalloff.None, null);

            Assert.That(reports, Has.Count.EqualTo(1));
            Assert.That(reports[0].Zone, Is.EqualTo(HitZone.Head));
            Assert.That(Vector3.Distance(reports[0].Direction.Value, Vector3.forward), Is.LessThan(1e-5f));
            Assert.That(reports[0].Kind, Is.EqualTo(DamageKind.Bullet));
        }

        [Test]
        public void PlayerHitFillsTheZoneTheShotDirectionAndBullet()
        {
            var target = CreateTarget("dummy", Vector3.zero, HitZone.Limb, out var reports);

            WeaponController.TryApplyHit(target.GetComponent<Collider>(), 3f, 10, DamageFalloff.None, null, Vector3.back * 3f, Vector3.forward);

            Assert.That(reports[0].Zone, Is.EqualTo(HitZone.Limb));
            Assert.That(Vector3.Distance(reports[0].Direction.Value, Vector3.forward), Is.LessThan(1e-5f));
            Assert.That(reports[0].Kind, Is.EqualTo(DamageKind.Bullet));
        }

        [Test]
        public void EnemyDamagedLogShowsTheZoneAndKind()
        {
            var report = new DamageReport(20, new Vector3(0f, 0f, -10f), HitZone.Head, Vector3.forward, DamageKind.Bullet);

            Assert.That(
                EnemyAgent.FormatDamagedLog("enemy-1", report),
                Is.EqualTo($"[AI] id=enemy-1 damaged dmg=20 from={new Vector3(0f, 0f, -10f)} zone=Head kind=Bullet"));
            Assert.That(
                EnemyAgent.FormatDamagedLog("enemy-1", new DamageReport(5, null)),
                Is.EqualTo("[AI] id=enemy-1 damaged dmg=5 from=unknown zone=none kind=Other"));
        }

        private Damageable CreateTarget(string id, Vector3 position, HitZone zone, out List<DamageReport> reports)
        {
            var target = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            _created.Add(target);
            target.name = id;
            target.transform.position = position;
            var damageable = target.AddComponent<Damageable>();
            damageable.Initialize(id, 100);
            target.AddComponent<Hitbox>().Initialize(zone, damageable);
            Physics.SyncTransforms();
            var received = new List<DamageReport>();
            damageable.Damaged += (_, report) => received.Add(report);
            reports = received;
            return damageable;
        }
    }
}
