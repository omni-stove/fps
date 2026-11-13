using System;
using Fps.Core;
using Fps.Editor;
using Fps.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Fps.Tests.EditMode
{
    /// <summary>
    /// 床・壁・壁の向こうの標的を置き、<see cref="CoverController.Tick"/> と <see cref="PlayerController.Tick"/> でカバーに入ってから
    /// <see cref="WeaponController.Fire"/> して、ブラインドファイアの始点・止め方・ルールでの切り替えを確かめる。
    /// Edit Mode では Awake が走らないので参照は直接渡す。寸法は <see cref="CoverControllerTests"/> と同じ。
    /// </summary>
    public sealed class BlindFireControllerTests
    {
        private const float _frame = 0.02f;
        private const int _targetHealth = 100;

        // 弾が壁の上の角をかすめないよう、散らばりは小さくする (始点は壁の上端から 0.15m 上、壁の奥の角まで約 1m)。
        private const float _spreadDegrees = 3f;

        private const float _height = 2f;
        private const float _radius = 0.5f;
        private static readonly Vector3 _bodyPosition = new Vector3(0f, 1f, 0f);
        private static readonly Vector3 _eyeOffset = new Vector3(0f, 0.6f, 0f);

        private const float _wallFaceZ = 0.8f;
        private const float _wallTop = 1f;
        private const float _wallThickness = 0.4f;
        private const float _targetZ = 5f;

        private GameObject _body;
        private GameObject _floor;
        private GameObject _wall;
        private GameObject _target;
        private StructureHitRecorder _wallHits;
        private Damageable _targetDamageable;
        private Transform _eye;
        private PlayerController _player;
        private CoverController _cover;
        private WeaponController _weapon;
        private BlindFireController _blindFire;
        private WeaponDefinition _rifle;
        private InputAction _move;
        private InputAction _sprint;
        private InputAction _crouch;
        private InputAction _jump;
        private InputAction _attack;
        private InputAction _reload;

        private static GameRules Rules(BlindFire blindFire)
        {
            var actions = new ActionRules(
                Downed.Off,
                ActionRules.DefaultDownedBleedoutSeconds,
                ActionRules.DefaultDownedHealthFraction,
                ActionRules.DefaultReviveSeconds,
                Cover.On,
                blindFire,
                ActiveReload.Off);
            return new GameRules(
                FriendlyFire.Off,
                RespawnMode.Individual,
                GameRules.DefaultWaveIntervalSeconds,
                0f,
                0f,
                DeathBody.Vanish,
                DeathCamera.None,
                false,
                1f,
                Destruction.Off,
                actions);
        }

        [SetUp]
        public void SetUp()
        {
            _floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _floor.name = "Floor";
            _floor.transform.position = new Vector3(0f, -0.5f, 0f);
            _floor.transform.localScale = new Vector3(20f, 1f, 20f);

            _body = new GameObject("Body");
            _body.transform.position = _bodyPosition;
            var characterController = _body.AddComponent<CharacterController>();
            characterController.height = _height;
            characterController.radius = _radius;
            characterController.center = Vector3.zero;
            characterController.skinWidth = 0.01f;

            // 一人称 (カメラが目に重なる) にして、ふだんの射撃は目から正面へまっすぐ飛ぶようにする。
            _eye = new GameObject("Eye").transform;
            _eye.SetParent(_body.transform, false);
            _eye.localPosition = _eyeOffset;
            var camera = new GameObject("Camera").AddComponent<Camera>();
            camera.transform.SetParent(_eye, false);
            var rig = _body.AddComponent<ViewRig>();
            rig.Initialize(_eye, camera);

            var look = _body.AddComponent<FirstPersonLook>();
            _player = _body.AddComponent<PlayerController>();
            _move = new InputAction("Move", InputActionType.Value, expectedControlType: "Vector2");
            _sprint = new InputAction("Sprint", InputActionType.Button);
            _crouch = new InputAction("Crouch", InputActionType.Button);
            _jump = new InputAction("Jump", InputActionType.Button);
            _player.Initialize(characterController, look, _move, _sprint, _crouch, _jump, new MotorSpec(5f, 8f, 1.2f, 20f, 1f, 2.5f));

            _rifle = ScriptableObject.CreateInstance<WeaponDefinition>();
            _rifle.name = "Rifle";
            WeaponAssetBuilder.Configure(_rifle, WeaponAssetBuilder.RifleSpec());
            _rifle.BlindFireSpreadDegrees = _spreadDegrees;

            // 壁越しに当たるかをブラインドファイアの散らばりだけで確かめるので、腰撃ちのばらつきは切る。
            _rifle.HipSpreadDegrees = 0f;
            _attack = new InputAction("Attack", InputActionType.Button);
            _reload = new InputAction("Reload", InputActionType.Button);
            _weapon = camera.gameObject.AddComponent<WeaponController>();
            _weapon.Initialize(camera.transform, _attack, _reload, _rifle);

            _cover = _body.AddComponent<CoverController>();
            _cover.Configure(Rules(BlindFire.On));
            _cover.Initialize(_player, null, null, null);

            _blindFire = _body.AddComponent<BlindFireController>();
            _blindFire.Initialize(_cover, _weapon);
            _blindFire.Configure(Rules(BlindFire.On));
            _blindFire.Random = new System.Random(1);
            Physics.SyncTransforms();
        }

        [TearDown]
        public void TearDown()
        {
            _move.Dispose();
            _sprint.Dispose();
            _crouch.Dispose();
            _jump.Dispose();
            _attack.Dispose();
            _reload.Dispose();
            Object.DestroyImmediate(_body);
            Object.DestroyImmediate(_floor);
            Object.DestroyImmediate(_rifle);
            if (_wall != null)
            {
                Object.DestroyImmediate(_wall);
            }

            if (_target != null)
            {
                Object.DestroyImmediate(_target);
            }
        }

        [Test]
        public void InitializeHooksItselfIntoTheWeapon()
        {
            Assert.That(_blindFire.IsInitialized, Is.True);
            Assert.That(_weapon.ShotFilter, Is.SameAs(_blindFire));
            Assert.That(_blindFire.IsBlindFiring, Is.False, "not in cover yet");
        }

        [Test]
        public void InitializeRejectsBadReferences()
        {
            Assert.Throws<ArgumentNullException>(() => _blindFire.Initialize(null, _weapon));
            Assert.Throws<ArgumentNullException>(() => _blindFire.Initialize(_cover, null));
            Assert.Throws<ArgumentNullException>(() => _blindFire.Random = null);

            var stranger = new GameObject("Stranger", typeof(Camera));
            try
            {
                var otherWeapon = stranger.AddComponent<WeaponController>();
                Assert.Throws<ArgumentException>(() => _blindFire.Initialize(_cover, otherWeapon));
            }
            finally
            {
                Object.DestroyImmediate(stranger);
            }
        }

        [Test]
        public void CrouchedBehindALowWallTheBlindFireGoesOverItAndHitsTheTarget()
        {
            BuildLowWallAndTarget();
            EnterAndSettle();
            Assert.That(_player.IsCrouched, Is.True);
            Assert.That(_eye.position.y, Is.LessThan(_wallTop), "a normal shot from the crouched eye would hit the wall");
            var bodyBefore = _body.transform.position;
            var ammoBefore = _weapon.Weapon.AmmoInMagazine;

            Assert.That(_blindFire.IsBlindFiring, Is.True);
            LogAssert.Expect(LogType.Log, "[Combat] blind-fire spread=3");
            Assert.That(_weapon.Fire(), Is.True);

            Assert.That(_targetDamageable.Health.Current, Is.LessThan(_targetHealth), "the target behind the wall is hit");
            Assert.That(_wallHits.Calls, Is.Empty, "the shot clears the wall");
            Assert.That(_weapon.Weapon.AmmoInMagazine, Is.EqualTo(ammoBefore - 1), "uses a round like a normal shot");
            Assert.That(_player.IsCrouched, Is.True, "the body stays down");
            Assert.That(_body.transform.position, Is.EqualTo(bodyBefore), "the body does not move");
            Assert.That(_weapon.Fire(), Is.False, "the fire interval still applies");
        }

        [Test]
        public void LowWallOriginIsAboveTheTopAndOutAlongTheNormal()
        {
            BuildLowWallAndTarget();
            EnterAndSettle();
            _blindFire.Random = new System.Random(1);
            var origin = _eye.position;
            var direction = Vector3.forward;

            LogAssert.Expect(LogType.Log, "[Combat] blind-fire spread=3");
            _blindFire.Filter(ref origin, ref direction);

            Assert.That(origin.y, Is.EqualTo(_wallTop + BlindFireShot.LowWallClearance).Within(1e-3f));
            Assert.That(origin.x, Is.EqualTo(_eye.position.x).Within(1e-3f));
            Assert.That(origin.z, Is.EqualTo(_eye.position.z - BlindFireShot.LowWallNormalOffset).Within(1e-3f));
            Assert.That(Vector3.Angle(Vector3.forward, direction), Is.LessThanOrEqualTo(_spreadDegrees + 1e-3f));
            Assert.That(direction.magnitude, Is.EqualTo(1f).Within(1e-4f));
        }

        [Test]
        public void WithBlindFireOffTheShotFromBehindALowWallHitsTheWall()
        {
            BuildLowWallAndTarget();
            _blindFire.Configure(Rules(BlindFire.Off));
            EnterAndSettle();

            Assert.That(_blindFire.enabled, Is.False);
            Assert.That(_weapon.ShotFilter, Is.Null);
            Assert.That(_blindFire.IsBlindFiring, Is.False);
            Assert.That(_weapon.Fire(), Is.True);

            Assert.That(_wallHits.Calls, Has.Count.EqualTo(1), "a normal shot hits the wall");
            Assert.That(_targetDamageable.Health.Current, Is.EqualTo(_targetHealth));
        }

        [Test]
        public void AimingFromCoverIsANormalShot()
        {
            BuildLowWallAndTarget();
            EnterAndSettle();

            LogAssert.Expect(LogType.Log, "[Cover] peek up");
            Frame(false, true);
            Assert.That(_cover.IsPeeking, Is.True);
            Assert.That(_blindFire.IsBlindFiring, Is.False);

            var origin = _eye.position;
            var direction = Vector3.forward;
            _blindFire.Filter(ref origin, ref direction);

            Assert.That(origin, Is.EqualTo(_eye.position), "the shot starts at the eye");
            Assert.That(direction, Is.EqualTo(Vector3.forward), "the shot is not spread");
        }

        [Test]
        public void OutOfCoverTheShotIsUntouched()
        {
            var origin = _eye.position;
            var direction = Vector3.forward;

            _blindFire.Filter(ref origin, ref direction);

            Assert.That(origin, Is.EqualTo(_eye.position));
            Assert.That(direction, Is.EqualTo(Vector3.forward));
        }

        [Test]
        public void AtTheEdgeOfAHighWallTheBlindFireComesOutSidewaysAndHitsTheTarget()
        {
            // 右端が x = 0.1。体の中心 (x = 0) からの正面の射撃は壁に当たり、0.65m 右からの射撃は端の外を通る。
            BuildWall(2f, -2f, 0.1f);
            var side = _radius + BlindFireShot.HighWallSideMargin;
            BuildTarget(new Vector3(side, 1.6f, _targetZ));
            EnterAndSettle();
            Assert.That(_cover.CoverHeight, Is.EqualTo(CoverHeight.High));
            Assert.That(_cover.LastProbe.IsAtRightEdge, Is.True);
            Assert.That(_blindFire.IsFireBlocked, Is.False);

            var origin = _eye.position;
            var direction = Vector3.forward;
            LogAssert.Expect(LogType.Log, "[Combat] blind-fire spread=3");
            _blindFire.Filter(ref origin, ref direction);
            Assert.That(origin.x - _eye.position.x, Is.EqualTo(side).Within(1e-3f), "steps right by the radius and the margin");
            Assert.That(origin.y, Is.EqualTo(_eye.position.y).Within(1e-3f), "at eye height");

            LogAssert.Expect(LogType.Log, "[Combat] blind-fire spread=3");
            Assert.That(_weapon.Fire(), Is.True);

            Assert.That(_targetDamageable.Health.Current, Is.LessThan(_targetHealth));
            Assert.That(_wallHits.Calls, Is.Empty);
        }

        [Test]
        public void AwayFromTheEdgesOfAHighWallAttackIsBlockedUntilLeavingCover()
        {
            BuildWall(2f, -3f, 3f);
            EnterAndSettle();
            _attack.Enable();

            Assert.That(_blindFire.IsFireBlocked, Is.True);
            _blindFire.RefreshAttackBlock();

            Assert.That(_blindFire.IsAttackBlocked, Is.True);
            Assert.That(_attack.enabled, Is.False, "the weapon cannot read Attack, so no round is used");

            var origin = _eye.position;
            var direction = Vector3.forward;
            _blindFire.Filter(ref origin, ref direction);
            Assert.That(origin, Is.EqualTo(_eye.position), "a shot that still gets through is left as it is");

            Frame(true, false);
            Assert.That(_cover.IsInCover, Is.False);
            _blindFire.RefreshAttackBlock();

            Assert.That(_blindFire.IsAttackBlocked, Is.False);
            Assert.That(_attack.enabled, Is.True, "Attack comes back after leaving cover");
        }

        [Test]
        public void TurningBlindFireOffReleasesTheBlockedAttack()
        {
            BuildWall(2f, -3f, 3f);
            EnterAndSettle();
            _attack.Enable();
            _blindFire.RefreshAttackBlock();
            Assert.That(_attack.enabled, Is.False);

            _blindFire.Configure(Rules(BlindFire.Off));

            Assert.That(_blindFire.IsAttackBlocked, Is.False);
            Assert.That(_attack.enabled, Is.True);
            Assert.That(_weapon.ShotFilter, Is.Null);
        }

        [Test]
        public void AttackThatWasNotEnabledIsLeftAlone()
        {
            BuildWall(2f, -3f, 3f);
            EnterAndSettle();
            Assert.That(_attack.enabled, Is.False);

            _blindFire.RefreshAttackBlock();
            Frame(true, false);
            _blindFire.RefreshAttackBlock();

            Assert.That(_attack.enabled, Is.False, "only re-enables what it disabled");
        }

        [Test]
        public void ConfigureOnAgainRestoresTheShotFilter()
        {
            _blindFire.Configure(Rules(BlindFire.Off));
            Assert.That(_weapon.ShotFilter, Is.Null);

            _blindFire.Configure(Rules(BlindFire.On));

            Assert.That(_weapon.ShotFilter, Is.SameAs(_blindFire));
        }

        private void BuildLowWallAndTarget()
        {
            BuildWall(_wallTop, -2f, 2f);
            BuildTarget(new Vector3(0f, 1.2f, _targetZ));
        }

        // 足元から height の高さの壁を、x が minX〜maxX、こちら側の面が z = 0.8 になるよう置く。撃たれたら _wallHits に残る。
        private void BuildWall(float height, float minX, float maxX)
        {
            _wall = new GameObject("Wall");
            _wall.transform.position = new Vector3((minX + maxX) * 0.5f, height * 0.5f, _wallFaceZ + (_wallThickness * 0.5f));
            var box = _wall.AddComponent<BoxCollider>();
            box.size = new Vector3(maxX - minX, height, _wallThickness);
            _wallHits = _wall.AddComponent<StructureHitRecorder>();
            Physics.SyncTransforms();
        }

        // 壁の向こうの、2m 四方の標的。
        private void BuildTarget(Vector3 center)
        {
            _target = new GameObject("Target");
            _target.transform.position = center;
            var box = _target.AddComponent<BoxCollider>();
            box.size = new Vector3(2f, 2f, 0.5f);
            _targetDamageable = _target.AddComponent<Damageable>();
            _targetDamageable.Initialize("target", _targetHealth);
            _target.AddComponent<Hitbox>().Initialize(HitZone.Body, _targetDamageable);
            Physics.SyncTransforms();
        }

        // Cover を押して寄せ終わるまで進め、もう 1 フレーム進めて (低い壁ならしゃがみを) 落ち着かせる。
        private void EnterAndSettle()
        {
            Frame(true, false);
            Assert.That(_cover.IsInCover, Is.True, "a wall in front should be enterable");
            for (var elapsed = 0f; elapsed < CoverState.EnterSeconds + _frame; elapsed += _frame)
            {
                Frame(false, false);
            }

            Frame(false, false);
            Assert.That(_cover.Phase, Is.EqualTo(CoverPhase.In));
        }

        private void Frame(bool coverPressed, bool aimHeld)
        {
            _cover.Tick(coverPressed, aimHeld, Vector2.zero, _frame);
            _player.Tick(Vector2.zero, false, false, false, _frame);
            Physics.SyncTransforms();
        }
    }
}
