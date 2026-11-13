using System;
using System.Text.RegularExpressions;
using Fps.Core;
using Fps.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Fps.Tests.EditMode
{
    public sealed class DeathBodyPresenterTests
    {
        private static readonly Vector3 Home = new Vector3(2f, 0f, 4f);

        private TargetDummy _dummy;
        private DeathBodyPresenter _presenter;

        [SetUp]
        public void SetUp()
        {
            _dummy = TargetDummy.Create("dummy", Home, Quaternion.identity, null, 50, TargetDummy.DefaultRespawnDelay);
            _presenter = DeathBodyPresenter.Attach(_dummy.gameObject);
        }

        [TearDown]
        public void TearDown()
        {
            // 抜け殻は体の子ではないので、体と別に片付ける
            var shell = _presenter != null ? _presenter.Shell : null;
            if (_dummy != null)
            {
                Object.DestroyImmediate(_dummy.gameObject);
            }

            if (shell != null)
            {
                Object.DestroyImmediate(shell);
            }
        }

        [Test]
        public void CollapseBuildsAShellWithARigidbodyAndNoHitbox()
        {
            _presenter.Bind(_dummy.Damageable, DeathBody.Collapse);
            LogAssert.Expect(LogType.Log, new Regex(@"^\[DeathBody\] collapse id=dummy parts=2 "));

            _dummy.Damageable.Kill();

            var shell = _presenter.Shell;
            Assert.That(shell, Is.Not.Null);
            Assert.That(shell.transform.parent, Is.Null, "the shell does not move with the hidden body");
            Assert.That(shell.transform.position, Is.EqualTo(Home));
            Assert.That(shell.GetComponent<Rigidbody>(), Is.Not.Null);
            Assert.That(shell.GetComponentsInChildren<Hitbox>(true), Is.Empty);
            Assert.That(shell.GetComponentsInChildren<Damageable>(true), Is.Empty);
            Assert.That(shell.GetComponentsInChildren<MeshRenderer>(true), Has.Length.EqualTo(2), "body and head are copied");
            Assert.That(shell.GetComponentsInChildren<Collider>(true), Has.Length.EqualTo(2));
            foreach (var part in shell.GetComponentsInChildren<Transform>(true))
            {
                Assert.That(part.gameObject.layer, Is.EqualTo(DeathBodyPresenter.ShellLayer), $"{part.name} is on the shell layer");
            }

            Assert.That(_presenter.RemainingSeconds, Is.EqualTo(DeathBodyPresenter.DefaultLifetime));
            Assert.That(_presenter.HasShell, Is.True);
        }

        [Test]
        public void CollapseHidesTheBodyAndItsHitboxes()
        {
            _presenter.Bind(_dummy.Damageable, DeathBody.Collapse);
            LogAssert.Expect(LogType.Log, new Regex(@"^\[DeathBody\] collapse "));

            _dummy.Damageable.Kill();

            foreach (var bodyRenderer in _dummy.GetComponentsInChildren<Renderer>(true))
            {
                Assert.That(bodyRenderer.enabled, Is.False, $"{bodyRenderer.name} is hidden");
            }

            foreach (var hitbox in _dummy.GetComponentsInChildren<Collider>(true))
            {
                Assert.That(hitbox.enabled, Is.False, $"{hitbox.name} no longer takes hits");
            }
        }

        [Test]
        public void TheShellLayerIsIgnoredByShots()
        {
            Assert.That(Physics.DefaultRaycastLayers & (1 << DeathBodyPresenter.ShellLayer), Is.Zero);
        }

        [Test]
        public void RespawnRemovesTheShell()
        {
            _presenter.Bind(_dummy.Damageable, DeathBody.Collapse);
            LogAssert.Expect(LogType.Log, new Regex(@"^\[DeathBody\] collapse "));
            _dummy.Damageable.Kill();
            Assert.That(_presenter.HasShell, Is.True);

            LogAssert.Expect(LogType.Log, "[DeathBody] removed id=dummy reason=respawn");
            _dummy.Respawn();

            Assert.That(_presenter.HasShell, Is.False);
            Assert.That(_presenter.Shell, Is.Null);
            Assert.That(_presenter.RemainingSeconds, Is.Zero);
        }

        [Test]
        public void TheShellIsRemovedAfterItsLifetime()
        {
            _presenter.Bind(_dummy.Damageable, DeathBody.Collapse);
            LogAssert.Expect(LogType.Log, new Regex(@"^\[DeathBody\] collapse "));
            _dummy.Damageable.Kill();

            Assert.That(_presenter.Tick(DeathBodyPresenter.DefaultLifetime - 0.1f), Is.False);
            Assert.That(_presenter.HasShell, Is.True);

            LogAssert.Expect(LogType.Log, "[DeathBody] removed id=dummy reason=timeout");
            Assert.That(_presenter.Tick(0.2f), Is.True);
            Assert.That(_presenter.HasShell, Is.False);
            Assert.That(_presenter.Tick(1f), Is.False, "nothing is left to remove");
        }

        [Test]
        public void VanishBuildsNoShell()
        {
            _presenter.Bind(_dummy.Damageable, DeathBody.Vanish);

            _dummy.Damageable.Kill();

            Assert.That(_presenter.HasShell, Is.False);
            Assert.That(_presenter.Present(Vector3.forward), Is.Null);
            Assert.That(CountShells("dummy"), Is.Zero);
        }

        [Test]
        public void VanishLeavesTheBodyToItsOwner()
        {
            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            try
            {
                var damageable = body.AddComponent<Damageable>();
                var presenter = DeathBodyPresenter.Attach(body);
                presenter.Bind(damageable, DeathBody.Vanish);

                damageable.Kill();

                Assert.That(body.GetComponent<Renderer>().enabled, Is.True, "Vanish keeps today's behaviour");
                Assert.That(body.GetComponent<Collider>().enabled, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(body);
            }
        }

        [Test]
        public void TheKillingShotPushesTheShellAwayFromTheShooter()
        {
            _presenter.Bind(_dummy.Damageable, DeathBody.Collapse);
            LogAssert.Expect(LogType.Log, new Regex(@"^\[DeathBody\] collapse "));

            var shooter = Home + new Vector3(-5f, 1.5f, 0f);
            _dummy.Damageable.TakeDamage(_dummy.Damageable.Health.Current, shooter);

            var body = _presenter.Shell.GetComponent<Rigidbody>();
            var horizontal = Vector3.ProjectOnPlane(body.linearVelocity, Vector3.up);
            Assert.That(horizontal.normalized.x, Is.EqualTo(1f).Within(1e-3f), "falls away from the shooter");
            Assert.That(horizontal.magnitude, Is.EqualTo(DeathBodyPresenter.PushSpeed).Within(1e-3f));
            Assert.That(body.angularVelocity.normalized.z, Is.EqualTo(-1f).Within(1e-3f), "the head tips toward +x");
        }

        [Test]
        public void HorizontalDirectionFallsBackToBehindTheBody()
        {
            var body = new GameObject("Facing");
            try
            {
                body.transform.rotation = Quaternion.LookRotation(Vector3.right, Vector3.up);

                Assert.That(DeathBodyPresenter.HorizontalDirection(null, body.transform), Is.EqualTo(Vector3.left));
                Assert.That(DeathBodyPresenter.HorizontalDirection(Vector3.up, body.transform), Is.EqualTo(Vector3.left), "straight down/up has no horizontal part");
                Assert.That(DeathBodyPresenter.HorizontalDirection(new Vector3(0f, -3f, 2f), body.transform), Is.EqualTo(Vector3.forward));
                Assert.That(DeathBodyPresenter.HorizontalDirection(null, null), Is.EqualTo(Vector3.back));
            }
            finally
            {
                Object.DestroyImmediate(body);
            }
        }

        [Test]
        public void CopiesTheBodyEvenIfAnotherListenerDeactivatesItFirst()
        {
            // KillTargetObjective は倒れた敵を非アクティブにする。Presenter より先に購読していても抜け殻は作れる
            _dummy.Damageable.Died += d => d.gameObject.SetActive(false);
            _presenter.Bind(_dummy.Damageable, DeathBody.Collapse);
            LogAssert.Expect(LogType.Log, new Regex(@"^\[DeathBody\] collapse id=dummy parts=2 "));

            _dummy.Damageable.Kill();

            Assert.That(_presenter.HasShell, Is.True);
            Assert.That(_presenter.Shell.activeSelf, Is.True);
        }

        [Test]
        public void DyingAgainReplacesTheOldShell()
        {
            _presenter.Mode = DeathBody.Collapse;
            LogAssert.Expect(LogType.Log, new Regex(@"^\[DeathBody\] collapse "));
            var first = _presenter.Present(null);

            LogAssert.Expect(LogType.Log, "[DeathBody] removed id=dummy reason=replaced");
            LogAssert.Expect(LogType.Log, new Regex(@"^\[DeathBody\] collapse "));
            var second = _presenter.Present(Vector3.left);

            Assert.That(first == null, Is.True, "the first shell is destroyed");
            Assert.That(second, Is.Not.Null);
            Assert.That(_presenter.Shell, Is.SameAs(second));
        }

        [Test]
        public void BindAllAttachesOnePresenterPerTarget()
        {
            var bound = DeathBodyPresenter.BindAll(new[] { _dummy.Damageable, null, _dummy.Damageable }, DeathBody.Collapse);

            Assert.That(bound, Is.EqualTo(2));
            Assert.That(_dummy.GetComponents<DeathBodyPresenter>(), Has.Length.EqualTo(1));
            Assert.That(_presenter.Mode, Is.EqualTo(DeathBody.Collapse));
            Assert.That(_presenter.Target, Is.SameAs(_dummy.Damageable));

            // 同じ相手に繋ぎ直しても購読は 1 本だけで、抜け殻も 1 つだけ
            LogAssert.Expect(LogType.Log, new Regex(@"^\[DeathBody\] collapse "));
            _dummy.Damageable.Kill();
            Assert.That(CountShells("dummy"), Is.EqualTo(1));
        }

        [Test]
        public void TickForgetsAShellDestroyedFromOutside()
        {
            _presenter.Bind(_dummy.Damageable, DeathBody.Collapse);
            LogAssert.Expect(LogType.Log, new Regex(@"^\[DeathBody\] collapse "));
            _dummy.Damageable.Kill();

            Object.DestroyImmediate(_presenter.Shell);

            Assert.That(_presenter.HasShell, Is.False);
            Assert.That(_presenter.Tick(10f), Is.False);
            Assert.That(_presenter.RemainingSeconds, Is.Zero);
        }

        [Test]
        public void RejectsInvalidSettings()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => _presenter.Mode = (DeathBody)99);
            Assert.Throws<ArgumentOutOfRangeException>(() => _presenter.Lifetime = 0f);
            Assert.Throws<ArgumentOutOfRangeException>(() => _presenter.Lifetime = float.PositiveInfinity);
            Assert.Throws<ArgumentOutOfRangeException>(() => _presenter.Tick(-1f));
            Assert.Throws<ArgumentNullException>(() => _presenter.Bind(null, DeathBody.Collapse));
            Assert.Throws<ArgumentNullException>(() => DeathBodyPresenter.Attach(null));
            Assert.Throws<ArgumentNullException>(() => DeathBodyPresenter.BindAll(null, DeathBody.Collapse));
        }

        private static int CountShells(string id)
        {
            var count = 0;
            foreach (var body in Object.FindObjectsByType<Rigidbody>(FindObjectsInactive.Include))
            {
                if (body.name == id + DeathBodyPresenter.ShellSuffix)
                {
                    count++;
                }
            }

            return count;
        }
    }
}
