using System;
using System.Collections.Generic;
using Shooter.Core;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    public sealed class EnemySensesVisionBlockerTests
    {
        private static readonly Vector3 _eye = new Vector3(0f, 1.6f, 0f);

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
        public void ARegisteredBlockerHidesTheTarget()
        {
            var target = CreateTarget(new Vector3(0f, 1f, 10f));
            Assert.That(EnemySenses.Perceive(Vector3.zero, _eye, target, 20f, null).HasLineOfSight, Is.True);

            var blocker = new FakeBlocker { Blocking = true };
            Register(blocker);
            var blocked = EnemySenses.Perceive(Vector3.zero, _eye, target, 20f, null);

            Assert.That(blocked.HasLineOfSight, Is.False);
            Assert.That(blocked.Target.HasValue, Is.True, "the brain still knows where the target is, it just cannot see it");
            Assert.That(blocker.LastEye, Is.EqualTo(_eye));
            Assert.That(blocker.LastTarget, Is.EqualTo(target.AimPoint));
        }

        [Test]
        public void ABlockerThatDoesNotBlockLeavesSightAlone()
        {
            var target = CreateTarget(new Vector3(0f, 1f, 10f));
            Register(new FakeBlocker { Blocking = false });

            Assert.That(EnemySenses.Perceive(Vector3.zero, _eye, target, 20f, null).HasLineOfSight, Is.True);
        }

        [Test]
        public void UnregisteringABlockerRestoresSight()
        {
            var target = CreateTarget(new Vector3(0f, 1f, 10f));
            var blocker = new FakeBlocker { Blocking = true };
            Register(blocker);

            Assert.That(EnemySenses.RegisterVisionBlocker(blocker), Is.False, "the same blocker is registered once");
            Assert.That(EnemySenses.UnregisterVisionBlocker(blocker), Is.True);
            Assert.That(EnemySenses.Perceive(Vector3.zero, _eye, target, 20f, null).HasLineOfSight, Is.True);
            Assert.Throws<ArgumentNullException>(() => EnemySenses.RegisterVisionBlocker(null));
        }

        private void Register(IVisionBlocker blocker)
        {
            Assert.That(EnemySenses.RegisterVisionBlocker(blocker), Is.True);
            _registered.Add(blocker);
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
