using System.Collections.Generic;
using System.Text.RegularExpressions;
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
    /// 崖のぼり (<see cref="PlayerController.MantleRule"/>)。Jump で、乗り越えより高い段の縁によじ登って上に立つこと、
    /// 上が狭い・頭上が塞がっている段は登らないこと、低い障害物は今までどおり乗り越えること。
    /// Edit Mode でも Physics の Raycast と重なり判定はコライダーに当たるので、置いたら SyncTransforms してから Tick する。
    /// </summary>
    public sealed class PlayerControllerMantleTests
    {
        private const float _tolerance = 1e-4f;
        private const float _step = 0.02f;

        // GameBootstrap のプレイヤーと同じ寸法。足元が y = 0 に来るよう本体を y = 1 に置く。
        private const float _height = 2f;
        private const float _radius = 0.5f;

        // 正面 (+Z) の段の、体の中心から手前の面までの距離。
        private const float _ledgeNearZ = 0.8f;
        private const float _ledgeHeight = 1.8f;

        private readonly List<GameObject> _created = new List<GameObject>();
        private GameObject _body;
        private CharacterController _characterController;
        private PlayerController _player;
        private InputAction _move;
        private InputAction _sprint;
        private InputAction _crouch;
        private InputAction _jump;

        [SetUp]
        public void SetUp()
        {
            _body = new GameObject("Body");
            _body.transform.position = new Vector3(0f, 1f, 0f);
            _characterController = _body.AddComponent<CharacterController>();
            _characterController.height = _height;
            _characterController.radius = _radius;
            _characterController.center = Vector3.zero;
            _player = _body.AddComponent<PlayerController>();
            var look = _body.AddComponent<FirstPersonLook>();
            _move = new InputAction("Move", InputActionType.Value, expectedControlType: "Vector2");
            _sprint = new InputAction("Sprint", InputActionType.Button);
            _crouch = new InputAction("Crouch", InputActionType.Button);
            _jump = new InputAction("Jump", InputActionType.Button);
            _player.Initialize(
                _characterController, look, _move, _sprint, _crouch, _jump, new MotorSpec(5f, 8f, 1.2f, 20f, 0.1f, 2.5f));
        }

        [TearDown]
        public void TearDown()
        {
            _move.Dispose();
            _sprint.Dispose();
            _crouch.Dispose();
            _jump.Dispose();
            Object.DestroyImmediate(_body);
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
        public void MantleIsOffByDefaultAndJumpStaysAJump()
        {
            LandOnFloor();
            PlaceBox(_ledgeHeight, 3f);
            _player.VaultRule = Vault.On;

            Assert.That(_player.MantleRule, Is.EqualTo(Mantle.Off));
            LogAssert.Expect(LogType.Log, new Regex(@"^\[Player\] vault=TooHigh "));
            _player.Tick(Vector2.zero, false, false, true, _step);

            Assert.That(_player.IsMantling, Is.False);
            Assert.That(_player.Motor.Velocity.Y, Is.GreaterThan(0f), "jumped");
        }

        [Test]
        public void JumpInFrontOfAHighLedgeClimbsOntoIt()
        {
            LandOnFloor();
            _player.VaultRule = Vault.On;
            _player.MantleRule = Mantle.On;
            PlaceBox(_ledgeHeight, 3f);
            Vector3? startedTowards = null;
            var ended = 0;
            _player.MantleStarted += direction => startedTowards = direction;
            _player.MantleEnded += () => ended++;

            LogAssert.Expect(LogType.Log, new Regex(@"^\[Player\] vault=TooHigh "));
            LogAssert.Expect(LogType.Log, new Regex(@"^\[Player\] mantle=Mantle height=1\.\d\d depth=inf$"));
            LogAssert.Expect(LogType.Log, "[Player] mantling=true");
            _player.Tick(Vector2.up, false, false, true, _step);

            Assert.That(_player.IsMantling, Is.True);
            Assert.That(_player.IsVaulting, Is.False);
            Assert.That(_player.LastMantleProbe.CanMantle, Is.True);
            Assert.That(startedTowards, Is.Not.Null);
            Assert.That(startedTowards.Value.z, Is.EqualTo(1f).Within(_tolerance));

            LogAssert.Expect(LogType.Log, "[Player] mantling=false");
            for (var i = 0; i < 200 && _player.IsMantling; i++)
            {
                _player.Tick(Vector2.up, true, true, true, _step);
                Assert.That(_player.IsCrouched, Is.False, "crouch is held off while mantling");
            }

            Assert.That(_player.IsMantling, Is.False, "the climb ends");
            Assert.That(ended, Is.EqualTo(1));
            Assert.That(FeetY(), Is.EqualTo(_ledgeHeight).Within(0.15f), "standing on the top");
            Assert.That(_body.transform.position.z - _radius, Is.GreaterThan(_ledgeNearZ), "the whole body is past the edge");

            // 縁の上で落ちずに立っていられる。
            for (var i = 0; i < 10; i++)
            {
                _player.Tick(Vector2.zero, false, false, false, _step);
            }

            Assert.That(_player.IsGrounded, Is.True);
            Assert.That(FeetY(), Is.EqualTo(_ledgeHeight).Within(0.15f));
        }

        [Test]
        public void MantleWorksWithVaultOff()
        {
            LandOnFloor();
            _player.MantleRule = Mantle.On;
            PlaceBox(_ledgeHeight, 3f);

            LogAssert.Expect(LogType.Log, new Regex(@"^\[Player\] mantle=Mantle "));
            LogAssert.Expect(LogType.Log, "[Player] mantling=true");
            _player.Tick(Vector2.zero, false, false, true, _step);

            Assert.That(_player.IsMantling, Is.True);
        }

        [Test]
        public void ANarrowTopIsNotClimbed()
        {
            LandOnFloor();
            _player.MantleRule = Mantle.On;
            PlaceBox(_ledgeHeight, 0.4f);

            LogAssert.Expect(LogType.Log, new Regex(@"^\[Player\] mantle=TooNarrow height=1\.\d\d depth=0\.40$"));
            _player.Tick(Vector2.zero, false, false, true, _step);

            Assert.That(_player.IsMantling, Is.False);
            Assert.That(_player.LastMantleProbe.Verdict, Is.EqualTo(VaultVerdict.TooNarrow));
            Assert.That(_player.Motor.Velocity.Y, Is.GreaterThan(0f), "jumped instead");
        }

        [Test]
        public void ABlockedHeadroomIsNotClimbed()
        {
            LandOnFloor();
            _player.MantleRule = Mantle.On;
            PlaceBox(_ledgeHeight, 3f);

            // 縁の上 1.2 m に天井を張る。立ち 2 m の体は入らない。
            var ceiling = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _created.Add(ceiling);
            ceiling.transform.position = new Vector3(0f, _ledgeHeight + 1.2f + 0.1f, _ledgeNearZ + 1.5f);
            ceiling.transform.localScale = new Vector3(3f, 0.2f, 3f);
            Physics.SyncTransforms();

            LogAssert.Expect(LogType.Log, new Regex(@"^\[Player\] mantle=Blocked "));
            _player.Tick(Vector2.zero, false, false, true, _step);

            Assert.That(_player.IsMantling, Is.False);
            Assert.That(_player.LastMantleProbe.Verdict, Is.EqualTo(VaultVerdict.Blocked));
        }

        [Test]
        public void ALedgeAboveTheReachIsNotClimbed()
        {
            LandOnFloor();
            _player.MantleRule = Mantle.On;
            PlaceBox(3f, 3f);

            LogAssert.Expect(LogType.Log, new Regex(@"^\[Player\] mantle=TooHigh "));
            _player.Tick(Vector2.zero, false, false, true, _step);

            Assert.That(_player.IsMantling, Is.False);
            Assert.That(_player.LastMantleProbe.Verdict, Is.EqualTo(VaultVerdict.TooHigh));
        }

        [Test]
        public void ALowObstacleIsStillVaulted()
        {
            LandOnFloor();
            _player.VaultRule = Vault.On;
            _player.MantleRule = Mantle.On;
            PlaceBox(1f, 0.4f);

            LogAssert.Expect(LogType.Log, new Regex(@"^\[Player\] vault=Vault "));
            LogAssert.Expect(LogType.Log, "[Player] vaulting=true");
            _player.Tick(Vector2.zero, false, false, true, _step);

            Assert.That(_player.IsVaulting, Is.True);
            Assert.That(_player.IsMantling, Is.False);
            Assert.That(_player.TryStartMantle(), Is.False, "no climb while vaulting");
        }

        [Test]
        public void TurningMantleOffStopsTheClimb()
        {
            LandOnFloor();
            _player.MantleRule = Mantle.On;
            PlaceBox(_ledgeHeight, 3f);
            LogAssert.Expect(LogType.Log, new Regex(@"^\[Player\] mantle=Mantle "));
            LogAssert.Expect(LogType.Log, "[Player] mantling=true");
            Assert.That(_player.TryStartMantle(), Is.True);

            LogAssert.Expect(LogType.Log, "[Player] mantling=false");
            _player.MantleRule = Mantle.Off;

            Assert.That(_player.IsMantling, Is.False);
        }

        [Test]
        public void NoVaultOrDodgeStartsWhileMantling()
        {
            LandOnFloor();
            _player.VaultRule = Vault.On;
            _player.DodgeRule = Dodge.On;
            _player.MantleRule = Mantle.On;
            PlaceBox(_ledgeHeight, 3f);
            LogAssert.Expect(LogType.Log, new Regex(@"^\[Player\] mantle=Mantle "));
            LogAssert.Expect(LogType.Log, "[Player] mantling=true");
            Assert.That(_player.TryStartMantle(), Is.True);

            Assert.That(_player.TryStartVault(), Is.False);
            Assert.That(_player.TryStartDodge(Vector2.right), Is.False);
            Assert.That(_player.TryStartMantle(), Is.False);
        }

        [Test]
        public void MantleNeedsGround()
        {
            _player.MantleRule = Mantle.On;

            Assert.That(_player.IsGrounded, Is.False);
            Assert.That(_player.TryStartMantle(), Is.False);
        }

        [Test]
        public void MantleLogsHaveTagAndValues()
        {
            Assert.That(PlayerController.FormatMantlingLog(true), Is.EqualTo("[Player] mantling=true"));
            Assert.That(PlayerController.FormatMantlingLog(false), Is.EqualTo("[Player] mantling=false"));

            var result = new VaultProbeResult(VaultVerdict.TooNarrow, 1.8f, 0.4f, System.Numerics.Vector3.UnitZ, null);
            Assert.That(PlayerController.FormatMantleProbeLog(result), Is.EqualTo("[Player] mantle=TooNarrow height=1.80 depth=0.40"));
            var deep = new VaultProbeResult(VaultVerdict.Mantle, 2f, float.PositiveInfinity, System.Numerics.Vector3.UnitZ, null);
            Assert.That(PlayerController.FormatMantleProbeLog(deep), Is.EqualTo("[Player] mantle=Mantle height=2.00 depth=inf"));
        }

        [Test]
        public void RejectsAnUndefinedRule()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() => _player.MantleRule = (Mantle)99);
        }

        // 正面 (+Z) の、体の中心から _ledgeNearZ 先に、奥行き depth・高さ height の箱を床の上に置く。
        private void PlaceBox(float height, float depth)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _created.Add(box);
            box.transform.position = new Vector3(0f, height / 2f, _ledgeNearZ + (depth / 2f));
            box.transform.localScale = new Vector3(3f, height, depth);
            Physics.SyncTransforms();
        }

        private void LandOnFloor()
        {
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _created.Add(floor);
            floor.transform.position = new Vector3(0f, -0.5f, 0f);
            floor.transform.localScale = new Vector3(40f, 1f, 40f);
            Physics.SyncTransforms();

            for (var i = 0; i < 10 && !_player.IsGrounded; i++)
            {
                _player.Tick(Vector2.zero, false, false, false, _step);
            }

            Assert.That(_player.IsGrounded, Is.True, "landed on the floor");
        }

        private float FeetY()
        {
            return _body.transform.position.y + _characterController.center.y - (_characterController.height * 0.5f);
        }
    }
}
