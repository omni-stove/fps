using System;
using System.Collections.Generic;
using System.IO;
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
    /// <summary>
    /// Edit Mode では Start/Update/OnTrigger* が走らず、シーンも読み直せない。
    /// そこで 1 つのシーンにあたる物を <see cref="CampaignScene"/> にまとめて組み、読み直しは差し替えた処理で数え、
    /// 読み直した後のシーンは新しい <see cref="CampaignScene"/> を組んで <see cref="CampaignRunner.Begin"/> を呼んで再現する。
    /// </summary>
    public sealed class CampaignRunnerTests
    {
        private const string MissionName = "CampaignTestMission";
        private const string CheckpointId = "cp1";

        private static readonly Vector3 _checkpointPosition = new Vector3(5f, 0f, 10f);
        private static readonly Vector3 _spawnPosition = new Vector3(0f, 1f, 0f);

        private readonly List<CampaignScene> _scenes = new List<CampaignScene>();
        private MissionDefinition _definition;
        private string _root;
        private FileSaveStorage _storage;
        private int _reloads;

        [SetUp]
        public void SetUp()
        {
            _definition = ScriptableObject.CreateInstance<MissionDefinition>();
            _definition.name = MissionName;
            _definition.SetObjectives(new[]
            {
                new ObjectiveDefinition("reach", ""),
                new ObjectiveDefinition("kill", "", 2, false, "reach"),
                new ObjectiveDefinition("extract", "", 1, false, "kill"),
            });

            _root = Path.Combine(Path.GetTempPath(), "fps-campaign-tests-" + Guid.NewGuid().ToString("N"));
            _storage = new FileSaveStorage(_root);
            _reloads = 0;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var scene in _scenes)
            {
                scene.Dispose();
            }

            _scenes.Clear();
            Object.DestroyImmediate(_definition);

            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, true);
            }
        }

        [Test]
        public void SavesAtACheckpointAndRestoresAfterThePlayerDies()
        {
            var first = BuildScene();
            ExpectCampaign($"started mission={MissionName}");
            ExpectCampaign($"no save; starting mission={MissionName} from the beginning");
            first.Begin(_storage, () => _reloads++);

            first.Missions.Progress("reach");
            first.Player.TakeDamage(30);
            LogAssert.Expect(LogType.Log, "[Combat] fire ammo=29");
            Assert.That(first.Weapon.Fire(), Is.True);

            ExpectCampaign($"saved mission={MissionName} checkpoint={CheckpointId} health=70 ammo=29/90");
            Assert.That(first.Checkpoint.HandleEnter(first.PlayerCollider), Is.True);
            Assert.That(first.Checkpoint.IsReached, Is.True);
            Assert.That(first.Campaign.LastCheckpointId, Is.EqualTo(CheckpointId));
            Assert.That(_storage.TryRead(CampaignRunner.SaveSlot, out _), Is.True);

            // 同じチェックポイントには 1 度しかセーブしない。後の進み具合はセーブに入らない。
            first.Missions.Progress("kill");
            Assert.That(first.Checkpoint.HandleEnter(first.PlayerCollider), Is.False);

            LogAssert.Expect(LogType.Log, "[Combat] target died id=Player");
            ExpectCampaign($"player died; reloading in 3s from checkpoint={CheckpointId}");
            first.Player.Kill();
            Assert.That(first.Campaign.IsReloadPending, Is.True);
            Assert.That(first.Campaign.SaveAt("other"), Is.False, "no saving while waiting for the reload");

            first.Campaign.Tick(2.9f);
            Assert.That(_reloads, Is.Zero);

            ExpectCampaign("reloading scene");
            first.Campaign.Tick(0.2f);
            Assert.That(_reloads, Is.EqualTo(1));
            Assert.That(first.Campaign.IsReloadPending, Is.False);
            first.Campaign.Tick(10f);
            Assert.That(_reloads, Is.EqualTo(1), "reloads only once per death");

            // 読み直したシーン。
            first.Dispose();
            var second = BuildScene();
            ExpectCampaign($"started mission={MissionName}");
            ExpectCampaign($"restored mission={MissionName} checkpoint={CheckpointId} health=70 ammo=29/90 state=Active");
            second.Begin(_storage, () => _reloads++);

            Assert.That(Vector3.Distance(second.Player.transform.position, second.Checkpoint.RespawnPosition), Is.LessThan(1e-4f));
            Assert.That(second.Checkpoint.RespawnPosition, Is.EqualTo(_checkpointPosition + new Vector3(0f, GameBootstrap.PlayerHeight / 2f, 0f)));
            Assert.That(second.Controller.enabled, Is.True, "the CharacterController is enabled again after the move");
            Assert.That(second.Player.Health.Current, Is.EqualTo(70));
            Assert.That(second.Weapon.Weapon.AmmoInMagazine, Is.EqualTo(29));
            Assert.That(second.Weapon.Weapon.ReserveAmmo, Is.EqualTo(90));
            var mission = second.Missions.Mission;
            Assert.That(mission.GetState("reach"), Is.EqualTo(ObjectiveState.Completed));
            Assert.That(mission.GetState("kill"), Is.EqualTo(ObjectiveState.Active));
            Assert.That(mission.GetCount("kill"), Is.Zero, "progress after the checkpoint is not saved");
            Assert.That(second.Checkpoint.IsReached, Is.True, "the checkpoint we restarted from does not save again");
            Assert.That(second.Campaign.LastCheckpointId, Is.EqualTo(CheckpointId));

            // 達成したらセーブを消す。
            second.Missions.Progress("kill", 2);
            ExpectCampaign($"mission={MissionName} completed");
            ExpectCampaign($"deleted save slot={CampaignRunner.SaveSlot}");
            second.Missions.Progress("extract");

            Assert.That(mission.State, Is.EqualTo(MissionState.Completed));
            Assert.That(_storage.TryRead(CampaignRunner.SaveSlot, out _), Is.False);
            ExpectCampaign(new Regex(@"^\[Campaign\] skipped save checkpoint=late reason=mission Completed$"));
            Assert.That(second.Campaign.SaveAt("late"), Is.False);
        }

        [Test]
        public void NoticeShowsTheSaveThenCountsDownToTheRestart()
        {
            var scene = BuildScene();
            BeginQuietly(scene);
            var notice = scene.Campaign.Notice;
            Assert.That(notice.Text, Is.EqualTo(HudFormat.NoNotice));

            ExpectCampaign(new Regex($@"^\[Campaign\] saved mission={MissionName} checkpoint={CheckpointId} "));
            Assert.That(scene.Checkpoint.HandleEnter(scene.PlayerCollider), Is.True);
            Assert.That(notice.Text, Is.EqualTo(HudFormat.CheckpointSaved));

            scene.Campaign.Tick(CampaignRunner.SavedNoticeSeconds - 0.1f);
            Assert.That(notice.Text, Is.EqualTo(HudFormat.CheckpointSaved));
            scene.Campaign.Tick(0.1f);
            Assert.That(notice.Text, Is.EqualTo(HudFormat.NoNotice), "the saved notice is temporary");

            LogAssert.Expect(LogType.Log, "[Combat] target died id=Player");
            ExpectCampaign($"player died; reloading in 3s from checkpoint={CheckpointId}");
            scene.Player.Kill();
            Assert.That(notice.Text, Is.EqualTo(HudFormat.CheckpointRestart(3)));

            scene.Campaign.Tick(0.5f);
            Assert.That(notice.Text, Is.EqualTo(HudFormat.CheckpointRestart(3)), "2.5s left still reads 3");
            scene.Campaign.Tick(1f);
            Assert.That(notice.Text, Is.EqualTo(HudFormat.CheckpointRestart(2)));
            scene.Campaign.Tick(1f);
            Assert.That(notice.Text, Is.EqualTo(HudFormat.CheckpointRestart(1)));

            ExpectCampaign("reloading scene");
            scene.Campaign.Tick(1f);
            Assert.That(_reloads, Is.EqualTo(1));
            Assert.That(notice.Text, Is.EqualTo(HudFormat.NoNotice));
        }

        [Test]
        public void TryBeginWithRejectsANullWorld()
        {
            var scene = BuildScene();

            Assert.Throws<ArgumentNullException>(() => scene.Campaign.TryBeginWith(null));
            Assert.That(scene.Campaign.IsStarted, Is.False);
        }

        [Test]
        public void DeathWithoutACheckpointReloadsFromTheStart()
        {
            var scene = BuildScene();
            scene.Campaign.ReloadDelaySeconds = 0f;
            BeginQuietly(scene);

            LogAssert.Expect(LogType.Log, "[Combat] target died id=Player");
            ExpectCampaign("player died; reloading in 0s from checkpoint=(start)");
            scene.Player.Kill();

            ExpectCampaign("reloading scene");
            scene.Campaign.Tick(0f);
            Assert.That(_reloads, Is.EqualTo(1));
            Assert.That(_storage.TryRead(CampaignRunner.SaveSlot, out _), Is.False);
        }

        [Test]
        public void IgnoresASaveOfAnotherMission()
        {
            var other = new CampaignSave("OtherMission", CheckpointId, 10, 1, 2, new[]
            {
                new ObjectiveProgress("reach", ObjectiveState.Completed, 1),
                new ObjectiveProgress("kill", ObjectiveState.Active, 0),
                new ObjectiveProgress("extract", ObjectiveState.Locked, 0),
            });
            _storage.Write(CampaignRunner.SaveSlot, other.ToJson());
            var scene = BuildScene();

            ExpectCampaign($"started mission={MissionName}");
            LogAssert.Expect(
                LogType.Warning,
                new Regex($@"^\[Campaign\] ignored the save of mission=OtherMission; starting mission={MissionName} from the beginning$"));
            scene.Begin(_storage, () => _reloads++);

            AssertStartedFresh(scene);
        }

        [Test]
        public void IgnoresABrokenSave()
        {
            _storage.Write(CampaignRunner.SaveSlot, "{\"version\":1,");
            var scene = BuildScene();

            ExpectCampaign($"started mission={MissionName}");
            LogAssert.Expect(LogType.Warning, new Regex(@"^\[Campaign\] ignored a broken save; starting mission=.* from the beginning: "));
            scene.Begin(_storage, () => _reloads++);

            AssertStartedFresh(scene);
        }

        [Test]
        public void IgnoresASaveWhoseObjectivesNoLongerMatch()
        {
            var stale = new CampaignSave(MissionName, CheckpointId, 10, 1, 2, new[]
            {
                new ObjectiveProgress("removed", ObjectiveState.Completed, 1),
            });
            _storage.Write(CampaignRunner.SaveSlot, stale.ToJson());
            var scene = BuildScene();

            ExpectCampaign($"started mission={MissionName}");
            LogAssert.Expect(LogType.Warning, new Regex(@"^\[Campaign\] ignored a save that does not match mission="));
            scene.Begin(_storage, () => _reloads++);

            AssertStartedFresh(scene);
        }

        [Test]
        public void RestoresTheRestWhenTheCheckpointIsMissing()
        {
            var save = new CampaignSave(MissionName, "gone", 40, 3, 4, new[]
            {
                new ObjectiveProgress("reach", ObjectiveState.Completed, 1),
                new ObjectiveProgress("kill", ObjectiveState.Active, 1),
                new ObjectiveProgress("extract", ObjectiveState.Locked, 0),
            });
            _storage.Write(CampaignRunner.SaveSlot, save.ToJson());
            var scene = BuildScene();

            ExpectCampaign($"started mission={MissionName}");
            LogAssert.Expect(LogType.Warning, "[Campaign] checkpoint=gone is not in the scene; the player stays at the spawn point");
            ExpectCampaign($"restored mission={MissionName} checkpoint=gone health=40 ammo=3/4 state=Active");
            scene.Begin(_storage, () => _reloads++);

            Assert.That(scene.Player.transform.position, Is.EqualTo(_spawnPosition));
            Assert.That(scene.Player.Health.Current, Is.EqualTo(40));
            Assert.That(scene.Missions.Mission.GetCount("kill"), Is.EqualTo(1));
            Assert.That(scene.Checkpoint.IsReached, Is.False);
        }

        [Test]
        public void AFailedWriteLeavesTheCheckpointOpenForRetry()
        {
            var storage = new FlakyStorage();
            var scene = BuildScene();
            ExpectCampaign($"started mission={MissionName}");
            ExpectCampaign($"no save; starting mission={MissionName} from the beginning");
            scene.Begin(storage, () => _reloads++);

            storage.FailWrites = true;
            LogAssert.Expect(LogType.Error, new Regex($@"^\[Campaign\] failed to save checkpoint={CheckpointId}: disk full$"));
            Assert.That(scene.Checkpoint.HandleEnter(scene.PlayerCollider), Is.False);
            Assert.That(scene.Checkpoint.IsReached, Is.False);
            Assert.That(scene.Campaign.LastCheckpointId, Is.Empty);

            storage.FailWrites = false;
            ExpectCampaign($"saved mission={MissionName} checkpoint={CheckpointId} health=100 ammo=30/90");
            Assert.That(scene.Checkpoint.HandleEnter(scene.PlayerCollider), Is.True);
            Assert.That(storage.Text, Does.Contain(CheckpointId));
        }

        [Test]
        public void CheckpointIgnoresOtherColliders()
        {
            var scene = BuildScene();
            BeginQuietly(scene);
            var stranger = Track(scene, new GameObject("Stranger")).AddComponent<SphereCollider>();

            Assert.That(scene.Checkpoint.HandleEnter(stranger), Is.False);
            Assert.That(scene.Checkpoint.HandleEnter(null), Is.False);
            Assert.That(_storage.TryRead(CampaignRunner.SaveSlot, out _), Is.False);
        }

        [Test]
        public void SaveBeforeBeginIsRejected()
        {
            var scene = BuildScene();
            LogAssert.Expect(LogType.Error, new Regex(@"^\[Campaign\] .*cp1"));

            Assert.That(scene.Campaign.SaveAt(CheckpointId), Is.False);
        }

        [Test]
        public void BeginValidatesItsArguments()
        {
            var scene = BuildScene();
            Action reload = () => _reloads++;

            Assert.Throws<ArgumentNullException>(() => scene.Campaign.Begin(null, scene.Player, scene.Weapon, _storage, reload));
            Assert.Throws<ArgumentNullException>(() => scene.Campaign.Begin(scene.Missions, null, scene.Weapon, _storage, reload));
            Assert.Throws<ArgumentNullException>(() => scene.Campaign.Begin(scene.Missions, scene.Player, null, _storage, reload));
            Assert.Throws<ArgumentNullException>(() => scene.Campaign.Begin(scene.Missions, scene.Player, scene.Weapon, null, reload));
            Assert.Throws<ArgumentNullException>(() => scene.Campaign.Begin(scene.Missions, scene.Player, scene.Weapon, _storage, null));
            Assert.That(scene.Campaign.IsStarted, Is.False);

            BeginQuietly(scene);
            Assert.Throws<InvalidOperationException>(() => scene.Campaign.Begin(scene.Missions, scene.Player, scene.Weapon, _storage, reload));
            Assert.Throws<ArgumentOutOfRangeException>(() => scene.Campaign.Tick(-1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => scene.Campaign.ReloadDelaySeconds = float.NaN);
        }

        private void AssertStartedFresh(CampaignScene scene)
        {
            Assert.That(scene.Campaign.IsStarted, Is.True);
            Assert.That(scene.Player.transform.position, Is.EqualTo(_spawnPosition));
            Assert.That(scene.Player.Health.Current, Is.EqualTo(scene.Player.MaxHealth));
            Assert.That(scene.Weapon.Weapon.AmmoInMagazine, Is.EqualTo(scene.Rifle.MagazineSize));
            Assert.That(scene.Weapon.Weapon.ReserveAmmo, Is.EqualTo(scene.Rifle.ReserveAmmo));
            Assert.That(scene.Missions.Mission.GetState("reach"), Is.EqualTo(ObjectiveState.Active));
            Assert.That(scene.Checkpoint.IsReached, Is.False);
            Assert.That(scene.Campaign.LastCheckpointId, Is.Empty);
        }

        private void BeginQuietly(CampaignScene scene)
        {
            ExpectCampaign($"started mission={MissionName}");
            ExpectCampaign($"no save; starting mission={MissionName} from the beginning");
            scene.Begin(_storage, () => _reloads++);
        }

        private static void ExpectCampaign(string rest)
        {
            LogAssert.Expect(LogType.Log, CampaignRunner.LogTag + " " + rest);
        }

        private static void ExpectCampaign(Regex pattern)
        {
            LogAssert.Expect(LogType.Log, pattern);
        }

        private CampaignScene BuildScene()
        {
            var scene = new CampaignScene(_definition, _checkpointPosition, _spawnPosition);
            _scenes.Add(scene);
            return scene;
        }

        private static T Track<T>(CampaignScene scene, T created)
            where T : Object
        {
            scene.Created.Add(created);
            return created;
        }

        /// <summary>読み直し 1 回分のシーン。GameBootstrap の代わりにプレイヤーを最小の構成で組む。</summary>
        private sealed class CampaignScene : IDisposable
        {
            public List<Object> Created { get; } = new List<Object>();
            private readonly InputAction _attack = new InputAction("Attack", InputActionType.Button);
            private readonly InputAction _reload = new InputAction("Reload", InputActionType.Button);
            private bool _disposed;

            public CampaignScene(MissionDefinition definition, Vector3 checkpointPosition, Vector3 spawnPosition)
            {
                Missions = Add(new GameObject("MissionRunner")).AddComponent<MissionRunner>();
                Missions.Definition = definition;

                var player = Add(new GameObject(GameBootstrap.PlayerName));
                player.tag = GameBootstrap.PlayerTag;
                player.transform.position = spawnPosition;
                Controller = player.AddComponent<CharacterController>();
                Player = player.AddComponent<Damageable>();
                Player.Initialize(GameBootstrap.PlayerName, GameBootstrap.PlayerMaxHealth);

                var camera = new GameObject("Camera", typeof(Camera));
                camera.transform.SetParent(player.transform, false);
                Rifle = ScriptableObject.CreateInstance<WeaponDefinition>();
                Rifle.AssignIdIfEmpty();
                Created.Add(Rifle);
                Weapon = camera.AddComponent<WeaponController>();
                Weapon.Initialize(camera.transform, _attack, _reload, Rifle);

                var checkpoint = Add(new GameObject("Checkpoint"));
                checkpoint.transform.position = checkpointPosition;
                checkpoint.AddComponent<BoxCollider>().isTrigger = true;
                Checkpoint = checkpoint.AddComponent<CheckpointTrigger>();
                Checkpoint.CheckpointId = CheckpointId;

                Campaign = Add(new GameObject("CampaignRunner")).AddComponent<CampaignRunner>();
                Checkpoint.Runner = Campaign;
            }

            public MissionRunner Missions { get; }
            public Damageable Player { get; }
            public CharacterController Controller { get; }
            public Collider PlayerCollider => Controller;
            public WeaponController Weapon { get; }

            /// <summary>プレイヤーの武器の定義 (既定値の rifle)。</summary>
            public WeaponDefinition Rifle { get; }

            public CheckpointTrigger Checkpoint { get; }
            public CampaignRunner Campaign { get; }

            public void Begin(ISaveStorage storage, Action reload)
            {
                Campaign.Begin(Missions, Player, Weapon, storage, reload);
            }

            public void Dispose()
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;

                // Edit Mode では OnDestroy が走らないので、購読はここで外す。
                if (Campaign != null)
                {
                    Campaign.Stop();
                }

                for (var i = Created.Count - 1; i >= 0; i--)
                {
                    if (Created[i] != null)
                    {
                        Object.DestroyImmediate(Created[i]);
                    }
                }

                Created.Clear();
                _attack.Dispose();
                _reload.Dispose();
            }

            private GameObject Add(GameObject created)
            {
                Created.Add(created);
                return created;
            }
        }

        /// <summary>書き込みを失敗させられるメモリ上のストレージ。</summary>
        private sealed class FlakyStorage : ISaveStorage
        {
            public bool FailWrites { get; set; }
            public string Text { get; private set; }

            public bool TryRead(string slot, out string text)
            {
                text = Text;
                return text != null;
            }

            public void Write(string slot, string text)
            {
                if (FailWrites)
                {
                    throw new IOException("disk full");
                }

                Text = text;
            }

            public void Delete(string slot)
            {
                Text = null;
            }
        }
    }
}
