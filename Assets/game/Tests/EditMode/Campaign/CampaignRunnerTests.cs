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

        // 古い形式のセーブは rifle の残弾として読むので、テストの rifle にもその Id を使う。
        private const string RifleId = CampaignSave.LegacyWeaponId;
        private const string HandgunId = "00000000-0000-4000-8000-0000000000a2";

        private static readonly Vector3 _checkpointPosition = new Vector3(5f, 0f, 10f);
        private static readonly Vector3 _spawnPosition = new Vector3(0f, 1f, 0f);

        private readonly List<CampaignScene> _scenes = new List<CampaignScene>();
        private readonly List<Object> _assets = new List<Object>();
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
            foreach (var asset in _assets)
            {
                Object.DestroyImmediate(asset);
            }

            _assets.Clear();
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
            first.Weapon.Tick(1f);
            LogAssert.Expect(LogType.Log, "[Combat] fire ammo=28");
            Assert.That(first.Weapon.Fire(), Is.True);

            // 持ち替えた武器と、武器ごとの残弾がセーブされる。
            Assert.That(first.Weapon.SelectNext(), Is.True);
            LogAssert.Expect(LogType.Log, "[Combat] fire ammo=29");
            Assert.That(first.Weapon.Fire(), Is.True);

            ExpectCampaign($"saved mission={MissionName} checkpoint={CheckpointId} health=70 equipped={HandgunId} ammo={RifleId}:28/90,{HandgunId}:29/90");
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
            ExpectCampaign($"restored mission={MissionName} checkpoint={CheckpointId} health=70 equipped={HandgunId} ammo={RifleId}:28/90,{HandgunId}:29/90 state=Active");
            second.Begin(_storage, () => _reloads++);

            Assert.That(Vector3.Distance(second.Player.transform.position, second.Checkpoint.RespawnPosition), Is.LessThan(1e-4f));
            Assert.That(second.Checkpoint.RespawnPosition, Is.EqualTo(_checkpointPosition + new Vector3(0f, GameBootstrap.PlayerHeight / 2f, 0f)));
            Assert.That(second.Controller.enabled, Is.True, "the CharacterController is enabled again after the move");
            Assert.That(second.Player.Health.Current, Is.EqualTo(70));
            Assert.That(second.Weapon.CurrentIndex, Is.EqualTo(1), "the handgun is held again");
            Assert.That(second.Weapon.Weapon.AmmoInMagazine, Is.EqualTo(29));
            Assert.That(second.Weapon.Weapon.ReserveAmmo, Is.EqualTo(90));
            Assert.That(second.Weapon.Loadout.Items[0].AmmoInMagazine, Is.EqualTo(28), "the rifle keeps its own ammo");
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
            var other = new CampaignSave("OtherMission", CheckpointId, 10, RifleAmmo(1, 2), RifleId, new[]
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
            var stale = new CampaignSave(MissionName, CheckpointId, 10, RifleAmmo(1, 2), RifleId, new[]
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
            var save = new CampaignSave(MissionName, "gone", 40, RifleAmmo(3, 4), RifleId, new[]
            {
                new ObjectiveProgress("reach", ObjectiveState.Completed, 1),
                new ObjectiveProgress("kill", ObjectiveState.Active, 1),
                new ObjectiveProgress("extract", ObjectiveState.Locked, 0),
            });
            _storage.Write(CampaignRunner.SaveSlot, save.ToJson());
            var scene = BuildScene();

            ExpectCampaign($"started mission={MissionName}");
            LogAssert.Expect(LogType.Warning, "[Campaign] checkpoint=gone is not in the scene; the player stays at the spawn point");
            ExpectCampaign($"restored mission={MissionName} checkpoint=gone health=40 equipped={RifleId} ammo={RifleId}:3/4,{HandgunId}:30/90 state=Active");
            scene.Begin(_storage, () => _reloads++);

            Assert.That(scene.Player.transform.position, Is.EqualTo(_spawnPosition));
            Assert.That(scene.Player.Health.Current, Is.EqualTo(40));
            Assert.That(scene.Missions.Mission.GetCount("kill"), Is.EqualTo(1));
            Assert.That(scene.Checkpoint.IsReached, Is.False);
        }

        [Test]
        public void RestoresALegacySingleWeaponSaveAsTheRiflesAmmo()
        {
            _storage.Write(
                CampaignRunner.SaveSlot,
                "{\"version\":1,\"missionId\":\"" + MissionName + "\",\"checkpointId\":\"" + CheckpointId + "\",\"health\":55," +
                "\"ammoInMagazine\":5,\"reserveAmmo\":6,\"objectives\":[" +
                "{\"id\":\"reach\",\"state\":\"completed\",\"count\":1}," +
                "{\"id\":\"kill\",\"state\":\"active\",\"count\":0}," +
                "{\"id\":\"extract\",\"state\":\"locked\",\"count\":0}]}");
            var scene = BuildScene();

            ExpectCampaign($"started mission={MissionName}");
            ExpectCampaign($"restored mission={MissionName} checkpoint={CheckpointId} health=55 equipped={RifleId} ammo={RifleId}:5/6,{HandgunId}:30/90 state=Active");
            scene.Begin(_storage, () => _reloads++);

            Assert.That(scene.Weapon.CurrentIndex, Is.Zero, "the rifle is held");
            Assert.That(scene.Weapon.Weapon.AmmoInMagazine, Is.EqualTo(5));
            Assert.That(scene.Weapon.Weapon.ReserveAmmo, Is.EqualTo(6));
            Assert.That(scene.Weapon.Loadout.Items[1].AmmoInMagazine, Is.EqualTo(scene.Handgun.MagazineSize), "the handgun was not in the old save");

            // 次のセーブは新しい形式で書く。
            scene.Campaign.SaveAt("next");
            Assert.That(_storage.TryRead(CampaignRunner.SaveSlot, out var text), Is.True);
            Assert.That(text, Does.StartWith("{\"version\":" + CampaignSave.CurrentVersion + ","));
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
            ExpectCampaign($"saved mission={MissionName} checkpoint={CheckpointId} health=100 equipped={RifleId} ammo={RifleId}:30/90,{HandgunId}:30/90");
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

        [Test]
        public void TryBeginWithAppliesTheWiredRulesToThePlayer()
        {
            var scene = BuildScene();
            scene.Campaign.RulesDefinition = NewRules("CampaignNormal", 3f, 20f, false, 1.5f);
            scene.Campaign.LaunchArgs = Array.Empty<string>();

            ExpectCampaign(new Regex(@"^\[Campaign\] rules=CampaignNormal difficulty=\(none\) regen=20/s after 3s reserve=x1\.5 infinite=False$"));
            BeginWithWorldQuietly(scene);

            Assert.That(scene.Campaign.Rules, Is.Not.Null);
            Assert.That(scene.Campaign.Rules.RegenPerSecond, Is.EqualTo(20f));
            Assert.That(scene.Weapon.Rules, Is.SameAs(scene.Campaign.Rules));
            Assert.That(scene.Weapon.Loadout.Items[0].ReserveAmmo, Is.EqualTo(135), "90 x 1.5");
            Assert.That(scene.Weapon.Loadout.Items[1].ReserveAmmo, Is.EqualTo(135));

            var regen = scene.Player.GetComponent<HealthRegen>();
            Assert.That(regen, Is.Not.Null, "the player gets a HealthRegen");
            Assert.That(regen.Target, Is.SameAs(scene.Player));
            Assert.That(regen.Rules, Is.SameAs(scene.Campaign.Rules));

            scene.Player.TakeDamage(40);
            Assert.That(regen.Tick(3f), Is.Zero);
            Assert.That(regen.Tick(1f), Is.EqualTo(20));
            Assert.That(scene.Player.Health.Current, Is.EqualTo(80));
        }

        [Test]
        public void TryBeginWithReusesThePlayersHealthRegen()
        {
            var scene = BuildScene();
            var existing = scene.Player.gameObject.AddComponent<HealthRegen>();
            existing.Bind(scene.Player, GameRules.Default);
            scene.Campaign.RulesDefinition = NewRules("CampaignEasy", 0f, 25f, false, 1f);
            scene.Campaign.LaunchArgs = Array.Empty<string>();

            BeginWithWorldQuietly(scene);

            Assert.That(scene.Player.GetComponents<HealthRegen>(), Has.Length.EqualTo(1));
            Assert.That(existing.Rules.RegenPerSecond, Is.EqualTo(25f), "the bootstrap's regen is rebound to the campaign rules");
        }

        [TestCase("easy", "CampaignEasy", 1.5f)]
        [TestCase("normal", "CampaignNormal", 1f)]
        [TestCase("hard", "CampaignHard", 0.6f)]
        public void DifficultyArgumentSelectsTheCampaignRules(string difficulty, string expectedAsset, float multiplier)
        {
            var scene = BuildScene();
            WireDifficulties(scene);
            scene.Campaign.LaunchArgs = new[] { "Unity", "-batchmode", CampaignLaunchOptions.DifficultyFlag, difficulty };

            ExpectCampaign(new Regex($@"^\[Campaign\] rules={expectedAsset} difficulty={difficulty} "));
            BeginWithWorldQuietly(scene);

            Assert.That(scene.Campaign.Rules.ReserveAmmoMultiplier, Is.EqualTo(multiplier));
            Assert.That(scene.Weapon.Weapon.ReserveAmmo, Is.EqualTo(Weapon.ScaleReserveAmmo(90, multiplier)));
        }

        [Test]
        public void WithoutADifficultyTheDefaultRulesDefinitionIsUsed()
        {
            var scene = BuildScene();
            WireDifficulties(scene);
            scene.Campaign.RulesDefinition = scene.Campaign.HardRules;
            scene.Campaign.LaunchArgs = new[] { "Unity", "-batchmode" };

            ExpectCampaign(new Regex(@"^\[Campaign\] rules=CampaignHard difficulty=\(none\) "));
            BeginWithWorldQuietly(scene);

            Assert.That(scene.Campaign.Rules.ReserveAmmoMultiplier, Is.EqualTo(0.6f));
        }

        [Test]
        public void AMalformedDifficultyIsIgnoredWithAWarning()
        {
            var scene = BuildScene();
            WireDifficulties(scene);
            scene.Campaign.LaunchArgs = new[] { CampaignLaunchOptions.DifficultyFlag, "nightmare" };

            LogAssert.Expect(LogType.Warning, new Regex(@"^\[Campaign\] コマンドライン引数を無視します \(-difficulty must be easy, normal or hard \(got 'nightmare'\)\)"));
            ExpectCampaign(new Regex(@"^\[Campaign\] rules=CampaignNormal difficulty=\(none\) "));
            BeginWithWorldQuietly(scene);

            Assert.That(scene.Campaign.IsStarted, Is.True);
            Assert.That(scene.Campaign.Rules.ReserveAmmoMultiplier, Is.EqualTo(1f));
        }

        [Test]
        public void AnUnwiredDifficultyFallsBackToTheDefaultWithAnError()
        {
            var scene = BuildScene();
            WireDifficulties(scene);
            scene.Campaign.HardRules = null;
            scene.Campaign.LaunchArgs = new[] { CampaignLaunchOptions.DifficultyFlag, "hard" };

            LogAssert.Expect(LogType.Error, new Regex(@"^\[Campaign\] -difficulty hard のルールが 'CampaignRunner' に配線されていない"));
            ExpectCampaign(new Regex(@"^\[Campaign\] rules=CampaignNormal difficulty=hard "));
            BeginWithWorldQuietly(scene);

            Assert.That(scene.Campaign.Rules.ReserveAmmoMultiplier, Is.EqualTo(1f));
        }

        [Test]
        public void WithoutRulesTheDefaultIsUsedWithAWarning()
        {
            var scene = BuildScene();
            scene.Campaign.LaunchArgs = Array.Empty<string>();

            LogAssert.Expect(LogType.Warning, new Regex(@"^\[Campaign\] 'CampaignRunner' に GameRulesDefinition が設定されていないため GameRules.Default を使います"));
            ExpectCampaign(new Regex(@"^\[Campaign\] rules=\(default\) difficulty=\(none\) "));
            BeginWithWorldQuietly(scene);

            Assert.That(scene.Campaign.Rules, Is.SameAs(GameRules.Default));
            Assert.That(scene.Player.GetComponent<HealthRegen>().Rules, Is.SameAs(GameRules.Default));
        }

        [Test]
        public void InvalidRulesRefuseToBegin()
        {
            var scene = BuildScene();
            var broken = NewRules("BrokenRules", 0f, 0f, false, 1f);
            broken.ReserveAmmoMultiplier = -1f;
            scene.Campaign.RulesDefinition = broken;
            scene.Campaign.LaunchArgs = Array.Empty<string>();
            ConfigureForWorld(scene);

            LogAssert.Expect(LogType.Error, new Regex(@"^\[Campaign\] キャンペーンを始められません: game rules 'BrokenRules' are invalid"));
            Assert.That(scene.Campaign.TryBeginWith(scene.World()), Is.False);

            Assert.That(scene.Campaign.IsStarted, Is.False);
            Assert.That(scene.Player.GetComponent<HealthRegen>(), Is.Null, "nothing is applied");
            Assert.That(scene.Weapon.Weapon.ReserveAmmo, Is.EqualTo(90));
        }

        [Test]
        public void RestoringASaveKeepsItsReserveWithoutTheMultiplier()
        {
            var save = new CampaignSave(MissionName, CheckpointId, 60, RifleAmmo(5, 20), RifleId, new[]
            {
                new ObjectiveProgress("reach", ObjectiveState.Completed, 1),
                new ObjectiveProgress("kill", ObjectiveState.Active, 0),
                new ObjectiveProgress("extract", ObjectiveState.Locked, 0),
            });
            _storage.Write(CampaignRunner.SaveSlot, save.ToJson());
            var scene = BuildScene();
            scene.Campaign.RulesDefinition = NewRules("CampaignEasy", 3f, 25f, false, 1.5f);
            scene.Campaign.LaunchArgs = Array.Empty<string>();
            ConfigureForWorld(scene);

            ExpectCampaign($"restored mission={MissionName} checkpoint={CheckpointId} health=60 equipped={RifleId} ammo={RifleId}:5/20,{HandgunId}:30/135 state=Active");
            Assert.That(scene.Campaign.TryBeginWith(scene.World()), Is.True);

            Assert.That(scene.Weapon.Loadout.Items[0].ReserveAmmo, Is.EqualTo(20), "the saved reserve is used as it is");
            Assert.That(scene.Weapon.Loadout.Items[1].ReserveAmmo, Is.EqualTo(135), "a weapon missing from the save starts with the multiplier");
            Assert.That(scene.Player.Health.Current, Is.EqualTo(60));

            // 戻した体力の Health にも回復が付いている。
            var regen = scene.Player.GetComponent<HealthRegen>();
            Assert.That(regen.Regenerator.Health, Is.SameAs(scene.Player.Health));
            Assert.That(regen.Tick(1f), Is.EqualTo(25));

            // セーブし直しても倍率は重ならない。
            ExpectCampaign(new Regex($@"^\[Campaign\] saved mission={MissionName} checkpoint=next .* ammo={RifleId}:5/20,{HandgunId}:30/135$"));
            Assert.That(scene.Campaign.SaveAt("next"), Is.True);
        }

        [Test]
        public void RestoringASaveKeepsInfiniteReserveFromTheRules()
        {
            var save = new CampaignSave(MissionName, CheckpointId, 60, RifleAmmo(0, 0), RifleId, new[]
            {
                new ObjectiveProgress("reach", ObjectiveState.Completed, 1),
                new ObjectiveProgress("kill", ObjectiveState.Active, 0),
                new ObjectiveProgress("extract", ObjectiveState.Locked, 0),
            });
            _storage.Write(CampaignRunner.SaveSlot, save.ToJson());
            var scene = BuildScene();
            scene.Campaign.RulesDefinition = NewRules("Range", 0f, 0f, true, 1f);
            scene.Campaign.LaunchArgs = Array.Empty<string>();
            ConfigureForWorld(scene);

            Assert.That(scene.Campaign.TryBeginWith(scene.World()), Is.True);

            Assert.That(scene.Weapon.Weapon.HasInfiniteReserve, Is.True);
            Assert.That(scene.Weapon.Weapon.ReserveAmmo, Is.Zero);
            Assert.That(scene.Weapon.StartReload(), Is.True, "an empty but infinite reserve can still reload");
        }

        [Test]
        public void BeginWithoutRulesLeavesThePlayerAsItIs()
        {
            var scene = BuildScene();
            BeginQuietly(scene);

            Assert.That(scene.Campaign.Rules, Is.Null);
            Assert.That(scene.Player.GetComponent<HealthRegen>(), Is.Null);
            Assert.That(scene.Weapon.Rules, Is.SameAs(GameRules.Default));
        }

        [Test]
        public void RulesForRejectsAnUnknownDifficulty()
        {
            var scene = BuildScene();

            Assert.Throws<ArgumentOutOfRangeException>(() => scene.Campaign.RulesFor((CampaignDifficulty)99));
        }

        private GameRulesDefinition NewRules(string assetName, float delaySeconds, float perSecond, bool infinite, float multiplier)
        {
            var definition = ScriptableObject.CreateInstance<GameRulesDefinition>();
            definition.name = assetName;
            definition.RegenDelaySeconds = delaySeconds;
            definition.RegenPerSecond = perSecond;
            definition.InfiniteReserve = infinite;
            definition.ReserveAmmoMultiplier = multiplier;
            _assets.Add(definition);
            return definition;
        }

        private void WireDifficulties(CampaignScene scene)
        {
            scene.Campaign.EasyRules = NewRules("CampaignEasy", 3f, 25f, false, 1.5f);
            scene.Campaign.NormalRules = NewRules("CampaignNormal", 0f, 0f, false, 1f);
            scene.Campaign.HardRules = NewRules("CampaignHard", 0f, 0f, false, 0.6f);
            scene.Campaign.RulesDefinition = scene.Campaign.NormalRules;
        }

        private void ConfigureForWorld(CampaignScene scene)
        {
            scene.Campaign.Runner = scene.Missions;
            scene.Campaign.Storage = _storage;
            scene.Campaign.SceneReloader = () => _reloads++;
        }

        private void BeginWithWorldQuietly(CampaignScene scene)
        {
            ConfigureForWorld(scene);
            ExpectCampaign($"started mission={MissionName}");
            ExpectCampaign($"no save; starting mission={MissionName} from the beginning");
            Assert.That(scene.Campaign.TryBeginWith(scene.World()), Is.True);
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

        private static WeaponAmmo[] RifleAmmo(int magazine, int reserve)
        {
            return new[] { new WeaponAmmo(RifleId, magazine, reserve) };
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
                // 読み直した後のシーンでも同じ武器と分かるよう、Id は固定する (値は既定のまま)。
                Rifle = ScriptableObject.CreateInstance<WeaponDefinition>();
                Rifle.Id = RifleId;
                Created.Add(Rifle);
                Handgun = ScriptableObject.CreateInstance<WeaponDefinition>();
                Handgun.Id = HandgunId;
                Created.Add(Handgun);
                Weapon = camera.AddComponent<WeaponController>();
                Weapon.Initialize(camera.transform, _attack, _reload, null, null, new[] { Rifle, Handgun });

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

            /// <summary>プレイヤーの 0 番の武器の定義 (既定値、Id は <see cref="RifleId"/>)。</summary>
            public WeaponDefinition Rifle { get; }

            /// <summary>プレイヤーの 1 番の武器の定義 (既定値、Id は <see cref="HandgunId"/>)。</summary>
            public WeaponDefinition Handgun { get; }

            public CheckpointTrigger Checkpoint { get; }
            public CampaignRunner Campaign { get; }

            public void Begin(ISaveStorage storage, Action reload)
            {
                Campaign.Begin(Missions, Player, Weapon, storage, reload);
            }

            /// <summary>GameBootstrap の生成物の代わりに、このシーンのプレイヤーを渡す。HUD は無い。</summary>
            public BootstrapWorld World()
            {
                return new BootstrapWorld(
                    null, Player.gameObject, Controller, null, null, null, null, Weapon, null, Array.Empty<TargetDummy>(), Player, null, null);
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
