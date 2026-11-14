using System.Text.RegularExpressions;
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
    // Edit Mode では Awake・Update が走らないので、参照は Initialize で渡し、振りの時間は Tick で進める。寸法は BlindFireControllerTests と同じ。
    public sealed class MeleeControllerTests
    {
        private const float _frame = 0.02f;
        private const float _height = 2f;
        private const float _radius = 0.5f;
        private static readonly Vector3 _bodyPosition = new Vector3(0f, 1f, 0f);
        private static readonly Vector3 _eyeOffset = new Vector3(0f, 0.6f, 0f);

        private GameObject _body;
        private GameObject _floor;
        private TargetDummy _dummy;
        private PlayerController _player;
        private WeaponController _weapon;
        private MeleeController _melee;
        private WeaponDefinition _rifle;
        private InputAction _move;
        private InputAction _sprint;
        private InputAction _crouch;
        private InputAction _jump;
        private InputAction _attack;
        private InputAction _reload;
        private InputAction _meleeAction;

        private static GameRules Rules(Melee melee)
        {
            return GameRules.Default.WithActions(ActionRules.Default.WithMelee(melee));
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

            var eye = new GameObject("Eye").transform;
            eye.SetParent(_body.transform, false);
            eye.localPosition = _eyeOffset;
            var camera = new GameObject("Camera").AddComponent<Camera>();
            camera.transform.SetParent(eye, false);
            _body.AddComponent<ViewRig>().Initialize(eye, camera);

            var look = _body.AddComponent<FirstPersonLook>();
            _player = _body.AddComponent<PlayerController>();
            _move = new InputAction("Move", InputActionType.Value, expectedControlType: "Vector2");
            _sprint = new InputAction("Sprint", InputActionType.Button);
            _crouch = new InputAction("Crouch", InputActionType.Button);
            _jump = new InputAction("Jump", InputActionType.Button);
            _player.Initialize(characterController, look, _move, _sprint, _crouch, _jump, new MotorSpec(5f, 8f, 1.2f, 20f, 1f, 2.5f));

            _rifle = ScriptableObject.CreateInstance<WeaponDefinition>();
            _rifle.name = "Rifle";
            WeaponAssets.Configure(_rifle, TestWeapons.RifleSpec());
            _attack = new InputAction("Attack", InputActionType.Button);
            _reload = new InputAction("Reload", InputActionType.Button);
            _weapon = camera.gameObject.AddComponent<WeaponController>();
            _weapon.Initialize(camera.transform, _attack, _reload, _rifle);

            _meleeAction = new InputAction("Melee", InputActionType.Button);
            _melee = _body.AddComponent<MeleeController>();
            _melee.Initialize(_player, _weapon, _meleeAction);
            _melee.Spec = new MeleeSpec(40, 2f, 90f, 0.5f, 0.4f, 0.2f, 0.75f);
            _melee.Configure(Rules(Melee.On));
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
            _meleeAction.Dispose();
            Object.DestroyImmediate(_body);
            Object.DestroyImmediate(_floor);
            Object.DestroyImmediate(_rifle);
            if (_dummy != null)
            {
                Object.DestroyImmediate(_dummy.gameObject);
            }
        }

        [Test]
        public void ConfiguringHooksIntoTheWeaponAndTheMover()
        {
            Assert.That(_melee.IsInitialized, Is.True);
            Assert.That(_weapon.Modifiers, Does.Contain(_melee));
            Assert.That(_player.MovementOverrides, Does.Contain(_melee));

            _melee.Configure(Rules(Melee.Off));
            Assert.That(_melee.enabled, Is.False);
            Assert.That(_weapon.Modifiers, Does.Not.Contain(_melee));
            Assert.That(_player.MovementOverrides, Does.Not.Contain(_melee));
        }

        [Test]
        public void WithTheRuleOffItCannotSwing()
        {
            _melee.Configure(Rules(Melee.Off));
            LogAssert.Expect(LogType.Log, "[Melee] refused id=Body reason=Off");
            Assert.That(_melee.TrySwing(), Is.False);
            Assert.That(_melee.IsSwinging, Is.False);
        }

        [Test]
        public void ANearTargetAheadIsHitWithMeleeDamageFromTheSwingDirection()
        {
            PlaceDummy(new Vector3(0f, 0f, 1.5f));
            DamageReport? received = null;
            _dummy.Damageable.Damaged += (_, report) => received = report;

            LogAssert.Expect(LogType.Log, "[Melee] swing id=Body");
            Assert.That(_melee.TrySwing(), Is.True);
            Assert.That(_melee.IsSwinging, Is.True);

            LogAssert.Expect(LogType.Log, new Regex(@"^\[Melee\] hit id=Body target=Dummy dmg=40 distance=\d+\.\d\d kind=Melee$"));
            Assert.That(_melee.Tick(0.1f), Is.False);
            Assert.That(_melee.Tick(0.1f), Is.True);

            Assert.That(_dummy.Damageable.Health.Current, Is.EqualTo(TargetDummy.DefaultMaxHealth - 40));
            Assert.That(received.HasValue, Is.True);
            Assert.That(received.Value.Kind, Is.EqualTo(DamageKind.Melee));
            Assert.That(received.Value.Zone.HasValue, Is.False);
            Assert.That(received.Value.Direction.HasValue, Is.True);
            Assert.That(Vector3.Dot(received.Value.Direction.Value, Vector3.forward), Is.GreaterThan(0.99f));
        }

        [Test]
        public void ATargetBehindIsNotHit()
        {
            PlaceDummy(new Vector3(0f, 0f, -1.5f));
            SwingThroughImpact();
            Assert.That(_dummy.Damageable.Health.Current, Is.EqualTo(TargetDummy.DefaultMaxHealth));
        }

        [Test]
        public void ATargetOutOfReachIsNotHit()
        {
            PlaceDummy(new Vector3(0f, 0f, 4f));
            SwingThroughImpact();
            Assert.That(_dummy.Damageable.Health.Current, Is.EqualTo(TargetDummy.DefaultMaxHealth));
        }

        [Test]
        public void ItCannotSwingAgainUntilTheCooldownPasses()
        {
            Assert.That(_melee.TrySwing(), Is.True);
            LogAssert.Expect(LogType.Log, "[Melee] refused id=Body reason=Cooldown");
            Assert.That(_melee.TrySwing(), Is.False);

            _melee.Tick(0.5f);
            Assert.That(_melee.IsSwinging, Is.False);
            Assert.That(_melee.TrySwing(), Is.False, "the cooldown is still running");
            _melee.Tick(0.45f);
            Assert.That(_melee.TrySwing(), Is.True);
        }

        [Test]
        public void WhileSwingingTheWeaponDoesNotFire()
        {
            Assert.That(_melee.TrySwing(), Is.True);
            Assert.That(_weapon.IsFireBlocked, Is.True);
            Assert.That(_weapon.Fire(), Is.False);

            _melee.Tick(0.5f);
            Assert.That(_weapon.IsFireBlocked, Is.False);
            Assert.That(_weapon.Fire(), Is.True);
        }

        [Test]
        public void SwingingInterruptsTheReload()
        {
            Assert.That(_weapon.Fire(), Is.True);
            Assert.That(_weapon.StartReload(), Is.True);
            Assert.That(_weapon.Weapon.IsReloading, Is.True);

            Assert.That(_melee.TrySwing(), Is.True);
            LogAssert.Expect(LogType.Log, "[Combat] reload-cancelled by=MeleeController");
            _weapon.Tick(0f);
            Assert.That(_weapon.Weapon.IsReloading, Is.False);
            Assert.That(_weapon.StartReload(), Is.False, "no reload while swinging");

            _melee.Tick(0.5f);
            Assert.That(_weapon.StartReload(), Is.True);
        }

        [Test]
        public void TheSwingLungesForwardUntilTheImpact()
        {
            SettleOnTheFloor();
            var before = _body.transform.position;
            Assert.That(_melee.TrySwing(), Is.True);
            for (var elapsed = 0f; elapsed < 0.2f; elapsed += _frame)
            {
                _player.Tick(Vector2.zero, false, false, false, _frame);
                Assert.That(_player.MovementOverrideConditions, Is.EqualTo(PlayerActionConditions.Meleeing).Or.EqualTo(PlayerActionConditions.None));
                _melee.Tick(_frame);
            }

            var moved = _body.transform.position - before;
            Assert.That(moved.z, Is.GreaterThan(0.5f));
            Assert.That(Mathf.Abs(moved.x), Is.LessThan(0.05f));
            Assert.That(_melee.IsLunging, Is.False);
        }

        [Test]
        public void AModifierCanScaleTheSpread()
        {
            var spec = _weapon.Weapon.Spec;
            var before = _weapon.CurrentConeDegrees(spec);
            var modifier = new FixedModifier(2f);
            Assert.That(_weapon.AddModifier(modifier), Is.True);
            Assert.That(_weapon.AddModifier(modifier), Is.False);
            Assert.That(_weapon.CurrentConeDegrees(spec), Is.EqualTo(before * 2f).Within(1e-4f));
            Assert.That(_weapon.RemoveModifier(modifier), Is.True);
            Assert.That(_weapon.CurrentConeDegrees(spec), Is.EqualTo(before).Within(1e-4f));
            Assert.Throws<System.ArgumentNullException>(() => _weapon.AddModifier(null));
        }

        [Test]
        public void ThePresenterHearsTheSwingAndTheHit()
        {
            PlaceDummy(new Vector3(0f, 0f, 1.5f));
            var presenter = new RecordingPresenter();
            _melee.Presenter = presenter;

            _melee.TrySwing();
            _melee.Tick(0.25f);
            Assert.That(presenter.Swings, Is.EqualTo(1));
            Assert.That(presenter.Hits, Is.EqualTo(1));
            Assert.That(presenter.Misses, Is.Zero);

            // 1 回目で振り終え、2 回目で待ち時間を過ごす (振り終えた Tick の残りは待ち時間に数えない)。
            _melee.Tick(1f);
            _melee.Tick(1f);
            _dummy.transform.position = new Vector3(0f, 0f, -3f);
            Physics.SyncTransforms();
            _melee.TrySwing();
            _melee.Tick(0.25f);
            Assert.That(presenter.Misses, Is.EqualTo(1));
        }

        [Test]
        public void TheInstallerFollowsTheRule()
        {
            var installer = new MeleeActionInstaller();
            installer.Install(_body, Rules(Melee.Off));
            Assert.That(_body.GetComponents<MeleeController>(), Has.Length.EqualTo(1));
            Assert.That(_melee.enabled, Is.False);

            installer.Install(_body, Rules(Melee.On));
            Assert.That(_melee.enabled, Is.True);
            Assert.That(_melee.Rule, Is.EqualTo(Melee.On));
        }

        private void PlaceDummy(Vector3 feet)
        {
            _dummy = TargetDummy.Create("Dummy", feet, Quaternion.identity);
            Physics.SyncTransforms();
        }

        private void SwingThroughImpact()
        {
            Assert.That(_melee.TrySwing(), Is.True);
            LogAssert.Expect(LogType.Log, "[Melee] miss id=Body");
            Assert.That(_melee.Tick(0.25f), Is.True);
        }

        private void SettleOnTheFloor()
        {
            for (var i = 0; i < 10; i++)
            {
                _player.Tick(Vector2.zero, false, false, false, _frame);
            }
        }

        private sealed class FixedModifier : IWeaponModifier
        {
            private readonly float _multiplier;

            public FixedModifier(float multiplier)
            {
                _multiplier = multiplier;
            }

            public bool BlocksFire => false;

            public bool BlocksReload => false;

            public float SpreadMultiplier => _multiplier;

            public float ModifyConeDegrees(WeaponSpec spec, float coneDegrees) => coneDegrees;

            public float ModifySpreadFraction(float spreadFraction) => spreadFraction;
        }

        private sealed class RecordingPresenter : IMeleePresenter
        {
            public int Swings { get; private set; }

            public int Hits { get; private set; }

            public int Misses { get; private set; }

            public void OnSwingStarted(GameObject attacker, MeleeSpec spec)
            {
                Swings++;
            }

            public void OnHit(GameObject attacker, Damageable target, Vector3 point, Vector3 direction, int damage)
            {
                Hits++;
            }

            public void OnMissed(GameObject attacker)
            {
                Misses++;
            }
        }
    }
}
