using System.Collections.Generic;
using NUnit.Framework;
using Shooter.Core;
using Shooter.Runtime;
using UnityEngine;
using UnityEngine.InputSystem;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    // 板は厚さ 0.5・幅 8 で、置いた位置の上面が y = 0。体は高さ 2・半径 0.5・中心 0 なので、立つと本体は y = 1 に来る。
    public sealed class PlayerControllerPlatformTests
    {
        private const float _tick = 0.02f;
        private const int _settleTicks = 10;
        private const float _halfThickness = 0.25f;

        private readonly List<Object> _created = new List<Object>();
        private GameObject _body;
        private PlayerController _player;
        private InputAction _move;
        private InputAction _sprint;
        private InputAction _crouch;
        private InputAction _jump;

        [TearDown]
        public void TearDown()
        {
            _move?.Dispose();
            _sprint?.Dispose();
            _crouch?.Dispose();
            _jump?.Dispose();
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
        public void StandingOnASlidingPlatformMovesWithIt()
        {
            var platform = NewPlatform(Settings(PlatformRouteKind.PingPong, 2f), new Vector3(0f, 0f, 20f));
            NewPlayer(new Vector3(0f, 1f, 0f));
            Settle();
            var bodyStart = _body.transform.position;
            var platformStart = platform.transform.position;

            Run(platform, 50, Vector2.zero, false);

            var carried = platform.transform.position - platformStart;
            Assert.That(carried.z, Is.EqualTo(2f).Within(1e-3f));
            Assert.That(_body.transform.position.z - bodyStart.z, Is.EqualTo(carried.z).Within(0.02f));
            Assert.That(_body.transform.position.y, Is.EqualTo(bodyStart.y).Within(0.02f));
            Assert.That(_player.IsGrounded, Is.True);
            Assert.That(_player.Platform, Is.SameAs(platform));
            Assert.That(platform.IsRiddenBy(_player), Is.True);
            Assert.That(_player.Motor.Velocity.Z, Is.EqualTo(0f).Within(1e-4f), "the speed on the platform is relative to it");
        }

        [Test]
        public void ARisingElevatorCarriesTheBodyUpAndKeepsItGrounded()
        {
            var platform = NewPlatform(Settings(PlatformRouteKind.OnCall, 1.5f), new Vector3(0f, 3f, 0f));
            NewPlayer(new Vector3(0f, 1f, 0f));
            Settle();
            var startY = _body.transform.position.y;

            for (var i = 0; i < 100; i++)
            {
                Frame(platform, Vector2.zero, false);
                Assert.That(_player.IsGrounded, Is.True, $"frame {i} should stay on the elevator");
            }

            Assert.That(platform.transform.position.y, Is.EqualTo(3f - _halfThickness).Within(1e-3f), "boarding sent the elevator up");
            Assert.That(_body.transform.position.y - startY, Is.EqualTo(3f).Within(0.05f));
        }

        [Test]
        public void ASpinningPlatformCarriesTheBodyRoundAndTurnsIt()
        {
            var settings = Settings(PlatformRouteKind.PingPong, 0f);
            settings.SpinDegreesPerSecond = 90f;
            var platform = NewPlatform(settings);
            NewPlayer(new Vector3(2f, 1f, 0f));
            Settle();

            Run(platform, 50, Vector2.zero, false);

            // 上から見て時計回りに 90 度回ると、+X にいた体は -Z へ来る
            Assert.That(_body.transform.position.x, Is.EqualTo(0f).Within(0.05f));
            Assert.That(_body.transform.position.z, Is.EqualTo(-2f).Within(0.05f));
            Assert.That(Mathf.DeltaAngle(_body.transform.eulerAngles.y, 90f), Is.EqualTo(0f).Within(0.5f));
        }

        [Test]
        public void JumpingOffAMovingPlatformKeepsItsSpeed()
        {
            var platform = NewPlatform(Settings(PlatformRouteKind.PingPong, 3f), new Vector3(20f, 0f, 0f));
            NewPlayer(new Vector3(0f, 1f, 0f));
            Settle();
            Run(platform, 10, Vector2.zero, false);

            Frame(platform, Vector2.zero, true);

            Assert.That(_player.IsGrounded, Is.False);
            Assert.That(_player.Platform, Is.Null);
            Assert.That(platform.RiderCount, Is.Zero);
            Assert.That(_player.Motor.Velocity.X, Is.EqualTo(3f).Within(1e-3f));
            Assert.That(_player.Motor.Velocity.Y, Is.GreaterThan(0f));

            var x = _body.transform.position.x;
            Frame(platform, Vector2.zero, false);

            // 宙に出た後は床に運ばれず、引き継いだ速さを空中の制御で落としながら進む
            var expected = 3f * (1f - MotorSpec.DefaultAirControl) * _tick;
            Assert.That(_body.transform.position.x - x, Is.EqualTo(expected).Within(2e-3f));
        }

        [Test]
        public void ARidingBodyIsNotSqueezedIntoTheCeilingAndTheElevatorTurnsBack()
        {
            const float ceilingBottom = 3f;
            var ceiling = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _created.Add(ceiling);
            ceiling.transform.position = new Vector3(0f, ceilingBottom + 0.5f, 0f);
            ceiling.transform.localScale = new Vector3(8f, 1f, 8f);
            var platform = NewPlatform(Settings(PlatformRouteKind.OnCall, 2f), new Vector3(0f, 4f, 0f));
            NewPlayer(new Vector3(0f, 1f, 0f));
            Settle();
            var blocked = false;

            for (var i = 0; i < 150; i++)
            {
                Frame(platform, Vector2.zero, false);
                blocked |= _player.LastRide.IsBlocked;
                var bodyY = _body.transform.position.y;
                var platformTop = platform.transform.position.y + _halfThickness;
                Assert.That(bodyY + 1f, Is.LessThanOrEqualTo(ceilingBottom + 0.01f), $"frame {i} pushed the head into the ceiling");
                Assert.That(bodyY - 1f, Is.GreaterThanOrEqualTo(platformTop - 0.05f), $"frame {i} let the elevator pass through the feet");
            }

            Assert.That(blocked, Is.True, "the rise should be blocked by the ceiling");
            Assert.That(platform.transform.position.y + _halfThickness, Is.LessThan(ceilingBottom - 2f + 0.05f));
        }

        [Test]
        public void SteppingOffOntoStillGroundLeavesThePlatform()
        {
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _created.Add(ground);
            ground.transform.position = new Vector3(0f, -_halfThickness, 9f);
            ground.transform.localScale = new Vector3(8f, 0.5f, 10f);
            var platform = NewPlatform(Settings(PlatformRouteKind.PingPong, 0f));
            NewPlayer(new Vector3(0f, 1f, 0f));
            Settle();
            Assert.That(_player.Platform, Is.SameAs(platform));

            Run(platform, 60, new Vector2(0f, 1f), false);

            Assert.That(_body.transform.position.z, Is.GreaterThan(4.5f));
            Assert.That(_player.IsGrounded, Is.True);
            Assert.That(_player.Platform, Is.Null);
            Assert.That(platform.RiderCount, Is.Zero);
        }

        private void Settle()
        {
            for (var i = 0; i < _settleTicks; i++)
            {
                _player.Tick(Vector2.zero, false, false, false, _tick);
            }

            Assert.That(_player.IsGrounded, Is.True, "the body should stand before the test");
        }

        private void Run(MovingPlatform platform, int frames, Vector2 move, bool jump)
        {
            for (var i = 0; i < frames; i++)
            {
                Frame(platform, move, jump);
            }
        }

        // MovingPlatform は PlayerController より先に動く (DefaultExecutionOrder) ので、同じ順で進める
        private void Frame(MovingPlatform platform, Vector2 move, bool jump)
        {
            platform.Step(_tick);
            _player.Tick(move, false, false, jump, _tick);
        }

        private MovingPlatform NewPlatform(MovingPlatformSettings settings, params Vector3[] moreStops)
        {
            var slab = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _created.Add(slab);
            slab.name = "Platform";
            slab.transform.position = new Vector3(0f, -_halfThickness, 0f);
            slab.transform.localScale = new Vector3(8f, _halfThickness * 2f, 8f);
            var platform = slab.AddComponent<MovingPlatform>();
            var stops = new List<Vector3> { Vector3.zero };
            stops.AddRange(moreStops);
            platform.Configure(settings, stops);
            return platform;
        }

        private MovingPlatformSettings Settings(PlatformRouteKind kind, float speed)
        {
            var settings = ScriptableObject.CreateInstance<MovingPlatformSettings>();
            _created.Add(settings);
            settings.Kind = kind;
            settings.Speed = speed;
            settings.WaitSeconds = 0f;
            settings.SpinDegreesPerSecond = 0f;
            settings.DepartWhenBoarded = true;
            return settings;
        }

        private void NewPlayer(Vector3 position)
        {
            _body = new GameObject("Rider");
            _created.Add(_body);
            _body.transform.position = position;
            var controller = _body.AddComponent<CharacterController>();
            controller.height = 2f;
            controller.radius = 0.5f;
            controller.center = Vector3.zero;

            var eye = new GameObject(GameBootstrap.EyeName).transform;
            eye.SetParent(_body.transform, false);
            eye.localPosition = new Vector3(0f, 0.6f, 0f);

            _player = _body.AddComponent<PlayerController>();
            _move = new InputAction("Move", InputActionType.Value, expectedControlType: "Vector2");
            _sprint = new InputAction("Sprint", InputActionType.Button);
            _crouch = new InputAction("Crouch", InputActionType.Button);
            _jump = new InputAction("Jump", InputActionType.Button);
            var look = _body.AddComponent<FirstPersonLook>();
            _player.Initialize(controller, look, _move, _sprint, _crouch, _jump, MotorSpec.Default);
            _player.Water = NoWaterVolumes.Instance;
            _player.SwimTuning = SwimTuning.Default;
            _player.WallRunTuning = WallRunTuning.Default;
        }
    }
}
