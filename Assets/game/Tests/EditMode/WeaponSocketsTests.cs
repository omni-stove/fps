using System;
using System.Collections.Generic;
using Fps.Runtime;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Fps.Tests.EditMode
{
    /// <summary><see cref="WeaponSockets"/> が武器のモデルの配下からソケットを名前で探し、無ければ既定の位置を返すことを確かめる。</summary>
    public sealed class WeaponSocketsTests
    {
        private readonly List<Object> _created = new List<Object>();

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
        public void SocketsAreFoundByNameAnywhereUnderTheModel()
        {
            var model = Create("Rifle", null);
            var body = Create("Body", model.transform);
            var muzzle = Create(WeaponSockets.MuzzleName, Create("Barrel", body.transform).transform);
            var eject = Create(WeaponSockets.ShellEjectName, body.transform);
            eject.SetActive(false);

            var sockets = new WeaponSockets(model.transform);

            Assert.That(sockets.ModelRoot, Is.SameAs(model.transform));
            Assert.That(sockets.Muzzle, Is.SameAs(muzzle.transform));
            Assert.That(sockets.ShellEject, Is.SameAs(eject.transform), "inactive sockets are found too");
        }

        [Test]
        public void SocketPosesFollowTheSocketTransforms()
        {
            var model = Create("Rifle", null);
            var muzzle = Create(WeaponSockets.MuzzleName, model.transform);
            muzzle.transform.SetPositionAndRotation(new Vector3(1f, 2f, 3f), Quaternion.Euler(10f, 20f, 30f));
            var eject = Create(WeaponSockets.ShellEjectName, model.transform);
            eject.transform.SetPositionAndRotation(new Vector3(-1f, 0f, 4f), Quaternion.Euler(0f, 90f, 0f));
            var sockets = new WeaponSockets(model.transform);

            var muzzlePose = sockets.MuzzlePose(Vector3.zero, Vector3.forward);
            var ejectPose = sockets.ShellEjectPose(Vector3.zero, Vector3.forward);

            AssertNear(muzzlePose.position, muzzle.transform.position);
            Assert.That(Quaternion.Angle(muzzlePose.rotation, muzzle.transform.rotation), Is.LessThan(0.01f));
            AssertNear(ejectPose.position, eject.transform.position);
            Assert.That(Quaternion.Angle(ejectPose.rotation, eject.transform.rotation), Is.LessThan(0.01f));
        }

        [Test]
        public void MissingSocketsFallBackToTheDefaultOffsets()
        {
            var model = Create("Rifle", null);
            Create("Socket_muzzle", model.transform);
            var sockets = new WeaponSockets(model.transform);

            Assert.That(sockets.Muzzle, Is.Null, "names are compared case-sensitively");
            Assert.That(sockets.ShellEject, Is.Null);

            var origin = new Vector3(0f, 1.6f, 0f);
            var muzzle = sockets.MuzzlePose(origin, Vector3.forward);
            AssertNear(muzzle.position, origin + WeaponSockets.DefaultMuzzleOffset);
            AssertNear(muzzle.rotation * Vector3.forward, Vector3.forward);

            var eject = sockets.ShellEjectPose(origin, Vector3.forward);
            AssertNear(eject.position, origin + WeaponSockets.DefaultShellEjectOffset);
            AssertNear(eject.rotation * Vector3.forward, Vector3.right, "shells fly to the shooter's right");
        }

        [Test]
        public void NoModelBehavesLikeNone()
        {
            var sockets = new WeaponSockets(null);

            Assert.That(sockets.ModelRoot, Is.Null);
            Assert.That(sockets.Muzzle, Is.Null);
            AssertNear(sockets.MuzzlePose(Vector3.zero, Vector3.forward).position, WeaponSockets.None.MuzzlePose(Vector3.zero, Vector3.forward).position);
        }

        [Test]
        public void DestroyedSocketFallsBackToTheDefault()
        {
            var model = Create("Rifle", null);
            var muzzle = Create(WeaponSockets.MuzzleName, model.transform);
            muzzle.transform.position = new Vector3(9f, 9f, 9f);
            var sockets = new WeaponSockets(model.transform);

            Object.DestroyImmediate(muzzle);

            AssertNear(sockets.MuzzlePose(Vector3.zero, Vector3.forward).position, WeaponSockets.DefaultMuzzleOffset);
        }

        [Test]
        public void DefaultOffsetsTurnWithTheShotAndOnlyMoveForwardWhenShootingStraightUp()
        {
            var right = WeaponSockets.OffsetPosition(Vector3.zero, Vector3.right, WeaponSockets.DefaultMuzzleOffset);
            var offset = WeaponSockets.DefaultMuzzleOffset;
            AssertNear(right, new Vector3(offset.z, offset.y, -offset.x), "facing +X, the shooter's right is -Z");

            var up = WeaponSockets.OffsetPosition(Vector3.zero, Vector3.up, offset);
            AssertNear(up, Vector3.up * offset.z);

            var zero = WeaponSockets.OffsetPosition(Vector3.zero, Vector3.zero, offset);
            AssertNear(zero, offset, "a zero direction is treated as forward");
        }

        [Test]
        public void TheDefaultMuzzleMatchesThePresentationMuzzle()
        {
            Assert.That(WeaponPresentation.MuzzleOffset, Is.EqualTo(WeaponSockets.DefaultMuzzleOffset));
            AssertNear(
                WeaponPresentation.MuzzlePosition(Vector3.one, Vector3.left),
                WeaponSockets.None.MuzzlePose(Vector3.one, Vector3.left).position);
        }

        [Test]
        public void FindSocketRejectsAnEmptyNameAndAcceptsNoRoot()
        {
            Assert.Throws<ArgumentException>(() => WeaponSockets.FindSocket(null, string.Empty));
            Assert.That(WeaponSockets.FindSocket(null, WeaponSockets.MuzzleName), Is.Null);

            var root = Create(WeaponSockets.MuzzleName, null);
            Assert.That(WeaponSockets.FindSocket(root.transform, WeaponSockets.MuzzleName), Is.SameAs(root.transform), "the root itself counts");
        }

        private GameObject Create(string name, Transform parent)
        {
            var go = new GameObject(name);
            _created.Add(go);
            if (parent != null)
            {
                go.transform.SetParent(parent, false);
            }

            return go;
        }

        private static void AssertNear(Vector3 actual, Vector3 expected, string message = null)
        {
            Assert.That(Vector3.Distance(actual, expected), Is.LessThan(1e-4f), message ?? $"expected {expected} but was {actual}");
        }
    }
}
