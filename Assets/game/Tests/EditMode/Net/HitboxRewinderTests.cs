using System;
using System.Collections.Generic;
using Fps.Core;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Fps.Net.Tests
{
    /// <summary>
    /// ラグ補償の当たり判定。<see cref="LagCompensator"/> で巻き戻した位置へコライダーを動かして Raycast し、
    /// 終わったら Transform と物理の両方が元の位置に戻ることを確かめる。
    /// </summary>
    public sealed class HitboxRewinderTests
    {
        private const float _shotRange = 100f;

        private readonly List<GameObject> _created = new List<GameObject>();
        private HitboxRewinder _rewinder;

        [SetUp]
        public void SetUp()
        {
            _rewinder = new HitboxRewinder();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _created)
            {
                if (go != null)
                {
                    Object.DestroyImmediate(go);
                }
            }

            _created.Clear();
            Physics.SyncTransforms();
        }

        [Test]
        public void HitsTargetAtRewoundPositionAndRestoresIt()
        {
            // tick 10 では x=0 にいて、今 (tick 20) は x=5 へ動いている
            var target = CreateCapsule("Target", new Vector3(5f, 0f, 10f));
            var compensator = new LagCompensator();
            compensator.Record(10, 1, new System.Numerics.Vector3(0f, 0f, 10f));
            compensator.Record(20, 1, new System.Numerics.Vector3(5f, 0f, 10f));
            var rewound = compensator.Rewind(10f, 20);

            var hitFound = _rewinder.Raycast(
                Targets(new RewindTarget(1, target.transform, true)),
                rewound,
                Vector3.zero,
                Vector3.forward,
                _shotRange,
                null,
                out var hit);

            Assert.That(hitFound, Is.True);
            Assert.That(hit.HitTarget, Is.True);
            Assert.That(hit.TargetId, Is.EqualTo(1));
            Assert.That(hit.TargetPosition, Is.EqualTo(new Vector3(0f, 0f, 10f)));
            Assert.That(hit.RaycastHit.distance, Is.EqualTo(9.5f).Within(1e-3f));

            // Transform も物理も今の位置へ戻っている
            Assert.That(target.transform.position, Is.EqualTo(new Vector3(5f, 0f, 10f)));
            Assert.That(Physics.Raycast(Vector3.zero, Vector3.forward, _shotRange), Is.False);
            Assert.That(Physics.Raycast(new Vector3(5f, 0f, 0f), Vector3.forward, out var now, _shotRange), Is.True);
            Assert.That(now.collider.gameObject, Is.SameAs(target));
        }

        [Test]
        public void MissesTargetThatOnlyCurrentlyStandsInTheLine()
        {
            var target = CreateCapsule("Target", new Vector3(5f, 0f, 10f));
            var rewound = Positions((1, new Vector3(0f, 0f, 10f)));

            var hitFound = _rewinder.Raycast(
                Targets(new RewindTarget(1, target.transform, true)),
                rewound,
                new Vector3(5f, 0f, 0f),
                Vector3.forward,
                _shotRange,
                null,
                out _);

            Assert.That(hitFound, Is.False);
            Assert.That(target.transform.position, Is.EqualTo(new Vector3(5f, 0f, 10f)));
        }

        [Test]
        public void InterpolatesBetweenRecordedTicks()
        {
            var target = CreateCapsule("Target", new Vector3(8f, 0f, 10f));
            var compensator = new LagCompensator();
            compensator.Record(10, 1, new System.Numerics.Vector3(0f, 0f, 10f));
            compensator.Record(11, 1, new System.Numerics.Vector3(4f, 0f, 10f));
            compensator.Record(12, 1, new System.Numerics.Vector3(8f, 0f, 10f));

            // tick 10.5 では x=2
            var hitFound = _rewinder.Raycast(
                Targets(new RewindTarget(1, target.transform, true)),
                compensator.Rewind(new ShotTime(10, 0.5f).ToTick(), 12),
                new Vector3(2f, 0f, 0f),
                Vector3.forward,
                _shotRange,
                null,
                out var hit);

            Assert.That(hitFound, Is.True);
            Assert.That(hit.TargetId, Is.EqualTo(1));
            Assert.That(hit.TargetPosition.x, Is.EqualTo(2f).Within(1e-4f));
            Assert.That(target.transform.position, Is.EqualTo(new Vector3(8f, 0f, 10f)));
        }

        [Test]
        public void TargetWithoutRewoundPositionIsPassedThrough()
        {
            var target = CreateCapsule("Target", new Vector3(0f, 0f, 10f));
            var wall = CreateCube("Wall", new Vector3(0f, 0f, 20f));

            var hitFound = _rewinder.Raycast(
                Targets(new RewindTarget(1, target.transform, true)),
                Positions(),
                Vector3.zero,
                Vector3.forward,
                _shotRange,
                null,
                out var hit);

            Assert.That(hitFound, Is.True);
            Assert.That(hit.HitTarget, Is.False);
            Assert.That(hit.TargetId, Is.EqualTo(RewindHit.NoTarget));
            Assert.That(hit.RaycastHit.collider.gameObject, Is.SameAs(wall));
            Assert.That(target.transform.position, Is.EqualTo(new Vector3(0f, 0f, 10f)));
        }

        [Test]
        public void UnhittableTargetIsNotMovedAndIsPassedThrough()
        {
            var dead = CreateCapsule("Dead", new Vector3(0f, 0f, 10f));
            var alive = CreateCapsule("Alive", new Vector3(30f, 0f, 15f));
            var rewound = Positions((1, new Vector3(0f, 0f, 10f)), (2, new Vector3(0f, 0f, 15f)));

            var hitFound = _rewinder.Raycast(
                Targets(new RewindTarget(1, dead.transform, false), new RewindTarget(2, alive.transform, true)),
                rewound,
                Vector3.zero,
                Vector3.forward,
                _shotRange,
                null,
                out var hit);

            Assert.That(hitFound, Is.True);
            Assert.That(hit.TargetId, Is.EqualTo(2));
            Assert.That(hit.RaycastHit.distance, Is.EqualTo(14.5f).Within(1e-3f));
            Assert.That(dead.transform.position, Is.EqualTo(new Vector3(0f, 0f, 10f)));
            Assert.That(alive.transform.position, Is.EqualTo(new Vector3(30f, 0f, 15f)));
        }

        [Test]
        public void WallInFrontOfRewoundTargetBlocksTheShot()
        {
            var target = CreateCapsule("Target", new Vector3(30f, 0f, 20f));
            var wall = CreateCube("Wall", new Vector3(0f, 0f, 10f));

            var hitFound = _rewinder.Raycast(
                Targets(new RewindTarget(1, target.transform, true)),
                Positions((1, new Vector3(0f, 0f, 20f))),
                Vector3.zero,
                Vector3.forward,
                _shotRange,
                null,
                out var hit);

            Assert.That(hitFound, Is.True);
            Assert.That(hit.HitTarget, Is.False);
            Assert.That(hit.RaycastHit.collider.gameObject, Is.SameAs(wall));
            Assert.That(target.transform.position, Is.EqualTo(new Vector3(30f, 0f, 20f)));
        }

        [Test]
        public void IgnoresTheShootersOwnColliders()
        {
            var shooter = CreateCapsule("Shooter", Vector3.zero);
            var target = CreateCapsule("Target", new Vector3(0f, 0f, 10f));
            var rewound = Positions((0, Vector3.zero), (1, new Vector3(0f, 0f, 10f)));

            var hitFound = _rewinder.Raycast(
                Targets(new RewindTarget(0, shooter.transform, false), new RewindTarget(1, target.transform, true)),
                rewound,
                Vector3.zero,
                Vector3.forward,
                _shotRange,
                shooter.transform,
                out var hit);

            Assert.That(hitFound, Is.True);
            Assert.That(hit.TargetId, Is.EqualTo(1));
        }

        [Test]
        public void RestoresEveryMovedTargetWhenNothingIsHit()
        {
            var a = CreateCapsule("A", new Vector3(10f, 0f, 10f));
            var b = CreateCapsule("B", new Vector3(-10f, 0f, 10f));
            var rewound = Positions((1, new Vector3(3f, 0f, 10f)), (2, new Vector3(-3f, 0f, 10f)));

            var hitFound = _rewinder.Raycast(
                Targets(new RewindTarget(1, a.transform, true), new RewindTarget(2, b.transform, true)),
                rewound,
                Vector3.zero,
                Vector3.forward,
                _shotRange,
                null,
                out _);

            Assert.That(hitFound, Is.False);
            Assert.That(a.transform.position, Is.EqualTo(new Vector3(10f, 0f, 10f)));
            Assert.That(b.transform.position, Is.EqualTo(new Vector3(-10f, 0f, 10f)));
            Assert.That(Physics.Raycast(new Vector3(10f, 0f, 0f), Vector3.forward, _shotRange), Is.True);
            Assert.That(Physics.Raycast(new Vector3(-10f, 0f, 0f), Vector3.forward, _shotRange), Is.True);
        }

        [Test]
        public void RejectsInvalidArgumentsWithoutMovingAnything()
        {
            var target = CreateCapsule("Target", new Vector3(5f, 0f, 10f));
            var targets = Targets(new RewindTarget(1, target.transform, true));
            var rewound = Positions((1, Vector3.zero));

            Assert.Throws<ArgumentException>(() => _rewinder.Raycast(targets, rewound, Vector3.zero, Vector3.zero, _shotRange, null, out _));
            Assert.Throws<ArgumentException>(
                () => _rewinder.Raycast(targets, rewound, new Vector3(float.NaN, 0f, 0f), Vector3.forward, _shotRange, null, out _));
            Assert.Throws<ArgumentOutOfRangeException>(() => _rewinder.Raycast(targets, rewound, Vector3.zero, Vector3.forward, 0f, null, out _));
            Assert.Throws<ArgumentNullException>(() => _rewinder.Raycast(null, rewound, Vector3.zero, Vector3.forward, _shotRange, null, out _));
            Assert.Throws<ArgumentNullException>(() => _rewinder.Raycast(targets, null, Vector3.zero, Vector3.forward, _shotRange, null, out _));
            Assert.Throws<ArgumentException>(
                () => _rewinder.Raycast(
                    Targets(new RewindTarget(RewindHit.NoTarget, target.transform, true)),
                    Positions((RewindHit.NoTarget, Vector3.zero)),
                    Vector3.zero,
                    Vector3.forward,
                    _shotRange,
                    null,
                    out _));

            Assert.That(target.transform.position, Is.EqualTo(new Vector3(5f, 0f, 10f)));
        }

        private GameObject CreateCapsule(string name, Vector3 position)
        {
            // プリミティブの Capsule は高さ 2・半径 0.5 の CapsuleCollider を持つ
            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = name;
            go.transform.position = position;
            _created.Add(go);
            Physics.SyncTransforms();
            return go;
        }

        private GameObject CreateCube(string name, Vector3 position)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.position = position;
            go.transform.localScale = new Vector3(4f, 4f, 1f);
            _created.Add(go);
            Physics.SyncTransforms();
            return go;
        }

        private static IReadOnlyList<RewindTarget> Targets(params RewindTarget[] targets)
        {
            return targets;
        }

        private static IReadOnlyDictionary<int, System.Numerics.Vector3> Positions(params (int Id, Vector3 Position)[] positions)
        {
            var result = new Dictionary<int, System.Numerics.Vector3>();
            foreach (var (id, position) in positions)
            {
                result.Add(id, NetMath.ToNumerics(position));
            }

            return result;
        }
    }
}
