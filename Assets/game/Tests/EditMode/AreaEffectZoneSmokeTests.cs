using System.Collections.Generic;
using System.Linq;
using Shooter.Core;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    public sealed class AreaEffectZoneSmokeTests
    {
        private static readonly Vector3 _eye = new Vector3(0f, 1.6f, 0f);
        private static readonly AreaEffectSpec _smoke = new AreaEffectSpec(3f, 10f, 0.5f, 0, visualId: "smoke-test");
        private static readonly AreaEffectSpec _fire = new AreaEffectSpec(3f, 10f, 0.5f, 5, visualId: "fire-test");

        private readonly List<GameObject> _created = new List<GameObject>();
        private readonly List<AreaEffectZone> _zones = new List<AreaEffectZone>();

        [TearDown]
        public void TearDown()
        {
            // Edit Mode では OnDestroy が走らないので、消す前に End で視線を遮る物の登録を外す
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
        public void SmokeBetweenAnEnemyAndItsTargetHidesTheTargetUntilItClears()
        {
            var target = CreateTarget(new Vector3(0f, 1f, 10f));
            Assert.That(See(target), Is.True);

            var zone = Spread(_smoke, new Vector3(0f, 1f, 5f));

            Assert.That(zone.BlocksSight, Is.True);
            Assert.That(EnemySenses.VisionBlockers, Has.Some.SameAs(zone));
            Assert.That(See(target), Is.False, "the smoke is between the eye and the target");

            Assert.That(zone.Tick(_smoke.DurationSeconds), Is.True);

            Assert.That(EnemySenses.VisionBlockers, Has.None.SameAs(zone));
            Assert.That(See(target), Is.True, "the target is seen again once the smoke clears");
        }

        [Test]
        public void SmokeOffToTheSideDoesNotHideTheTarget()
        {
            var target = CreateTarget(new Vector3(0f, 1f, 10f));
            Spread(_smoke, new Vector3(6f, 1f, 5f));

            Assert.That(See(target), Is.True);
        }

        [Test]
        public void SmokeDoesNoDamage()
        {
            var target = CreateTarget(new Vector3(0f, 1f, 5f));
            var zone = Spread(_smoke, target.transform.position);

            zone.Tick(_smoke.DurationSeconds / 2f);

            Assert.That(target.Health.Current, Is.EqualTo(target.Health.Max));
        }

        [Test]
        public void AnAreaThatHurtsDoesNotBlockSight()
        {
            var target = CreateTarget(new Vector3(0f, 1f, 10f));
            var zone = Spread(_fire, new Vector3(0f, 1f, 5f));

            Assert.That(zone.BlocksSight, Is.False);
            Assert.That(EnemySenses.VisionBlockers, Has.None.SameAs(zone));
            Assert.That(See(target), Is.True);
        }

        [Test]
        public void WithoutAVisualTheSmokeIsBuiltFromParticles()
        {
            var zone = Spread(_smoke, Vector3.zero);

            var visual = zone.transform.Find(AreaEffectZone.VisualName);
            Assert.That(visual, Is.Not.Null);
            Assert.That(visual.GetComponent<ParticleSystem>(), Is.Not.Null);
            Assert.That(visual.GetComponent<Collider>(), Is.Null, "smoke lets shots through");
        }

        [Test]
        public void ShownSmokeOnANetClientIsParticlesButDoesNotBlockSight()
        {
            var smokeVisualId = KitTuning.Grenades.Types
                .Select(grenade => grenade.Impact.AreaEffect)
                .First(area => area != null && SmokeScreen.IsSmoke(area))
                .VisualId;
            var zone = AreaEffectZone.Show(smokeVisualId, Vector3.zero, 3f, 5f);
            _zones.Add(zone);

            Assert.That(zone.BlocksSight, Is.False, "the server decides what bots see");
            Assert.That(zone.transform.Find(AreaEffectZone.VisualName).GetComponent<ParticleSystem>(), Is.Not.Null);
        }

        private AreaEffectZone Spread(AreaEffectSpec spec, Vector3 center)
        {
            var zone = AreaEffectZone.Spread(spec, center, null, string.Empty);
            _zones.Add(zone);
            return zone;
        }

        private static bool See(Damageable target)
        {
            return EnemySenses.Perceive(Vector3.zero, _eye, target, 20f, null).HasLineOfSight;
        }

        private Damageable CreateTarget(Vector3 position)
        {
            var target = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            _created.Add(target);
            target.name = "player";
            target.transform.position = position;
            var damageable = target.AddComponent<Damageable>();
            damageable.Initialize("player", 100);
            target.AddComponent<Hitbox>().Initialize(HitZone.Body, damageable);
            Physics.SyncTransforms();
            return damageable;
        }
    }
}
