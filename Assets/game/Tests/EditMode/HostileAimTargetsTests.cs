using System;
using System.Collections.Generic;
using Shooter.Core;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEngine;
using NVector3 = System.Numerics.Vector3;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    public sealed class HostileAimTargetsTests
    {
        private static readonly Vector3 _eye = new Vector3(0f, 1f, 0f);

        private readonly List<GameObject> _created = new List<GameObject>();
        private readonly List<IVisionBlocker> _registered = new List<IVisionBlocker>();

        [TearDown]
        public void TearDown()
        {
            foreach (var blocker in _registered)
            {
                EnemySenses.UnregisterVisionBlocker(blocker);
            }

            _registered.Clear();
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
        public void CollectsLivingVisibleHostilesButNotTheShooterAlliesTheDeadTheDownedOrOnesBehindAWall()
        {
            var shooter = CreateBody("shooter", _eye, 0);
            var seen = CreateBody("seen", new Vector3(0f, 1f, 10f), 1);
            CreateBody("ally", new Vector3(-2.5f, 1f, 10f), 0);
            CreateBody("hidden", new Vector3(5f, 1f, 10f), 1);
            CreateWall(new Vector3(2.5f, 1f, 5f), new Vector3(2f, 4f, 0.5f));
            CreateBody("dead", new Vector3(-5f, 1f, 10f), 1).Kill();
            CreateBody("downed", new Vector3(-7.5f, 1f, 10f), 1).SetDowned(true);
            CreateBody("far", new Vector3(0f, 1f, 80f), 1);

            var found = Collect(shooter, 50f);

            Assert.That(found, Has.Count.EqualTo(1));
            Assert.That(found[0].ToUnity(), Is.EqualTo(seen.AimPoint));
        }

        [Test]
        public void ATargetWithoutASideIsHostile()
        {
            var shooter = CreateBody("shooter", _eye, 0);
            var stranger = CreateBody("stranger", new Vector3(0f, 1f, 10f), null);

            var found = Collect(shooter, 50f);

            Assert.That(found, Has.Count.EqualTo(1));
            Assert.That(found[0].ToUnity(), Is.EqualTo(stranger.AimPoint));
        }

        [Test]
        public void AShooterWithoutASideTakesEveryoneElse()
        {
            var shooter = CreateBody("shooter", _eye, null);
            CreateBody("a", new Vector3(0f, 1f, 10f), 0);
            CreateBody("b", new Vector3(3f, 1f, 10f), 1);

            Assert.That(Collect(shooter, 50f), Has.Count.EqualTo(2));
        }

        [Test]
        public void ATargetInSmokeIsLeftOut()
        {
            var shooter = CreateBody("shooter", _eye, 0);
            var target = CreateBody("target", new Vector3(0f, 1f, 10f), 1);
            var smoke = new FakeBlocker { Blocking = true };
            Register(smoke);

            Assert.That(Collect(shooter, 50f), Is.Empty);
            Assert.That(smoke.LastEye, Is.EqualTo(_eye));
            Assert.That(smoke.LastTarget, Is.EqualTo(target.AimPoint));

            smoke.Blocking = false;
            Assert.That(Collect(shooter, 50f), Has.Count.EqualTo(1), "once the smoke clears the target counts again");
        }

        [Test]
        public void CollectingThroughTheSharedInterfaceGivesTheSamePoints()
        {
            var shooter = CreateBody("shooter", _eye, 0);
            var seen = CreateBody("seen", new Vector3(0f, 1f, 10f), 1);
            IAimAssistTargets targets = new HostileAimTargets(shooter.transform, Physics.DefaultRaycastLayers);

            var found = new List<NVector3>();
            targets.Collect(_eye.ToNumerics(), 50f, found);

            Assert.That(found, Has.Count.EqualTo(1));
            Assert.That(found[0].ToUnity(), Is.EqualTo(seen.AimPoint));
        }

        [Test]
        public void RejectsBadArguments()
        {
            var targets = new HostileAimTargets(null, Physics.DefaultRaycastLayers);

            Assert.Throws<ArgumentNullException>(() => targets.Collect(_eye, 10f, null));
            Assert.Throws<ArgumentOutOfRangeException>(() => targets.Collect(_eye, 0f, new List<NVector3>()));
            Assert.Throws<ArgumentOutOfRangeException>(() => targets.Collect(_eye, float.PositiveInfinity, new List<NVector3>()));
        }

        private static List<NVector3> Collect(Damageable shooter, float range)
        {
            var found = new List<NVector3>();
            new HostileAimTargets(shooter.transform, Physics.DefaultRaycastLayers).Collect(_eye, range, found);
            return found;
        }

        private void Register(IVisionBlocker blocker)
        {
            Assert.That(EnemySenses.RegisterVisionBlocker(blocker), Is.True);
            _registered.Add(blocker);
        }

        private void CreateWall(Vector3 center, Vector3 size)
        {
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _created.Add(wall);
            wall.name = "wall";
            wall.transform.position = center;
            wall.transform.localScale = size;
            Physics.SyncTransforms();
        }

        private Damageable CreateBody(string id, Vector3 position, int? side)
        {
            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            _created.Add(body);
            body.name = id;
            body.transform.position = position;
            var damageable = body.AddComponent<Damageable>();
            damageable.Initialize(id, 100);
            body.AddComponent<Hitbox>().Initialize(HitZone.Body, damageable);
            if (side.HasValue)
            {
                body.AddComponent<SideMember>().Side = new Side(side.Value);
            }

            Physics.SyncTransforms();
            return damageable;
        }

        private sealed class FakeBlocker : IVisionBlocker
        {
            public bool Blocking { get; set; }

            public Vector3 LastEye { get; private set; }

            public Vector3 LastTarget { get; private set; }

            public bool Blocks(Vector3 eye, Vector3 target)
            {
                LastEye = eye;
                LastTarget = target;
                return Blocking;
            }
        }
    }
}
