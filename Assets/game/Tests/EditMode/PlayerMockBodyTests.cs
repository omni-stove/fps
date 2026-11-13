using System;
using Fps.Core;
using Fps.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Fps.Tests.EditMode
{
    public sealed class PlayerMockBodyTests
    {
        private const float Tolerance = 1e-4f;
        private const float FeetY = -1f;

        private GameObject _player;
        private Transform _camera;
        private PlayerMockBody _mock;

        [SetUp]
        public void SetUp()
        {
            // GameBootstrap と同じく、本体はプリミティブのカプセル (自分のレンダラーを持つ) > 目 > カメラ。
            _player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            _player.name = "Player";
            var eye = new GameObject("Eye").transform;
            eye.SetParent(_player.transform, false);
            eye.localPosition = new Vector3(0f, 0.6f, 0f);
            _camera = new GameObject("Camera").transform;
            _camera.SetParent(eye, false);
            _camera.gameObject.AddComponent<Camera>();
            _mock = _player.AddComponent<PlayerMockBody>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_mock != null)
            {
                _mock.Dismantle();
            }

            Object.DestroyImmediate(_player);
        }

        private static PlayerActionState State(PlayerActionConditions flags, ViewMode view)
        {
            var reload = (flags & PlayerActionConditions.Reloading) != 0 ? 0.5f : 0f;
            return new PlayerActionState(flags, reload, null, ShoulderSide.Right, view, "Rifle", 0f);
        }

        private void Apply(PlayerActionConditions flags, ViewMode view, float deltaSeconds = 10f)
        {
            _mock.Apply(State(flags, view), Vector3.zero, Vector3.zero, Quaternion.identity, FeetY, deltaSeconds);
        }

        [Test]
        public void BuildPutsTheBodyOnThePlayerAndTheGunOnTheCameraWithoutColliders()
        {
            _mock.Build(_camera);

            Assert.That(_mock.IsBuilt, Is.True);
            Assert.That(_mock.BodyRoot.parent, Is.SameAs(_player.transform));
            Assert.That(_mock.ViewModelRoot.parent, Is.SameAs(_camera));
            Assert.That(_mock.Torso.GetComponent<MeshFilter>(), Is.Not.Null);
            Assert.That(_mock.Head.GetComponent<MeshFilter>(), Is.Not.Null);
            Assert.That(_mock.BodyGun.GetComponentInChildren<MeshRenderer>(), Is.Not.Null);
            Assert.That(_mock.ViewModelGun.GetComponentInChildren<MeshRenderer>(), Is.Not.Null);
            Assert.That(_mock.BodyRoot.GetComponentsInChildren<Collider>(true), Is.Empty);
            Assert.That(_mock.ViewModelRoot.GetComponentsInChildren<Collider>(true), Is.Empty);
        }

        [Test]
        public void BodyAndGunUseDistinctFlatColors()
        {
            _mock.Build(_camera);

            var body = _mock.Torso.GetComponent<Renderer>().sharedMaterial;
            var gun = _mock.BodyGun.GetComponentInChildren<Renderer>().sharedMaterial;
            Assert.That(body.color, Is.EqualTo(PlayerMockBody.BodyColor));
            Assert.That(gun.color, Is.EqualTo(PlayerMockBody.GunColor));
            Assert.That(_mock.Head.GetComponent<Renderer>().sharedMaterial, Is.SameAs(body));
            Assert.That(_mock.ViewModelGun.GetComponentInChildren<Renderer>().sharedMaterial, Is.SameAs(gun));
        }

        [Test]
        public void BuildHidesThePlayersOwnCapsuleAndDismantleRestoresIt()
        {
            var capsule = _player.GetComponent<Renderer>();

            _mock.Build(_camera);
            Assert.That(capsule.enabled, Is.False, "the mock body replaces the capsule");

            _mock.Dismantle();
            Assert.That(capsule.enabled, Is.True);
            Assert.That(_mock.IsBuilt, Is.False);
            Assert.That(_player.transform.Find(PlayerMockBody.BodyRootName), Is.Null);
            Assert.That(_camera.Find(PlayerMockBody.ViewModelRootName), Is.Null);
        }

        [Test]
        public void ThirdPersonShowsTheBodyAndHidesTheViewModel()
        {
            _mock.Build(_camera);

            Apply(PlayerActionConditions.Grounded, ViewMode.ThirdPerson);

            Assert.That(_mock.BodyRoot.gameObject.activeSelf, Is.True);
            Assert.That(_mock.Torso.GetComponent<Renderer>().shadowCastingMode, Is.EqualTo(ShadowCastingMode.On));
            Assert.That(_mock.ViewModelRoot.gameObject.activeSelf, Is.False);
            Assert.That(_mock.BodyRoot.localPosition, Is.EqualTo(new Vector3(0f, FeetY, 0f)));
        }

        [Test]
        public void FirstPersonLeavesOnlyTheBodysShadowAndShowsTheViewModel()
        {
            _mock.Build(_camera);

            Apply(PlayerActionConditions.Grounded, ViewMode.FirstPerson);

            Assert.That(_mock.Torso.GetComponent<Renderer>().shadowCastingMode, Is.EqualTo(ShadowCastingMode.ShadowsOnly));
            Assert.That(_mock.Head.GetComponent<Renderer>().shadowCastingMode, Is.EqualTo(ShadowCastingMode.ShadowsOnly));
            Assert.That(_mock.ViewModelRoot.gameObject.activeSelf, Is.True);
            var viewRenderer = _mock.ViewModelGun.GetComponentInChildren<Renderer>();
            Assert.That(viewRenderer.shadowCastingMode, Is.EqualTo(ShadowCastingMode.Off));
            Assert.That(_mock.ViewModelGun.localPosition, Is.EqualTo(PlayerMockPose.ViewHipPosition));
        }

        [Test]
        public void ViewModelIsFixedEvenAfterTheRigRewritesEveryRenderer()
        {
            _mock.Build(_camera);
            Apply(PlayerActionConditions.Grounded, ViewMode.FirstPerson);

            // ViewRig.ApplyBodyVisibility は本体の子孫のレンダラーをまとめて影だけにする。
            foreach (var bodyRenderer in _player.GetComponentsInChildren<Renderer>(true))
            {
                bodyRenderer.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
            }

            Apply(PlayerActionConditions.Grounded, ViewMode.FirstPerson);

            Assert.That(_mock.ViewModelGun.GetComponentInChildren<Renderer>().shadowCastingMode, Is.EqualTo(ShadowCastingMode.Off));
        }

        [Test]
        public void CrouchShrinksTheTorso()
        {
            _mock.Build(_camera);

            Apply(PlayerActionConditions.Grounded, ViewMode.ThirdPerson);
            var standing = _mock.Torso.localScale.y;
            Apply(PlayerActionConditions.Crouching | PlayerActionConditions.Grounded, ViewMode.ThirdPerson);

            Assert.That(_mock.Torso.localScale.y, Is.EqualTo(standing * PlayerMockPose.CrouchScale).Within(Tolerance));
            Assert.That(_mock.Head.localPosition, Is.EqualTo(PlayerMockPose.Head(PlayerMockPose.CrouchScale)));
        }

        [Test]
        public void PoseBlendsTowardTheTargetOverTime()
        {
            _mock.Build(_camera);
            Apply(PlayerActionConditions.Grounded, ViewMode.FirstPerson);

            Apply(PlayerActionConditions.Aiming, ViewMode.FirstPerson, deltaSeconds: 0f);
            Assert.That(_mock.ViewModelGun.localPosition, Is.EqualTo(PlayerMockPose.ViewHipPosition), "no time, no movement");

            Apply(PlayerActionConditions.Aiming, ViewMode.FirstPerson, deltaSeconds: 0.05f);
            var halfway = _mock.ViewModelGun.localPosition.x;
            Assert.That(halfway, Is.LessThan(PlayerMockPose.ViewHipPosition.x));
            Assert.That(halfway, Is.GreaterThan(PlayerMockPose.ViewAimPosition.x));
        }

        [Test]
        public void DodgeLeansTheBodyTowardTheVelocity()
        {
            _mock.Build(_camera);
            _player.transform.rotation = Quaternion.Euler(0f, 90f, 0f);

            // 本体は右 (+X) を向いているので、ワールドの -Z へ進むと本体から見て右。
            _mock.Apply(
                State(PlayerActionConditions.Dodging, ViewMode.ThirdPerson), Vector3.back * 6f, Vector3.zero, Quaternion.identity, FeetY, 10f);

            Assert.That((_mock.BodyRoot.localRotation * Vector3.up).x, Is.GreaterThan(0.2f));
        }

        [Test]
        public void KickPullsTheViewModelBackAndRecovers()
        {
            _mock.Build(_camera);
            Apply(PlayerActionConditions.Grounded, ViewMode.FirstPerson);

            _mock.AddKick();
            Apply(PlayerActionConditions.Grounded, ViewMode.FirstPerson, deltaSeconds: 0f);
            Assert.That(_mock.Kick, Is.EqualTo(1f));
            Assert.That(
                _mock.ViewModelGun.localPosition.z,
                Is.EqualTo(PlayerMockPose.ViewHipPosition.z - PlayerMockPose.ViewKickDistance).Within(Tolerance));

            Apply(PlayerActionConditions.Grounded, ViewMode.FirstPerson, deltaSeconds: 1f);
            Assert.That(_mock.Kick, Is.Zero);
            Assert.That(_mock.ViewModelGun.localPosition.z, Is.EqualTo(PlayerMockPose.ViewHipPosition.z).Within(Tolerance));
        }

        [Test]
        public void ApplyBeforeBuildThrows()
        {
            Assert.Throws<InvalidOperationException>(() => Apply(PlayerActionConditions.Grounded, ViewMode.ThirdPerson));
        }

        [Test]
        public void BuildRejectsACameraOutsideThePlayer()
        {
            var stray = new GameObject("Stray");
            try
            {
                Assert.Throws<ArgumentException>(() => _mock.Build(stray.transform));
                Assert.Throws<ArgumentNullException>(() => _mock.Build(null));
                Assert.That(_mock.IsBuilt, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(stray);
            }
        }
    }
}
