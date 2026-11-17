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
    // Edit Mode では LateUpdate が走らないので、LateUpdate が毎フレームする記録を RecordKillCam へ時刻つきで、
    // 切り替えを Step へ、経過を Advance へ直接渡す。
    public sealed class PlayerKillCamTests
    {
        private static readonly Vector3 _playerPosition = new Vector3(0f, 1f, 0f);
        private static readonly Vector3 _killerStart = new Vector3(10f, 0f, 0f);

        private readonly List<Object> _objects = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            GameSignals.ResetAll();
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
        public void ReplaysTheKillerThenSwitchesToTheKillerView()
        {
            var deathCam = Player(out var rig);
            var camera = rig.Camera;
            var killer = Killer();
            deathCam.Mode = DeathCamera.KillCam;

            deathCam.Step(DeathCamera.KillCam, true, false);
            RecordWalk(deathCam, killer, 20);
            Assert.That(deathCam.KillCamSubjectCount, Is.EqualTo(1), "only the other Damageable, not the player");

            Die(deathCam, killer);
            LogAssert.Expect(LogType.Log, new Regex(@"^\[DeathCam\] started on Player \(KillCam\)"));
            LogAssert.Expect(LogType.Log, "[KillCam] replaying Killer for 2.0s at 1.00x on Player");
            Assert.That(deathCam.Step(DeathCamera.KillCam, false, false), Is.EqualTo(DeathCamTransition.Started));
            Assert.That(deathCam.IsReplayingKillCam, Is.True);
            AssertOverShoulder(rig, camera, EyeAt(killer, 0));

            deathCam.Advance(0, Vector2.zero, Vector2.zero, 1f);
            Assert.That(deathCam.IsReplayingKillCam, Is.True);
            AssertOverShoulder(rig, camera, EyeAt(killer, 10));

            LogAssert.Expect(LogType.Log, "[KillCam] finished replaying Killer on Player; switching to the killer view");
            LogAssert.Expect(LogType.Log, "[DeathCam] watching killer Killer from Player");
            deathCam.Advance(0, Vector2.zero, Vector2.zero, 1.5f);

            Assert.That(deathCam.IsReplayingKillCam, Is.False);
            Assert.That(deathCam.IsActive, Is.True, "keeps watching the killer until the respawn");
            var toKiller = (PlayerDeathCamera.EyeOf(killer) - camera.transform.position).normalized;
            Assert.That(Vector3.Dot(camera.transform.forward, toKiller), Is.GreaterThan(0.999f), "faces the killer as Killer does");
        }

        [Test]
        public void AKillerWithoutARecordSwitchesToTheKillerViewAtOnce()
        {
            var deathCam = Player(out var rig);
            var camera = rig.Camera;
            var killer = Track(new GameObject("Turret"));
            killer.transform.position = _killerStart;
            deathCam.Mode = DeathCamera.KillCam;

            deathCam.Step(DeathCamera.KillCam, true, false);
            deathCam.RecordKillCam(0d);
            deathCam.RecordKillCam(1d);
            Die(deathCam, killer);

            LogAssert.Expect(LogType.Log, "[KillCam] no record of Turret for Player; switching to the killer view");
            deathCam.Step(DeathCamera.KillCam, false, false);
            Assert.That(deathCam.IsReplayingKillCam, Is.False);

            LogAssert.Expect(LogType.Log, "[DeathCam] watching killer Turret from Player");
            deathCam.Advance(0, Vector2.zero, Vector2.zero, 0.1f);
            var toKiller = (PlayerDeathCamera.EyeOf(killer) - camera.transform.position).normalized;
            Assert.That(Vector3.Dot(camera.transform.forward, toKiller), Is.GreaterThan(0.999f));
        }

        [Test]
        public void WithoutAKillerSkipsTheReplay()
        {
            var deathCam = Player(out _);
            deathCam.Mode = DeathCamera.KillCam;

            deathCam.Step(DeathCamera.KillCam, true, false);
            deathCam.GetComponent<Damageable>().Kill();

            LogAssert.Expect(LogType.Log, "[KillCam] no killer for Player; skipping the replay");
            deathCam.Step(DeathCamera.KillCam, false, false);

            LogAssert.Expect(LogType.Log, "[DeathCam] no killer for Player; looking down at the death spot");
            deathCam.Advance(0, Vector2.zero, Vector2.zero, 0.1f);
        }

        [Test]
        public void RespawningDuringTheReplayStopsItAndRestoresTheView()
        {
            var deathCam = Player(out var rig);
            var killer = Killer();
            deathCam.Mode = DeathCamera.KillCam;

            deathCam.Step(DeathCamera.KillCam, true, false);
            RecordWalk(deathCam, killer, 20);
            Die(deathCam, killer);
            deathCam.Step(DeathCamera.KillCam, false, false);
            deathCam.Advance(0, Vector2.zero, Vector2.zero, 0.5f);

            deathCam.GetComponent<Damageable>().ResetHealth();
            LogAssert.Expect(LogType.Log, "[KillCam] stopped replaying Killer on Player (Respawned)");
            Assert.That(deathCam.Step(DeathCamera.KillCam, true, false), Is.EqualTo(DeathCamTransition.Respawned));

            Assert.That(deathCam.IsReplayingKillCam, Is.False);
            Assert.That(rig.enabled, Is.True);
            Assert.That(GameSignals.KillCam.Value, Is.Null, "the stopped replay leaves the signal");
        }

        [Test]
        public void TheReplayIsPublishedForTheUiUntilItEnds()
        {
            var deathCam = Player(out _);
            var killer = Killer();
            deathCam.Mode = DeathCamera.KillCam;
            deathCam.KillCamSpeed = 2f;
            var seen = new List<KillCamStatus?>();
            GameSignals.KillCam.Changed += status => seen.Add(status);

            deathCam.Step(DeathCamera.KillCam, true, false);
            RecordWalk(deathCam, killer, 20);
            Die(deathCam, killer);
            Assert.That(GameSignals.KillCam.Value, Is.Null, "nothing is published before the replay starts");

            deathCam.Step(DeathCamera.KillCam, false, false);
            var started = GameSignals.KillCam.Value;
            Assert.That(started.HasValue, Is.True);
            Assert.That(started.Value.KillerName, Is.EqualTo("Killer"));
            Assert.That(started.Value.WeaponId, Is.EqualTo("rifle"));
            Assert.That(started.Value.DurationSeconds, Is.EqualTo(2f).Within(1e-4f));
            Assert.That(started.Value.Speed, Is.EqualTo(2f));
            Assert.That(started.Value.Progress, Is.EqualTo(0f));
            Assert.That(started.Value.RemainingSeconds, Is.EqualTo(1f).Within(1e-4f));

            deathCam.Advance(0, Vector2.zero, Vector2.zero, 0.25f);
            Assert.That(GameSignals.KillCam.Value.Value.ElapsedSeconds, Is.EqualTo(0.5f).Within(1e-4f), "twice as fast");
            Assert.That(GameSignals.KillCam.Value.Value.RemainingSeconds, Is.EqualTo(0.75f).Within(1e-4f));

            deathCam.Advance(0, Vector2.zero, Vector2.zero, 1f);
            Assert.That(deathCam.IsReplayingKillCam, Is.False);
            Assert.That(GameSignals.KillCam.Value, Is.Null, "the finished replay leaves the signal");
            Assert.That(seen.Count, Is.EqualTo(4), "start, one step, the end at full progress, then cleared");
            Assert.That(seen[2].Value.Progress, Is.EqualTo(1f));
            Assert.That(seen[3], Is.Null);
        }

        [Test]
        public void AKillerWithoutARecordPublishesNothing()
        {
            var deathCam = Player(out _);
            var killer = Track(new GameObject("Turret"));
            deathCam.Mode = DeathCamera.KillCam;

            deathCam.Step(DeathCamera.KillCam, true, false);
            deathCam.RecordKillCam(0d);
            Die(deathCam, killer);
            LogAssert.Expect(LogType.Log, "[KillCam] no record of Turret for Player; switching to the killer view");
            deathCam.Step(DeathCamera.KillCam, false, false);

            Assert.That(GameSignals.KillCam.Value, Is.Null);
        }

        [Test]
        public void TheConfiguredSpeedPlaysTheReplayFaster()
        {
            var deathCam = Player(out _);
            var killer = Killer();
            deathCam.Mode = DeathCamera.KillCam;
            deathCam.KillCamSpeed = 2f;

            deathCam.Step(DeathCamera.KillCam, true, false);
            RecordWalk(deathCam, killer, 20);
            Die(deathCam, killer);

            LogAssert.Expect(LogType.Log, "[KillCam] replaying Killer for 2.0s at 2.00x on Player");
            deathCam.Step(DeathCamera.KillCam, false, false);

            deathCam.Advance(0, Vector2.zero, Vector2.zero, 0.5f);
            Assert.That(deathCam.IsReplayingKillCam, Is.True);
            Assert.That(GameSignals.KillCam.Value.Value.Progress, Is.EqualTo(0.5f).Within(1e-4f), "twice as fast");

            LogAssert.Expect(LogType.Log, "[KillCam] finished replaying Killer on Player; switching to the killer view");
            deathCam.Advance(0, Vector2.zero, Vector2.zero, 0.5f);
            Assert.That(deathCam.IsReplayingKillCam, Is.False, "two seconds at 2x take one second");
        }

        [Test]
        public void AnInvalidSpeedReplaysAtNormalSpeedWithAWarning()
        {
            var deathCam = Player(out _);
            var killer = Killer();
            deathCam.Mode = DeathCamera.KillCam;
            deathCam.KillCamSpeed = 0f;

            deathCam.Step(DeathCamera.KillCam, true, false);
            RecordWalk(deathCam, killer, 20);
            Die(deathCam, killer);

            LogAssert.Expect(LogType.Warning, "[KillCam] Player has playback speed 0; replaying at 1.00x");
            LogAssert.Expect(LogType.Log, "[KillCam] replaying Killer for 2.0s at 1.00x on Player");
            deathCam.Step(DeathCamera.KillCam, false, false);
        }

        [Test]
        public void KillCamSecondsLimitsTheReplay()
        {
            var deathCam = Player(out _);
            var killer = Killer();
            deathCam.Mode = DeathCamera.KillCam;
            deathCam.KillCamSeconds = 1f;

            deathCam.Step(DeathCamera.KillCam, true, false);
            RecordWalk(deathCam, killer, 20);
            Die(deathCam, killer);

            LogAssert.Expect(LogType.Log, "[KillCam] replaying Killer for 1.0s at 1.00x on Player");
            deathCam.Step(DeathCamera.KillCam, false, false);
        }

        private static void RecordWalk(PlayerDeathCamera deathCam, GameObject killer, int steps)
        {
            for (var i = 0; i <= steps; i++)
            {
                killer.transform.position = _killerStart + new Vector3(0f, 0f, i * 0.5f);
                deathCam.RecordKillCam(i / 10.0);
            }
        }

        private static Vector3 EyeAt(GameObject killer, int step)
        {
            var offset = PlayerDeathCamera.EyeOf(killer) - killer.transform.position;
            return _killerStart + new Vector3(0f, 0f, step * 0.5f) + offset;
        }

        private static void AssertOverShoulder(ViewRig rig, Camera camera, Vector3 eye)
        {
            var expected = eye + (Quaternion.Euler(0f, 90f, 0f) * rig.ThirdPersonOffset);
            Assert.That(Vector3.Distance(camera.transform.position, expected), Is.LessThan(1e-3f), "over the killer's shoulder at that moment");
            Assert.That(Vector3.Dot(camera.transform.forward, Vector3.right), Is.GreaterThan(0.999f), "faces where the killer faced");
        }

        private static void Die(PlayerDeathCamera deathCam, GameObject killer)
        {
            using (CombatEvents.Attribute(killer, "rifle"))
            {
                deathCam.GetComponent<Damageable>().Kill();
            }
        }

        private GameObject Killer()
        {
            var killer = Track(new GameObject("Killer"));
            killer.transform.SetPositionAndRotation(_killerStart, Quaternion.Euler(0f, 90f, 0f));
            killer.AddComponent<Damageable>().Initialize("Killer", Damageable.DefaultMaxHealth);
            return killer;
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

        private GameObject Track(GameObject go)
        {
            _objects.Add(go);
            return go;
        }
    }
}
