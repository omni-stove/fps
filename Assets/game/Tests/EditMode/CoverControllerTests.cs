using System;
using Fps.Core;
using Fps.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Fps.Tests.EditMode
{
    /// <summary>
    /// 床と壁 (BoxCollider) を置き、<see cref="CoverController.Tick"/> と <see cref="PlayerController.Tick"/> を交互に呼んで、
    /// 張り付き・壁沿いの移動・しゃがみ・覗き・出たあとの移動を確かめる。Edit Mode では Awake が走らないので参照は直接渡す。
    /// </summary>
    public sealed class CoverControllerTests
    {
        private const float _frame = 0.02f;
        private const float _tolerance = 1e-3f;

        // GameBootstrap のプレイヤーと同じ寸法。足元が y = 0 (床の上面) に来るよう本体を y = 1 に置く。
        private const float _height = 2f;
        private const float _radius = 0.5f;
        private const float _skinWidth = 0.01f;
        private static readonly Vector3 _bodyPosition = new Vector3(0f, 1f, 0f);
        private static readonly Vector3 _eyeOffset = new Vector3(0f, 0.6f, 0f);

        // 壁のこちら側の面の z。プレイヤー (z = 0、+Z 向き) から 0.8m 先。
        private const float _wallFaceZ = 0.8f;
        private const float _wallThickness = 0.4f;

        private GameObject _body;
        private GameObject _floor;
        private GameObject _wall;
        private CharacterController _characterController;
        private PlayerController _player;
        private ViewRig _rig;
        private CoverController _cover;
        private InputAction _move;
        private InputAction _sprint;
        private InputAction _crouch;
        private InputAction _jump;

        private static GameRules Rules(Cover cover)
        {
            var actions = new ActionRules(
                Downed.Off,
                ActionRules.DefaultDownedBleedoutSeconds,
                ActionRules.DefaultDownedHealthFraction,
                ActionRules.DefaultReviveSeconds,
                cover,
                BlindFire.Off,
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
            _characterController = _body.AddComponent<CharacterController>();
            _characterController.height = _height;
            _characterController.radius = _radius;
            _characterController.center = Vector3.zero;
            _characterController.skinWidth = _skinWidth;

            var eye = new GameObject("Eye").transform;
            eye.SetParent(_body.transform, false);
            eye.localPosition = _eyeOffset;
            var camera = new GameObject("Camera").AddComponent<Camera>();
            camera.transform.SetParent(eye, false);
            _rig = _body.AddComponent<ViewRig>();
            _rig.Initialize(eye, camera);

            var look = _body.AddComponent<FirstPersonLook>();
            _player = _body.AddComponent<PlayerController>();
            _move = new InputAction("Move", InputActionType.Value, expectedControlType: "Vector2");
            _sprint = new InputAction("Sprint", InputActionType.Button);
            _crouch = new InputAction("Crouch", InputActionType.Button);
            _jump = new InputAction("Jump", InputActionType.Button);

            // 寄せている間は CharacterController.Move で横に動くので接地が外れることがある。空中でも入力どおりに動くよう空中制御を 1 にする。
            _player.Initialize(_characterController, look, _move, _sprint, _crouch, _jump, new MotorSpec(5f, 8f, 1.2f, 20f, 1f, 2.5f));

            _cover = _body.AddComponent<CoverController>();
            _cover.Configure(Rules(Cover.On));
            _cover.Initialize(_player, null, null, null);
            Physics.SyncTransforms();
        }

        [TearDown]
        public void TearDown()
        {
            _move.Dispose();
            _sprint.Dispose();
            _crouch.Dispose();
            _jump.Dispose();
            Object.DestroyImmediate(_body);
            Object.DestroyImmediate(_floor);
            if (_wall != null)
            {
                Object.DestroyImmediate(_wall);
            }
        }

        [Test]
        public void InitializeHooksItselfIntoThePlayersMovement()
        {
            Assert.That(_cover.IsInitialized, Is.True);
            Assert.That(_player.MoveFilter, Is.SameAs(_cover));
            Assert.That(_cover.IsInCover, Is.False);
        }

        [Test]
        public void InitializeRejectsBadPlayers()
        {
            Assert.Throws<ArgumentNullException>(() => _cover.Initialize(null, null, null, null));

            var stranger = new GameObject("Stranger");
            try
            {
                stranger.AddComponent<CharacterController>();
                var otherPlayer = stranger.AddComponent<PlayerController>();
                Assert.Throws<ArgumentException>(() => _cover.Initialize(otherPlayer, null, null, null));
            }
            finally
            {
                Object.DestroyImmediate(stranger);
            }
        }

        [Test]
        public void EntersAHighWallInFrontAndHugsIt()
        {
            BuildWall(2f, -2f, 2f);

            LogAssert.Expect(LogType.Log, "[Cover] enter high");
            Enter();

            Assert.That(_cover.IsInCover, Is.True);
            Assert.That(_cover.Phase, Is.EqualTo(CoverPhase.In));
            Assert.That(_cover.CoverHeight, Is.EqualTo(CoverHeight.High));
            Assert.That(_cover.CoverTopY, Is.EqualTo(2f).Within(_tolerance));
            Assert.That(Vector3.Distance(_cover.CoverNormal, Vector3.back), Is.LessThan(_tolerance));
            var hold = CoverProbe.HoldDistance(_radius);
            Assert.That(WallDistance(), Is.EqualTo(hold).Within(_skinWidth + 0.01f));
        }

        [Test]
        public void EnteringTurnsToFaceTheWall()
        {
            BuildWall(2f, -3f, 3f);
            _body.transform.rotation = Quaternion.Euler(0f, 30f, 0f);
            Physics.SyncTransforms();

            Enter();

            Assert.That(_cover.IsInCover, Is.True);
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(_body.transform.eulerAngles.y, 0f)), Is.LessThan(0.1f));
        }

        [Test]
        public void DoesNotEnterWhenTheWallIsTooFar()
        {
            BuildWall(2f, -2f, 2f, faceZ: 1.2f);

            _cover.Tick(true, false, Vector2.zero, _frame);

            Assert.That(_cover.IsInCover, Is.False);
        }

        [Test]
        public void DoesNotEnterWhenFacingAwayFromTheWall()
        {
            BuildWall(2f, -2f, 2f);
            _body.transform.rotation = Quaternion.Euler(0f, 60f, 0f);
            Physics.SyncTransforms();

            _cover.Tick(true, false, Vector2.zero, _frame);

            Assert.That(_cover.IsInCover, Is.False);
        }

        [Test]
        public void MovingSidewaysKeepsTheDistanceToTheWall()
        {
            BuildWall(2f, -3f, 3f);
            Enter();
            var distance = WallDistance();
            var startX = _body.transform.position.x;

            for (var i = 0; i < 20; i++)
            {
                Frame(false, false, new Vector2(1f, 0f));
            }

            Assert.That(_cover.IsInCover, Is.True);
            Assert.That(_body.transform.position.x, Is.GreaterThan(startX + 0.5f), "slides right along the wall");
            Assert.That(WallDistance(), Is.EqualTo(distance).Within(_tolerance));
        }

        [Test]
        public void MovesAtCrouchSpeedWithoutSprintAndIgnoresForward()
        {
            BuildWall(2f, -3f, 3f);
            Enter();

            var move = new Vector2(1f, 1f);
            _cover.Tick(false, false, move, _frame);
            _player.Tick(move, true, false, true, _frame);

            var velocity = _player.Motor.Velocity;
            Assert.That(velocity.X, Is.EqualTo(_player.Motor.Spec.CrouchSpeed).Within(_tolerance));
            Assert.That(velocity.Z, Is.EqualTo(0f).Within(_tolerance));
            Assert.That(velocity.Y, Is.LessThanOrEqualTo(0f), "jump is ignored");
        }

        [Test]
        public void CrouchesBehindALowWall()
        {
            BuildWall(1f, -2f, 2f);

            LogAssert.Expect(LogType.Log, "[Cover] enter low");
            Enter();
            Frame(false, false, Vector2.zero);

            Assert.That(_cover.CoverHeight, Is.EqualTo(CoverHeight.Low));
            Assert.That(_cover.CoverTopY, Is.EqualTo(1f).Within(_tolerance));
            Assert.That(_player.IsCrouched, Is.True);
        }

        [Test]
        public void StaysStandingBehindAHighWall()
        {
            BuildWall(2f, -2f, 2f);

            Enter();
            Frame(false, false, Vector2.zero);

            Assert.That(_player.IsCrouched, Is.False);
        }

        [Test]
        public void AimingBehindALowWallStandsUpToPeek()
        {
            BuildWall(1f, -2f, 2f);
            Enter();
            Frame(false, false, Vector2.zero);
            Assert.That(_player.IsCrouched, Is.True);

            LogAssert.Expect(LogType.Log, "[Cover] peek up");
            Frame(false, true, Vector2.zero);

            Assert.That(_cover.IsPeeking, Is.True);
            Assert.That(_player.IsCrouched, Is.False);

            LogAssert.Expect(LogType.Log, "[Cover] unpeek");
            Frame(false, false, Vector2.zero);

            Assert.That(_cover.IsPeeking, Is.False);
            Assert.That(_player.IsCrouched, Is.True);
        }

        [Test]
        public void AimingAtTheEdgeOfAHighWallStepsOutAndBack()
        {
            // 右端が x = 0.1。体の中心 (x = 0) には壁があり、0.3m 右には無い。
            BuildWall(2f, -2f, 0.1f);
            Enter();
            var startX = _body.transform.position.x;

            LogAssert.Expect(LogType.Log, "[Cover] peek right");
            for (var i = 0; i < 15; i++)
            {
                Frame(false, true, Vector2.zero);
            }

            Assert.That(_cover.IsPeeking, Is.True);
            Assert.That(_cover.IsInCover, Is.True, "the wall is checked from where the peek started");
            Assert.That(_body.transform.position.x - startX, Is.EqualTo(CoverProbe.PeekSideDistance).Within(0.02f));

            for (var i = 0; i < 15; i++)
            {
                Frame(false, false, Vector2.zero);
            }

            Assert.That(_cover.IsPeeking, Is.False);
            Assert.That(_body.transform.position.x, Is.EqualTo(startX).Within(0.02f));
        }

        [Test]
        public void AimingAwayFromTheEdgeOfAHighWallDoesNotPeek()
        {
            BuildWall(2f, -3f, 3f);
            Enter();

            Frame(false, true, Vector2.zero);

            Assert.That(_cover.IsPeeking, Is.False);
        }

        [Test]
        public void TheLeftEdgeSwapsTheShoulderUntilLeaving()
        {
            BuildWall(2f, -0.1f, 2f);
            Assert.That(_rig.Shoulder, Is.EqualTo(ShoulderSide.Right));

            Enter();
            Assert.That(_rig.Shoulder, Is.EqualTo(ShoulderSide.Left));

            Frame(true, false, Vector2.zero);
            Assert.That(_rig.Shoulder, Is.EqualTo(ShoulderSide.Right));
        }

        [Test]
        public void PressingCoverAgainExitsAndRestoresNormalMovement()
        {
            BuildWall(2f, -2f, 2f);
            Enter();

            LogAssert.Expect(LogType.Log, "[Cover] exit button");
            Frame(true, false, Vector2.zero);

            Assert.That(_cover.IsInCover, Is.False);
            Assert.That(_cover.IsPeeking, Is.False);

            // 下がりながら (壁から離れる向き) ダッシュすると、元どおりダッシュの速さで後ろへ動く。
            _player.Tick(new Vector2(0f, -1f), true, false, false, _frame);
            var velocity = _player.Motor.Velocity;
            Assert.That(velocity.X, Is.EqualTo(0f).Within(_tolerance));
            Assert.That(velocity.Z, Is.EqualTo(-_player.Motor.Spec.SprintSpeed).Within(_tolerance));
        }

        [Test]
        public void HoldingBackExits()
        {
            BuildWall(2f, -2f, 2f);
            Enter();

            LogAssert.Expect(LogType.Log, "[Cover] exit back");
            for (var i = 0; i < 12; i++)
            {
                Frame(false, false, new Vector2(0f, -1f));
            }

            Assert.That(_cover.IsInCover, Is.False);
        }

        [Test]
        public void LosingTheWallExits()
        {
            BuildWall(2f, -2f, 2f);
            Enter();

            Object.DestroyImmediate(_wall);
            _wall = null;
            Physics.SyncTransforms();

            LogAssert.Expect(LogType.Log, "[Cover] exit lost");
            Frame(false, false, Vector2.zero);

            Assert.That(_cover.IsInCover, Is.False);
        }

        [Test]
        public void CoverOffRulesCannotEnter()
        {
            BuildWall(2f, -2f, 2f);
            _cover.Configure(Rules(Cover.Off));

            _cover.Tick(true, false, Vector2.zero, _frame);

            Assert.That(_cover.enabled, Is.False);
            Assert.That(_cover.IsInCover, Is.False);
            Assert.That(_player.MoveFilter, Is.Null);
        }

        [Test]
        public void TurningCoverOffWhileInCoverExits()
        {
            BuildWall(2f, -2f, 2f);
            Enter();

            LogAssert.Expect(LogType.Log, "[Cover] exit disabled");
            _cover.Configure(Rules(Cover.Off));

            Assert.That(_cover.IsInCover, Is.False);
            Assert.That(_player.MoveFilter, Is.Null);
        }

        // 足元から height の高さの壁を、x が minX〜maxX、こちら側の面が faceZ になるよう置く。
        private void BuildWall(float height, float minX, float maxX, float faceZ = _wallFaceZ)
        {
            _wall = new GameObject("Wall");
            _wall.transform.position = new Vector3((minX + maxX) * 0.5f, height * 0.5f, faceZ + (_wallThickness * 0.5f));
            var box = _wall.AddComponent<BoxCollider>();
            box.size = new Vector3(maxX - minX, height, _wallThickness);
            Physics.SyncTransforms();
        }

        // Cover を押し、寄せ終わるまで進める。
        private void Enter()
        {
            Frame(true, false, Vector2.zero);
            Assert.That(_cover.IsInCover, Is.True, "a wall in front should be enterable");
            for (var elapsed = 0f; elapsed < CoverState.EnterSeconds + _frame; elapsed += _frame)
            {
                Frame(false, false, Vector2.zero);
            }

            Assert.That(_cover.Phase, Is.EqualTo(CoverPhase.In));
        }

        private void Frame(bool coverPressed, bool aimHeld, Vector2 move)
        {
            _cover.Tick(coverPressed, aimHeld, move, _frame);
            _player.Tick(move, false, false, false, _frame);
            Physics.SyncTransforms();
        }

        private float WallDistance()
        {
            var face = _wall != null ? _wall.transform.position.z - (_wallThickness * 0.5f) : _wallFaceZ;
            return face - _body.transform.position.z;
        }
    }
}
