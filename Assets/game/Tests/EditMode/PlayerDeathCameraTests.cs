using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Shooter.Core;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    // Edit Mode では LateUpdate が走らないので、LateUpdate が集める状態 (ルール・生死・試合の終わり) を Step へ、入力を Advance へ直接渡す。
    public sealed class PlayerDeathCameraTests
    {
        private static readonly Vector3 _playerPosition = new Vector3(0f, 1f, 0f);

        private readonly List<Object> _objects = new List<Object>();
        private RecordingModeRunner _runner;

        [TearDown]
        public void TearDown()
        {
            if (_runner != null)
            {
                _runner.Stop();
                _runner = null;
            }

            for (var i = _objects.Count - 1; i >= 0; i--)
            {
                if (_objects[i] != null)
                {
                    Object.DestroyImmediate(_objects[i]);
                }
            }

            _objects.Clear();
        }

        [Test]
        public void KillerFacesWhoKilledThePlayerAndRestoresOnRespawn()
        {
            var deathCam = Player(out var rig);
            var camera = rig.Camera;
            var mode = rig.Mode;
            rig.UpdateCameraPosition();
            var localPosition = camera.transform.localPosition;
            var localRotation = camera.transform.localRotation;
            var eye = rig.Eye.position;
            var killer = Track(new GameObject("Killer"));
            killer.transform.position = eye + new Vector3(10f, 0f, 0f);
            deathCam.Mode = DeathCamera.Killer;

            Assert.That(deathCam.Step(DeathCamera.Killer, true, false), Is.EqualTo(DeathCamTransition.None));
            using (CombatEvents.Attribute(killer, "rifle"))
            {
                deathCam.GetComponent<Damageable>().Kill();
            }

            LogAssert.Expect(LogType.Log, new Regex(@"^\[DeathCam\] started on Player \(Killer\)"));
            Assert.That(deathCam.Step(DeathCamera.Killer, false, false), Is.EqualTo(DeathCamTransition.Started));
            Assert.That(rig.enabled, Is.False, "the rig must not pull the camera back to the eye");
            Assert.That(rig.Mode, Is.EqualTo(ViewMode.ThirdPerson));

            LogAssert.Expect(LogType.Log, "[DeathCam] watching killer Killer from Player");
            deathCam.Advance(0, Vector2.zero, Vector2.zero, 0f);

            Assert.That(deathCam.Killer, Is.SameAs(killer));
            var position = camera.transform.position;
            Assert.That(position.y, Is.GreaterThan(eye.y + 0.5f), "a little above where the player fell");
            Assert.That(position.x, Is.LessThan(eye.x), "backs away from the killer so the death spot stays in view");
            var toKiller = (killer.transform.position - position).normalized;
            Assert.That(Vector3.Dot(camera.transform.forward, toKiller), Is.GreaterThan(0.999f), "faces the killer");

            deathCam.GetComponent<Damageable>().ResetHealth();
            LogAssert.Expect(LogType.Log, new Regex(@"^\[DeathCam\] stopped on Player \(Respawned\)"));
            Assert.That(deathCam.Step(DeathCamera.Killer, true, false), Is.EqualTo(DeathCamTransition.Respawned));

            Assert.That(deathCam.IsActive, Is.False);
            Assert.That(deathCam.Killer, Is.Null);
            Assert.That(camera.transform.localPosition, Is.EqualTo(localPosition));
            Assert.That(Quaternion.Angle(camera.transform.localRotation, localRotation), Is.LessThan(1e-3f));
            Assert.That(rig.enabled, Is.True);
            Assert.That(rig.Mode, Is.EqualTo(mode));
        }

        [Test]
        public void WithoutAKillerLooksDownAtTheDeathSpot()
        {
            var deathCam = Player(out var rig);
            var camera = rig.Camera;
            var eye = rig.Eye.position;
            deathCam.Mode = DeathCamera.Killer;

            deathCam.Step(DeathCamera.Killer, true, false);
            deathCam.GetComponent<Damageable>().Kill();
            deathCam.Step(DeathCamera.Killer, false, false);
            LogAssert.Expect(LogType.Log, "[DeathCam] no killer for Player; looking down at the death spot");
            deathCam.Advance(0, Vector2.zero, Vector2.zero, 0f);

            Assert.That(deathCam.Killer, Is.Null);
            var position = camera.transform.position;
            Assert.That(Vector3.Dot(camera.transform.forward, (eye - position).normalized), Is.GreaterThan(0.999f),
                "looks at where the player fell");
            Assert.That(camera.transform.forward.y, Is.LessThan(-0.3f), "looking down");
        }

        [Test]
        public void AllyWatchesASameSideMemberAndNextMovesToTheNextOne()
        {
            var deathCam = Player(out var rig);
            var camera = rig.Camera;
            var self = deathCam.GetComponent<SideMember>();
            var allyA = Member("Ally-A", 0, new Vector3(20f, 0f, 0f), 90f);
            var allyB = Member("Ally-B", 0, new Vector3(-20f, 0f, 0f), 0f);
            var enemy = Member("Enemy", 1, new Vector3(0f, 0f, 20f), 180f);
            BeginMode(self, allyA, allyB, enemy);
            deathCam.ModeRunner = _runner;
            deathCam.Mode = DeathCamera.Ally;

            deathCam.Step(DeathCamera.Ally, true, false);
            Assert.That(deathCam.Step(DeathCamera.Ally, false, false), Is.EqualTo(DeathCamTransition.Started));

            deathCam.Advance(0, Vector2.zero, Vector2.zero, 0f);

            Assert.That(deathCam.Ally, Is.SameAs(allyA), "the first ally by id");
            Assert.That(deathCam.IsFreeCamera, Is.False);
            var expected = allyA.transform.position + (Quaternion.Euler(0f, 90f, 0f) * rig.ThirdPersonOffset);
            Assert.That(Vector3.Distance(camera.transform.position, expected), Is.LessThan(1e-3f), "over the ally's shoulder");
            Assert.That(Vector3.Dot(camera.transform.forward, Vector3.right), Is.GreaterThan(0.999f), "faces where the ally faces");

            deathCam.Advance(1, Vector2.zero, Vector2.zero, 0f);
            Assert.That(deathCam.Ally, Is.SameAs(allyB), "Next moves to the next ally and skips the enemy");

            deathCam.Advance(1, Vector2.zero, Vector2.zero, 0f);
            Assert.That(deathCam.Ally, Is.SameAs(allyA), "wraps around");

            allyA.Damageable.Kill();
            deathCam.Advance(0, Vector2.zero, Vector2.zero, 0f);
            Assert.That(deathCam.Ally, Is.SameAs(allyB), "a fallen ally hands over to the next one");
        }

        [Test]
        public void AllyFallsBackToTheFreeCameraWhenNoAllyCanBeWatched()
        {
            var deathCam = Player(out var rig);
            var camera = rig.Camera;
            var self = deathCam.GetComponent<SideMember>();
            var enemy = Member("Enemy", 1, new Vector3(0f, 0f, 20f), 180f);
            BeginMode(self, enemy);
            deathCam.ModeRunner = _runner;
            deathCam.Mode = DeathCamera.Ally;
            deathCam.FreeSpeed = 4f;

            deathCam.Step(DeathCamera.Ally, true, false);
            deathCam.Step(DeathCamera.Ally, false, false);
            var start = camera.transform.position;

            LogAssert.Expect(LogType.Log, "[DeathCam] no ally to watch for Player; using the free camera");
            deathCam.Advance(0, new Vector2(0f, 1f), new Vector2(30f, 0f), 0.5f);

            Assert.That(deathCam.IsFreeCamera, Is.True);
            Assert.That(deathCam.Ally, Is.Null);
            var expected = start + (camera.transform.forward * 2f);
            Assert.That(Vector3.Distance(camera.transform.position, expected), Is.LessThan(1e-3f), "Move flies the way it faces");
        }

        [Test]
        public void AllyWithoutAModeUsesTheFreeCamera()
        {
            var deathCam = Player(out _);
            deathCam.Mode = DeathCamera.Ally;

            deathCam.Step(DeathCamera.Ally, true, false);
            deathCam.Step(DeathCamera.Ally, false, false);
            deathCam.Advance(0, Vector2.zero, Vector2.zero, 0f);

            Assert.That(deathCam.IsFreeCamera, Is.True, "the campaign has no allies to watch");
        }

        [Test]
        public void FreeTurnsAndFliesFromAboveTheDeathSpot()
        {
            var deathCam = Player(out var rig);
            var camera = rig.Camera;
            var eye = rig.Eye.position;
            deathCam.Mode = DeathCamera.Free;
            deathCam.FreeSpeed = 4f;

            deathCam.Step(DeathCamera.Free, true, false);
            deathCam.Step(DeathCamera.Free, false, false);
            var start = camera.transform.position;
            Assert.That(start.y, Is.GreaterThan(eye.y + 0.5f), "starts above where the player fell");

            LogAssert.Expect(LogType.Log, new Regex(@"^\[DeathCam\] free camera on Player from "));
            var yawBefore = camera.transform.eulerAngles.y;
            deathCam.Advance(0, new Vector2(0f, 1f), new Vector2(30f, 10f), 0.5f);

            Assert.That(deathCam.IsFreeCamera, Is.True);
            Assert.That(Mathf.DeltaAngle(yawBefore, camera.transform.eulerAngles.y), Is.EqualTo(30f).Within(1e-2f), "Look turns the camera");
            var expected = start + (camera.transform.forward * 2f);
            Assert.That(Vector3.Distance(camera.transform.position, expected), Is.LessThan(1e-3f), "Move flies the way it faces");
        }

        [Test]
        public void MatchEndYieldsToTheEndOfMatchView()
        {
            var deathCam = Player(out var rig);
            deathCam.Mode = DeathCamera.Free;

            deathCam.Step(DeathCamera.Free, true, false);
            deathCam.Step(DeathCamera.Free, false, false);

            LogAssert.Expect(LogType.Log, new Regex(@"^\[DeathCam\] stopped on Player \(MatchEnded\)"));
            Assert.That(deathCam.Step(DeathCamera.Free, false, true), Is.EqualTo(DeathCamTransition.MatchEnded));
            Assert.That(rig.enabled, Is.True);
        }

        [Test]
        public void DownedIsNotDeadSoTheCameraWaits()
        {
            var deathCam = Player(out _);
            deathCam.Mode = DeathCamera.Free;
            var damageable = deathCam.GetComponent<Damageable>();

            deathCam.Step(DeathCamera.Free, true, false);
            damageable.Kill();
            damageable.SetDowned(true);

            // LateUpdate と同じく、ダウン中は生きているとして渡す
            Assert.That(deathCam.Step(DeathCamera.Free, !damageable.IsDead || damageable.IsDowned, false), Is.EqualTo(DeathCamTransition.None));
            Assert.That(deathCam.IsActive, Is.False);
        }

        [Test]
        public void FallenDropsTheOwnEyeToTheFloorTiltedAndRestoresOnRespawn()
        {
            var deathCam = Player(out var rig);
            var camera = rig.Camera;
            rig.UpdateCameraPosition();
            var localPosition = camera.transform.localPosition;
            var eye = rig.Eye.position;
            deathCam.Mode = DeathCamera.Fallen;
            deathCam.FallSeconds = 1f;
            deathCam.FallenEyeHeight = 0.2f;
            deathCam.FallenRollDegrees = 80f;
            deathCam.FallenPitchDegrees = 10f;

            deathCam.Step(DeathCamera.Fallen, true, false);
            deathCam.GetComponent<Damageable>().Kill();
            LogAssert.Expect(LogType.Log, new Regex(@"^\[DeathCam\] started on Player \(Fallen\)"));
            Assert.That(deathCam.Step(DeathCamera.Fallen, false, false), Is.EqualTo(DeathCamTransition.Started));
            Assert.That(Vector3.Distance(camera.transform.position, eye), Is.LessThan(1e-4f), "starts from the own eye, not from above");
            Assert.That(rig.enabled, Is.False);
            Assert.That(rig.Mode, Is.EqualTo(ViewMode.FirstPerson), "stays first person so the own body is drawn as a shadow only");

            // 半分の時間: 目 (1.6) から足元 (1.0) + 0.2 までの 0.4 のうち 0.25 落ち、傾きは 0.75 進む。
            deathCam.Advance(0, Vector2.zero, Vector2.zero, 0.5f);
            Assert.That(deathCam.FallenElapsed, Is.EqualTo(0.5f).Within(1e-5f));
            Assert.That(camera.transform.position.y, Is.EqualTo(eye.y - 0.1f).Within(1e-4f));
            Assert.That(camera.transform.position.x, Is.EqualTo(eye.x).Within(1e-4f));
            Assert.That(camera.transform.position.z, Is.EqualTo(eye.z).Within(1e-4f));
            Assert.That(Mathf.DeltaAngle(0f, camera.transform.eulerAngles.z), Is.EqualTo(60f).Within(1e-2f));

            LogAssert.Expect(LogType.Log, new Regex(@"^\[DeathCam\] fallen view settled on Player at \(0\.0, 1\.2, 0\.0\)"));
            deathCam.Advance(0, Vector2.one, Vector2.one, 0.6f);
            Assert.That(camera.transform.position.y, Is.EqualTo(1.2f).Within(1e-4f), "0.2m above the feet");
            Assert.That(Mathf.DeltaAngle(0f, camera.transform.eulerAngles.z), Is.EqualTo(80f).Within(1e-2f));
            Assert.That(Mathf.DeltaAngle(0f, camera.transform.eulerAngles.x), Is.EqualTo(10f).Within(1e-2f));
            Assert.That(Mathf.DeltaAngle(0f, camera.transform.eulerAngles.y), Is.EqualTo(0f).Within(1e-2f), "keeps the body's yaw");

            deathCam.Advance(0, Vector2.one, Vector2.one, 5f);
            Assert.That(camera.transform.position.y, Is.EqualTo(1.2f).Within(1e-4f), "stays down; move input does not fly it");

            deathCam.GetComponent<Damageable>().ResetHealth();
            LogAssert.Expect(LogType.Log, new Regex(@"^\[DeathCam\] stopped on Player \(Respawned\)"));
            Assert.That(deathCam.Step(DeathCamera.Fallen, true, false), Is.EqualTo(DeathCamTransition.Respawned));
            Assert.That(camera.transform.localPosition, Is.EqualTo(localPosition));
            Assert.That(rig.enabled, Is.True);
            Assert.That(deathCam.FallenElapsed, Is.EqualTo(0f));
        }

        [Test]
        public void FallenMidJumpSettlesOnTheFloorBelowNotInTheAir()
        {
            var deathCam = Player(out var rig);
            var camera = rig.Camera;
            var floor = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
            floor.transform.position = new Vector3(0f, -2.5f, 0f);
            Physics.SyncTransforms();
            deathCam.Mode = DeathCamera.Fallen;
            deathCam.FallSeconds = 1f;
            deathCam.FallenEyeHeight = 0.2f;

            // 足元 (y=1) は空中で、床の上面は y=-2。
            deathCam.Step(DeathCamera.Fallen, true, false);
            deathCam.GetComponent<Damageable>().Kill();
            deathCam.Step(DeathCamera.Fallen, false, false);
            Assert.That(deathCam.FallenGroundY, Is.EqualTo(-2f).Within(1e-3f));

            deathCam.Advance(0, Vector2.zero, Vector2.zero, 2f);
            Assert.That(camera.transform.position.y, Is.InRange(-1.9f, -1.5f), "about 0.2m above the floor, not above the airborne feet");
        }

        [Test]
        public void FallenHolstersTheWeaponAndHidesTheOwnShell()
        {
            var deathCam = Player(out var rig);
            var weapon = rig.Camera.gameObject.AddComponent<WeaponController>();
            var torso = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
            torso.name = "Torso";
            torso.transform.SetParent(deathCam.transform, false);
            var body = DeathBodyPresenter.Attach(deathCam.gameObject);
            body.Bind(deathCam.GetComponent<Damageable>(), DeathBody.Collapse);
            deathCam.Mode = DeathCamera.Fallen;

            deathCam.Step(DeathCamera.Fallen, true, false);
            deathCam.GetComponent<Damageable>().Kill();
            var shell = body.Shell;
            Assert.That(shell, Is.Not.Null);
            Track(shell);

            deathCam.Step(DeathCamera.Fallen, false, false);

            Assert.That(weapon.IsHolstered, Is.True, "no gun floating in front of the fallen view");
            foreach (var shellRenderer in shell.GetComponentsInChildren<Renderer>(true))
            {
                Assert.That(shellRenderer.enabled, Is.False, "neither the shell nor its shadow is drawn: " + shellRenderer.name);
            }

            deathCam.GetComponent<Damageable>().ResetHealth();
            deathCam.Step(DeathCamera.Fallen, true, false);
            Assert.That(weapon.IsHolstered, Is.False, "the weapon comes back on respawn");
        }

        [Test]
        public void FallenHidesTheOwnBodyShadowAndShowsTheBodyAgainOnRespawn()
        {
            var deathCam = Player(out _);
            var torso = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
            torso.name = "Torso";
            torso.transform.SetParent(deathCam.transform, false);
            var torsoRenderer = torso.GetComponent<Renderer>();
            torsoRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
            var alreadyHidden = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
            alreadyHidden.name = "AlreadyHidden";
            alreadyHidden.transform.SetParent(deathCam.transform, false);
            var alreadyHiddenRenderer = alreadyHidden.GetComponent<Renderer>();
            alreadyHiddenRenderer.enabled = false;
            deathCam.Mode = DeathCamera.Fallen;

            deathCam.Step(DeathCamera.Fallen, true, false);
            Assert.That(torsoRenderer.enabled, Is.True, "alive, the first-person body still casts its shadow");

            deathCam.Step(DeathCamera.Fallen, false, false);
            Assert.That(torsoRenderer.enabled, Is.False, "no standing shadow left behind while the view falls");

            deathCam.Step(DeathCamera.Fallen, true, false);
            Assert.That(torsoRenderer.enabled, Is.True, "the body is drawn again on respawn");
            Assert.That(alreadyHiddenRenderer.enabled, Is.False, "a renderer hidden before the fall stays hidden");
        }

        [Test]
        public void FreeLeavesTheOwnBodyAsItWas()
        {
            var deathCam = Player(out _);
            var torso = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
            torso.name = "Torso";
            torso.transform.SetParent(deathCam.transform, false);
            var torsoRenderer = torso.GetComponent<Renderer>();
            deathCam.Mode = DeathCamera.Free;

            deathCam.Step(DeathCamera.Free, true, false);
            deathCam.Step(DeathCamera.Free, false, false);

            Assert.That(torsoRenderer.enabled, Is.True, "only the fallen view hides the own body");
        }

        [Test]
        public void FallenSettingsAreKeptInRange()
        {
            var deathCam = Player(out _);

            deathCam.FallSeconds = -1f;
            deathCam.FallenEyeHeight = float.NaN;
            deathCam.FallenRollDegrees = 200f;
            deathCam.FallenPitchDegrees = -200f;

            Assert.That(deathCam.FallSeconds, Is.EqualTo(0f));
            Assert.That(deathCam.FallenEyeHeight, Is.EqualTo(FallenView.DefaultEyeHeight));
            Assert.That(deathCam.FallenRollDegrees, Is.EqualTo(FallenView.MaxDegrees));
            Assert.That(deathCam.FallenPitchDegrees, Is.EqualTo(-FallenView.MaxDegrees));
            Assert.That(deathCam.FallenMotion.Seconds, Is.EqualTo(0f));
        }

        [Test]
        public void NoneKeepsTheOwnView()
        {
            var deathCam = Player(out var rig);
            var camera = rig.Camera;
            rig.UpdateCameraPosition();
            var localPosition = camera.transform.localPosition;

            deathCam.Step(DeathCamera.None, true, false);

            Assert.That(deathCam.Step(DeathCamera.None, false, false), Is.EqualTo(DeathCamTransition.None));
            deathCam.Advance(1, Vector2.one, Vector2.one, 1f);

            Assert.That(deathCam.IsActive, Is.False);
            Assert.That(camera.transform.localPosition, Is.EqualTo(localPosition));
            Assert.That(rig.enabled, Is.True);
        }

        [Test]
        public void ApplyRulesTakesTheRulesDeathCamera()
        {
            var deathCam = Player(out _);

            deathCam.ApplyRules(GameRules.Default.WithDeathCamera(DeathCamera.Ally));
            Assert.That(deathCam.Mode, Is.EqualTo(DeathCamera.Ally));

            deathCam.ApplyRules(null);
            Assert.That(deathCam.Mode, Is.EqualTo(DeathCamera.None));
        }

        private PlayerDeathCamera Player(out ViewRig rig)
        {
            var go = Track(new GameObject("Player"));
            go.transform.SetPositionAndRotation(_playerPosition, Quaternion.identity);
            go.AddComponent<Damageable>().Initialize("Player", Damageable.DefaultMaxHealth);
            go.AddComponent<SideMember>().Side = new Side(0);

            var eye = new GameObject(GameBootstrap.EyeName).transform;
            eye.SetParent(go.transform, false);
            eye.localPosition = GameBootstrap.EyeOffset;
            var cameraObject = new GameObject(GameBootstrap.CameraName);
            cameraObject.transform.SetParent(eye, false);
            var camera = cameraObject.AddComponent<Camera>();

            rig = go.AddComponent<ViewRig>();
            rig.Initialize(eye, camera);
            return go.AddComponent<PlayerDeathCamera>();
        }

        private SideMember Member(string name, int side, Vector3 position, float yaw)
        {
            var go = Track(new GameObject(name));
            go.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            go.AddComponent<Damageable>().Initialize(name, Damageable.DefaultMaxHealth);
            var member = go.AddComponent<SideMember>();
            member.Side = new Side(side);
            return member;
        }

        private void BeginMode(SideMember localPlayer, params SideMember[] others)
        {
            _runner = Track(new GameObject("Runner")).AddComponent<RecordingModeRunner>();
            _runner.Factory = () => TeamDeathmatchRunner.CreateSession(3, 4, 60f, 2, 1f);
            var members = new List<SideMember> { localPlayer };
            members.AddRange(others);
            _runner.Begin(_runner.Factory(), members, Array.Empty<SideSpawnPoint>(), localPlayer);
        }

        private GameObject Track(GameObject go)
        {
            _objects.Add(go);
            return go;
        }
    }
}
