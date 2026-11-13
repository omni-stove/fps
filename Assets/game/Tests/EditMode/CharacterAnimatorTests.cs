using System;
using System.Text.RegularExpressions;
using Fps.Core;
using Fps.Editor;
using Fps.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Fps.Tests.EditMode
{
    /// <summary>
    /// CharacterAnimator が読み元 (プレイヤー・敵) の状態を変えるとパラメーターを変えること、リグを足元に置いてカプセルと
    /// 自機モックの体を隠すこと、一人称では影だけにすることを確かめる。Animator を動かす部分 (クリップの再生) は Play で確かめる。
    /// </summary>
    public sealed class CharacterAnimatorTests
    {
        private const float DeltaSeconds = 0.016f;

        private GameObject _player;
        private Camera _camera;
        private Transform _eye;
        private PlayerController _mover;
        private Damageable _damageable;
        private AimDownSights _ads;
        private CharacterAnimator _animator;
        private InputAction _move;
        private InputAction _sprint;
        private InputAction _crouch;
        private InputAction _jump;
        private InputAction _aim;
        private GameObject _rigPrefab;

        [SetUp]
        public void SetUp()
        {
            // GameBootstrap と同じく、本体はプリミティブのカプセル (自分のレンダラーを持つ) で、当たりは CharacterController。
            _player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            _player.name = "Player";
            Object.DestroyImmediate(_player.GetComponent<CapsuleCollider>());
            _player.transform.position = new Vector3(0f, 1f, 0f);
            var controller = _player.AddComponent<CharacterController>();
            controller.height = 2f;
            controller.radius = 0.5f;
            controller.center = Vector3.zero;

            _eye = new GameObject("Eye").transform;
            _eye.SetParent(_player.transform, false);
            _eye.localPosition = new Vector3(0f, 0.6f, 0f);
            var cameraObject = new GameObject("Camera");
            cameraObject.transform.SetParent(_eye, false);
            _camera = cameraObject.AddComponent<Camera>();

            _move = new InputAction("Move", InputActionType.Value, expectedControlType: "Vector2");
            _sprint = new InputAction("Sprint", InputActionType.Button);
            _crouch = new InputAction("Crouch", InputActionType.Button);
            _jump = new InputAction("Jump", InputActionType.Button);
            _aim = new InputAction("Aim", InputActionType.Button);

            var look = _player.AddComponent<FirstPersonLook>();
            _mover = _player.AddComponent<PlayerController>();
            _mover.Initialize(controller, look, _move, _sprint, _crouch, _jump, new MotorSpec(5f, 8f, 1.2f, 20f, 0.1f, 2.5f));
            _damageable = _player.AddComponent<Damageable>();
            _damageable.Initialize("player", 100);
            _ads = _player.AddComponent<AimDownSights>();
            _ads.Initialize(_camera, look, _aim);
            _animator = _player.AddComponent<CharacterAnimator>();

            _rigPrefab = CreateRigPrefab();
        }

        [TearDown]
        public void TearDown()
        {
            if (_animator != null)
            {
                _animator.Dismantle();
            }

            Object.DestroyImmediate(_player);
            Object.DestroyImmediate(_rigPrefab);
            _move.Dispose();
            _sprint.Dispose();
            _crouch.Dispose();
            _jump.Dispose();
            _aim.Dispose();
        }

        // CharacterRigBuilder のプレハブと同じく、根に Animator、子に見た目を持つ。コライダーは外すことを確かめるため付けたままにする。
        private static GameObject CreateRigPrefab()
        {
            var root = new GameObject("FakeRig");
            root.AddComponent<Animator>();
            var part = GameObject.CreatePrimitive(PrimitiveType.Cube);
            part.name = "TorsoMesh";
            part.transform.SetParent(root.transform, false);
            part.transform.localPosition = new Vector3(0f, 1f, 0f);
            return root;
        }

        [Test]
        public void BindsThePlayerWhenAPlayerControllerIsOnTheSameObject()
        {
            Assert.That(_animator.TryBind(), Is.True);

            Assert.That(_animator.Source, Is.EqualTo(CharacterAnimatorSource.Player));
            Assert.That(_animator.Pose.WalkSpeed, Is.EqualTo(_mover.WalkSpeed));
            Assert.That(_animator.Pose.SprintSpeed, Is.EqualTo(_mover.SprintSpeed));
            Assert.That(_animator.Pose.CrouchSpeed, Is.EqualTo(_mover.CrouchSpeed));
        }

        [Test]
        public void DoesNothingWithoutAPlayerOrAnEnemy()
        {
            var stranger = new GameObject("Stranger");
            try
            {
                var animator = stranger.AddComponent<CharacterAnimator>();

                Assert.That(animator.TryBind(), Is.False);
                Assert.That(animator.Source, Is.EqualTo(CharacterAnimatorSource.None));
                Assert.Throws<InvalidOperationException>(() => animator.Refresh(DeltaSeconds));
            }
            finally
            {
                Object.DestroyImmediate(stranger);
            }
        }

        [Test]
        public void RefreshRejectsAnInvalidDelta()
        {
            _animator.TryBind();

            Assert.Throws<ArgumentOutOfRangeException>(() => _animator.Refresh(-1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => _animator.Refresh(float.NaN));
        }

        [Test]
        public void IdlePlayerStandsStillWithoutAWeapon()
        {
            _animator.TryBind();

            var parameters = _animator.Refresh(DeltaSeconds);

            Assert.That(_animator.HasParameters, Is.True);
            Assert.That(parameters.Speed, Is.EqualTo(CharacterPose.IdleSpeedValue));
            Assert.That(parameters.Crouch, Is.False);
            Assert.That(parameters.Aim, Is.False);
            Assert.That(parameters.Cover, Is.EqualTo(CharacterPose.CoverNone));
            Assert.That(parameters.Dead, Is.False);
            Assert.That(parameters.WeaponCategory, Is.EqualTo(CharacterPose.NoWeapon));
        }

        [Test]
        public void CrouchingSetsCrouch()
        {
            _animator.TryBind();
            Assert.That(_animator.Refresh(DeltaSeconds).Crouch, Is.False);

            _mover.Tick(Vector2.zero, false, true, false, DeltaSeconds);
            Assert.That(_mover.IsCrouched, Is.True);

            Assert.That(_animator.Refresh(DeltaSeconds).Crouch, Is.True);
        }

        [Test]
        public void AimingDownSightsSetsAim()
        {
            _animator.TryBind();
            Assert.That(_animator.Refresh(DeltaSeconds).Aim, Is.False);

            _ads.Tick(true, true, DeltaSeconds);
            Assert.That(_ads.IsAiming, Is.True);

            Assert.That(_animator.Refresh(DeltaSeconds).Aim, Is.True);
        }

        [Test]
        public void DyingSetsDeadAndDropsTheOtherPoses()
        {
            _animator.TryBind();
            _mover.Tick(Vector2.zero, false, true, false, DeltaSeconds);
            _ads.Tick(true, true, DeltaSeconds);

            _damageable.Kill();
            var parameters = _animator.Refresh(DeltaSeconds);

            Assert.That(parameters.Dead, Is.True);
            Assert.That(parameters.Crouch, Is.False);
            Assert.That(parameters.Aim, Is.False);

            _damageable.ResetHealth();
            Assert.That(_animator.Refresh(DeltaSeconds).Dead, Is.False);
        }

        [Test]
        public void ChangedMotorSpeedsRebuildThePose()
        {
            _animator.TryBind();
            var before = _animator.Pose;

            var serialized = new UnityEditor.SerializedObject(_mover);
            serialized.FindProperty("_walkSpeed").floatValue = 4f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            _animator.Refresh(DeltaSeconds);

            Assert.That(_animator.Pose, Is.Not.SameAs(before));
            Assert.That(_animator.Pose.WalkSpeed, Is.EqualTo(4f));
        }

        [Test]
        public void BuildRigPutsTheRigAtTheFeetWithoutCollidersAndHidesTheCapsule()
        {
            _animator.TryBind();
            _animator.RigPrefab = _rigPrefab;
            var capsule = _player.GetComponent<MeshRenderer>();

            Assert.That(_animator.BuildRig(), Is.True);

            Assert.That(_animator.IsBuilt, Is.True);
            Assert.That(_animator.Rig.parent, Is.SameAs(_player.transform));
            Assert.That(_animator.Rig.name, Is.EqualTo(CharacterAnimator.RigObjectName));
            Assert.That(_animator.Rig.localPosition, Is.EqualTo(new Vector3(0f, -1f, 0f)));
            Assert.That(_animator.Animator, Is.Not.Null);
            Assert.That(_animator.Rig.GetComponentsInChildren<Collider>(true), Is.Empty);
            Assert.That(capsule.enabled, Is.False, "the rig replaces the capsule");
            Assert.That(_animator.BuildRig(), Is.True, "a second call is a no-op");
            Assert.That(_player.transform.Find(CharacterAnimator.RigObjectName + "(Clone)"), Is.Null);

            _animator.Dismantle();
            Assert.That(_animator.IsBuilt, Is.False);
            Assert.That(capsule.enabled, Is.True);
        }

        [Test]
        public void BuildRigRejectsAPrefabWithoutAnAnimator()
        {
            _animator.TryBind();
            var broken = new GameObject("Broken");
            try
            {
                _animator.RigPrefab = broken;
                LogAssert.Expect(LogType.Error, new Regex(@"^\[CharacterAnimator\] 'Player': リグのプレハブ 'Broken' の根に Animator がありません"));

                Assert.That(_animator.BuildRig(), Is.False);
                Assert.That(_animator.IsBuilt, Is.False);
                Assert.That(_player.GetComponent<MeshRenderer>().enabled, Is.True, "the capsule stays");
            }
            finally
            {
                Object.DestroyImmediate(broken);
            }
        }

        [Test]
        public void BuildRigHidesTheMockBodyButKeepsItsViewModel()
        {
            var mock = _player.AddComponent<PlayerMockBody>();
            _animator.TryBind();
            _animator.RigPrefab = _rigPrefab;

            _animator.BuildRig();
            Assert.That(mock.ShowsBody, Is.False);

            _animator.Dismantle();
            Assert.That(mock.ShowsBody, Is.True);
        }

        [Test]
        public void FirstPersonLeavesOnlyTheRigsShadowAndThirdPersonDrawsIt()
        {
            var view = _player.AddComponent<ViewRig>();
            view.Initialize(_eye, _camera);
            view.Mode = ViewMode.FirstPerson;
            view.ApplyBodyVisibility(true);
            _animator.TryBind();
            _animator.RigPrefab = _rigPrefab;
            _animator.BuildRig();
            var part = _animator.Rig.GetComponentInChildren<Renderer>();

            _animator.Refresh(DeltaSeconds);
            Assert.That(part.shadowCastingMode, Is.EqualTo(ShadowCastingMode.ShadowsOnly));

            view.Mode = ViewMode.ThirdPerson;
            view.ApplyBodyVisibility(true);
            _animator.Refresh(DeltaSeconds);
            Assert.That(part.shadowCastingMode, Is.EqualTo(ShadowCastingMode.On));
        }

        [Test]
        public void SomeoneElsesFirstPersonBodyIsDrawn()
        {
            var view = _player.AddComponent<ViewRig>();
            view.Initialize(_eye, _camera);
            view.Mode = ViewMode.FirstPerson;
            view.ApplyBodyVisibility(false);
            _animator.TryBind();
            _animator.RigPrefab = _rigPrefab;
            _animator.BuildRig();

            _animator.Refresh(DeltaSeconds);

            Assert.That(_animator.Rig.GetComponentInChildren<Renderer>().shadowCastingMode, Is.EqualTo(ShadowCastingMode.On));
        }

        [Test]
        public void HealthResetRedrawsTheRigHiddenByTheDeathBody()
        {
            _animator.TryBind();
            _animator.RigPrefab = _rigPrefab;
            _animator.BuildRig();
            var part = _animator.Rig.GetComponentInChildren<Renderer>();

            // DeathBodyPresenter は倒れたときに体のレンダラーをすべて止める。
            part.enabled = false;
            _damageable.ResetHealth();

            Assert.That(part.enabled, Is.True);
            Assert.That(_player.GetComponent<MeshRenderer>().enabled, Is.False, "the capsule stays hidden");
        }

        // NetSceneBuilder の NetPlayer と同じく、本体にレンダラーは無く、当たりは CharacterController。
        private static GameObject CreateNetworkBody(out FakeCharacterPoseSource source, out CharacterAnimator animator)
        {
            var body = new GameObject("NetBody");
            body.transform.position = new Vector3(0f, 1f, 0f);
            var controller = body.AddComponent<CharacterController>();
            controller.height = 2f;
            controller.radius = 0.5f;
            controller.center = Vector3.zero;
            source = body.AddComponent<FakeCharacterPoseSource>();
            animator = body.AddComponent<CharacterAnimator>();
            return body;
        }

        [Test]
        public void BindsAPoseSourceWhenThereIsNoPlayerOrEnemy()
        {
            var body = CreateNetworkBody(out var source, out var animator);
            try
            {
                source.WalkSpeed = 4f;
                source.SprintSpeed = 7f;
                source.CrouchSpeed = 2f;

                Assert.That(animator.TryBind(), Is.True);

                Assert.That(animator.Source, Is.EqualTo(CharacterAnimatorSource.Network));
                Assert.That(animator.Pose.WalkSpeed, Is.EqualTo(4f));
                Assert.That(animator.Pose.SprintSpeed, Is.EqualTo(7f));
                Assert.That(animator.Pose.CrouchSpeed, Is.EqualTo(2f));
            }
            finally
            {
                Object.DestroyImmediate(body);
            }
        }

        [Test]
        public void PoseSourceGetsTheSpeedFromThePositionChange()
        {
            var body = CreateNetworkBody(out var source, out var animator);
            try
            {
                animator.TryBind();
                animator.Refresh(0f);
                Assert.That(source.LastSpeed, Is.EqualTo(0f));

                // 0.1 秒で 0.5m は歩き (5 m/s)
                body.transform.position += new Vector3(0.5f, 0f, 0f);
                var walking = animator.Refresh(HorizontalSpeedMeter.DefaultWindowSeconds);
                Assert.That(source.LastSpeed, Is.EqualTo(5f).Within(1e-3f));
                Assert.That(walking.Speed, Is.EqualTo(CharacterPose.WalkSpeedValue).Within(1e-3f));

                // 0.1 秒で 0.8m はダッシュ (8 m/s)
                body.transform.position += new Vector3(0f, 0f, 0.8f);
                var running = animator.Refresh(HorizontalSpeedMeter.DefaultWindowSeconds);
                Assert.That(animator.MeasuredSpeed, Is.EqualTo(8f).Within(1e-3f));
                Assert.That(running.Speed, Is.EqualTo(CharacterPose.RunSpeedValue).Within(1e-3f));

                // 落ちるだけ (縦の移動) では足を動かさない
                body.transform.position += new Vector3(0f, -1f, 0f);
                Assert.That(animator.Refresh(HorizontalSpeedMeter.DefaultWindowSeconds).Speed, Is.EqualTo(CharacterPose.IdleSpeedValue));
            }
            finally
            {
                Object.DestroyImmediate(body);
            }
        }

        [Test]
        public void PoseSourceStateDrivesTheParameters()
        {
            var body = CreateNetworkBody(out var source, out var animator);
            try
            {
                animator.TryBind();
                source.Weapon = WeaponCategory.AssaultRifle;
                source.Crouching = true;
                source.Aiming = true;

                var aiming = animator.Refresh(DeltaSeconds);
                Assert.That(aiming.Crouch, Is.True);
                Assert.That(aiming.Aim, Is.True);
                Assert.That(aiming.WeaponCategory, Is.EqualTo((int)WeaponCategory.AssaultRifle));

                source.Downed = true;
                var downed = animator.Refresh(DeltaSeconds);
                Assert.That(downed.Downed, Is.True);
                Assert.That(downed.Crouch, Is.False);
                Assert.That(downed.Aim, Is.False);

                source.Dead = true;
                Assert.That(animator.Refresh(DeltaSeconds).Dead, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(body);
            }
        }

        [Test]
        public void BuildRigUsesTheRigAlreadyUnderTheBodyAndKeepsIt()
        {
            var body = CreateNetworkBody(out _, out var animator);
            try
            {
                // NetSceneBuilder がプレハブに置くリグと同じく、本体の直下の CharacterRig で、根に Animator を持つ
                var placed = new GameObject(CharacterAnimator.RigObjectName);
                placed.transform.SetParent(body.transform, false);
                placed.AddComponent<Animator>();
                var part = new GameObject("TorsoMesh");
                part.transform.SetParent(placed.transform, false);
                part.AddComponent<MeshFilter>();
                part.AddComponent<MeshRenderer>();
                animator.TryBind();
                animator.RigPrefab = _rigPrefab;

                Assert.That(animator.BuildRig(), Is.True);

                Assert.That(animator.Rig, Is.SameAs(placed.transform));
                Assert.That(animator.OwnsRig, Is.False);
                Assert.That(animator.Animator, Is.SameAs(placed.GetComponent<Animator>()));
                Assert.That(animator.Rig.localPosition, Is.EqualTo(new Vector3(0f, -1f, 0f)), "the rig stands on the capsule's feet");
                Assert.That(body.transform.childCount, Is.EqualTo(1), "no second rig is placed from the prefab");

                animator.Dismantle();

                Assert.That(animator.IsBuilt, Is.False);
                Assert.That(placed == null, Is.False, "the prefab's own rig is not destroyed");
            }
            finally
            {
                Object.DestroyImmediate(body);
            }
        }

        [Test]
        public void BuildRigPlacesThePrefabWhenTheChildHasNoAnimator()
        {
            var body = CreateNetworkBody(out _, out var animator);
            try
            {
                var stray = new GameObject(CharacterAnimator.RigObjectName);
                stray.transform.SetParent(body.transform, false);
                animator.TryBind();
                animator.RigPrefab = _rigPrefab;
                LogAssert.Expect(LogType.Warning, new Regex(@"^\[CharacterAnimator\] 'NetBody' の子 'CharacterRig' の根に Animator がない"));

                Assert.That(animator.BuildRig(), Is.True);

                Assert.That(animator.Rig, Is.Not.SameAs(stray.transform));
                Assert.That(animator.OwnsRig, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(body);
            }
        }

        [Test]
        public void EnemyStateDrivesTheParameters()
        {
            var rifle = ScriptableObject.CreateInstance<WeaponDefinition>();
            rifle.name = "Rifle";
            WeaponAssetBuilder.Configure(rifle, WeaponAssetBuilder.RifleSpec());
            var enemy = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            enemy.name = "Enemy";
            try
            {
                var agent = enemy.AddComponent<EnemyAgent>();
                agent.WeaponDefinition = rifle;
                Assert.That(agent.TryInitialize(), Is.True);
                var animator = enemy.AddComponent<CharacterAnimator>();

                Assert.That(animator.TryBind(), Is.True);
                Assert.That(animator.Source, Is.EqualTo(CharacterAnimatorSource.Enemy));
                Assert.That(animator.Pose.WalkSpeed, Is.EqualTo(enemy.GetComponent<NavMeshAgent>().speed));

                var alive = animator.Refresh(DeltaSeconds);
                Assert.That(alive.Dead, Is.False);
                Assert.That(alive.Aim, Is.False, "an idle enemy does not aim");
                Assert.That(alive.WeaponCategory, Is.EqualTo((int)WeaponAssetBuilder.RifleSpec().Category));

                enemy.GetComponent<Damageable>().Kill();
                Assert.That(agent.State, Is.EqualTo(EnemyState.Dead));

                var dead = animator.Refresh(DeltaSeconds);
                Assert.That(dead.Dead, Is.True);
                Assert.That(dead.Speed, Is.EqualTo(CharacterPose.IdleSpeedValue));
            }
            finally
            {
                Object.DestroyImmediate(enemy);
                Object.DestroyImmediate(rifle);
            }
        }

        [Test]
        public void EnemyRigStandsOnTheNavMeshAgentsFeet()
        {
            var rifle = ScriptableObject.CreateInstance<WeaponDefinition>();
            WeaponAssetBuilder.Configure(rifle, WeaponAssetBuilder.RifleSpec());
            var enemy = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            enemy.name = "Enemy";
            try
            {
                var agent = enemy.AddComponent<EnemyAgent>();
                agent.WeaponDefinition = rifle;
                enemy.GetComponent<NavMeshAgent>().baseOffset = 1f;
                var animator = enemy.AddComponent<CharacterAnimator>();
                animator.TryBind();
                animator.RigPrefab = _rigPrefab;

                Assert.That(animator.BuildRig(), Is.True);

                Assert.That(animator.Rig.localPosition, Is.EqualTo(new Vector3(0f, -1f, 0f)));
                Assert.That(enemy.GetComponent<MeshRenderer>().enabled, Is.False);
                Assert.That(enemy.GetComponent<CapsuleCollider>(), Is.Not.Null, "the hitbox stays");
            }
            finally
            {
                Object.DestroyImmediate(enemy);
                Object.DestroyImmediate(rifle);
            }
        }
    }
}
