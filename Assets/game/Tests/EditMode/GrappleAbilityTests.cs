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
    // Edit Mode でも Physics の Raycast と CharacterController.Move はコライダーに当たるので、置いたら SyncTransforms してから使う。
    // AbilityHost の Update は走らないので、能力は Step、移動は PlayerController.Tick を交互に呼んで進める。
    public sealed class GrappleAbilityTests
    {
        private const float _step = 0.02f;
        private const int _maxTicks = 400;

        // 正面 (+Z) の壁。手前の面の z と高さ。崖のぼりで上に立てるよう奥行きを持たせる。
        private const float _wallFaceZ = 10f;
        private const float _wallHeight = 5f;
        private const float _wallDepth = 3f;

        private static readonly GameRules _on = GameRules.Default.WithActions(ActionRules.Default.WithAbilities(Abilities.On));

        private readonly List<Object> _created = new List<Object>();
        private GameObject _body;
        private CharacterController _controller;
        private PlayerController _mover;
        private AbilityHost _host;
        private GrappleAbilityDefinition _definition;
        private InputAction _move;
        private InputAction _sprint;
        private InputAction _crouch;
        private InputAction _jump;

        [SetUp]
        public void SetUp()
        {
            _body = new GameObject("GrappleAbilityTests.Player");
            _body.transform.position = new Vector3(0f, 1f, 0f);
            _controller = _body.AddComponent<CharacterController>();
            _controller.height = 2f;
            _controller.radius = 0.5f;
            _controller.center = Vector3.zero;
            _mover = _body.AddComponent<PlayerController>();
            var look = _body.AddComponent<FirstPersonLook>();
            _move = new InputAction("Move", InputActionType.Value, expectedControlType: "Vector2");
            _sprint = new InputAction("Sprint", InputActionType.Button);
            _crouch = new InputAction("Crouch", InputActionType.Button);
            _jump = new InputAction("Jump", InputActionType.Button);
            _mover.Initialize(_controller, look, _move, _sprint, _crouch, _jump, new MotorSpec(5f, 8f, 1.2f, 20f, 0.1f, 2.5f));
            _host = _body.AddComponent<AbilityHost>();
            _host.Configure(_on);

            _definition = ScriptableObject.CreateInstance<GrappleAbilityDefinition>();
            _definition.name = "Grapple";
            _definition.Id = "grapple";
            _definition.MaxCharges = 2;
            _definition.RechargeSeconds = 10f;
            _definition.CooldownSeconds = 0f;
            _definition.WindupSeconds = 0f;
            _definition.GrabbableLayers = 1 << 0;
            _created.Add(_definition);

            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Floor";
            floor.transform.position = new Vector3(0f, -0.5f, 0f);
            floor.transform.localScale = new Vector3(60f, 1f, 60f);
            _created.Add(floor);
            Physics.SyncTransforms();
            for (var i = 0; i < 10 && !_mover.IsGrounded; i++)
            {
                _mover.Tick(Vector2.zero, false, false, false, _step);
            }

            Assert.That(_mover.IsGrounded, Is.True, "landed on the floor");
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
        public void NothingInRangeIsRefusedAndKeepsTheCharge()
        {
            _host.Equip(0, _definition);
            Face(Vector3.up);

            LogAssert.Expect(LogType.Log, "[Grapple] miss reason=OutOfRange surface=- distance=-");
            LogAssert.Expect(LogType.Log, "[Ability] slot=1 id=grapple rejected reason=Refused");
            Assert.That(_host.TryUse(0), Is.False);

            Assert.That(_host.Slot(0).Charges, Is.EqualTo(2));
            Assert.That(_host.IsRunning(0), Is.False);
            Assert.That(_mover.MovementOverrides, Is.Empty);
        }

        [Test]
        public void AWallBeyondTheRangeIsOutOfRange()
        {
            _definition.MaxRange = 5f;
            _host.Equip(0, _definition);
            PlaceWall(1 << 0);
            Face(Vector3.forward);

            LogAssert.Expect(LogType.Log, new Regex(@"^\[Grapple\] miss reason=OutOfRange "));
            Assert.That(_host.TryUse(0), Is.False);
            Assert.That(_host.Slot(0).Charges, Is.EqualTo(2));
        }

        [Test]
        public void ASurfaceTooCloseIsRefused()
        {
            _definition.MinRange = 12f;
            _host.Equip(0, _definition);
            PlaceWall(0);
            Face(Vector3.forward);

            LogAssert.Expect(LogType.Log, new Regex(@"^\[Grapple\] miss reason=TooClose surface=Wall distance=10\.00$"));
            Assert.That(_host.TryUse(0), Is.False);
            Assert.That(_host.Slot(0).Charges, Is.EqualTo(2));
        }

        [Test]
        public void ASurfaceOnAnotherLayerIsNotGrabbableAndBlocksTheRope()
        {
            _host.Equip(0, _definition);
            var wall = PlaceWall(0);
            wall.layer = 1;

            // 掴めない壁の奥に掴める壁があっても、縄は手前の壁で止まる。
            var behind = PlaceBox("Behind", new Vector3(0f, 2.5f, _wallFaceZ + _wallDepth + 1f), new Vector3(6f, 5f, 1f));
            behind.layer = 0;
            Face(new Vector3(0f, 0.2f, 1f));

            LogAssert.Expect(LogType.Log, new Regex(@"^\[Grapple\] miss reason=NotGrabbable surface=Wall distance=\d+\.\d\d$"));
            Assert.That(_host.TryUse(0), Is.False);
            Assert.That(_host.Slot(0).Charges, Is.EqualTo(2));
            Assert.That(_host.IsRunning(0), Is.False);
        }

        [Test]
        public void TagsNarrowTheGrabbableSurfaces()
        {
            var filter = new GrappleSurfaceFilter(1 << 0, new[] { "Finish" });
            var tagged = PlaceBox("Tagged", new Vector3(20f, 0.5f, 20f), Vector3.one);
            tagged.tag = "Finish";
            var untagged = PlaceBox("Untagged", new Vector3(22f, 0.5f, 20f), Vector3.one);
            var wrongLayer = PlaceBox("WrongLayer", new Vector3(24f, 0.5f, 20f), Vector3.one);
            wrongLayer.tag = "Finish";
            wrongLayer.layer = 1;

            Assert.That(filter.Allows(tagged), Is.True);
            Assert.That(filter.Allows(untagged), Is.False);
            Assert.That(filter.Allows(wrongLayer), Is.False, "the layer must match too");
            Assert.That(filter.Allows(null), Is.False);
            Assert.That(new GrappleSurfaceFilter(1 << 0, null).Allows(untagged), Is.True, "no tags means any tag");
            Assert.Throws<System.ArgumentException>(() => new GrappleSurfaceFilter(1 << 0, new[] { " " }));
        }

        [Test]
        public void AimingNearTheTopSnapsTheAnchorToTheEdge()
        {
            var ability = (GrappleAbility)_definition.CreateAbility();
            PlaceWall(0);
            var origin = _body.transform.position;
            var target = new Vector3(0f, _wallHeight - 0.7f, _wallFaceZ);

            var miss = ability.TryAim(_body.transform, origin, target - origin, out var aim);

            Assert.That(miss, Is.EqualTo(GrappleMiss.None));
            Assert.That(aim.Surface.name, Is.EqualTo("Wall"));
            Assert.That(aim.SnappedToEdge, Is.True);
            Assert.That(aim.Point.y, Is.EqualTo(_wallHeight).Within(1e-3f));
            Assert.That(aim.Point.z, Is.EqualTo(_wallFaceZ).Within(1e-3f));

            var low = new Vector3(0f, 1.5f, _wallFaceZ);
            Assert.That(ability.TryAim(_body.transform, origin, low - origin, out var lowAim), Is.EqualTo(GrappleMiss.None));
            Assert.That(lowAim.SnappedToEdge, Is.False, "the edge is too far above");
            Assert.That(lowAim.Point.y, Is.EqualTo(1.5f).Within(1e-3f));
        }

        [Test]
        public void PullsToTheEdgeAndClimbsWhenMantleIsOn()
        {
            _mover.MantleRule = Mantle.On;
            _host.Equip(0, _definition);
            PlaceWall(0);
            Face(new Vector3(0f, _wallHeight - 0.5f, _wallFaceZ) - _body.transform.position);

            LogAssert.Expect(LogType.Log, new Regex(@"^\[Grapple\] attached surface=Wall distance=\d+\.\d\d edge=true$"));
            LogAssert.Expect(LogType.Log, new Regex(@"^\[Grapple\] arrived seconds=\d+\.\d\d wall=true$"));
            LogAssert.Expect(LogType.Log, "[Grapple] climb started=true");
            Assert.That(_host.TryUse(0), Is.True);
            Assert.That(_host.Slot(0).Charges, Is.EqualTo(1));

            var sawAbilityMoving = false;
            for (var i = 0; i < _maxTicks && !_mover.IsMantling && _host.IsRunning(0); i++)
            {
                _host.Step(PlayerCommands.None, _step);
                _mover.Tick(Vector2.zero, false, false, false, _step);
                sawAbilityMoving |= (_mover.MovementOverrideConditions & PlayerActionConditions.AbilityMoving) != 0;
            }

            Assert.That(sawAbilityMoving, Is.True);
            Assert.That(_mover.IsMantling, Is.True, "climbs from where it stopped");
            Assert.That(_host.IsRunning(0), Is.False);
            Assert.That(_mover.IsMovementOverridden, Is.False);

            for (var i = 0; i < _maxTicks && _mover.IsMantling; i++)
            {
                _mover.Tick(Vector2.zero, false, false, false, _step);
            }

            for (var i = 0; i < 10; i++)
            {
                _mover.Tick(Vector2.zero, false, false, false, _step);
            }

            Assert.That(_mover.IsGrounded, Is.True);
            Assert.That(FeetY(), Is.EqualTo(_wallHeight).Within(0.15f), "standing on top of the wall");
            Assert.That(_body.transform.position.z - 0.5f, Is.GreaterThan(_wallFaceZ));
        }

        [Test]
        public void WithMantleOffItStopsAtTheEdgeThenDrops()
        {
            _definition.HangSeconds = 0.2f;
            _host.Equip(0, _definition);
            PlaceWall(0);
            Face(new Vector3(0f, _wallHeight - 0.5f, _wallFaceZ) - _body.transform.position);
            Assert.That(_host.TryUse(0), Is.True);

            for (var i = 0; i < _maxTicks && !IsHanging(); i++)
            {
                _mover.Tick(Vector2.zero, false, false, false, _step);
                _host.Step(PlayerCommands.None, _step);
            }

            Assert.That(IsHanging(), Is.True, "stops at the edge");
            var hangY = _body.transform.position.y;
            Assert.That(hangY + 0.8f, Is.EqualTo(_wallHeight).Within(0.35f), "the hand is at the edge");
            Assert.That(_body.transform.forward.z, Is.EqualTo(1f).Within(1e-3f), "faces the wall");

            _mover.Tick(Vector2.zero, false, false, false, _step);
            Assert.That(_body.transform.position.y, Is.EqualTo(hangY).Within(1e-3f), "holds without falling");

            LogAssert.Expect(LogType.Log, new Regex(@"^\[Grapple\] released reason=Dropped "));
            for (var i = 0; i < 20 && _host.IsRunning(0); i++)
            {
                _host.Step(PlayerCommands.None, _step);
                _mover.Tick(Vector2.zero, false, false, false, _step);
            }

            Assert.That(_host.IsRunning(0), Is.False);
            Assert.That(_mover.IsMantling, Is.False);
            _mover.Tick(Vector2.zero, false, false, false, _step);
            Assert.That(_body.transform.position.y, Is.LessThan(hangY), "falls once it lets go");
        }

        [Test]
        public void JumpingOffTheRopeKeepsTheMomentum()
        {
            _host.Equip(0, _definition);
            PlaceWall(0);
            var start = _body.transform.position;
            Face(new Vector3(0f, _wallHeight - 0.5f, _wallFaceZ) - start);
            Assert.That(_host.TryUse(0), Is.True);

            for (var i = 0; i < _maxTicks && Vector3.Distance(_body.transform.position, start) < 3f; i++)
            {
                _mover.Tick(Vector2.zero, false, false, false, _step);
                _host.Step(PlayerCommands.None, _step);
            }

            Assert.That(_host.IsRunning(0), Is.True, "still pulled");
            _mover.Tick(Vector2.zero, false, false, true, _step);
            var velocity = _mover.Motor.Velocity;

            LogAssert.Expect(LogType.Log, new Regex(@"^\[Grapple\] released reason=Jumped speed=\d+\.\d\d$"));
            _host.Step(PlayerCommands.None, _step);

            Assert.That(_host.IsRunning(0), Is.False);
            Assert.That(_mover.IsMovementOverridden, Is.False);
            Assert.That(velocity.Z, Is.GreaterThan(5f), "keeps flying toward the wall");
            Assert.That(velocity.Y, Is.GreaterThan(0f), "and upward");

            var before = _body.transform.position;
            _mover.Tick(Vector2.zero, false, false, false, _step);
            Assert.That(_body.transform.position.z, Is.GreaterThan(before.z), "the momentum carries on after the rope");
        }

        [Test]
        public void DeathOrRuleOffInterruptsThePull()
        {
            _host.Equip(0, _definition);
            PlaceWall(0);
            Face(new Vector3(0f, _wallHeight - 0.5f, _wallFaceZ) - _body.transform.position);
            Assert.That(_host.TryUse(0), Is.True);
            _mover.Tick(Vector2.zero, false, false, false, _step);
            Assert.That(_mover.IsMovementOverridden, Is.True);

            LogAssert.Expect(LogType.Log, new Regex(@"^\[Grapple\] released reason=Interrupted "));
            _host.Configure(GameRules.Default);

            Assert.That(_host.IsRunning(0), Is.False);
            Assert.That(_mover.IsMovementOverridden, Is.False);
            Assert.That(_mover.MovementOverrides, Is.Empty);
        }

        [Test]
        public void ALostSurfaceLetsGo()
        {
            _host.Equip(0, _definition);
            var wall = PlaceWall(0);
            Face(new Vector3(0f, _wallHeight - 0.5f, _wallFaceZ) - _body.transform.position);
            Assert.That(_host.TryUse(0), Is.True);
            _mover.Tick(Vector2.zero, false, false, false, _step);

            wall.SetActive(false);
            LogAssert.Expect(LogType.Log, new Regex(@"^\[Grapple\] released reason=Lost "));
            _host.Step(PlayerCommands.None, _step);

            Assert.That(_host.IsRunning(0), Is.False);
            Assert.That(_mover.IsMovementOverridden, Is.False);
        }

        [Test]
        public void TheWindupDelaysThePull()
        {
            _definition.WindupSeconds = 0.1f;
            _host.Equip(0, _definition);
            PlaceWall(0);
            Face(new Vector3(0f, _wallHeight - 0.5f, _wallFaceZ) - _body.transform.position);
            var start = _body.transform.position;

            Assert.That(_host.TryUse(0), Is.True);
            _mover.Tick(Vector2.zero, false, false, false, _step);
            _host.Step(PlayerCommands.None, _step);
            _mover.Tick(Vector2.zero, false, false, false, _step);
            Assert.That(_mover.IsMovementOverridden, Is.False, "not pulled before the effect");
            Assert.That(_body.transform.position.z, Is.EqualTo(start.z).Within(1e-3f));

            for (var i = 0; i < 10; i++)
            {
                _host.Step(PlayerCommands.None, _step);
                _mover.Tick(Vector2.zero, false, false, false, _step);
            }

            Assert.That(_mover.IsMovementOverridden, Is.True);
            Assert.That(_body.transform.position.z, Is.GreaterThan(start.z + 0.1f));
        }

        [Test]
        public void InvalidValuesFailToEquip()
        {
            _definition.MinRange = 40f;
            _definition.MaxRange = 30f;

            Assert.Throws<AbilityDefinitionException>(() => _host.Equip(0, _definition));
            Assert.That(_host.Definition(0), Is.Null);

            _definition.MinRange = 2f;
            _definition.GrabbableTags = new[] { "" };
            Assert.Throws<AbilityDefinitionException>(() => _host.Equip(0, _definition));
        }

        private bool IsHanging()
        {
            return _mover.ActiveMovementOverride is AbilityMovementOverride wrapped
                && wrapped.Inner is GrappleMovement movement
                && movement.IsHanging;
        }

        // 正面 (+Z) に、手前の面が _wallFaceZ に来る高い壁を置く。
        private GameObject PlaceWall(int layer)
        {
            var wall = PlaceBox("Wall", new Vector3(0f, _wallHeight / 2f, _wallFaceZ + (_wallDepth / 2f)), new Vector3(6f, _wallHeight, _wallDepth));
            wall.layer = layer;
            return wall;
        }

        private GameObject PlaceBox(string name, Vector3 center, Vector3 size)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.position = center;
            box.transform.localScale = size;
            _created.Add(box);
            Physics.SyncTransforms();
            return box;
        }

        // ViewRig が無いので、能力は本体の正面へ撃つ。上下も本体ごと傾ける。
        private void Face(Vector3 direction)
        {
            _body.transform.rotation = Quaternion.LookRotation(direction.normalized, Mathf.Abs(direction.normalized.y) > 0.99f ? Vector3.forward : Vector3.up);
            Physics.SyncTransforms();
        }

        private float FeetY()
        {
            return _body.transform.position.y + _controller.center.y - (_controller.height * 0.5f);
        }
    }
}
