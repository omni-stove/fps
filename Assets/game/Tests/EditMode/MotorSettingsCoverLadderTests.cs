using Shooter.Core;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    public sealed class MotorSettingsCoverLadderTests
    {
        private const float _frame = 0.02f;
        private const float _wallThickness = 0.4f;

        private MotorSettings _settings;
        private NetMatchDefinition _match;
        private GameObject _body;
        private GameObject _floor;
        private GameObject _wall;
        private InputAction _move;
        private InputAction _sprint;
        private InputAction _crouch;
        private InputAction _jump;

        [SetUp]
        public void SetUp()
        {
            _settings = ScriptableObject.CreateInstance<MotorSettings>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_settings);
            foreach (var created in new Object[] { _match, _body, _floor, _wall })
            {
                if (created != null)
                {
                    Object.DestroyImmediate(created);
                }
            }

            _move?.Dispose();
            _sprint?.Dispose();
            _crouch?.Dispose();
            _jump?.Dispose();
        }

        [Test]
        public void NewSettingsHoldTheDefaults()
        {
            var spec = _settings.CreateSpec();

            Assert.That(spec.CoverMaxEnterDistance, Is.EqualTo(4f));
            Assert.That(spec.CoverMaxEnterAngleDegrees, Is.EqualTo(45f));
            Assert.That(spec.CoverLowWallHeight, Is.EqualTo(1.1f));
            Assert.That(spec.CoverPeekSideDistance, Is.EqualTo(0.6f));
            Assert.That(spec.CoverEnterSeconds, Is.EqualTo(0.15f));
            Assert.That(spec.CoverEnterSpeed, Is.EqualTo(8f));
            Assert.That(spec.CoverBackHoldSeconds, Is.EqualTo(0.2f));
            Assert.That(spec.CoverPeekSlideSeconds, Is.EqualTo(0.15f));
            Assert.That(spec.LadderTopEntryDepth, Is.EqualTo(0.3f));
            Assert.That(spec.LadderMinApproachDot, Is.EqualTo(0.5f));
            Assert.That(spec.LadderMinApproachSpeed, Is.EqualTo(0.5f));
        }

        [Test]
        public void ResolveCarriesTheValues()
        {
            SetValues();

            var spec = MotorSettings.Resolve(_settings);

            Assert.That(spec.CoverMaxEnterDistance, Is.EqualTo(2f));
            Assert.That(spec.CoverMaxEnterAngleDegrees, Is.EqualTo(30f));
            Assert.That(spec.CoverLowWallHeight, Is.EqualTo(1.4f));
            Assert.That(spec.CoverPeekSideDistance, Is.EqualTo(0.9f));
            Assert.That(spec.CoverEnterSeconds, Is.EqualTo(0.4f));
            Assert.That(spec.CoverEnterSpeed, Is.EqualTo(3f));
            Assert.That(spec.CoverBackHoldSeconds, Is.EqualTo(0.7f));
            Assert.That(spec.CoverPeekSlideSeconds, Is.EqualTo(0.25f));
            Assert.That(spec.LadderTopEntryDepth, Is.EqualTo(0.6f));
            Assert.That(spec.LadderMinApproachDot, Is.EqualTo(0.75f));
            Assert.That(spec.LadderMinApproachSpeed, Is.EqualTo(1.5f));
        }

        [Test]
        public void ResetRestoresTheDefaults()
        {
            SetValues();

            _settings.ResetToDefaults();

            var spec = _settings.CreateSpec();
            Assert.That(spec.CoverMaxEnterDistance, Is.EqualTo(MotorSpec.DefaultCoverMaxEnterDistance));
            Assert.That(spec.CoverEnterSeconds, Is.EqualTo(MotorSpec.DefaultCoverEnterSeconds));
            Assert.That(spec.CoverEnterSpeed, Is.EqualTo(MotorSpec.DefaultCoverEnterSpeed));
            Assert.That(spec.CoverPeekSlideSeconds, Is.EqualTo(MotorSpec.DefaultCoverPeekSlideSeconds));
            Assert.That(spec.LadderTopEntryDepth, Is.EqualTo(MotorSpec.DefaultLadderTopEntryDepth));
            Assert.That(spec.LadderMinApproachSpeed, Is.EqualTo(MotorSpec.DefaultLadderMinApproachSpeed));
        }

        [Test]
        public void InvalidValuesThrow()
        {
            _settings.CoverEnterSeconds = 0f;
            Assert.Throws<System.ArgumentOutOfRangeException>(() => MotorSettings.Resolve(_settings));

            _settings.ResetToDefaults();
            _settings.CoverEnterSpeed = 0f;
            Assert.Throws<System.ArgumentOutOfRangeException>(() => MotorSettings.Resolve(_settings));

            _settings.ResetToDefaults();
            _settings.LadderMinApproachDot = 2f;
            Assert.Throws<System.ArgumentOutOfRangeException>(() => MotorSettings.Resolve(_settings));
        }

        [Test]
        public void NetMatchToMotorCarriesTheValues()
        {
            SetValues();
            _match = ScriptableObject.CreateInstance<NetMatchDefinition>();
            _match.MotorSettings = _settings;

            var motor = _match.ToMotor().Motor;

            Assert.That(motor.CoverMaxEnterDistance, Is.EqualTo(2f));
            Assert.That(motor.CoverPeekSlideSeconds, Is.EqualTo(0.25f));
            Assert.That(motor.CoverEnterSpeed, Is.EqualTo(3f));
            Assert.That(motor.LadderMinApproachSpeed, Is.EqualTo(1.5f));
        }

        [TestCase(1f, false)]
        [TestCase(2f, true)]
        public void TheCoverControllerEntersAWall1Point5MetersAwayOnlyWithinTheEnterDistance(float enterDistance, bool enters)
        {
            _settings.CoverMaxEnterDistance = enterDistance;
            var cover = CreateCoverPlayer(_settings.CreateSpec());
            BuildWall(faceZ: 1.5f);

            cover.Tick(true, false, Vector2.zero, _frame);

            Assert.That(cover.IsInCover, Is.EqualTo(enters));
        }

        // 3 m 先の壁まで、半径 0.5 m と隙間 0.05 m を残した 2.45 m を走る。1 フレーム目は入るだけで動かない。
        [TestCase(8f, 17)]
        [TestCase(4f, 32)]
        public void TheCoverControllerRunsToAFarWallAtTheEnterSpeed(float speed, int expectedFrames)
        {
            _settings.CoverEnterSpeed = speed;
            var cover = CreateCoverPlayer(_settings.CreateSpec());
            BuildWall(faceZ: 3f);

            cover.Tick(true, false, Vector2.zero, _frame);
            Assert.That(cover.Phase, Is.EqualTo(CoverPhase.Entering));
            var frames = 1;
            while (cover.Phase == CoverPhase.Entering && frames < 500)
            {
                cover.Tick(false, false, Vector2.zero, _frame);
                Physics.SyncTransforms();
                frames++;
            }

            Assert.That(cover.Phase, Is.EqualTo(CoverPhase.In));
            Assert.That(frames, Is.EqualTo(expectedFrames).Within(1));
            Assert.That(_body.transform.position.z, Is.EqualTo(3f - 0.55f).Within(0.03f), "stops at the hold distance from the wall");
        }

        private void SetValues()
        {
            _settings.CoverMaxEnterDistance = 2f;
            _settings.CoverMaxEnterAngleDegrees = 30f;
            _settings.CoverLowWallHeight = 1.4f;
            _settings.CoverPeekSideDistance = 0.9f;
            _settings.CoverEnterSeconds = 0.4f;
            _settings.CoverEnterSpeed = 3f;
            _settings.CoverBackHoldSeconds = 0.7f;
            _settings.CoverPeekSlideSeconds = 0.25f;
            _settings.LadderTopEntryDepth = 0.6f;
            _settings.LadderMinApproachDot = 0.75f;
            _settings.LadderMinApproachSpeed = 1.5f;
        }

        private CoverController CreateCoverPlayer(MotorSpec spec)
        {
            _floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _floor.transform.position = new Vector3(0f, -0.5f, 0f);
            _floor.transform.localScale = new Vector3(20f, 1f, 20f);

            _body = new GameObject("Body");
            _body.transform.position = new Vector3(0f, 1f, 0f);
            var controller = _body.AddComponent<CharacterController>();
            controller.height = 2f;
            controller.radius = 0.5f;
            controller.center = Vector3.zero;
            var look = _body.AddComponent<FirstPersonLook>();
            var player = _body.AddComponent<PlayerController>();
            _move = new InputAction("Move", InputActionType.Value, expectedControlType: "Vector2");
            _sprint = new InputAction("Sprint", InputActionType.Button);
            _crouch = new InputAction("Crouch", InputActionType.Button);
            _jump = new InputAction("Jump", InputActionType.Button);
            player.Initialize(controller, look, _move, _sprint, _crouch, _jump, spec);

            var cover = _body.AddComponent<CoverController>();
            cover.Configure(CoverRules());
            cover.Initialize(player, null, null, null);
            Physics.SyncTransforms();
            return cover;
        }

        private void BuildWall(float faceZ)
        {
            _wall = new GameObject("Wall");
            _wall.transform.position = new Vector3(0f, 1f, faceZ + (_wallThickness * 0.5f));
            _wall.AddComponent<BoxCollider>().size = new Vector3(4f, 2f, _wallThickness);
            Physics.SyncTransforms();
        }

        private static GameRules CoverRules()
        {
            var actions = new ActionRules(
                Downed.Off,
                ActionRules.DefaultDownedBleedoutSeconds,
                ActionRules.DefaultDownedHealthFraction,
                ActionRules.DefaultReviveSeconds,
                Cover.On,
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
    }
}
