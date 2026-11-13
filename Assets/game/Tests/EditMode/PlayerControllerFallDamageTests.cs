using System;
using Fps.Core;
using Fps.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using Object = UnityEngine.Object;

namespace Fps.Tests.EditMode
{
    /// <summary>
    /// オフラインの落下ダメージ (<see cref="PlayerController.FallDamage"/>)。床の上の高さ drop から落として着地させる。
    /// </summary>
    public sealed class PlayerControllerFallDamageTests
    {
        private const float _tickSeconds = 0.02f;
        private const int _maxTicks = 1000;
        private const int _maxHealth = 100;

        // 重力 20 で安全 10 m/s (落差 2.5 m)・致死 20 m/s (落差 10 m)
        private const float _safeFallSpeed = 10f;
        private const float _lethalFallSpeed = 20f;

        private GameObject _floor;
        private GameObject _body;
        private CharacterController _characterController;
        private PlayerController _player;
        private Damageable _damageable;
        private InputAction _move;
        private InputAction _sprint;
        private InputAction _crouch;
        private InputAction _jump;

        [SetUp]
        public void SetUp()
        {
            _floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _floor.transform.position = new Vector3(0f, -0.5f, 0f);
            _floor.transform.localScale = new Vector3(20f, 1f, 20f);

            _body = new GameObject("Body");
            _characterController = _body.AddComponent<CharacterController>();
            _characterController.height = 2f;
            _characterController.radius = 0.5f;
            _characterController.center = Vector3.zero;
            _player = _body.AddComponent<PlayerController>();
            _damageable = _body.AddComponent<Damageable>();
            _damageable.Initialize("Player", _maxHealth);
            _move = new InputAction("Move", InputActionType.Value, expectedControlType: "Vector2");
            _sprint = new InputAction("Sprint", InputActionType.Button);
            _crouch = new InputAction("Crouch", InputActionType.Button);
            _jump = new InputAction("Jump", InputActionType.Button);

            var look = _body.AddComponent<FirstPersonLook>();
            var spec = new MotorSpec(5f, 8f, 1.2f, 20f, 0.1f, 2.5f, _safeFallSpeed, _lethalFallSpeed);
            _player.Initialize(_characterController, look, _move, _sprint, _crouch, _jump, spec);
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
        }

        [Test]
        public void IsOffByDefault()
        {
            Assert.That(_player.FallDamage, Is.EqualTo(FallDamage.Off));
        }

        [Test]
        public void ApplyRulesTakesTheFallDamageRule()
        {
            _player.ApplyRules(RulesWith(FallDamage.On));
            Assert.That(_player.FallDamage, Is.EqualTo(FallDamage.On));

            _player.ApplyRules(RulesWith(FallDamage.Off));
            Assert.That(_player.FallDamage, Is.EqualTo(FallDamage.Off));
        }

        [Test]
        public void ApplyRulesRejectsNull()
        {
            Assert.Throws<ArgumentNullException>(() => _player.ApplyRules(null));
        }

        [Test]
        public void RejectsAnUndefinedValue()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => _player.FallDamage = (FallDamage)99);
        }

        [Test]
        public void LogsHaveTheTag()
        {
            Assert.That(PlayerController.FormatFallDamageRuleLog(FallDamage.On), Is.EqualTo("[Player] fallDamage=On"));
            Assert.That(PlayerController.FormatFallDamageLog(42, 17.9f, 58), Is.EqualTo("[Player] fall damage=42 speed=17.90 health=58"));
        }

        [Test]
        public void ALowDropDoesNotHurt()
        {
            _player.FallDamage = FallDamage.On;

            DropFrom(1f);

            Assert.That(_damageable.Health.Current, Is.EqualTo(_maxHealth));
        }

        [Test]
        public void AMiddleDropHurtsWithoutKilling()
        {
            _player.FallDamage = FallDamage.On;

            DropFrom(5f);

            Assert.That(_damageable.Health.Current, Is.InRange(1, _maxHealth - 1));
        }

        [Test]
        public void AHighDropKills()
        {
            _player.FallDamage = FallDamage.On;

            DropFrom(15f);

            Assert.That(_damageable.IsDead, Is.True);
        }

        [Test]
        public void DoesNotHurtWhenOff()
        {
            DropFrom(15f);

            Assert.That(_damageable.Health.Current, Is.EqualTo(_maxHealth));
        }

        private static GameRules RulesWith(FallDamage fallDamage)
        {
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
                fallDamage: fallDamage);
        }

        // 足元が床から drop の高さに来るよう置き、接地するまで入力なしで Tick する
        private void DropFrom(float drop)
        {
            _body.transform.position = new Vector3(0f, 1f + drop, 0f);
            Physics.SyncTransforms();

            for (var i = 0; i < _maxTicks; i++)
            {
                _player.Tick(Vector2.zero, false, false, false, _tickSeconds);
                if (i > 0 && _player.IsGrounded)
                {
                    return;
                }
            }

            Assert.Fail("the player never landed");
        }
    }
}
