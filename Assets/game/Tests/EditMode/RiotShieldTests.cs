using System.Text.RegularExpressions;
using Shooter.Core;
using Shooter.Editor;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    // Edit Mode では Awake・Update が走らないので、参照は Initialize で渡し、盾の出し入れと振りの時間は Tick で進める。体の寸法は MeleeControllerTests と同じ。
    public sealed class RiotShieldTests
    {
        private const string ShieldId = "7c1e4a92-5b3d-4f68-9a20-3e8d6b1f4c57";
        private const string HolderId = "holder";

        // 開いているシーンの物に当たらないよう、離れた所に置く。
        private static readonly Vector3 Origin = new Vector3(600f, 0f, 600f);
        private static readonly Vector3 BodyCenter = Origin + Vector3.up;

        private GameObject _body;
        private GameObject _shooter;
        private Damageable _self;
        private TargetDummy _dummy;
        private PlayerController _player;
        private WeaponController _weapon;
        private MeleeController _melee;
        private WeaponDefinition _shield;
        private WeaponDefinition _rifle;
        private InputAction _move;
        private InputAction _sprint;
        private InputAction _crouch;
        private InputAction _jump;
        private InputAction _attack;
        private InputAction _reload;
        private InputAction _meleeAction;

        [SetUp]
        public void SetUp()
        {
            _body = new GameObject("Body");
            _body.transform.position = BodyCenter;
            var characterController = _body.AddComponent<CharacterController>();
            characterController.height = 2f;
            characterController.radius = 0.5f;
            characterController.center = Vector3.zero;
            characterController.skinWidth = 0.01f;
            _self = _body.AddComponent<Damageable>();
            _self.Initialize(HolderId, 100);
            _body.AddComponent<Hitbox>().Initialize(HitZone.Body, _self);

            var eye = new GameObject("Eye").transform;
            eye.SetParent(_body.transform, false);
            eye.localPosition = new Vector3(0f, 0.6f, 0f);
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

            _shield = ScriptableObject.CreateInstance<WeaponDefinition>();
            _shield.name = "RiotShield";
            WeaponAssets.Configure(_shield, new WeaponSpec(ShieldId, "weapon.riotShield", WeaponCategory.Handgun, shield: RiotShieldSpec.Default));
            _rifle = ScriptableObject.CreateInstance<WeaponDefinition>();
            _rifle.name = "Rifle";
            WeaponAssets.Configure(_rifle, TestWeapons.RifleSpec());

            _attack = new InputAction("Attack", InputActionType.Button);
            _reload = new InputAction("Reload", InputActionType.Button);
            _weapon = camera.gameObject.AddComponent<WeaponController>();
            _weapon.Initialize(camera.transform, _attack, _reload, null, null, new[] { _shield, _rifle });

            _meleeAction = new InputAction("Melee", InputActionType.Button);
            _melee = _body.AddComponent<MeleeController>();
            _melee.Initialize(_player, _weapon, _meleeAction);
            _melee.Spec = new MeleeSpec(20, 2f, 90f, 0.5f, 0.4f, 0.2f, 0f);
            _melee.Configure(GameRules.Default.WithActions(ActionRules.Default.WithMelee(Melee.On)));

            _shooter = new GameObject("Shooter");
            _weapon.Tick(0f);
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
            Object.DestroyImmediate(_shooter);
            Object.DestroyImmediate(_shield);
            Object.DestroyImmediate(_rifle);
            if (_dummy != null)
            {
                Object.DestroyImmediate(_dummy.gameObject);
            }
        }

        [Test]
        public void HoldingTheShieldPutsAPlateInFrontOfTheHolder()
        {
            var held = _weapon.HeldShield;

            Assert.That(_weapon.IsShieldWeapon, Is.True);
            Assert.That(held, Is.Not.Null);
            Assert.That(held.WeaponId, Is.EqualTo(ShieldId));
            Assert.That(held.transform.parent, Is.SameAs(_body.transform));
            Assert.That(held.name, Is.EqualTo(HeldRiotShield.PlaceholderName));
            // サーバー (ServerHitboxes) は同じ RiotShieldSpec の値で板を置くので、オフラインの仮の板もその値の所に出る
            Assert.That(held.transform.localPosition, Is.EqualTo(new Vector3(0f, 0f, RiotShieldSpec.HeldForward)));
            Assert.That(held.transform.localScale, Is.EqualTo(new Vector3(RiotShieldSpec.DefaultWidth, RiotShieldSpec.DefaultHeight, RiotShieldSpec.HeldThickness)));
            Assert.That(HeldRiotShield.PlaceholderForward, Is.EqualTo(0.6f));
            Assert.That(HeldRiotShield.PlaceholderThickness, Is.EqualTo(0.05f));
            Assert.That(held.gameObject.layer, Is.EqualTo(_body.layer), "the plate is on the layer bullets hit the holder on");
            Assert.That(held.Plate.BlockArcDegrees, Is.EqualTo(RiotShieldSpec.DefaultBlockArcDegrees));
            Assert.That(held.IsRaised, Is.True);
            Assert.That(Physics.GetIgnoreCollision(held.Plate.Collider, _body.GetComponent<CharacterController>()), Is.True);
            Assert.That(_weapon.ShieldMoveSpeedMultiplier, Is.EqualTo(RiotShieldSpec.DefaultMoveSpeedMultiplier));
        }

        [Test]
        public void ABulletFromTheFrontIsStopped()
        {
            LogAssert.Expect(LogType.Log, "[AI] id=holder shield blocked dmg=25 by=Shooter");
            var result = FireFrom(new Vector3(0f, 0f, 5f));

            Assert.That(result.Hit, Is.True);
            Assert.That(result.Hitbox, Is.Null);
            Assert.That(result.Damage, Is.Zero);
            Assert.That(_self.Health.Current, Is.EqualTo(100));
        }

        [Test]
        public void ABulletFromBehindPassesToTheHolder()
        {
            var result = FireFrom(new Vector3(0f, 0f, -5f));

            Assert.That(result.Hitbox, Is.Not.Null);
            Assert.That(result.Damage, Is.EqualTo(25));
            Assert.That(_self.Health.Current, Is.EqualTo(75));

            // 盾の板そのものに後ろから当たっても止めず、呼び手がそのまま持ち主へ着弾させる。
            var plate = _weapon.HeldShield.Plate;
            Assert.That(BulletShield.TryBlock(plate.Collider, 25, BodyCenter + new Vector3(0f, 0f, -5f), _shooter), Is.False);
            Assert.That(BulletShield.TryBlock(plate.Collider, 25, BodyCenter + new Vector3(10f, 0f, 0f), _shooter), Is.False, "beside is outside the 120 degree arc");
            LogAssert.Expect(LogType.Log, "[AI] id=holder shield blocked dmg=25 by=Shooter");
            Assert.That(BulletShield.TryBlock(plate.Collider, 25, BodyCenter + new Vector3(2f, 0f, 5f), _shooter), Is.True);
        }

        [Test]
        public void ALoweredShieldLetsBulletsThroughAndDoesNotSlowTheHolder()
        {
            var held = _weapon.HeldShield;

            Assert.That(held.SetRaised(false), Is.True);
            Physics.SyncTransforms();

            Assert.That(_weapon.IsShieldRaised, Is.False);
            Assert.That(_weapon.ShieldMoveSpeedMultiplier, Is.EqualTo(1f));
            Assert.That(FireFrom(new Vector3(0f, 0f, 5f)).Damage, Is.EqualTo(25));

            Assert.That(held.SetRaised(true), Is.True);
            Physics.SyncTransforms();

            Assert.That(Physics.GetIgnoreCollision(held.Plate.Collider, _body.GetComponent<CharacterController>()), Is.True, "raising again keeps the plate off the holder's body");
            LogAssert.Expect(LogType.Log, "[AI] id=holder shield blocked dmg=25 by=Shooter");
            Assert.That(FireFrom(new Vector3(0f, 0f, 5f)).Damage, Is.Zero);
        }

        [Test]
        public void TheShieldCannotFireOrReload()
        {
            Assert.That(_weapon.IsFireBlocked, Is.True);
            Assert.That(_weapon.Fire(), Is.False);
            Assert.That(_weapon.PullTrigger(true, true), Is.False);
            Assert.That(_weapon.StartReload(), Is.False);
            Assert.That(_weapon.ShotCount, Is.Zero);
        }

        [Test]
        public void TheAttackButtonBashesWithTheShieldValues()
        {
            PlaceDummy(new Vector3(0f, 0f, 1.5f));
            string attributed = null;
            _dummy.Damageable.Damaged += (_, _) => attributed = CombatEvents.CurrentAttribution?.WeaponId;
            var normal = _melee.Spec;

            LogAssert.Expect(LogType.Log, "[Melee] shield-bash id=holder weapon=" + ShieldId);
            Assert.That(_weapon.PullTrigger(true, true), Is.False, "the shield bashes instead of firing");

            Assert.That(_melee.IsSwinging, Is.True);
            Assert.That(_melee.IsShieldBashing, Is.True);
            AssertSwingValues(_melee.Swing.CurrentSpec, RiotShieldSpec.DefaultBash);
            Assert.That(_melee.Spec, Is.SameAs(normal), "the tuned melee values are kept for the next normal swing");

            LogAssert.Expect(LogType.Log, new Regex(@"^\[Melee\] hit id=holder target=Dummy dmg=40 "));
            Assert.That(_melee.Tick(RiotShieldSpec.DefaultBashImpactSeconds), Is.True);

            Assert.That(_dummy.Damageable.Health.Current, Is.EqualTo(TargetDummy.DefaultMaxHealth - RiotShieldSpec.DefaultBashDamage));
            Assert.That(attributed, Is.EqualTo(ShieldId));

            _melee.Tick(RiotShieldSpec.DefaultBashSwingSeconds);
            Assert.That(_melee.IsShieldBashing, Is.False);
        }

        [Test]
        public void HoldingTheAttackButtonBashesOnlyOnce()
        {
            LogAssert.Expect(LogType.Log, "[Melee] shield-bash id=holder weapon=" + ShieldId);
            _weapon.PullTrigger(true, true);
            _melee.Tick(RiotShieldSpec.DefaultBashSwingSeconds);
            _melee.Tick(RiotShieldSpec.DefaultBashCooldownSeconds);

            _weapon.PullTrigger(true, false);

            Assert.That(_melee.IsSwinging, Is.False);
        }

        [Test]
        public void TheMeleeButtonAlsoBashesWhileHoldingTheShield()
        {
            LogAssert.Expect(LogType.Log, "[Melee] shield-bash id=holder weapon=" + ShieldId);
            Assert.That(_melee.TrySwing(), Is.True);

            AssertSwingValues(_melee.Swing.CurrentSpec, RiotShieldSpec.DefaultBash);
        }

        [Test]
        public void PuttingTheShieldAwayRemovesItAndTheRifleFiresAgain()
        {
            var plate = _weapon.HeldShield.gameObject;

            Assert.That(_weapon.Select(TestWeapons.RifleId), Is.True);
            LogAssert.Expect(LogType.Log, "[Combat] riot-shield away id=holder weapon=" + ShieldId);
            TickUntilDrawn();
            Physics.SyncTransforms();

            Assert.That(_weapon.HeldShield, Is.Null);
            Assert.That(plate == null, Is.True, "the plate is destroyed");
            Assert.That(_body.transform.Find(HeldRiotShield.PlaceholderName), Is.Null);
            Assert.That(_weapon.IsFireBlocked, Is.False);
            Assert.That(_weapon.ShieldMoveSpeedMultiplier, Is.EqualTo(1f));
            Assert.That(FireFrom(new Vector3(0f, 0f, 5f)).Damage, Is.EqualTo(25), "nothing stops bullets once the shield is put away");

            LogAssert.Expect(LogType.Log, "[Melee] swing id=holder");
            Assert.That(_melee.TrySwing(), Is.True);
            Assert.That(_melee.Swing.CurrentSpec, Is.SameAs(_melee.Spec), "without the shield the normal melee values are used");
        }

        [Test]
        public void TakingTheShieldOutAgainMakesANewPlate()
        {
            _weapon.Select(TestWeapons.RifleId);
            TickUntilDrawn();

            Assert.That(_weapon.Select(ShieldId), Is.True);
            LogAssert.Expect(LogType.Log, "[Combat] riot-shield out id=holder weapon=" + ShieldId);
            TickUntilDrawn();

            Assert.That(_weapon.HeldShield, Is.Not.Null);
            Assert.That(_body.GetComponentsInChildren<BulletShield>(), Has.Length.EqualTo(1));
        }

        [Test]
        public void DisarmingRemovesTheShield()
        {
            _weapon.Disarm();
            _weapon.Tick(0f);

            Assert.That(_weapon.HeldShield, Is.Null);
            Assert.That(_body.GetComponentsInChildren<BulletShield>(), Is.Empty);
        }

        private void TickUntilDrawn()
        {
            for (var i = 0; i < 100 && (_weapon.IsSwitching || i == 0); i++)
            {
                _weapon.Tick(0.1f);
            }
        }

        private HitscanResult FireFrom(Vector3 offset)
        {
            _shooter.transform.position = BodyCenter + offset;
            return Hitscan.Fire(_shooter.transform.position, -offset.normalized, 20f, 25, DamageFalloff.None, _shooter.transform);
        }

        private void PlaceDummy(Vector3 feet)
        {
            _dummy = TargetDummy.Create("Dummy", Origin + feet, Quaternion.identity);
            Physics.SyncTransforms();
        }

        private static void AssertSwingValues(MeleeSpec actual, MeleeSpec expected)
        {
            Assert.That(actual, Is.Not.Null);
            Assert.That(actual.Damage, Is.EqualTo(expected.Damage));
            Assert.That(actual.Reach, Is.EqualTo(expected.Reach));
            Assert.That(actual.ArcDegrees, Is.EqualTo(expected.ArcDegrees));
            Assert.That(actual.SwingSeconds, Is.EqualTo(expected.SwingSeconds));
            Assert.That(actual.CooldownSeconds, Is.EqualTo(expected.CooldownSeconds));
            Assert.That(actual.ImpactSeconds, Is.EqualTo(expected.ImpactSeconds));
            Assert.That(actual.LungeDistance, Is.EqualTo(expected.LungeDistance));
        }
    }
}
