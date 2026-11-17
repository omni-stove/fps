using System.Collections.Generic;
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
    // Edit Mode では Update が走らないので、撃てるかは WeaponController.IsFireBlocked と散らばりの値で確かめる。
    public sealed class PlayerVehicleRiderFiringTests
    {
        private const float PassengerArcDegrees = 180f;
        private const float PassengerSpreadMultiplier = 2f;

        private readonly List<Object> _created = new List<Object>();
        private InputAction _move;
        private InputAction _sprint;
        private InputAction _crouch;
        private InputAction _jump;
        private InputAction _attack;
        private InputAction _reload;
        private WeaponDefinition _rifle;
        private WeaponController _weapon;
        private PlayerVehicleRider _rider;
        private Vehicle _vehicle;
        private VehicleWeaponMount _gun;

        [SetUp]
        public void SetUp()
        {
            var body = Track(new GameObject("Player"));
            body.transform.position = new Vector3(-1.3f, 1f, 0f);
            var controller = body.AddComponent<CharacterController>();
            controller.height = 2f;
            controller.radius = 0.5f;
            controller.center = Vector3.zero;
            var player = body.AddComponent<PlayerController>();
            _move = new InputAction("Move", InputActionType.Value, expectedControlType: "Vector2");
            _sprint = new InputAction("Sprint", InputActionType.Button);
            _crouch = new InputAction("Crouch", InputActionType.Button);
            _jump = new InputAction("Jump", InputActionType.Button);
            _attack = new InputAction("Attack", InputActionType.Button);
            _reload = new InputAction("Reload", InputActionType.Button);
            var look = body.AddComponent<FirstPersonLook>();
            player.Initialize(controller, look, _move, _sprint, _crouch, _jump, new MotorSpec(5f, 8f, 1.2f, 20f, 0.1f, 2.5f));

            _rifle = ScriptableObject.CreateInstance<WeaponDefinition>();
            _rifle.name = "Rifle";
            WeaponAssets.Configure(_rifle, TestWeapons.RifleSpec());
            _created.Add(_rifle);
            var camera = new GameObject("Camera", typeof(Camera));
            camera.transform.SetParent(body.transform, false);
            _weapon = camera.AddComponent<WeaponController>();
            _weapon.Initialize(camera.transform, _attack, _reload, _rifle);

            _rider = body.AddComponent<PlayerVehicleRider>();
            _rider.Bind(player, _weapon);

            var root = Track(new GameObject("Car"));
            CreateSeat(root.transform, "Driver", new Vector3(-0.5f, 0.4f, 0f));
            CreateSeat(root.transform, "Passenger", new Vector3(0.5f, 0.4f, 0f));
            CreateSeat(root.transform, "Gunner", new Vector3(0f, 1.2f, -1f));
            _gun = new GameObject("Gun").AddComponent<VehicleWeaponMount>();
            _gun.transform.SetParent(root.transform, false);
            _gun.transform.localPosition = new Vector3(0f, 2f, 0f);
            _gun.SetWeapon(TestWeapons.HandgunSpec());

            _vehicle = root.AddComponent<Vehicle>();
            _vehicle.Configure(new VehicleSpec(
                "car",
                500,
                new[]
                {
                    new VehicleSeatSpec(VehicleSeatRole.Driver),
                    new VehicleSeatSpec(
                        VehicleSeatRole.Passenger, VehicleSeatPose.Seated, VehicleCameraView.Seat, PassengerArcDegrees, PassengerSpreadMultiplier),
                    new VehicleSeatSpec(VehicleSeatRole.Gunner, VehicleSeatPose.Standing),
                },
                new[] { new VehicleWeaponSpec(TestWeapons.HandgunId, 2) }));
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
        public void TheDriverCannotShoot()
        {
            LogAssert.Expect(LogType.Log, "[Vehicle] 'Car' firing rider=Player seat=0 with=None");
            Assert.That(_vehicle.TryEnter(_rider, 0, requireInRange: false), Is.True);

            Assert.That(_rider.Firing, Is.EqualTo(VehicleSeatFiring.None));
            Assert.That(_rider.SeatWeapon, Is.Null);
            Assert.That(_weapon.Modifiers, Is.Empty);
            Assert.That(_weapon.HandedOffTrigger, Is.Null);
            Assert.That(((IHandedOffTrigger)_rider).TryReadTrigger(out _, out _, out _), Is.False);
        }

        [Test]
        public void ThePassengerShootsTheirOwnGunWithinTheArcAndWiderSpread()
        {
            var hipCone = _weapon.CurrentConeDegrees(_weapon.Weapon.Spec);

            LogAssert.Expect(LogType.Log, "[Vehicle] 'Car' firing rider=Player seat=1 with=OwnWeapon");
            Assert.That(_vehicle.TryEnter(_rider, 1, requireInRange: false), Is.True);

            Assert.That(_rider.Firing, Is.EqualTo(VehicleSeatFiring.OwnWeapon));
            Assert.That(_weapon.Modifiers, Has.Member(_rider.OwnWeaponModifier));
            Assert.That(_weapon.HandedOffTrigger, Is.SameAs(_rider));
            Assert.That(((IHandedOffTrigger)_rider).TryReadTrigger(out var held, out _, out _), Is.True);
            Assert.That(held, Is.False);
            Assert.That(_weapon.CurrentConeDegrees(_weapon.Weapon.Spec), Is.EqualTo(hipCone * PassengerSpreadMultiplier).Within(1e-4f));

            _weapon.transform.rotation = Quaternion.Euler(0f, 80f, 0f);
            Assert.That(_weapon.IsFireBlocked, Is.False, "80 degrees is inside the 180 degree arc");

            _weapon.transform.rotation = Quaternion.Euler(0f, 120f, 0f);
            Assert.That(_weapon.IsFireBlocked, Is.True, "120 degrees is outside the arc");
            Assert.That(_weapon.Fire(), Is.False);

            _vehicle.ForceExit(_rider, VehicleLeaveReason.Forced);

            Assert.That(_weapon.Modifiers, Is.Empty);
            Assert.That(_weapon.HandedOffTrigger, Is.Null);
            Assert.That(_weapon.IsFireBlocked, Is.False);
            Assert.That(_weapon.CurrentConeDegrees(_weapon.Weapon.Spec), Is.EqualTo(hipCone).Within(1e-4f));
        }

        [Test]
        public void TheGunnerUsesTheVehicleGunAndTheHudFollowsIt()
        {
            var model = new HudModel();
            using var binding = new HudWeaponBinding(model, _weapon, _rider);
            Assert.That(model.WeaponSpec.Id, Is.EqualTo(TestWeapons.RifleId));
            var changes = new List<Weapon>();
            _rider.SeatWeaponChanged += changes.Add;

            LogAssert.Expect(LogType.Log, "[Vehicle] 'Car' firing rider=Player seat=2 with=VehicleWeapon");
            Assert.That(_vehicle.TryEnter(_rider, 2, requireInRange: false), Is.True);

            Assert.That(_rider.Firing, Is.EqualTo(VehicleSeatFiring.VehicleWeapon));
            Assert.That(_rider.SeatWeapon, Is.SameAs(_gun.Weapon));
            Assert.That(changes, Is.EqualTo(new[] { _gun.Weapon }));
            Assert.That(model.WeaponSpec.Id, Is.EqualTo(TestWeapons.HandgunId));
            Assert.That(_weapon.Modifiers, Is.Empty, "the rider's own gun stays down");

            Assert.That(_vehicle.TrySwitchToSeat(_rider, 1), Is.True);
            Assert.That(_rider.SeatWeapon, Is.Null);
            Assert.That(model.WeaponSpec.Id, Is.EqualTo(TestWeapons.RifleId));

            Assert.That(_vehicle.TrySwitchToSeat(_rider, 2), Is.True);
            _vehicle.ForceExit(_rider, VehicleLeaveReason.Forced);

            Assert.That(changes, Is.EqualTo(new[] { _gun.Weapon, null, _gun.Weapon, null }));
            Assert.That(model.WeaponSpec.Id, Is.EqualTo(TestWeapons.RifleId));
        }

        private void CreateSeat(Transform parent, string name, Vector3 localPosition)
        {
            var seat = new GameObject(name).AddComponent<VehicleSeat>();
            seat.transform.SetParent(parent, false);
            seat.transform.localPosition = localPosition;
        }

        private GameObject Track(GameObject gameObject)
        {
            _created.Add(gameObject);
            return gameObject;
        }
    }
}
