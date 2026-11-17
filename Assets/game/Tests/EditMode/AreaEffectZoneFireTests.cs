using System.Collections.Generic;
using System.Linq;
using Shooter.Core;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    // Edit Mode では Update が走らないので、範囲と燃焼を同じ刻みで手で進める
    public sealed class AreaEffectZoneFireTests
    {
        private const string _weaponId = "b9e59060-e2b8-4109-a693-0a87015e6817";
        private const float _step = 0.1f;
        private static readonly AreaEffectSpec _fire = new AreaEffectSpec(3f, 10f, 0.5f, 5, visualId: "fire-test");
        private static readonly AreaEffectSpec _smoke = new AreaEffectSpec(3f, 10f, 0.5f, 0, visualId: "smoke-test");

        private readonly List<Object> _created = new List<Object>();
        private readonly List<AreaEffectZone> _zones = new List<AreaEffectZone>();
        private readonly List<KillFeedEntry> _kills = new List<KillFeedEntry>();

        [SetUp]
        public void SetUp()
        {
            CombatEvents.ClearSubscribers();
            CombatEvents.Killed += _kills.Add;
        }

        [TearDown]
        public void TearDown()
        {
            CombatEvents.ClearSubscribers();
            _kills.Clear();
            foreach (var zone in _zones)
            {
                if (zone != null)
                {
                    zone.End();
                }
            }

            _zones.Clear();
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
        public void SomeoneInTheFireKeepsBurningAndStopsOnceTheBurnRunsOutAfterLeaving()
        {
            var target = CreateTarget(new Vector3(0f, 1f, 0f), 1000);
            var zone = Spread(_fire, Vector3.zero, null);
            var burn = FireArea.Affliction(_fire);

            Advance(zone, target, 2f);

            Assert.That(target.StatusEffects.Has(StatusEffectKinds.Burn), Is.True);
            Assert.That(target.StatusEffects.RemainingSeconds(StatusEffectKinds.Burn), Is.GreaterThan(burn.DurationSeconds - 0.5f), "still topped up inside");
            var inside = target.Health.Current;
            Assert.That(inside, Is.LessThan(target.Health.Max));

            target.transform.position = new Vector3(20f, 1f, 0f);
            Physics.SyncTransforms();
            Advance(zone, target, 1f);
            Assert.That(target.Health.Current, Is.LessThan(inside), "it keeps burning after leaving");

            Advance(zone, target, burn.DurationSeconds);
            Assert.That(target.StatusEffects.Has(StatusEffectKinds.Burn), Is.False);
            var burntOut = target.Health.Current;
            Advance(zone, target, 1f);
            Assert.That(target.Health.Current, Is.EqualTo(burntOut), "nothing more once the burn runs out");
            Assert.That(zone.HasEnded, Is.False, "the fire is still there");
        }

        [Test]
        public void SomeoneOutsideTheFireDoesNotCatchFire()
        {
            var target = CreateTarget(new Vector3(10f, 1f, 0f), 100);
            var zone = Spread(_fire, Vector3.zero, null);

            Advance(zone, target, 1f);

            Assert.That(target.StatusEffects == null || !target.StatusEffects.Has(StatusEffectKinds.Burn), Is.True);
            Assert.That(target.Health.Current, Is.EqualTo(100));
        }

        [Test]
        public void SmokeDoesNotBurn()
        {
            var target = CreateTarget(new Vector3(0f, 1f, 0f), 100);
            var zone = Spread(_smoke, Vector3.zero, null);

            Advance(zone, target, 1f);

            Assert.That(target.StatusEffects == null || !target.StatusEffects.Has(StatusEffectKinds.Burn), Is.True);
        }

        [Test]
        public void AKillByTheFireIsCreditedToTheThrowerAndTheGrenade()
        {
            var target = CreateTarget(new Vector3(0f, 1f, 0f), 10);
            var thrower = new GameObject("thrower");
            _created.Add(thrower);
            var zone = Spread(_fire, Vector3.zero, thrower);

            Advance(zone, target, 2f);

            Assert.That(target.IsDead, Is.True);
            Assert.That(_kills, Has.Count.EqualTo(1));
            Assert.That(_kills[0].KillerId, Is.EqualTo(CombatEvents.OfflineId(thrower)));
            Assert.That(_kills[0].WeaponId, Is.EqualTo(_weaponId));
        }

        [Test]
        public void WithoutAVisualTheFireIsBuiltFromParticles()
        {
            var zone = Spread(_fire, Vector3.zero, null);

            var visual = zone.transform.Find(AreaEffectZone.VisualName);
            Assert.That(visual, Is.Not.Null);
            Assert.That(visual.GetComponent<ParticleSystem>(), Is.Not.Null);
            Assert.That(visual.GetComponent<Collider>(), Is.Null, "fire lets shots through");
        }

        [Test]
        public void ShownFireOnANetClientIsParticlesButBurnsNoOne()
        {
            var fireVisualId = KitTuning.Grenades.Types
                .Select(grenade => grenade.Impact.AreaEffect)
                .First(FireArea.IsFire)
                .VisualId;
            var target = CreateTarget(new Vector3(0f, 1f, 0f), 100);
            var zone = AreaEffectZone.Show(fireVisualId, Vector3.zero, 3f, 5f);
            _zones.Add(zone);

            Advance(zone, target, 1f);

            Assert.That(zone.transform.Find(AreaEffectZone.VisualName).GetComponent<ParticleSystem>(), Is.Not.Null);
            Assert.That(target.Health.Current, Is.EqualTo(100), "the server decides who burns");
        }

        private static void Advance(AreaEffectZone zone, Damageable target, float seconds)
        {
            for (var elapsed = 0f; elapsed < seconds - 1e-4f; elapsed += _step)
            {
                zone.Tick(_step);
                if (target.StatusEffects != null)
                {
                    target.StatusEffects.Advance(_step);
                }
            }
        }

        private AreaEffectZone Spread(AreaEffectSpec spec, Vector3 center, GameObject owner)
        {
            var zone = AreaEffectZone.Spread(spec, center, owner, _weaponId);
            _zones.Add(zone);
            return zone;
        }

        private Damageable CreateTarget(Vector3 position, int maxHealth)
        {
            var target = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            _created.Add(target);
            target.name = "target";
            target.transform.position = position;
            var damageable = target.AddComponent<Damageable>();
            damageable.Initialize("target", maxHealth);
            Physics.SyncTransforms();
            return damageable;
        }
    }
}
