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
    public sealed class ProjectileDeliveryTests
    {
        private const float _step = 0.02f;

        private static readonly Vector3 _eye = new Vector3(0f, 1f, 0f);

        private readonly List<GameObject> _created = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (var body in Object.FindObjectsByType<ProjectileBody>(FindObjectsInactive.Include))
            {
                Object.DestroyImmediate(body.gameObject);
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
        public void TheRoundHitsTheTargetOnlyAfterItHasFlownThere()
        {
            var target = CreateTarget("dummy", new Vector3(0f, 1f, 10f));
            var landed = new List<ShotOutcome>();
            var shots = new List<ShotFiredInfo>();
            void Record(ShotFiredInfo shot) => shots.Add(shot);
            CombatEvents.ShotFired += Record;
            try
            {
                var outcome = ShotDeliveries.Deliver(Request(Projectile(50f, 0f, 5f), 10, landed.Add), Straight());

                Assert.That(outcome.Hit, Is.False, "nothing is hit at the moment of the shot");
                Assert.That(target.Health.Current, Is.EqualTo(100));
                Assert.That(shots, Has.Count.EqualTo(1));
                Assert.That(shots[0].Tracer, Is.False, "the round itself is seen, so no straight tracer is drawn");
            }
            finally
            {
                CombatEvents.ShotFired -= Record;
            }

            var body = Object.FindAnyObjectByType<ProjectileBody>();
            Assert.That(body, Is.Not.Null);
            body.Tick(0.1f);
            Assert.That(target.Health.Current, Is.EqualTo(100), "5 m in, the round is still on its way");

            LogAssert.Expect(LogType.Log, new System.Text.RegularExpressions.Regex(@"^\[Combat\] projectile weapon=test-crossbow end=HitTarget .* hit=dummy dmg=10$"));
            TickUntilGone(body);

            Assert.That(target.Health.Current, Is.EqualTo(90));
            Assert.That(landed, Has.Count.EqualTo(1));
            Assert.That(landed[0].Owner, Is.SameAs(target));
            Assert.That(landed[0].Damage, Is.EqualTo(10));
        }

        [Test]
        public void ARoundUnderGravityLandsBelowWhereItWasAimed()
        {
            CreateWall(new Vector3(0f, 1f, 20.5f), new Vector3(20f, 40f, 1f));
            var body = ProjectileBody.Launch(Request(Projectile(20f, 1f, 5f, WorldHitResponse.Stick), 10), Straight());

            var drop = -Physics.gravity.y * 0.5f;
            for (var i = 0; i < 1000 && !body.IsStuck; i++)
            {
                body.Tick(_step);
            }

            Assert.That(body.IsStuck, Is.True);
            Assert.That(body.transform.position.z, Is.EqualTo(20f).Within(0.01f));
            Assert.That(body.transform.position.y, Is.EqualTo(_eye.y - drop).Within(0.05f), "1 s of flight drops g/2");
        }

        [Test]
        public void AStuckRoundStaysForItsStuckSecondsThenGoes()
        {
            CreateWall(new Vector3(0f, 1f, 10.5f), new Vector3(4f, 4f, 1f));
            var look = new ProjectileSettings { StuckSeconds = 2f };
            var body = ProjectileBody.Launch(Request(Projectile(50f, 0f, 5f, WorldHitResponse.Stick), 10, look: look), Straight());

            for (var i = 0; i < 20 && !body.IsStuck; i++)
            {
                body.Tick(0.05f);
            }

            Assert.That(body.IsStuck, Is.True);
            Assert.That(body.transform.position.z, Is.EqualTo(10f).Within(0.01f));
            Assert.That(body.transform.forward.z, Is.EqualTo(1f).Within(1e-3f), "the round points the way it flew");

            body.Tick(1.5f);
            Assert.That(body != null, Is.True, "still stuck in the wall");
            body.Tick(0.6f);
            Assert.That(body == null, Is.True);
        }

        [Test]
        public void AStoppingRoundGoesAtTheWall()
        {
            CreateWall(new Vector3(0f, 1f, 10.5f), new Vector3(4f, 4f, 1f));
            var body = ProjectileBody.Launch(Request(Projectile(50f, 0f, 5f), 10), Straight());

            LogAssert.Expect(LogType.Log, new System.Text.RegularExpressions.Regex(@"end=Stopped .* hit=none dmg=0$"));
            TickUntilGone(body);
        }

        [Test]
        public void ARoundThatHitsNothingGoesWhenItsLifetimeEnds()
        {
            var body = ProjectileBody.Launch(Request(Projectile(10f, 0f, 0.3f), 10), Straight());

            body.Tick(0.2f);
            Assert.That(body != null, Is.True);
            LogAssert.Expect(LogType.Log, new System.Text.RegularExpressions.Regex(@"end=Expired .* age=0\.30 "));
            body.Tick(0.2f);
            Assert.That(body == null, Is.True);
        }

        [Test]
        public void TheShooterIsNotHitByItsOwnRound()
        {
            var shooter = CreateTarget("shooter", new Vector3(0f, 1f, 2f));
            var target = CreateTarget("target", new Vector3(0f, 1f, 6f));
            var request = new ShotRequest(
                shooter.gameObject,
                shooter.transform,
                null,
                new RoundSpec(RoundDelivery.Projectile, projectile: Projectile(50f, 0f, 5f)),
                new ImpactSpec(10),
                "test-crossbow",
                Physics.DefaultRaycastLayers);

            var body = ProjectileBody.Launch(request, Straight());
            TickUntilGone(body);

            Assert.That(shooter.Health.Current, Is.EqualTo(100));
            Assert.That(target.Health.Current, Is.EqualTo(90));
        }

        [Test]
        public void TheLookComesFromTheWeaponAndARodIsBuiltOnlyWhenItIsEmpty()
        {
            var rod = ProjectileBody.Launch(Request(Projectile(10f, 0f, 5f), 10), Straight());
            Assert.That(rod.transform.Find(ProjectileBody.RodName), Is.Not.Null);
            foreach (var collider in rod.GetComponentsInChildren<Collider>(true))
            {
                Assert.That(collider.enabled, Is.False);
            }

            var visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _created.Add(visual);
            visual.name = "Bolt";
            var look = new ProjectileSettings { Visual = visual };
            var bolt = ProjectileBody.Launch(Request(Projectile(10f, 0f, 5f), 10, look: look), Straight());

            Assert.That(bolt.gameObject, Is.Not.SameAs(visual));
            Assert.That(bolt.transform.Find(ProjectileBody.RodName), Is.Null);
            Assert.That(bolt.GetComponent<MeshFilter>(), Is.Not.Null, "the visual is a copy of the reference");
            Assert.That(bolt.GetComponent<Collider>().enabled, Is.False, "nothing else hits the visual");
            Assert.That(visual.GetComponent<Collider>().enabled, Is.True, "the reference itself is left alone");
        }

        [Test]
        public void ARoundWithoutAProjectileIsRejected()
        {
            Assert.Throws<ArgumentException>(
                () => ProjectileDelivery.Instance.Deliver(
                    new ShotRequest(null, null, null, new RoundSpec(), new ImpactSpec(10), "test-rifle", Physics.DefaultRaycastLayers),
                    Straight()));
            Assert.That(Object.FindAnyObjectByType<ProjectileBody>(), Is.Null);
        }

        private static ProjectileSpec Projectile(
            float speed, float gravityMultiplier, float lifetimeSeconds, WorldHitResponse onWorldHit = WorldHitResponse.Stop)
        {
            return new ProjectileSpec(speed, gravityMultiplier, lifetimeSeconds, onWorldHit);
        }

        private static ShotRequest Request(
            ProjectileSpec projectile, int damage, Action<ShotOutcome> landed = null, ProjectileSettings look = null)
        {
            return new ShotRequest(
                null,
                null,
                null,
                new RoundSpec(RoundDelivery.Projectile, projectile: projectile),
                new ImpactSpec(damage),
                "test-crossbow",
                Physics.DefaultRaycastLayers,
                projectileLook: look,
                landed: landed);
        }

        private static PlannedRound Straight()
        {
            return new PlannedRound(_eye.ToNumerics(), Vector3.forward.ToNumerics(), 0);
        }

        private static void TickUntilGone(ProjectileBody body)
        {
            for (var i = 0; i < 1000 && body != null; i++)
            {
                body.Tick(_step);
            }

            Assert.That(body == null, Is.True, "the round is gone once it has landed");
        }

        private GameObject CreateWall(Vector3 center, Vector3 size)
        {
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _created.Add(wall);
            wall.name = "wall";
            wall.transform.position = center;
            wall.transform.localScale = size;
            Physics.SyncTransforms();
            return wall;
        }

        private Damageable CreateTarget(string id, Vector3 position)
        {
            var target = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            _created.Add(target);
            target.name = id;
            target.transform.position = position;
            var damageable = target.AddComponent<Damageable>();
            damageable.Initialize(id, 100);
            target.AddComponent<Hitbox>().Initialize(HitZone.Body, damageable);
            Physics.SyncTransforms();
            return damageable;
        }
    }
}
