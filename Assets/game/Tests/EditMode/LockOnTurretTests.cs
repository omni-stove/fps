using System.Collections.Generic;
using Shooter.Core;
using Shooter.Editor;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    // Edit Mode では Awake も Update も走らないので、タレットの Tick と捕捉の Tick を直接呼ぶ。
    public sealed class LockOnTurretTests
    {
        private const string GuidedId = "9d2c4b6a-1e3f-4a5b-8c7d-0e1f2a3b4c5d";
        private const float Range = 60f;

        private readonly List<Object> _created = new List<Object>();
        private WeaponDefinition _guided;

        [SetUp]
        public void SetUp()
        {
            _guided = ScriptableObject.CreateInstance<WeaponDefinition>();
            _guided.name = "Guided";
            _created.Add(_guided);
            WeaponAssets.Configure(
                _guided,
                new WeaponSpec(
                    GuidedId,
                    "weapon.guided",
                    WeaponCategory.Launcher,
                    round: new RoundSpec(
                        RoundDelivery.Projectile,
                        Range,
                        projectile: new ProjectileSpec(20f, 0f, 5f),
                        homing: new HomingSpec(1f, 10f, 90f))));
        }

        [TearDown]
        public void TearDown()
        {
            GameSignals.ResetAll();
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
        public void TurnsItsHeadToATargetInTheAirAndLocksItWithoutTouchingTheHud()
        {
            var target = CreateTarget("flyer", new Vector3(0f, 20f, 20f));
            var turret = CreateTurret(Vector3.zero);
            var model = new HudModel();
            GameSignals.HudModel.Publish(model);

            Assert.That(turret.Tick(0.1f), Is.True);
            Assert.That(turret.HasTarget, Is.True);
            Assert.That(turret.TargetPoint, Is.EqualTo(target.AimPoint));
            var before = Vector3.Angle(turret.Head.forward, target.AimPoint - turret.Head.position);
            Assert.That(before, Is.GreaterThan(5f), "the head turns at its turn speed, not at once");

            turret.Tick(1f);
            Assert.That(Vector3.Angle(turret.Head.forward, target.AimPoint - turret.Head.position), Is.LessThan(0.5f));

            turret.Aim.Tick(0.5f);
            Assert.That(turret.Aim.Stage, Is.EqualTo(LockOnWarningStage.Tracking));
            turret.Aim.Tick(0.6f);
            Assert.That(turret.Aim.Stage, Is.EqualTo(LockOnWarningStage.Locked));
            Assert.That(turret.Aim.Target, Is.SameAs(target));
            Assert.That(turret.Aim.PublishesToHud, Is.False);
            Assert.That(model.LockOnProgress, Is.Null, "the enemy's lock does not show on the player's reticle");
        }

        [Test]
        public void SomeoneOnTheGroundIsNotTaken()
        {
            CreateTarget("walker", new Vector3(0f, 1f, 20f));
            var turret = CreateTurret(Vector3.zero);

            Assert.That(turret.Tick(0.1f), Is.False);
            Assert.That(turret.HasTarget, Is.False);

            turret.Aim.Tick(1f);
            Assert.That(turret.Aim.Stage, Is.EqualTo(LockOnWarningStage.None), "without a target the turret does not aim");
        }

        [Test]
        public void TheTargetItTracksIsKeptOverANearerNewcomer()
        {
            var first = CreateTarget("first", new Vector3(0f, 20f, 40f));
            var turret = CreateTurret(Vector3.zero);
            turret.Tick(2f);
            turret.Aim.Tick(0.5f);
            Assert.That(turret.Aim.Target, Is.SameAs(first));

            CreateTarget("nearer", new Vector3(10f, 15f, 10f));
            turret.Tick(0.1f);

            Assert.That(turret.TargetPoint, Is.EqualTo(first.AimPoint));
        }

        [Test]
        public void TheNearestFreeTargetIsTakenFirstAndAlliesAreSkipped()
        {
            var turret = CreateTurret(Vector3.zero);
            turret.gameObject.AddComponent<SideMember>().Side = new Side(1);
            var ally = CreateTarget("ally", new Vector3(0f, 10f, 10f));
            ally.gameObject.AddComponent<SideMember>().Side = new Side(1);
            var far = CreateTarget("far", new Vector3(0f, 20f, 40f));
            var near = CreateTarget("near", new Vector3(-10f, 20f, 15f));

            turret.Tick(0.1f);

            Assert.That(turret.TargetPoint, Is.EqualTo(near.AimPoint));
            Assert.That(turret.TargetPoint, Is.Not.EqualTo(far.AimPoint));
        }

        [Test]
        public void ConfigureReplacesTheTurnSpeedAndHeight()
        {
            CreateTarget("low", new Vector3(0f, 3f, 20f));
            var turret = CreateTurret(Vector3.zero);
            Assert.That(turret.Tick(0.1f), Is.False);

            turret.Configure(new LockOnTurretSpec(30f, 1f));

            Assert.That(turret.Spec.TurnDegreesPerSecond, Is.EqualTo(30f));
            Assert.That(turret.Tick(0.1f), Is.True, "a lower floor lets it take the low target");
            Assert.Throws<System.ArgumentNullException>(() => turret.Configure(null));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => turret.Tick(-1f));
        }

        [Test]
        public void AWeaponThatCannotHomeLeavesTheTurretIdleWithOneWarning()
        {
            CreateTarget("flyer", new Vector3(0f, 20f, 20f));
            var rifle = ScriptableObject.CreateInstance<WeaponDefinition>();
            rifle.name = "Rifle";
            _created.Add(rifle);
            WeaponAssets.Configure(rifle, TestWeapons.RifleSpec());
            var turret = CreateTurret(Vector3.zero);
            turret.Weapon = rifle;

            LogAssert.Expect(LogType.Warning, $"[LockOnTurret] 'Turret' cannot lock on with weapon '{TestWeapons.RifleId}': its round has no homing");
            Assert.That(turret.Tick(0.1f), Is.False);
            Assert.That(turret.Tick(0.1f), Is.False, "the warning is not repeated");

            turret.Weapon = null;
            LogAssert.Expect(LogType.Warning, "[LockOnTurret] 'Turret' has no weapon; set one whose round has homing to lock on");
            Assert.That(turret.Tick(0.1f), Is.False);
        }

        [Test]
        public void ATargetTagLetsItWatchTheGroundForPlayersOnlyAndSkipsTheDummies()
        {
            var dummy = CreateTarget("dummy", new Vector3(-3f, 1f, 5f));
            var player = CreateTarget("player", new Vector3(0f, 1f, 20f));
            player.gameObject.tag = GameBootstrap.PlayerTag;
            var turret = CreateTurret(Vector3.zero);
            turret.Configure(new LockOnTurretSpec(LockOnTurretSpec.DefaultTurnDegreesPerSecond, 0f, GameBootstrap.PlayerTag));

            Assert.That(turret.Spec.TargetTag, Is.EqualTo(GameBootstrap.PlayerTag));
            Assert.That(turret.Tick(0.1f), Is.True, "a floor of 0m takes someone walking on the ground");
            Assert.That(turret.TargetPoint, Is.EqualTo(player.AimPoint), "the nearer dummy has no player tag");
            Assert.That(turret.TargetPoint, Is.Not.EqualTo(dummy.AimPoint));

            turret.Tick(2f);
            turret.Aim.Tick(0.5f);
            Assert.That(turret.Aim.Stage, Is.EqualTo(LockOnWarningStage.Tracking));
            turret.Aim.Tick(0.6f);
            Assert.That(turret.Aim.Stage, Is.EqualTo(LockOnWarningStage.Locked));
            Assert.That(turret.Aim.Target, Is.SameAs(player));
        }

        [Test]
        public void TheTargetTagIsAlsoFoundOnTheParentOfTheBody()
        {
            var holder = new GameObject("holder") { tag = GameBootstrap.PlayerTag };
            _created.Add(holder);
            var body = CreateTarget("body", new Vector3(0f, 1f, 20f));
            body.transform.SetParent(holder.transform, true);
            Physics.SyncTransforms();
            var turret = CreateTurret(Vector3.zero);
            turret.Configure(new LockOnTurretSpec(90f, 0f, GameBootstrap.PlayerTag));

            Assert.That(turret.Tick(0.1f), Is.True);
            Assert.That(turret.TargetPoint, Is.EqualTo(body.AimPoint));
        }

        [Test]
        public void WithoutAPlayerOnTheGroundATaggedTurretStaysIdle()
        {
            CreateTarget("dummy", new Vector3(0f, 1f, 5f));
            CreateTarget("flyer", new Vector3(0f, 20f, 20f));
            var turret = CreateTurret(Vector3.zero);
            turret.Configure(new LockOnTurretSpec(90f, 0f, GameBootstrap.PlayerTag));

            Assert.That(turret.Tick(0.1f), Is.False, "untagged bodies are not taken, high or low");
            turret.Aim.Tick(1f);
            Assert.That(turret.Aim.Stage, Is.EqualTo(LockOnWarningStage.None));

            turret.Configure(LockOnTurretSpec.Default);
            Assert.That(turret.Spec.TargetTag, Is.Empty);
            Assert.That(turret.Tick(0.1f), Is.True, "an empty tag takes anyone again");
        }

        private LockOnTurret CreateTurret(Vector3 position)
        {
            var root = new GameObject("Turret");
            _created.Add(root);
            root.transform.position = position;
            var head = new GameObject("Head").transform;
            head.SetParent(root.transform, false);
            head.localPosition = new Vector3(0f, 1.5f, 0f);
            var turret = root.AddComponent<LockOnTurret>();
            turret.Weapon = _guided;
            turret.Head = head;
            return turret;
        }

        private Damageable CreateTarget(string id, Vector3 position)
        {
            var target = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            _created.Add(target);
            target.name = id;
            target.transform.position = position;
            var damageable = target.AddComponent<Damageable>();
            damageable.Initialize(id, 100);
            target.AddComponent<Hitbox>().Initialize(HitZone.Body, damageable);
            Physics.SyncTransforms();
            return damageable;
        }
    }
}
