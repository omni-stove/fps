using System.Collections.Generic;
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
    // Edit Mode では Awake・Update・物理が走らないので、参照は Initialize で渡し、溜め・振り・投げた武器の当たりと着地は直接呼んで進める。体の寸法は RiotShieldTests と同じ。
    public sealed class MeleeWeaponTests
    {
        private const string CrowbarId = "2b92ec24-9e3b-4b5a-bf0a-ebaf3b48b544";
        private const string HolderId = "holder";

        // 開いているシーンの物に当たらないよう、離れた所に置く。
        private static readonly Vector3 Origin = new Vector3(-600f, 0f, 600f);
        private static readonly Vector3 BodyCenter = Origin + Vector3.up;

        // 踏み込みで相手との距離が変わらないよう、踏み込まない値にする。
        private static readonly MeleeSpec Light = new MeleeSpec(30, 2f, 70f, 0.4f, 0.1f, 0.15f, 0f);
        private static readonly MeleeSpec Heavy = new MeleeSpec(80, 2.5f, 90f, 0.6f, 0.2f, 0.25f, 0f);
        private static readonly MeleeThrowSpec Throw = new MeleeThrowSpec(55, 18f, 4f, 720f, 0.12f, 8f);
        private static readonly MeleeWeaponSpec Crowbar = new MeleeWeaponSpec(Light, Heavy, 0.2f, 0.8f, Throw);

        private readonly List<Object> _spawned = new List<Object>();
        private GameObject _body;
        private Damageable _self;
        private TargetDummy _dummy;
        private PlayerController _player;
        private WeaponController _weapon;
        private MeleeController _melee;
        private WeaponDefinition _crowbar;
        private WeaponDefinition _rifle;
        private InputAction _move;
        private InputAction _sprint;
        private InputAction _crouch;
        private InputAction _jump;
        private InputAction _attack;
        private InputAction _reload;
        private InputAction _meleeAction;
        private InputAction _throwAction;

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

            _crowbar = ScriptableObject.CreateInstance<WeaponDefinition>();
            _crowbar.name = "Crowbar";
            WeaponAssets.Configure(_crowbar, new WeaponSpec(CrowbarId, "weapon.crowbar", WeaponCategory.Melee, melee: Crowbar));
            _rifle = ScriptableObject.CreateInstance<WeaponDefinition>();
            _rifle.name = "Rifle";
            WeaponAssets.Configure(_rifle, TestWeapons.RifleSpec());

            _attack = new InputAction("Attack", InputActionType.Button);
            _reload = new InputAction("Reload", InputActionType.Button);
            _weapon = camera.gameObject.AddComponent<WeaponController>();
            _weapon.Initialize(camera.transform, _attack, _reload, null, null, new[] { _crowbar, _rifle });

            _meleeAction = new InputAction("Melee", InputActionType.Button);
            _throwAction = new InputAction("Throw", InputActionType.Button);
            _melee = _body.AddComponent<MeleeController>();
            _melee.Initialize(_player, _weapon, _meleeAction, _throwAction);
            _melee.Spec = new MeleeSpec(20, 2f, 90f, 0.5f, 0.4f, 0.2f, 0f);
            _melee.Configure(GameRules.Default.WithActions(ActionRules.Default.WithMelee(Melee.On)));

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
            _throwAction.Dispose();
            foreach (var thrown in Object.FindObjectsByType<ThrownMeleeWeapon>(FindObjectsSortMode.None))
            {
                Object.DestroyImmediate(thrown.gameObject);
            }

            foreach (var pickup in Object.FindObjectsByType<Pickup>(FindObjectsSortMode.None))
            {
                if (pickup.Contents != null && pickup.Contents.Weapon == _crowbar)
                {
                    Object.DestroyImmediate(pickup.gameObject);
                }
            }

            foreach (var spawned in _spawned)
            {
                if (spawned != null)
                {
                    Object.DestroyImmediate(spawned);
                }
            }

            Object.DestroyImmediate(_body);
            Object.DestroyImmediate(_crowbar);
            Object.DestroyImmediate(_rifle);
            if (_dummy != null)
            {
                Object.DestroyImmediate(_dummy.gameObject);
            }
        }

        [Test]
        public void TheMeleeWeaponCannotFireOrReload()
        {
            Assert.That(_weapon.IsMeleeWeapon, Is.True);
            Assert.That(_weapon.IsFireBlocked, Is.True);
            Assert.That(_weapon.Fire(), Is.False);
            Assert.That(_weapon.PullTrigger(true, true), Is.False);
            Assert.That(_weapon.StartReload(), Is.False);
            Assert.That(_weapon.ShotCount, Is.Zero);
            Assert.That(_melee.ThrowAction, Is.SameAs(_throwAction));
        }

        [Test]
        public void AClickSwingsTheWeaponWhenTheButtonIsReleased()
        {
            PlaceDummy(new Vector3(0f, 0f, 1.5f));
            string attributed = null;
            _dummy.Damageable.Damaged += (_, _) => attributed = CombatEvents.CurrentAttribution?.WeaponId;
            var normal = _melee.Spec;

            _weapon.PullTrigger(true, true);
            Assert.That(_melee.IsSwinging, Is.False, "pressing alone does not swing");

            LogAssert.Expect(LogType.Log, "[Melee] weapon-swing id=holder weapon=" + CrowbarId + " charge=0.00");
            _weapon.PullTrigger(false, false);

            Assert.That(_melee.IsSwinging, Is.True);
            Assert.That(_melee.IsMeleeWeaponSwinging, Is.True);
            AssertSwingValues(_melee.Swing.CurrentSpec, Light);
            Assert.That(_melee.Spec, Is.SameAs(normal), "the tuned melee values are kept for the next bare-handed swing");

            LogAssert.Expect(LogType.Log, new Regex(@"^\[Melee\] hit id=holder target=Dummy dmg=30 "));
            Assert.That(_melee.Tick(Light.ImpactSeconds), Is.True);

            Assert.That(_dummy.Damageable.Health.Current, Is.EqualTo(TargetDummy.DefaultMaxHealth - Light.Damage));
            Assert.That(attributed, Is.EqualTo(CrowbarId));

            _melee.Tick(Light.SwingSeconds);
            Assert.That(_melee.IsMeleeWeaponSwinging, Is.False);
        }

        [Test]
        public void PressingAndReleasingInOneFrameStillSwings()
        {
            LogAssert.Expect(LogType.Log, "[Melee] weapon-swing id=holder weapon=" + CrowbarId + " charge=0.00");
            _weapon.PullTrigger(false, true);

            Assert.That(_melee.IsSwinging, Is.True);
        }

        [Test]
        public void HoldingTheButtonChargesAHeavySwing()
        {
            PlaceDummy(new Vector3(0f, 0f, 1.5f));

            _weapon.PullTrigger(true, true);
            _weapon.Tick(0.6f);
            _weapon.PullTrigger(true, false);
            Assert.That(_weapon.ChargeFraction, Is.EqualTo(0.5f).Within(1e-4f));
            Assert.That(_melee.IsSwinging, Is.False, "holding does not swing");

            _weapon.Tick(1f);
            Assert.That(_weapon.ChargeFraction, Is.EqualTo(1f));

            LogAssert.Expect(LogType.Log, "[Melee] weapon-swing id=holder weapon=" + CrowbarId + " charge=1.00");
            _weapon.PullTrigger(false, false);

            AssertSwingValues(_melee.Swing.CurrentSpec, Heavy);
            Assert.That(_weapon.ChargeFraction, Is.Zero, "the charge starts over after the swing");

            LogAssert.Expect(LogType.Log, new Regex(@"^\[Melee\] hit id=holder target=Dummy dmg=80 "));
            _melee.Tick(Heavy.ImpactSeconds);
            Assert.That(_dummy.Damageable.Health.Current, Is.EqualTo(TargetDummy.DefaultMaxHealth - Heavy.Damage));
        }

        [Test]
        public void SwitchingAwayDropsTheCharge()
        {
            _weapon.PullTrigger(true, true);
            _weapon.Tick(1f);

            Assert.That(_weapon.Select(TestWeapons.RifleId), Is.True);
            TickUntilDrawn();
            Assert.That(_weapon.Select(CrowbarId), Is.True);
            TickUntilDrawn();

            _weapon.PullTrigger(false, false);
            Assert.That(_melee.IsSwinging, Is.False, "letting go after switching back does not swing with the old charge");
            Assert.That(_weapon.ChargeFraction, Is.Zero);
        }

        [Test]
        public void TheMeleeButtonSwingsTheWeaponsLightSwing()
        {
            LogAssert.Expect(LogType.Log, "[Melee] weapon-swing id=holder weapon=" + CrowbarId + " charge=0.00");
            Assert.That(_melee.TrySwing(), Is.True);

            AssertSwingValues(_melee.Swing.CurrentSpec, Light);
        }

        [Test]
        public void MissingAPersonButHittingAWallMakesAnImpact()
        {
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _spawned.Add(wall);
            wall.transform.position = BodyCenter + new Vector3(0f, 0.6f, 1.3f);
            wall.transform.localScale = new Vector3(3f, 3f, 0.2f);
            Physics.SyncTransforms();
            var impacts = new List<ImpactInfo>();
            CombatEvents.Impact += impacts.Add;
            try
            {
                _melee.TrySwing();
                LogAssert.Expect(LogType.Log, "[Melee] miss id=holder");
                LogAssert.Expect(LogType.Log, "[Melee] world-hit id=holder material=" + SurfaceMaterial.DefaultId);
                _melee.Tick(Light.ImpactSeconds);
            }
            finally
            {
                CombatEvents.Impact -= impacts.Add;
            }

            Assert.That(impacts, Has.Count.EqualTo(1));
            Assert.That(impacts[0].Collider, Is.SameAs(wall.GetComponent<Collider>()));
            Assert.That(impacts[0].Point.z, Is.EqualTo(BodyCenter.z + 1.2f).Within(1e-3f));
            Assert.That(impacts[0].IsCharacter, Is.False);
        }

        [Test]
        public void ABareHandedMissMakesNoImpact()
        {
            Assert.That(_weapon.Select(TestWeapons.RifleId), Is.True);
            TickUntilDrawn();
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _spawned.Add(wall);
            wall.transform.position = BodyCenter + new Vector3(0f, 0.6f, 1.3f);
            wall.transform.localScale = new Vector3(3f, 3f, 0.2f);
            Physics.SyncTransforms();
            var impacts = 0;
            void Count(ImpactInfo _) => impacts++;
            CombatEvents.Impact += Count;
            try
            {
                _melee.TrySwing();
                _melee.Tick(_melee.Spec.ImpactSeconds);
            }
            finally
            {
                CombatEvents.Impact -= Count;
            }

            Assert.That(impacts, Is.Zero);
        }

        [Test]
        public void ThrowingTakesTheWeaponOutOfTheHandsAndHitsThePersonItTouches()
        {
            PlaceDummy(new Vector3(0f, 0f, 6f));
            string attributed = null;
            _dummy.Damageable.Damaged += (_, _) => attributed = CombatEvents.CurrentAttribution?.WeaponId;

            LogAssert.Expect(LogType.Log, "[Melee] thrown id=holder weapon=" + CrowbarId);
            Assert.That(_melee.TryThrowWeapon(out var thrown), Is.True);

            Assert.That(_weapon.Loadout.IndexOf(CrowbarId), Is.EqualTo(-1), "the thrown weapon leaves the loadout");
            Assert.That(thrown, Is.Not.Null);
            Assert.That(thrown.WeaponId, Is.EqualTo(CrowbarId));
            Assert.That(thrown.Thrower, Is.SameAs(_body));
            Assert.That(thrown.transform.Find(ThrownMeleeWeapon.PlaceholderName), Is.Not.Null, "without a prefab a placeholder is built");
            var velocity = thrown.Body.linearVelocity;
            Assert.That(velocity.magnitude, Is.EqualTo(Throw.Speed).Within(1e-3f));
            Assert.That(velocity.y, Is.GreaterThan(0f), "it is thrown a little above the aim");
            Assert.That(velocity.z, Is.GreaterThan(0f));

            var target = _dummy.GetComponentInChildren<Collider>();
            LogAssert.Expect(LogType.Log, "[Melee] throw-hit id=holder weapon=" + CrowbarId + " target=Dummy dmg=55 kind=Melee");
            Assert.That(thrown.Strike(target, target.bounds.center, Vector3.back), Is.True);
            Assert.That(thrown.Strike(target, target.bounds.center, Vector3.back), Is.False, "only the first touch hits");

            Assert.That(_dummy.Damageable.Health.Current, Is.EqualTo(TargetDummy.DefaultMaxHealth - Throw.Damage));
            Assert.That(attributed, Is.EqualTo(CrowbarId));
            Assert.That(thrown.Body.linearVelocity, Is.EqualTo(Vector3.zero), "a hit drops the weapon at the target's feet");
        }

        [Test]
        public void ThrowingIntoAWallMakesAnImpactAndItLandsAsTheSameWeapon()
        {
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _spawned.Add(wall);
            _melee.TryThrowWeapon(out var thrown);
            var impacts = new List<ImpactInfo>();
            CombatEvents.Impact += impacts.Add;
            try
            {
                LogAssert.Expect(LogType.Log, "[Melee] throw-world-hit id=holder weapon=" + CrowbarId + " material=" + SurfaceMaterial.DefaultId);
                thrown.Strike(wall.GetComponent<Collider>(), wall.transform.position, Vector3.back);
            }
            finally
            {
                CombatEvents.Impact -= impacts.Add;
            }

            Assert.That(impacts, Has.Count.EqualTo(1));

            LogAssert.Expect(LogType.Log, "[Melee] throw-landed id=holder weapon=" + CrowbarId);
            var pickup = thrown.Land();

            Assert.That(thrown == null, Is.True, "the flying weapon is replaced by the pickup");
            Assert.That(pickup.Spec.Kind, Is.EqualTo(PickupKind.Weapon));
            Assert.That(pickup.Contents.Weapon, Is.SameAs(_crowbar));
            Assert.That(pickup.CollectMode, Is.EqualTo(PickupCollectMode.Touch));
        }

        [Test]
        public void ThrownWeaponLandsOnceItStopsOrFliesTooLong()
        {
            _melee.TryThrowWeapon(out var thrown);

            Assert.That(thrown.Advance(1f), Is.False, "still flying");
            thrown.Body.linearVelocity = Vector3.zero;
            Assert.That(thrown.Advance(ThrownMeleeWeapon.SettleSeconds / 2f), Is.False, "a moment at rest is not yet a stop");
            LogAssert.Expect(LogType.Log, "[Melee] throw-landed id=holder weapon=" + CrowbarId);
            Assert.That(thrown.Advance(ThrownMeleeWeapon.SettleSeconds), Is.True);

            Assert.That(_weapon.AddWeapon(_crowbar), Is.True);
            Assert.That(_weapon.Select(CrowbarId), Is.True);
            TickUntilDrawn();
            Assert.That(_melee.TryThrowWeapon(out var flying), Is.True);
            flying.Body.linearVelocity = Vector3.forward * 10f;
            LogAssert.Expect(LogType.Log, "[Melee] throw-landed id=holder weapon=" + CrowbarId);
            Assert.That(flying.Advance(Throw.MaxFlightSeconds), Is.True);
        }

        [Test]
        public void AWeaponThatCannotBeThrownStaysInHand()
        {
            Assert.That(_weapon.Select(TestWeapons.RifleId), Is.True);
            TickUntilDrawn();

            Assert.That(_melee.TryThrowWeapon(out var thrown), Is.False, "a gun is not thrown");
            Assert.That(thrown, Is.Null);
            Assert.That(Object.FindObjectsByType<ThrownMeleeWeapon>(FindObjectsSortMode.None), Is.Empty);
        }

        [Test]
        public void TheWeaponIsNotThrownMidSwing()
        {
            _melee.TrySwing();

            LogAssert.Expect(LogType.Log, "[Melee] refused id=holder reason=Cooldown");
            Assert.That(_melee.TryThrowWeapon(), Is.False);
            Assert.That(_weapon.Loadout.IndexOf(CrowbarId), Is.EqualTo(0));

            _melee.Tick(Light.SwingSeconds);
            LogAssert.Expect(LogType.Log, "[Melee] thrown id=holder weapon=" + CrowbarId);
            Assert.That(_melee.TryThrowWeapon(), Is.True, "the cooldown after a swing does not stop a throw");
        }

        [Test]
        public void TheLaunchDirectionTiltsUpAroundTheAimsRight()
        {
            var direction = ThrownMeleeWeapon.LaunchDirection(Vector3.forward, Vector3.right, 30f);

            Assert.That(direction.y, Is.EqualTo(0.5f).Within(1e-4f));
            Assert.That(direction.z, Is.EqualTo(Mathf.Sqrt(3f) / 2f).Within(1e-4f));
            Assert.That(ThrownMeleeWeapon.LaunchDirection(Vector3.forward, Vector3.right, 0f), Is.EqualTo(Vector3.forward));
        }

        private void TickUntilDrawn()
        {
            for (var i = 0; i < 100 && (_weapon.IsSwitching || i == 0); i++)
            {
                _weapon.Tick(0.1f);
            }
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
