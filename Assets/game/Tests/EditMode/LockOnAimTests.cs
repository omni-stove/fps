using System.Collections.Generic;
using Shooter.Core;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    public sealed class LockOnAimTests
    {
        private const float _step = 0.25f;

        private static readonly Vector3 _eye = new Vector3(0f, 1f, 0f);

        private readonly List<Object> _created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            GameSignals.ResetAll();
            foreach (var body in Object.FindObjectsByType<ProjectileBody>(FindObjectsInactive.Include))
            {
                Object.DestroyImmediate(body.gameObject);
            }

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
        public void CollectsLivingVisibleTargetsButNotTheShooterOrOnesBehindAWall()
        {
            var shooter = CreateTarget("shooter", _eye);
            var seen = CreateTarget("seen", new Vector3(0f, 1f, 10f));
            CreateTarget("hidden", new Vector3(5f, 1f, 10f));
            CreateWall(new Vector3(2.5f, 1f, 5f), new Vector3(2f, 4f, 0.5f));
            var dead = CreateTarget("dead", new Vector3(-5f, 1f, 10f));
            dead.Kill();
            CreateTarget("far", new Vector3(0f, 1f, 80f));

            var targets = new HitboxLockOnTargets(shooter.transform, Physics.DefaultRaycastLayers);
            var found = new List<LockOnCandidate>();
            targets.Collect(_eye.ToNumerics(), Vector3.forward.ToNumerics(), 50f, found);

            Assert.That(found, Has.Count.EqualTo(1));
            Assert.That(found[0].Point.ToUnity(), Is.EqualTo(seen.AimPoint));
            var follow = targets.Follow(found[0].Id);
            seen.transform.position += Vector3.right;
            Physics.SyncTransforms();
            Assert.That(follow().Value.ToUnity(), Is.EqualTo(seen.AimPoint), "the round follows where the target is now");
            seen.Kill();
            Assert.That(follow(), Is.Null);
            Assert.That(targets.Follow(99), Is.Null);
        }

        [Test]
        public void AimingAtATargetLocksAfterTheLockOnSecondsAndShowsOnTheHud()
        {
            CreateTarget("target", new Vector3(0f, 1f, 20f));
            var aiming = true;
            var aim = Create(Guided(), () => aiming);
            var model = new HudModel();

            LogAssert.Expect(LogType.Log, new System.Text.RegularExpressions.Regex(@"^\[Combat\] lock-on tracking target=\d+$"));
            aim.Tick(_step);
            aim.ShowOn(model);
            Assert.That(model.LockOnProgress, Is.EqualTo(0.25f).Within(1e-4f));
            Assert.That(aim.LockedTargetId, Is.Null);
            Assert.That(aim.FollowLocked(), Is.Null, "nothing to follow before the lock");

            aim.Tick(_step);
            aim.Tick(_step);
            LogAssert.Expect(LogType.Log, new System.Text.RegularExpressions.Regex(@"^\[Combat\] lock-on locked target=\d+$"));
            aim.Tick(_step);
            aim.ShowOn(model);
            Assert.That(model.IsLockedOn, Is.True);
            Assert.That(aim.LockedTargetId, Is.Not.Null);
            Assert.That(aim.FollowLocked(), Is.Not.Null);

            aiming = false;
            LogAssert.Expect(LogType.Log, new System.Text.RegularExpressions.Regex(@"^\[Combat\] lock-on lost target=none$"));
            aim.Tick(_step);
            aim.ShowOn(model);
            Assert.That(model.LockOnProgress, Is.Null);
            Assert.That(aim.LockedTargetId, Is.Null);
        }

        [Test]
        public void TheTrackedTargetAndItsStageCanBeReadFromOutside()
        {
            var target = CreateTarget("target", new Vector3(0f, 1f, 20f));
            var aiming = true;
            var aim = Create(Guided(), () => aiming);
            var targets = new HitboxLockOnTargets(aim.transform, Physics.DefaultRaycastLayers);
            aim.Targets = targets;
            aim.TargetLookup = targets.Find;

            Assert.That(aim.Stage, Is.EqualTo(LockOnWarningStage.None));
            Assert.That(aim.TargetId, Is.Null);
            Assert.That(aim.Target, Is.Null);

            LogAssert.Expect(LogType.Log, new System.Text.RegularExpressions.Regex(@"^\[Combat\] lock-on tracking target=\d+$"));
            aim.Tick(_step);
            Assert.That(aim.Stage, Is.EqualTo(LockOnWarningStage.Tracking));
            Assert.That(aim.TargetId, Is.Not.Null);
            Assert.That(aim.Target, Is.SameAs(target));

            LogAssert.Expect(LogType.Log, new System.Text.RegularExpressions.Regex(@"^\[Combat\] lock-on locked target=\d+$"));
            aim.Tick(1f);
            Assert.That(aim.Stage, Is.EqualTo(LockOnWarningStage.Locked));
            Assert.That(aim.Target, Is.SameAs(target));

            aiming = false;
            LogAssert.Expect(LogType.Log, new System.Text.RegularExpressions.Regex(@"^\[Combat\] lock-on lost target=none$"));
            aim.Tick(_step);
            Assert.That(aim.Stage, Is.EqualTo(LockOnWarningStage.None));
            Assert.That(aim.Target, Is.Null);
        }

        [Test]
        public void WithoutATargetLookupTheTargetCannotBeNamed()
        {
            CreateTarget("target", new Vector3(0f, 1f, 20f));
            var aim = Create(Guided(), () => true);

            LogAssert.Expect(LogType.Log, new System.Text.RegularExpressions.Regex(@"^\[Combat\] lock-on tracking target=\d+$"));
            aim.Tick(_step);

            Assert.That(aim.Stage, Is.EqualTo(LockOnWarningStage.Tracking));
            Assert.That(aim.Target, Is.Null);
        }

        [Test]
        public void HitboxTargetsFindOnlyTheOnesTheyHaveSeen()
        {
            var shooter = CreateTarget("shooter", _eye);
            var seen = CreateTarget("seen", new Vector3(0f, 1f, 10f));
            var targets = new HitboxLockOnTargets(shooter.transform, Physics.DefaultRaycastLayers);
            var found = new List<LockOnCandidate>();
            targets.Collect(_eye.ToNumerics(), Vector3.forward.ToNumerics(), 50f, found);

            Assert.That(targets.Find(found[0].Id), Is.SameAs(seen));
            Assert.That(targets.Find(-1), Is.Null);
            Assert.That(targets.Find(99), Is.Null);

            Object.DestroyImmediate(seen.gameObject);
            Assert.That(targets.Find(found[0].Id) is null, Is.True, "a destroyed body is a plain null, not a destroyed object");
        }

        [Test]
        public void EachTickWritesTheLockOnToThePublishedHudModel()
        {
            CreateTarget("target", new Vector3(0f, 1f, 20f));
            var aiming = true;
            var aim = Create(Guided(), () => aiming);
            var model = new HudModel();
            GameSignals.HudModel.Publish(model);

            LogAssert.Expect(LogType.Log, new System.Text.RegularExpressions.Regex(@"^\[Combat\] lock-on tracking target=\d+$"));
            aim.Tick(_step);
            Assert.That(model.LockOnProgress, Is.EqualTo(0.25f).Within(1e-4f));

            aiming = false;
            LogAssert.Expect(LogType.Log, new System.Text.RegularExpressions.Regex(@"^\[Combat\] lock-on lost target=none$"));
            aim.Tick(_step);
            Assert.That(model.LockOnProgress, Is.Null);
        }

        [Test]
        public void TickingWithoutAPublishedHudModelWritesNowhere()
        {
            CreateTarget("target", new Vector3(0f, 1f, 20f));
            var aim = Create(Guided(), () => true);

            LogAssert.Expect(LogType.Log, new System.Text.RegularExpressions.Regex(@"^\[Combat\] lock-on tracking target=\d+$"));
            Assert.DoesNotThrow(() => aim.Tick(_step));
            Assert.That(aim.Progress, Is.EqualTo(0.25f).Within(1e-4f));
        }

        [Test]
        public void AWeaponThatDoesNotHomeNeverLocks()
        {
            CreateTarget("target", new Vector3(0f, 1f, 20f));
            var aim = Create(new RoundSpec(), () => true);

            aim.Tick(10f);

            Assert.That(aim.Progress, Is.Null);
            Assert.That(aim.LockedTargetId, Is.Null);
        }

        [Test]
        public void TheLockingAndLockedSoundsComeFromTheWeapon()
        {
            CreateTarget("target", new Vector3(0f, 1f, 20f));
            var locking = AudioClip.Create("locking", 441, 1, 44100, false);
            var locked = AudioClip.Create("locked", 441, 1, 44100, false);
            _created.Add(locking);
            _created.Add(locked);
            var look = new HomingSettings { LockingSound = locking, LockedSound = locked };
            var aim = Create(Guided(), () => true);
            aim.LookSource = () => look;

            aim.Tick(_step);
            var audio = aim.GetComponent<AudioSource>();
            Assert.That(audio, Is.Not.Null);
            Assert.That(audio.clip, Is.SameAs(locking));
            Assert.That(audio.spatialBlend, Is.Zero, "the shooter hears it in their head");

            aim.Tick(1f);
            Assert.That(audio.clip, Is.SameAs(locked));
        }

        [Test]
        public void WithoutSoundsNothingIsAddedToPlayThem()
        {
            CreateTarget("target", new Vector3(0f, 1f, 20f));
            var aim = Create(Guided(), () => true);
            aim.LookSource = () => new HomingSettings();

            aim.Tick(1f);

            Assert.That(aim.GetComponent<AudioSource>(), Is.Null);
        }

        [Test]
        public void AHomingRoundChasesTheLockedTargetItWouldMissFlyingStraight()
        {
            var target = CreateTarget("target", new Vector3(3f, 1f, 15f));
            var round = Guided();
            var request = new ShotRequest(
                null,
                null,
                null,
                round,
                new ImpactSpec(10),
                "test-guided",
                Physics.DefaultRaycastLayers,
                homingTarget: () => target.AimPoint.ToNumerics());

            var body = ProjectileBody.Launch(request, new PlannedRound(_eye.ToNumerics(), Vector3.forward.ToNumerics(), 0));
            LogAssert.Expect(LogType.Log, new System.Text.RegularExpressions.Regex(@"^\[Combat\] projectile weapon=test-guided end=HitTarget .* hit=target dmg=10$"));
            for (var i = 0; i < 500 && body != null; i++)
            {
                body.Tick(0.02f);
            }

            Assert.That(target.Health.Current, Is.EqualTo(90));
        }

        [Test]
        public void AnAimThatDoesNotPublishLeavesThePlayersHudAlone()
        {
            CreateTarget("target", new Vector3(0f, 1f, 20f));
            var model = new HudModel();
            model.SetLockOn(0.5f);
            GameSignals.HudModel.Publish(model);
            var aim = Create(Guided(), () => true);
            aim.PublishesToHud = false;

            LogAssert.Expect(LogType.Log, new System.Text.RegularExpressions.Regex(@"^\[Combat\] lock-on locked target=\d+$"));
            aim.Tick(2f);

            Assert.That(aim.Stage, Is.EqualTo(LockOnWarningStage.Locked));
            Assert.That(model.LockOnProgress, Is.EqualTo(0.5f).Within(1e-4f), "the player's own lock-on reading stays as it was");
            Assert.That(model.IsLockedOn, Is.False);
        }

        private static RoundSpec Guided()
        {
            return new RoundSpec(
                RoundDelivery.Projectile,
                range: 50f,
                projectile: new ProjectileSpec(20f, 0f, 5f),
                homing: new HomingSpec(1f, 15f, 180f));
        }

        private LockOnAim Create(RoundSpec round, System.Func<bool> aiming)
        {
            var player = new GameObject("player");
            _created.Add(player);
            player.transform.position = _eye;
            var aim = player.AddComponent<LockOnAim>();
            aim.RoundSource = () => round;
            aim.AimingSource = aiming;
            aim.AimSource = () => new Ray(_eye, Vector3.forward);
            aim.Targets = new HitboxLockOnTargets(player.transform, Physics.DefaultRaycastLayers);
            return aim;
        }

        private void CreateWall(Vector3 center, Vector3 size)
        {
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _created.Add(wall);
            wall.name = "wall";
            wall.transform.position = center;
            wall.transform.localScale = size;
            Physics.SyncTransforms();
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
