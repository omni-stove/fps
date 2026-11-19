using System;
using System.Collections.Generic;
using Shooter.Core;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using NVector3 = System.Numerics.Vector3;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    // Edit Mode では Update・LateUpdate も OnEnable も走らないので、狙う側の Tick と Refresh を直接呼び、狙う側の一覧も差し込む。
    public sealed class LockOnWarningPublisherTests
    {
        private const float Step = 0.25f;
        private const float Epsilon = 1e-3f;

        private readonly List<Object> _created = new List<Object>();
        private readonly List<LockOnAim> _shooters = new List<LockOnAim>();
        private Damageable _self;
        private LockOnWarningPublisher _publisher;

        [SetUp]
        public void SetUp()
        {
            GameSignals.ResetAll();
            _self = CreateBody("self", Vector3.zero);
            _publisher = Track(new GameObject("Publisher")).AddComponent<LockOnWarningPublisher>();
            _publisher.Self = _self.transform;
            _publisher.Sources = () => _shooters;
        }

        [TearDown]
        public void TearDown()
        {
            GameSignals.ResetAll();
            _shooters.Clear();
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
        public void NothingIsPublishedWhileNobodyAimsAtYou()
        {
            Assert.That(_publisher.Refresh(), Is.Null, "no shooters at all");

            var other = CreateBody("other", new Vector3(0f, 0f, 30f));
            var shooter = CreateShooter("enemy", new Vector3(10f, 0f, 0f), other);
            shooter.Tick(2f);
            Assert.That(shooter.Stage, Is.EqualTo(LockOnWarningStage.Locked), "the shooter has locked someone else");

            Assert.That(_publisher.Refresh(), Is.Null);
            Assert.That(_publisher.Published, Is.Null);
            Assert.That(GameSignals.LockOnWarning.HasValue, Is.False);
        }

        [Test]
        public void TheStageRisesFromTrackingToLockedAndClearsWhenTheShooterLetsGo()
        {
            var aiming = true;
            var shooter = CreateShooter("enemy", new Vector3(10f, 0f, 0f), _self, () => aiming);

            shooter.Tick(Step);
            var tracking = _publisher.Refresh();
            Assert.That(tracking.HasValue, Is.True);
            Assert.That(tracking.Value.Stage, Is.EqualTo(LockOnWarningStage.Tracking));
            Assert.That(tracking.Value.BearingDegrees, Is.EqualTo(90f).Within(Epsilon), "the shooter stands to the right of a player facing +Z");
            Assert.That(GameSignals.LockOnWarning.Value, Is.EqualTo(tracking));

            shooter.Tick(1f);
            var locked = _publisher.Refresh();
            Assert.That(locked.Value.Stage, Is.EqualTo(LockOnWarningStage.Locked));
            Assert.That(GameSignals.LockOnWarning.Value, Is.EqualTo(locked));

            aiming = false;
            shooter.Tick(Step);
            Assert.That(_publisher.Refresh(), Is.Null);
            Assert.That(GameSignals.LockOnWarning.Value, Is.Null);
        }

        [Test]
        public void TheBearingIsMeasuredFromWhereYouFace()
        {
            CreateShooter("enemy", new Vector3(-10f, 0f, -10f), _self).Tick(Step);

            Assert.That(_publisher.Refresh().Value.BearingDegrees, Is.EqualTo(-135f).Within(Epsilon), "behind on the left");

            _self.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            Assert.That(_publisher.Refresh().Value.BearingDegrees, Is.EqualTo(45f).Within(Epsilon), "turned around, the shooter is ahead on the right");

            _publisher.HeadingSource = () => 270f;
            Assert.That(_publisher.Refresh().Value.BearingDegrees, Is.EqualTo(-135f + 90f).Within(Epsilon), "an injected heading wins over the body");
        }

        [Test]
        public void TheLocalHeadingSignalIsUsedWhenPublished()
        {
            CreateShooter("enemy", new Vector3(10f, 0f, 0f), _self).Tick(Step);
            GameSignals.LocalHeadingDegrees.Publish(() => 90f);

            Assert.That(_publisher.Refresh().Value.BearingDegrees, Is.EqualTo(0f).Within(Epsilon));
        }

        [Test]
        public void WithSeveralShootersTheFurthestStageAndItsDirectionWin()
        {
            var lockerAiming = true;
            var locker = CreateShooter("locker", new Vector3(10f, 0f, 0f), _self, () => lockerAiming);
            var tracker = CreateShooter("tracker", new Vector3(-10f, 0f, 0f), _self);
            locker.Tick(1f);
            tracker.Tick(Step);

            var status = _publisher.Refresh().Value;
            Assert.That(status.Stage, Is.EqualTo(LockOnWarningStage.Locked));
            Assert.That(status.BearingDegrees, Is.EqualTo(90f).Within(Epsilon), "the locked shooter on the right");

            lockerAiming = false;
            locker.Tick(Step);
            status = _publisher.Refresh().Value;
            Assert.That(status.Stage, Is.EqualTo(LockOnWarningStage.Tracking));
            Assert.That(status.BearingDegrees, Is.EqualTo(-90f).Within(Epsilon), "only the tracking shooter on the left is left");
        }

        [Test]
        public void AmongTrackingShootersTheOneClosestToALockWins()
        {
            var early = CreateShooter("early", new Vector3(10f, 0f, 0f), _self);
            var late = CreateShooter("late", new Vector3(-10f, 0f, 0f), _self);
            early.Tick(0.75f);
            late.Tick(Step);

            Assert.That(_publisher.Refresh().Value.BearingDegrees, Is.EqualTo(90f).Within(Epsilon));
        }

        [Test]
        public void YourOwnLockOnIsNotAWarning()
        {
            var own = _self.gameObject.AddComponent<LockOnAim>();
            ConfigureShooter(own, _self.transform.position, _self, () => true);
            _shooters.Add(own);
            own.Tick(Step);

            Assert.That(own.Stage, Is.EqualTo(LockOnWarningStage.Tracking));
            Assert.That(_publisher.Refresh(), Is.Null);
        }

        [Test]
        public void WithoutAnInjectedSelfTheLocalPlayerIsUsed()
        {
            _publisher.Self = null;
            CreateShooter("enemy", new Vector3(10f, 0f, 0f), _self).Tick(Step);

            Assert.That(_publisher.Refresh(), Is.Null, "no local player yet");

            GameSignals.LocalPlayer.Publish(new LocalPlayerSignal(_self.gameObject, null, null, null, null, null, null, null));

            Assert.That(_publisher.Refresh().Value.Stage, Is.EqualTo(LockOnWarningStage.Tracking));
        }

        [Test]
        public void ClearingDoesNotRemoveAValueAnotherPartPublished()
        {
            var aiming = true;
            var shooter = CreateShooter("enemy", new Vector3(10f, 0f, 0f), _self, () => aiming);
            shooter.Tick(Step);
            _publisher.Refresh();
            var other = new LockOnWarningStatus(LockOnWarningStage.Locked, 10f);
            GameSignals.LockOnWarning.Publish(other);

            aiming = false;
            shooter.Tick(Step);
            _publisher.Refresh();

            Assert.That(GameSignals.LockOnWarning.Value, Is.EqualTo(other));
        }

        [Test]
        public void WhileRidingAShooterAimingAtTheVehicleIsAWarning()
        {
            var rider = BindRider(out var actions);
            try
            {
                _publisher.Rider = rider;
                var vehicle = CreateVehicle();
                var shooter = CreateShooter("enemy", new Vector3(100f, 0f, 0f), vehicle.Damageable);
                shooter.Tick(Step);

                Assert.That(_publisher.Refresh(), Is.Null, "the vehicle is not yours until you board it");

                Assert.That(vehicle.TryEnter(rider, 0, false), Is.True);
                var status = _publisher.Refresh();
                Assert.That(status.HasValue, Is.True);
                Assert.That(status.Value.Stage, Is.EqualTo(LockOnWarningStage.Tracking));
                Assert.That(status.Value.BearingDegrees, Is.EqualTo(0f).Within(1f), "the vehicle faces +X, straight at the shooter");

                Assert.That(vehicle.ForceExit(rider, VehicleLeaveReason.Forced), Is.True);
                Assert.That(_publisher.Refresh(), Is.Null);
                Assert.That(GameSignals.LockOnWarning.Value, Is.Null);
            }
            finally
            {
                foreach (var action in actions)
                {
                    action.Dispose();
                }
            }
        }

        [Test]
        public void WithoutAnInjectedRiderTheSelfsRiderIsUsed()
        {
            var rider = BindRider(out var actions);
            try
            {
                var vehicle = CreateVehicle();
                CreateShooter("enemy", new Vector3(100f, 0f, 0f), vehicle.Damageable).Tick(Step);
                Assert.That(vehicle.TryEnter(rider, 0, false), Is.True);

                Assert.That(_publisher.Refresh().Value.Stage, Is.EqualTo(LockOnWarningStage.Tracking));
            }
            finally
            {
                foreach (var action in actions)
                {
                    action.Dispose();
                }
            }
        }

        private PlayerVehicleRider BindRider(out InputAction[] actions)
        {
            var body = _self.gameObject;
            var controller = body.AddComponent<CharacterController>();
            controller.height = 2f;
            controller.radius = 0.5f;
            controller.center = Vector3.zero;
            var player = body.AddComponent<PlayerController>();
            var move = new InputAction("Move", InputActionType.Value, expectedControlType: "Vector2");
            var sprint = new InputAction("Sprint", InputActionType.Button);
            var crouch = new InputAction("Crouch", InputActionType.Button);
            var jump = new InputAction("Jump", InputActionType.Button);
            actions = new[] { move, sprint, crouch, jump };
            var look = body.AddComponent<FirstPersonLook>();
            player.Initialize(controller, look, move, sprint, crouch, jump, new MotorSpec(5f, 8f, 1.2f, 20f, 0.1f, 2.5f));
            var rider = body.AddComponent<PlayerVehicleRider>();
            rider.Bind(player, null);
            return rider;
        }

        private Vehicle CreateVehicle()
        {
            var root = Track(new GameObject("Car"));
            root.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
            var seat = new GameObject("Driver Seat").AddComponent<VehicleSeat>();
            seat.transform.SetParent(root.transform, false);
            seat.transform.localPosition = new Vector3(0f, 0.5f, 0f);
            var vehicle = root.AddComponent<Vehicle>();
            vehicle.Configure(new VehicleSpec("car", 500, new[] { new VehicleSeatSpec(VehicleSeatRole.Driver) }));
            return vehicle;
        }

        private LockOnAim CreateShooter(string name, Vector3 position, Damageable target, Func<bool> aiming = null)
        {
            var body = Track(new GameObject(name));
            body.transform.position = position;
            var aim = body.AddComponent<LockOnAim>();
            ConfigureShooter(aim, position, target, aiming ?? (() => true));
            _shooters.Add(aim);
            return aim;
        }

        private static void ConfigureShooter(LockOnAim aim, Vector3 position, Damageable target, Func<bool> aiming)
        {
            var round = new RoundSpec(
                RoundDelivery.Projectile,
                range: 500f,
                projectile: new ProjectileSpec(20f, 0f, 5f),
                homing: new HomingSpec(1f, 15f, 180f));
            aim.RoundSource = () => round;
            aim.AimingSource = aiming;
            aim.AimSource = () =>
            {
                var offset = target.AimPoint - position;
                return new Ray(position, offset.sqrMagnitude > 1e-6f ? offset.normalized : Vector3.forward);
            };
            aim.Targets = new FixedTargets(target);
            aim.TargetLookup = id => id == FixedTargets.TargetId ? target : null;
        }

        private Damageable CreateBody(string id, Vector3 position)
        {
            var body = Track(new GameObject(id));
            body.transform.position = position;
            var damageable = body.AddComponent<Damageable>();
            damageable.Initialize(id, 100);
            return damageable;
        }

        private GameObject Track(GameObject gameObject)
        {
            _created.Add(gameObject);
            return gameObject;
        }

        private sealed class FixedTargets : ILockOnTargets
        {
            public const int TargetId = 7;

            private readonly Damageable _target;

            public FixedTargets(Damageable target)
            {
                _target = target;
            }

            public void Collect(NVector3 eye, NVector3 forward, float range, List<LockOnCandidate> into)
            {
                if (_target != null && !_target.IsDead)
                {
                    into.Add(new LockOnCandidate(TargetId, _target.AimPoint.ToNumerics()));
                }
            }

            public Func<NVector3?> Follow(int id)
            {
                return null;
            }

            public ILockOnDecoy Decoy(int id) => null;
        }
    }
}
