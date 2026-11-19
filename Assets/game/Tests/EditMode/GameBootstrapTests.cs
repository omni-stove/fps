using System.Collections.Generic;
using System.Linq;
using Shooter.Core;
using Shooter.Editor;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;
using UnderwaterPresenter = Shooter.Runtime.Traversal.UnderwaterPresenter;
using WaterVolumes = Shooter.Runtime.Traversal.WaterVolumes;

namespace Shooter.Tests.EditMode
{
    public sealed class GameBootstrapTests
    {
        private GameObject _host;
        private BootstrapWorld _world;
        private WeaponDefinition _rifle;
        private WeaponDefinition _handgun;
        private GameRulesDefinition _rules;

        private static readonly GameRules _testRules = new GameRules(
            FriendlyFire.Off,
            RespawnMode.Individual,
            GameRules.DefaultWaveIntervalSeconds,
            2f,
            10f,
            DeathBody.Vanish,
            DeathCamera.None,
            true,
            2f,
            Destruction.Off);

        [SetUp]
        public void SetUp()
        {
            _rifle = ScriptableObject.CreateInstance<WeaponDefinition>();
            _rifle.name = "Rifle";
            WeaponAssets.Configure(_rifle, TestWeapons.RifleSpec());
            _handgun = ScriptableObject.CreateInstance<WeaponDefinition>();
            _handgun.name = "Handgun";
            WeaponAssets.Configure(_handgun, TestWeapons.HandgunSpec());
            _rules = ScriptableObject.CreateInstance<GameRulesDefinition>();
            _rules.name = "TestRules";
            GameRulesAssets.Configure(_rules, _testRules);

            GameSignals.ResetAll();
            _host = new GameObject("Bootstrap");
            var bootstrap = _host.AddComponent<GameBootstrap>();
            bootstrap.WeaponDefinitions = new[] { _rifle, _handgun };
            bootstrap.RulesDefinition = _rules;
            LogAssert.Expect(LogType.Log, GameBootstrap.ReadyLog);
            _world = bootstrap.Build();
        }

        [TearDown]
        public void TearDown()
        {
            GameRulesOverride.Clear();
            GameRulesOverride.ClearMutators();
            DestroyWorld(_world);
            Object.DestroyImmediate(_host);
            Object.DestroyImmediate(_rifle);
            Object.DestroyImmediate(_handgun);
            Object.DestroyImmediate(_rules);
            GameSignals.ResetAll();
        }

        [Test]
        public void BuildPublishesTheLocalPlayerAndTheHudModel()
        {
            var player = GameSignals.LocalPlayer.Value;

            Assert.That(player, Is.Not.Null);
            Assert.That(player.Player, Is.SameAs(_world.Player));
            Assert.That(player.Damageable, Is.SameAs(_world.PlayerDamageable));
            Assert.That(player.Weapon, Is.SameAs(_world.Weapon));
            Assert.That(player.Ads, Is.SameAs(_world.Ads));
            Assert.That(player.Mover, Is.SameAs(_world.Mover));
            Assert.That(player.Grenades, Is.SameAs(_world.Grenades));
            Assert.That(player.Abilities, Is.SameAs(_world.Abilities));
            Assert.That(player.Actions, Is.SameAs(_world.ActionReader));
            Assert.That(GameSignals.HudModel.Value, Is.SameAs(_world.HudModel));
            Assert.That(GameSignals.HudDensity.Value, Is.EqualTo((HudDensity?)_world.Rules.HudDensity));
            Assert.That(GameSignals.PauseTimeMode.Value, Is.EqualTo((PauseTimeMode?)PauseTimeMode.Freeze), "offline pauses stop time");
        }

        [Test]
        public void DeadPlayerCannotFireReloadOrMoveUntilRespawned()
        {
            var damageable = _world.PlayerDamageable;
            Assert.That(_world.Mover.IsControlHandedOff, Is.False);
            Assert.That(_world.Weapon.IsInputHandedOff, Is.False);

            LogAssert.Expect(LogType.Log, "[Player] control held while Player is dead");
            damageable.Kill();

            Assert.That(_world.Mover.IsControlHandedOff, Is.True, "no move input while dead");
            Assert.That(_world.Weapon.IsInputHandedOff, Is.True, "no fire, reload or weapon switch while dead");

            LogAssert.Expect(LogType.Log, "[Player] control returned to Player");
            damageable.ResetHealth();

            Assert.That(_world.Mover.IsControlHandedOff, Is.False);
            Assert.That(_world.Weapon.IsInputHandedOff, Is.False);
        }

        [Test]
        public void DeathHoldDoesNotReleaseAVehicleSeatsClaim()
        {
            var seat = new object();
            _world.Mover.Handoff.Claim(seat);

            _world.PlayerDamageable.Kill();
            _world.PlayerDamageable.ResetHealth();

            Assert.That(_world.Mover.Handoff.IsHeldBy(seat), Is.True, "respawning only returns what the death took");
            Assert.That(_world.Mover.IsControlHandedOff, Is.True);
            _world.Mover.Handoff.Release(seat);
            Assert.That(_world.Mover.IsControlHandedOff, Is.False);
        }

        [Test]
        public void ClearSignalsRemovesOnlyWhatTheBootstrapPublished()
        {
            var bootstrap = _host.GetComponent<GameBootstrap>();
            var cleared = new List<LocalPlayerSignal>();
            GameSignals.LocalPlayer.Changed += cleared.Add;

            bootstrap.ClearSignals();

            Assert.That(GameSignals.LocalPlayer.Value, Is.Null);
            Assert.That(GameSignals.HudModel.Value, Is.Null);
            Assert.That(GameSignals.HudDensity.Value, Is.Null);
            Assert.That(GameSignals.PauseTimeMode.Value, Is.Null);
            Assert.That(cleared, Is.EqualTo(new LocalPlayerSignal[] { null }), "subscribers hear the clear");

            var other = new LocalPlayerSignal(null, null, null, null, null, null, null, null);
            GameSignals.LocalPlayer.Publish(other);
            bootstrap.ClearSignals();
            Assert.That(GameSignals.LocalPlayer.Value, Is.SameAs(other), "a player published by someone else stays");
        }

        [Test]
        public void PlayerRegeneratesHealthWithTheWiredRules()
        {
            var regen = _world.Regen;

            Assert.That(regen, Is.Not.Null);
            Assert.That(regen, Is.SameAs(_world.Player.GetComponent<HealthRegen>()));
            Assert.That(regen.Target, Is.SameAs(_world.PlayerDamageable));
            Assert.That(regen.Rules.RegenPerSecond, Is.EqualTo(10f));
            Assert.That(_world.Rules.RegenDelaySeconds, Is.EqualTo(2f));

            _world.PlayerDamageable.TakeDamage(50);
            Assert.That(regen.Tick(2f), Is.Zero, "waits for the delay after a hit");
            Assert.That(regen.Tick(1f), Is.EqualTo(10));
            Assert.That(_world.PlayerDamageable.Health.Current, Is.EqualTo(60));
        }

        [Test]
        public void PlayerHasTheDeathCameraWithTheWiredRules()
        {
            var deathCamera = _world.DeathCamera;

            Assert.That(deathCamera, Is.Not.Null);
            Assert.That(deathCamera, Is.SameAs(_world.Player.GetComponent<PlayerDeathCamera>()));
            Assert.That(deathCamera.Mode, Is.EqualTo(_testRules.DeathCamera));
            Assert.That(deathCamera.IsActive, Is.False);
        }

        [Test]
        public void PlayerHasTheViewmodelPresenterAndMotion()
        {
            var player = _world.Player;

            Assert.That(player.GetComponent<ViewmodelPresenter>(), Is.Not.Null);
            Assert.That(player.GetComponent<ViewmodelMotion>(), Is.Not.Null);
            Assert.That(player.GetComponent<WeaponPresentation>(), Is.Not.Null, "the viewmodel hands its muzzle socket to the weapon presentation");
        }

        [Test]
        public void PlayerHasTheActionReaderWiredToTheMockBody()
        {
            var player = _world.Player;
            var reader = _world.ActionReader;

            Assert.That(reader, Is.Not.Null);
            Assert.That(reader, Is.SameAs(player.GetComponent<PlayerActionReader>()));
            Assert.That(reader.IsBound, Is.True);
            Assert.That(reader.Mover, Is.SameAs(_world.Mover));
            Assert.That(reader.Weapon, Is.SameAs(_world.Weapon));
            Assert.That(reader.Ads, Is.SameAs(_world.Ads));
            Assert.That(reader.Cover, Is.SameAs(player.GetComponent<CoverController>()));
            Assert.That(reader.BlindFire, Is.SameAs(player.GetComponent<BlindFireController>()));
            Assert.That(reader.Damageable, Is.SameAs(_world.PlayerDamageable));
            Assert.That(reader.Rig, Is.SameAs(_world.Rig));

            Assert.That(player.GetComponent<PlayerMockBody>().Reader, Is.SameAs(reader));
        }

        [Test]
        public void ActionReaderReadsTheViewFromTheRig()
        {
            var reader = _world.ActionReader;

            var firstPerson = reader.Refresh(0f);
            Assert.That(firstPerson.ViewMode, Is.EqualTo(ViewMode.FirstPerson));
            Assert.That(firstPerson.Shoulder, Is.EqualTo(ShoulderSide.Right));
            Assert.That(firstPerson.IsFiring, Is.False);
            Assert.That(firstPerson.IsReloading, Is.False);
            Assert.That(firstPerson.IsInCover, Is.False);
            Assert.That(firstPerson.IsDowned, Is.False);

            _world.Rig.Mode = ViewMode.ThirdPerson;
            _world.Rig.Shoulder = ShoulderSide.Left;

            var thirdPerson = reader.Refresh(0f);
            Assert.That(thirdPerson.ViewMode, Is.EqualTo(ViewMode.ThirdPerson));
            Assert.That(thirdPerson.Shoulder, Is.EqualTo(ShoulderSide.Left));
            Assert.That(reader.Current, Is.EqualTo(thirdPerson));
        }

        [Test]
        public void TargetDummiesDoNotRegenerate()
        {
            foreach (var dummy in _world.Targets)
            {
                Assert.That(dummy.GetComponentInChildren<HealthRegen>(true), Is.Null, $"{dummy.name} has no regen");
            }
        }

        [Test]
        public void TargetDummiesPresentDeathWithTheWiredRules()
        {
            foreach (var dummy in _world.Targets)
            {
                var presenter = dummy.GetComponent<DeathBodyPresenter>();
                Assert.That(presenter, Is.Not.Null, $"{dummy.name} has a death body presenter");
                Assert.That(presenter.Target, Is.SameAs(dummy.Damageable));
                Assert.That(presenter.Mode, Is.EqualTo(DeathBody.Vanish));
            }

            Assert.That(_world.Player.GetComponent<DeathBodyPresenter>(), Is.Null, "the player's own body is not collapsed");
        }

        [Test]
        public void PlayerDodgeFollowsTheRulesAndBlocksTheWeapon()
        {
            Assert.That(_world.Mover.DodgeRule, Is.EqualTo(_world.Rules.Actions.Dodge));
            Assert.That(_world.Mover.DodgeBlocker, Is.Not.Null, "cover blocks the dodge");
            Assert.That(_world.Weapon.FireBlocker, Is.Not.Null);
            Assert.That(_world.Weapon.IsFireBlocked, Is.False, "not dodging");
        }

        [Test]
        public void PlayerVaultFollowsTheRules()
        {
            Assert.That(_world.Mover.VaultRule, Is.EqualTo(_world.Rules.Actions.Vault));
            Assert.That(_world.Mover.IsVaulting, Is.False);
            Assert.That(_world.Weapon.IsFireBlocked, Is.False, "not vaulting");
        }

        [Test]
        public void ThePlayerGetsTheMinimapRumbleDeadzoneLeanAndUnderwaterParts()
        {
            var player = _world.Player;

            Assert.That(player.GetComponent<MinimapPublisher>(), Is.Not.Null);
            Assert.That(player.GetComponent<StickDeadzoneApplier>(), Is.Not.Null);

            var rumble = player.GetComponent<GamepadRumble>();
            Assert.That(rumble, Is.Not.Null);
            Assert.That(rumble.Rig, Is.SameAs(_world.Rig), "the rumble listens for shots from the view it belongs to");

            var underwater = player.GetComponent<UnderwaterPresenter>();
            Assert.That(underwater, Is.Not.Null);
            Assert.That(underwater.Eye, Is.SameAs(_world.Camera.transform), "under water is judged at the camera");
            Assert.That(underwater.Damageable, Is.SameAs(_world.PlayerDamageable), "drowning hurts the player");

            Assert.That(player.GetComponent<LeanController>(), Is.Not.Null, "the lean installer runs with the other actions");
            Assert.That(PlayerActions.Installers.Any(i => i is LeanActionInstaller), Is.True);
        }

        [Test]
        public void TheMinimapAndTheUnderwaterPartsAreOnlyOnThePlayer()
        {
            foreach (var dummy in _world.Targets)
            {
                Assert.That(dummy.GetComponent<MinimapPublisher>(), Is.Null, dummy.name);
                Assert.That(dummy.GetComponent<GamepadRumble>(), Is.Null, dummy.name);
                Assert.That(dummy.GetComponent<UnderwaterPresenter>(), Is.Null, dummy.name);
            }
        }

        [Test]
        public void ThePlayerHandsItsMotorStateToTheLeaningViewAndBody()
        {
            var source = _world.Player.GetComponent<ICharacterMotorStateSource>();

            Assert.That(source, Is.InstanceOf<PlayerMotorStateSource>());
            Assert.That(((PlayerMotorStateSource)source).Mover, Is.SameAs(_world.Mover));
            Assert.That(source.Spec, Is.Not.Null);
            Assert.That(source.TryGetMotorState(out _), Is.False, "Edit Mode leaves the mover uninitialized, so there is no state yet");
        }

        [Test]
        public void TheDamageablesRecordAssistsWithTheAssistTuning()
        {
            var asset = Resources.Load<TextAsset>(KitTuning.AssistResourcePath);
            Assert.That(asset, Is.Not.Null, "Resources/" + KitTuning.AssistResourcePath + ".json");
            var expected = AssistJson.Parse(asset.text, KitTuning.AssistResourcePath);

            var spec = KillFeedAssists.Ledger.Spec;

            Assert.That(spec, Is.SameAs(KitTuning.Assist));
            Assert.That(spec.WindowSeconds, Is.EqualTo(expected.WindowSeconds));
            Assert.That(spec.MinDamage, Is.EqualTo(expected.MinDamage));
        }

        [Test]
        public void SwimmingBlocksFiringAndAimingUntilThePlayerLeavesTheWater()
        {
            var move = new InputAction("Move", InputActionType.Value, expectedControlType: "Vector2");
            var sprint = new InputAction("Sprint", InputActionType.Button);
            var crouch = new InputAction("Crouch", InputActionType.Button);
            var jump = new InputAction("Jump", InputActionType.Button);
            try
            {
                var mover = _world.Mover;
                mover.Initialize(_world.Controller, _world.Look, move, sprint, crouch, jump, MotorSpec.Default);
                mover.SwimTuning = SwimTuning.Default;
                mover.Water = new WaterVolumes(new[]
                {
                    new WaterBox(new System.Numerics.Vector3(-10f, -1f, -10f), new System.Numerics.Vector3(10f, 4f, 10f), 3f),
                });
                Assert.That(_world.Weapon.IsFireBlocked, Is.False, "dry before the tick");
                Assert.That(_world.Ads.AdsBlocker(), Is.False);

                mover.Tick(Vector2.zero, false, false, false, 0.02f);

                Assert.That(mover.IsSwimming, Is.True);
                Assert.That(_world.Weapon.IsFireBlocked, Is.True);
                Assert.That(_world.Ads.AdsBlocker(), Is.True);

                mover.Water = NoWaterVolumes.Instance;
                mover.Tick(Vector2.zero, false, false, false, 0.02f);

                Assert.That(mover.IsSwimming, Is.False);
                Assert.That(_world.Weapon.IsFireBlocked, Is.False);
                Assert.That(_world.Ads.AdsBlocker(), Is.False);
            }
            finally
            {
                move.Dispose();
                sprint.Dispose();
                crouch.Dispose();
                jump.Dispose();
            }
        }

        [Test]
        public void PlayerWeaponsUseTheWiredReserveRules()
        {
            Assert.That(_world.Weapon.Rules, Is.SameAs(_world.Rules));
            Assert.That(_world.Rules.InfiniteReserve, Is.True);
            Assert.That(_world.Rules.ReserveAmmoMultiplier, Is.EqualTo(2f));
        }

        [Test]
        public void WithoutRulesTheDefaultIsUsedWithAWarning()
        {
            var host = new GameObject("Bootstrap-NoRules");
            BootstrapWorld world = null;
            try
            {
                var bootstrap = host.AddComponent<GameBootstrap>();
                bootstrap.CreatesFloor = false;
                bootstrap.WeaponDefinitions = new[] { _rifle, _handgun };

                LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(@"^\[Bootstrap\] GameRulesDefinition が設定されていない"));
                LogAssert.Expect(LogType.Log, GameBootstrap.ReadyLog);
                world = bootstrap.Build();

                Assert.That(world.Rules, Is.SameAs(GameRules.Default));
                Assert.That(world.Regen.Rules, Is.SameAs(GameRules.Default));
                Assert.That(world.Weapon.Rules, Is.SameAs(GameRules.Default));
            }
            finally
            {
                DestroyWorld(world);
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void WithoutSceneTuningThePlayerReadsTheGameWideMovementValues()
        {
            Assert.That(_world.Mover.MotorSettings, Is.Null);
            Assert.That(_world.Mover.WallRunTuning, Is.SameAs(TraversalTuning.Current.WallRun));
            Assert.That(_world.Mover.SwimTuning, Is.SameAs(TraversalTuning.Current.Swim));
        }

        [Test]
        public void TheLoadoutSwitchSecondsReachThePlayersWeapon()
        {
            var host = new GameObject("Bootstrap-Loadout");
            var loadout = ScriptableObject.CreateInstance<LoadoutDefinition>();
            loadout.SwitchSeconds = 0.25f;
            BootstrapWorld world = null;
            try
            {
                var bootstrap = host.AddComponent<GameBootstrap>();
                bootstrap.CreatesFloor = false;
                bootstrap.WeaponDefinitions = new[] { _rifle, _handgun };
                bootstrap.RulesDefinition = _rules;
                bootstrap.Loadout = loadout;
                Assert.That(bootstrap.EffectiveSwitchSeconds, Is.EqualTo(0.25f));

                LogAssert.Expect(LogType.Log, GameBootstrap.ReadyLog);
                world = bootstrap.Build();

                Assert.That(world.Weapon.SwitchSeconds, Is.EqualTo(0.25f));
                Assert.That(world.Weapon.IsSwitching, Is.False, "the player starts with the first weapon ready");
                Assert.That(world.Weapon.SelectNext(), Is.True);
                Assert.That(world.Weapon.IsSwitching, Is.True);
                Assert.That(world.Weapon.Fire(), Is.False, "the handgun does not fire while it is being drawn");
                world.Weapon.Tick(0.25f);
                Assert.That(world.Weapon.IsSwitching, Is.False);
            }
            finally
            {
                DestroyWorld(world);
                Object.DestroyImmediate(host);
                Object.DestroyImmediate(loadout);
            }
        }

        [Test]
        public void TheSceneMotorSettingsAndTraversalReachThePlayer()
        {
            var host = new GameObject("Bootstrap-Tuning");
            var settings = ScriptableObject.CreateInstance<MotorSettings>();
            settings.AirJumps = 1;
            var traversal = new TextAsset("{ \"swim\": { \"speed\": 4 }, \"wallRun\": { \"enabled\": true } }") { name = "SceneTraversal" };
            BootstrapWorld world = null;
            try
            {
                var bootstrap = host.AddComponent<GameBootstrap>();
                bootstrap.CreatesFloor = false;
                bootstrap.WeaponDefinitions = new[] { _rifle, _handgun };
                bootstrap.RulesDefinition = _rules;
                bootstrap.MotorSettings = settings;
                bootstrap.TraversalTuningJson = traversal;

                LogAssert.Expect(LogType.Log, GameBootstrap.ReadyLog);
                world = bootstrap.Build();

                Assert.That(world.Mover.MotorSettings, Is.SameAs(settings));
                Assert.That(world.Mover.Spec.AirJumps, Is.EqualTo(1));
                Assert.That(world.Mover.WallRunTuning.Enabled, Is.True);
                Assert.That(world.Mover.SwimTuning.Speed, Is.EqualTo(4f));
                Assert.That(world.Player.GetComponent<UnderwaterPresenter>().Swim, Is.SameAs(world.Mover.SwimTuning), "breath and drowning use the same swim values");
            }
            finally
            {
                DestroyWorld(world);
                Object.DestroyImmediate(host);
                Object.DestroyImmediate(settings);
                Object.DestroyImmediate(traversal);
            }
        }

        [Test]
        public void AnUnreadableSceneTraversalFallsBackToTheGameWideValuesWithAnError()
        {
            var host = new GameObject("Bootstrap-BadTraversal");
            var traversal = new TextAsset("{ \"wallRun\": { \"maxSeconds\": -1 } }") { name = "BadTraversal" };
            BootstrapWorld world = null;
            try
            {
                var bootstrap = host.AddComponent<GameBootstrap>();
                bootstrap.CreatesFloor = false;
                bootstrap.WeaponDefinitions = new[] { _rifle, _handgun };
                bootstrap.RulesDefinition = _rules;
                bootstrap.TraversalTuningJson = traversal;

                LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex(@"^\[Tuning\] .*BadTraversal"));
                LogAssert.Expect(LogType.Log, GameBootstrap.ReadyLog);
                world = bootstrap.Build();

                Assert.That(world.Mover.WallRunTuning, Is.SameAs(TraversalTuning.Current.WallRun));
            }
            finally
            {
                DestroyWorld(world);
                Object.DestroyImmediate(host);
                Object.DestroyImmediate(traversal);
            }
        }

        [Test]
        public void TheWiredRulesAreUsedWhenNothingOverridesThem()
        {
            Assert.That(GameRulesOverride.Current, Is.Null);
            Assert.That(_world.Rules.RegenPerSecond, Is.EqualTo(_testRules.RegenPerSecond));
            Assert.That(_world.Rules.ReserveAmmoMultiplier, Is.EqualTo(_testRules.ReserveAmmoMultiplier));
        }

        [Test]
        public void OverrideRulesWinOverTheWiredRulesAndAreLogged()
        {
            var overridden = _testRules.WithRegenPerSecond(30f).WithInfiniteReserve(false);
            GameRulesOverride.Set(overridden);
            var host = new GameObject("Bootstrap-Overridden");
            BootstrapWorld world = null;
            try
            {
                var bootstrap = host.AddComponent<GameBootstrap>();
                bootstrap.CreatesFloor = false;
                bootstrap.WeaponDefinitions = new[] { _rifle, _handgun };
                bootstrap.RulesDefinition = _rules;

                LogAssert.Expect(LogType.Log, GameBootstrap.FormatOverrideLog(_rules.name));
                LogAssert.Expect(LogType.Log, GameBootstrap.ReadyLog);
                world = bootstrap.Build();

                Assert.That(world.Rules, Is.SameAs(overridden));
                Assert.That(world.Regen.Rules, Is.SameAs(overridden));
                Assert.That(world.Weapon.Rules, Is.SameAs(overridden));
            }
            finally
            {
                DestroyWorld(world);
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void OverrideRulesReplaceAMissingOrBrokenDefinitionWithoutTheWarning()
        {
            GameRulesOverride.Set(_testRules);
            var broken = ScriptableObject.CreateInstance<GameRulesDefinition>();
            var hosts = new[] { new GameObject("Bootstrap-OverriddenNoRules"), new GameObject("Bootstrap-OverriddenBroken") };
            var worlds = new BootstrapWorld[hosts.Length];
            var problems = new List<string>();
            Application.LogCallback record = (message, _, type) =>
            {
                if (type != LogType.Log)
                {
                    problems.Add(type + ": " + message);
                }
            };
            Application.logMessageReceived += record;
            try
            {
                broken.RegenPerSecond = -1f;
                for (var i = 0; i < hosts.Length; i++)
                {
                    var bootstrap = hosts[i].AddComponent<GameBootstrap>();
                    bootstrap.CreatesFloor = false;
                    bootstrap.WeaponDefinitions = new[] { _rifle, _handgun };
                    bootstrap.RulesDefinition = i == 0 ? null : broken;

                    LogAssert.Expect(LogType.Log, GameBootstrap.FormatOverrideLog(i == 0 ? null : broken.name));
                    LogAssert.Expect(LogType.Log, GameBootstrap.ReadyLog);
                    worlds[i] = bootstrap.Build();

                    Assert.That(worlds[i].Rules, Is.SameAs(_testRules), hosts[i].name);
                }

                // 組み立ての通常のログは機能が増えるたびに増えるので、全部を並べず警告とエラーが無いことだけを見る。
                Assert.That(problems, Is.Empty);
            }
            finally
            {
                Application.logMessageReceived -= record;
                foreach (var world in worlds)
                {
                    DestroyWorld(world);
                }

                foreach (var host in hosts)
                {
                    Object.DestroyImmediate(host);
                }

                Object.DestroyImmediate(broken);
            }
        }

        [Test]
        public void OverrideCanBeClearedAndRejectsNull()
        {
            Assert.Throws<System.ArgumentNullException>(() => GameRulesOverride.Set(null));
            Assert.That(GameRulesOverride.Current, Is.Null);

            GameRulesOverride.Set(GameRules.Default);
            Assert.That(GameRulesOverride.Current, Is.SameAs(GameRules.Default));
            GameRulesOverride.Set(_testRules);
            Assert.That(GameRulesOverride.Current, Is.SameAs(_testRules), "a later Set replaces the earlier one");

            GameRulesOverride.Clear();
            Assert.That(GameRulesOverride.Current, Is.Null);
        }

        [Test]
        public void OverrideLogNamesTheReplacedRules()
        {
            Assert.That(GameBootstrap.FormatOverrideLog("Range"), Does.StartWith("[Bootstrap] ").And.Contain("'Range'"));
            Assert.That(GameBootstrap.FormatOverrideLog(null), Does.StartWith("[Bootstrap] ").And.Contain("(none)"));
            Assert.That(GameBootstrap.FormatOverrideLog(string.Empty), Is.EqualTo(GameBootstrap.FormatOverrideLog(null)));
        }

        [Test]
        public void PushedMutatorsStackOnTheWiredRulesInOrderAndAreLogged()
        {
            var lowHealth = new GameRulesMutator("low-health", "{ \"playerMaxHealth\": 50 }");
            var headshots = new GameRulesMutator("headshots-only", "{ \"zoneDamage\": { \"body\": 0, \"limb\": 0 } }");
            GameRulesOverride.PushMutator(lowHealth);
            GameRulesOverride.PushMutator(headshots);
            var host = new GameObject("Bootstrap-Mutated");
            BootstrapWorld world = null;
            try
            {
                var bootstrap = host.AddComponent<GameBootstrap>();
                bootstrap.CreatesFloor = false;
                bootstrap.WeaponDefinitions = new[] { _rifle, _handgun };
                bootstrap.RulesDefinition = _rules;

                var afterLowHealth = lowHealth.Apply(_testRules);
                LogAssert.Expect(LogType.Log, MutatorLog.Applied(lowHealth, _testRules, afterLowHealth));
                LogAssert.Expect(LogType.Log, MutatorLog.Applied(headshots, afterLowHealth, headshots.Apply(afterLowHealth)));
                LogAssert.Expect(LogType.Log, GameBootstrap.ReadyLog);
                world = bootstrap.Build();

                Assert.That(world.Rules.PlayerMaxHealth, Is.EqualTo(50));
                Assert.That(world.Rules.ZoneDamage.Body, Is.EqualTo(0d));
                Assert.That(world.Rules.ZoneDamage.Limb, Is.EqualTo(0d));
                Assert.That(world.Rules.ZoneDamage.Head, Is.EqualTo(_testRules.ZoneDamage.Head));
                Assert.That(world.Rules.RegenPerSecond, Is.EqualTo(_testRules.RegenPerSecond), "an item no mutator writes keeps the wired value");
                Assert.That(world.Rules.InfiniteReserve, Is.EqualTo(_testRules.InfiniteReserve));
                Assert.That(world.PlayerDamageable.MaxHealth, Is.EqualTo(50));
                Assert.That(world.Regen.Rules, Is.SameAs(world.Rules));
            }
            finally
            {
                DestroyWorld(world);
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void MutatorsStackOnTheOverrideRulesToo()
        {
            var overridden = _testRules.WithRegenPerSecond(30f);
            var lowHealth = new GameRulesMutator("low-health", "{ \"playerMaxHealth\": 50 }");
            GameRulesOverride.Set(overridden);
            GameRulesOverride.PushMutator(lowHealth);
            var host = new GameObject("Bootstrap-OverriddenAndMutated");
            BootstrapWorld world = null;
            try
            {
                var bootstrap = host.AddComponent<GameBootstrap>();
                bootstrap.CreatesFloor = false;
                bootstrap.WeaponDefinitions = new[] { _rifle, _handgun };
                bootstrap.RulesDefinition = _rules;

                LogAssert.Expect(LogType.Log, GameBootstrap.FormatOverrideLog(_rules.name));
                LogAssert.Expect(LogType.Log, MutatorLog.Applied(lowHealth, overridden, lowHealth.Apply(overridden)));
                LogAssert.Expect(LogType.Log, GameBootstrap.ReadyLog);
                world = bootstrap.Build();

                Assert.That(world.Rules.RegenPerSecond, Is.EqualTo(30f));
                Assert.That(world.Rules.PlayerMaxHealth, Is.EqualTo(50));
            }
            finally
            {
                DestroyWorld(world);
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void AMutatorThatDoesNotFitTheRulesIsSkippedWithAnErrorAndTheRestStillApply()
        {
            var blindFire = new GameRulesMutator("blind-fire", "{ \"actions\": { \"blindFire\": \"On\" } }");
            var lowHealth = new GameRulesMutator("low-health", "{ \"playerMaxHealth\": 50 }");
            GameRulesOverride.PushMutator(blindFire);
            GameRulesOverride.PushMutator(lowHealth);
            var host = new GameObject("Bootstrap-MutatorSkipped");
            BootstrapWorld world = null;
            try
            {
                var bootstrap = host.AddComponent<GameBootstrap>();
                bootstrap.CreatesFloor = false;
                bootstrap.WeaponDefinitions = new[] { _rifle, _handgun };
                bootstrap.RulesDefinition = _rules;

                var reason = Assert.Throws<GameRulesJsonException>(() => blindFire.Apply(_testRules));
                LogAssert.Expect(LogType.Error, MutatorLog.Skipped(blindFire, reason));
                LogAssert.Expect(LogType.Log, MutatorLog.Applied(lowHealth, _testRules, lowHealth.Apply(_testRules)));
                LogAssert.Expect(LogType.Log, GameBootstrap.ReadyLog);
                world = bootstrap.Build();

                Assert.That(world.Rules.Actions.BlindFire, Is.EqualTo(BlindFire.Off));
                Assert.That(world.Rules.PlayerMaxHealth, Is.EqualTo(50));
            }
            finally
            {
                DestroyWorld(world);
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void AnUnknownSceneMutatorIdIsSkippedWithAnError()
        {
            var host = new GameObject("Bootstrap-UnknownMutator");
            BootstrapWorld world = null;
            try
            {
                var bootstrap = host.AddComponent<GameBootstrap>();
                bootstrap.CreatesFloor = false;
                bootstrap.WeaponDefinitions = new[] { _rifle, _handgun };
                bootstrap.RulesDefinition = _rules;
                bootstrap.MutatorIds = new[] { "no-such-mutator" };

                LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex(
                    @"^\[Mutator\] skipped id=no-such-mutator: no mutator has this id; known ids are "));
                LogAssert.Expect(LogType.Log, GameBootstrap.ReadyLog);
                world = bootstrap.Build();

                Assert.That(world.Rules.PlayerMaxHealth, Is.EqualTo(_testRules.PlayerMaxHealth));
                Assert.That(world.Rules.RegenPerSecond, Is.EqualTo(_testRules.RegenPerSecond));
            }
            finally
            {
                DestroyWorld(world);
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void MutatorIdsCopyTheGivenListAndTreatNullAsEmpty()
        {
            var bootstrap = _host.GetComponent<GameBootstrap>();
            Assert.That(bootstrap.MutatorIds, Is.Empty);

            var ids = new[] { "low-health", "headshots-only" };
            bootstrap.MutatorIds = ids;
            ids[0] = "changed";
            Assert.That(bootstrap.MutatorIds, Is.EqualTo(new[] { "low-health", "headshots-only" }));

            bootstrap.MutatorIds = null;
            Assert.That(bootstrap.MutatorIds, Is.Empty);
        }

        [Test]
        public void InvalidRulesFailTheBuildBeforeCreatingAnything()
        {
            var host = new GameObject("Bootstrap-BrokenRules");
            var broken = ScriptableObject.CreateInstance<GameRulesDefinition>();
            try
            {
                broken.name = "BrokenRules";
                broken.RegenPerSecond = -1f;
                var bootstrap = host.AddComponent<GameBootstrap>();
                bootstrap.CreatesFloor = false;
                bootstrap.WeaponDefinitions = new[] { _rifle, _handgun };
                bootstrap.RulesDefinition = broken;
                var playersBefore = Object.FindObjectsByType<Damageable>().Length;

                var e = Assert.Throws<GameRulesDefinitionException>(() => bootstrap.Build());
                Assert.That(e.Message, Does.Contain("BrokenRules"));
                Assert.That(bootstrap.World, Is.Null);
                Assert.That(
                    Object.FindObjectsByType<Damageable>().Length,
                    Is.EqualTo(playersBefore),
                    "no player is left behind");
            }
            finally
            {
                Object.DestroyImmediate(host);
                Object.DestroyImmediate(broken);
            }
        }

        private static void DestroyWorld(BootstrapWorld world)
        {
            if (world == null)
            {
                return;
            }

            if (world.Floor != null)
            {
                Object.DestroyImmediate(world.Floor);
            }

            Object.DestroyImmediate(world.Player);
            Object.DestroyImmediate(world.Light.gameObject);
            Object.DestroyImmediate(world.TargetRoot);
        }

        [Test]
        public void BuildIsIdempotent()
        {
            var bootstrap = _host.GetComponent<GameBootstrap>();

            Assert.That(bootstrap.World, Is.SameAs(_world));
            Assert.That(bootstrap.Build(), Is.SameAs(_world));
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void BuiltIsRaisedOnceWithTheWorld()
        {
            var host = new GameObject("Bootstrap-Built");
            BootstrapWorld world = null;
            try
            {
                var bootstrap = host.AddComponent<GameBootstrap>();
                bootstrap.CreatesFloor = false;
                var received = new System.Collections.Generic.List<BootstrapWorld>();
                bootstrap.Built += _ => throw new System.InvalidOperationException("a broken subscriber");
                bootstrap.Built += received.Add;

                LogAssert.Expect(LogType.Log, GameBootstrap.ReadyLog);
                LogAssert.Expect(LogType.Exception, new System.Text.RegularExpressions.Regex("a broken subscriber"));
                world = bootstrap.Build();

                Assert.That(received, Is.EqualTo(new[] { world }), "a throwing subscriber does not stop the others");
                Assert.That(bootstrap.World, Is.SameAs(world), "a throwing subscriber does not fail the build");

                Assert.That(bootstrap.Build(), Is.SameAs(world));
                Assert.That(received.Count(), Is.EqualTo(1), "raised only when the world is first built");
            }
            finally
            {
                DestroyWorld(world);
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void CreatesFloorPlane()
        {
            var floor = _world.Floor;

            Assert.That(floor.name, Is.EqualTo(GameBootstrap.FloorName));
            Assert.That(floor.transform.position, Is.EqualTo(Vector3.zero));
            Assert.That(floor.transform.localScale, Is.EqualTo(GameBootstrap.FloorScale));
            Assert.That(floor.GetComponent<MeshCollider>(), Is.Not.Null, "the player needs something to stand on");
        }

        [Test]
        public void ThePrefabsReplaceThePlaceholderFloorDummiesMockBodyAndGrenade()
        {
            var floorPrefab = new GameObject("Ground");
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = "Slab";
            ground.transform.SetParent(floorPrefab.transform, false);
            var dummyPrefab = new GameObject("Mannequin");
            GameObject.CreatePrimitive(PrimitiveType.Cube).transform.SetParent(dummyPrefab.transform, false);
            var gunPrefab = new GameObject("Carbine");
            var grenadePrefab = new GameObject("Frag");
            var mockBodyPrefabs = new PlayerMockBodyPrefabs(null, null, gunPrefab);

            var host = new GameObject("Bootstrap-Prefabs");
            BootstrapWorld world = null;
            try
            {
                var bootstrap = host.AddComponent<GameBootstrap>();
                bootstrap.WeaponDefinitions = new[] { _rifle, _handgun };
                bootstrap.RulesDefinition = _rules;
                bootstrap.FloorPrefab = floorPrefab;
                bootstrap.TargetDummyPrefab = dummyPrefab;
                bootstrap.MockBodyPrefabs = mockBodyPrefabs;
                bootstrap.GrenadePrefab = grenadePrefab;

                LogAssert.Expect(LogType.Log, GameBootstrap.ReadyLog);
                world = bootstrap.Build();

                Assert.That(world.Floor.name, Is.EqualTo(GameBootstrap.FloorName));
                Assert.That(world.Floor.GetComponent<MeshCollider>(), Is.Null, "no placeholder plane");
                Assert.That(world.Floor.transform.localScale, Is.EqualTo(Vector3.one), "the prefab keeps its own size");
                Assert.That(world.Floor.transform.Find("Slab").GetComponent<Collider>(), Is.Not.Null, "the floor keeps its collider to stand on");

                foreach (var dummy in world.Targets)
                {
                    Assert.That(dummy.transform.Find(TargetDummy.VisualName), Is.Not.Null, dummy.name);
                    Assert.That(dummy.transform.Find(TargetDummy.BodyName).GetComponent<Renderer>(), Is.Null, dummy.name);
                }

                Assert.That(world.Player.GetComponent<PlayerMockBody>().Prefabs, Is.SameAs(mockBodyPrefabs));
                Assert.That(world.Grenades.GrenadePrefab, Is.SameAs(grenadePrefab));
            }
            finally
            {
                DestroyWorld(world);
                Object.DestroyImmediate(host);
                Object.DestroyImmediate(floorPrefab);
                Object.DestroyImmediate(dummyPrefab);
                Object.DestroyImmediate(gunPrefab);
                Object.DestroyImmediate(grenadePrefab);
            }
        }

        [Test]
        public void CreatesPlayerWithCharacterControllerOnly()
        {
            var player = _world.Player;

            Assert.That(player.name, Is.EqualTo(GameBootstrap.PlayerName));
            Assert.That(player.CompareTag(GameBootstrap.PlayerTag), Is.True, "mission area triggers find the player by this tag");
            Assert.That(player.transform.position, Is.EqualTo(GameBootstrap.PlayerSpawn));
            Assert.That(_world.Controller, Is.SameAs(player.GetComponent<CharacterController>()));
            Assert.That(_world.Controller.height, Is.EqualTo(GameBootstrap.PlayerHeight));
            Assert.That(_world.Controller.radius, Is.EqualTo(GameBootstrap.PlayerRadius));
            Assert.That(player.GetComponent<CapsuleCollider>(), Is.Null, "the primitive's collider would double up with the CharacterController");
        }

        [Test]
        public void ThePlayerTagIsTheOneTheTriggersLookForByDefault()
        {
            Assert.That(GameBootstrap.PlayerTag, Is.EqualTo("Player"), "Unity's built-in Player tag, so no tag has to be added to the project");
            Assert.That(CoopInteractable.DefaultPlayerTag, Is.EqualTo(GameBootstrap.PlayerTag));
        }

        [Test]
        public void PlayerFeetRestOnTheFloor()
        {
            var bottom = _world.Player.transform.position.y + _world.Controller.center.y - _world.Controller.height / 2f;

            Assert.That(bottom, Is.EqualTo(0f).Within(1e-4f));
        }

        [Test]
        public void CameraIsUnderTheEyeAtEyeHeight()
        {
            var camera = _world.Camera;
            var eye = camera.transform.parent;

            Assert.That(camera, Is.Not.Null);
            Assert.That(camera.name, Is.EqualTo(GameBootstrap.CameraName));
            Assert.That(eye.name, Is.EqualTo(GameBootstrap.EyeName));
            Assert.That(eye.parent, Is.SameAs(_world.Player.transform));
            Assert.That(eye.localPosition, Is.EqualTo(GameBootstrap.EyeOffset));
            Assert.That(camera.transform.localPosition, Is.EqualTo(Vector3.zero), "first person by default");
            Assert.That(camera.CompareTag("MainCamera"), Is.True);
            Assert.That(camera.GetComponent<AudioListener>(), Is.Not.Null);
        }

        [Test]
        public void LookIsOnThePlayerAndFindsTheCamera()
        {
            Assert.That(_world.Look, Is.SameAs(_world.Player.GetComponent<FirstPersonLook>()));
            Assert.That(FirstPersonLook.FindChildCamera(_world.Player.transform), Is.SameAs(_world.Camera));
        }

        [Test]
        public void RigIsFirstPersonByDefaultAndHidesTheOwnBody()
        {
            var rig = _world.Rig;

            Assert.That(rig, Is.SameAs(_world.Player.GetComponent<ViewRig>()));
            Assert.That(rig.Mode, Is.EqualTo(ViewMode.FirstPerson));
            Assert.That(rig.Eye, Is.SameAs(_world.Camera.transform.parent));
            Assert.That(rig.Camera, Is.SameAs(_world.Camera));
            Assert.That(_world.Player.GetComponent<Renderer>().shadowCastingMode, Is.EqualTo(ShadowCastingMode.ShadowsOnly));
        }

        [Test]
        public void RigHasShakeAndViewmodelRootsUnderTheCamera()
        {
            var rig = _world.Rig;

            Assert.That(rig.ShakeRoot, Is.Not.Null);
            Assert.That(rig.ViewmodelRoot, Is.Not.Null);
            Assert.That(rig.ShakeRoot.parent, Is.SameAs(_world.Camera.transform));
            Assert.That(rig.ViewmodelRoot.parent, Is.SameAs(_world.Camera.transform));
        }

        [Test]
        public void ThirdPersonBuildPutsTheCameraBehindAndShowsTheBody()
        {
            // SetUp のプレイヤーが同じ場所に立っていると、三人称のカメラがそのカプセルに当たって縮む
            _world.Player.SetActive(false);
            Physics.SyncTransforms();
            var host = new GameObject("BootstrapThirdPerson");
            BootstrapWorld world = null;
            try
            {
                var bootstrap = host.AddComponent<GameBootstrap>();
                bootstrap.CreatesFloor = false;
                _rules.ViewMode = ViewMode.ThirdPerson;
                bootstrap.RulesDefinition = _rules;
                bootstrap.WeaponDefinitions = new[] { _rifle, _handgun };
                LogAssert.Expect(LogType.Log, GameBootstrap.ReadyLog);

                world = bootstrap.Build();

                Assert.That(world.Rig.Mode, Is.EqualTo(ViewMode.ThirdPerson));
                Assert.That(world.Camera.transform.localPosition.z, Is.LessThan(-1f));
                Assert.That(world.Player.GetComponent<Renderer>().shadowCastingMode, Is.EqualTo(ShadowCastingMode.On));

                // Edit Mode では WeaponController の Awake が走らないので、入力を渡して初期化する
                using (var attack = new InputAction("Attack", InputActionType.Button))
                using (var reload = new InputAction("Reload", InputActionType.Button))
                {
                    world.Weapon.Initialize(world.Camera.transform, attack, reload);
                    world.Weapon.ResolveShot(out var origin, out _);
                    Assert.That(Vector3.Distance(origin, world.Rig.Eye.position), Is.LessThan(1e-5f), "third person fires from the eye");
                }
            }
            finally
            {
                DestroyWorld(world);
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void PlayerHasTheAimAssistWiredToTheLookAndTheAds()
        {
            var assist = _world.Player.GetComponent<AimAssist>();

            Assert.That(assist, Is.Not.Null);
            Assert.That(assist.Look, Is.SameAs(_world.Look));
            Assert.That(assist.Targets, Is.Not.Null);
            Assert.That(assist.AimSource, Is.Not.Null);
            Assert.That(assist.AimingSource, Is.Not.Null);
            Assert.That(assist.AimingSource(), Is.False, "not aiming down the sights yet");
            Assert.That(_world.Player.GetComponent<LockOnAim>(), Is.Not.Null, "the aim assist sits beside the lock-on");
        }

        [Test]
        public void PlayerHasTheStreaksWhenTheRulesTurnThemOn()
        {
            Assert.That(_world.Rules.Streaks, Is.EqualTo(Streaks.On), "the kit default");

            var streaks = _world.Player.GetComponent<StreakController>();

            Assert.That(streaks, Is.Not.Null);
            Assert.That(streaks.enabled, Is.True);
            Assert.That(streaks.gameObject, Is.SameAs(_world.PlayerDamageable.gameObject), "counted against the player's own deaths");
        }

        [Test]
        public void CampaignRulesLeaveTheStreaksOffButKeepTheAimAssist()
        {
            _world.Player.SetActive(false);
            var host = new GameObject("BootstrapCampaign");
            BootstrapWorld world = null;
            try
            {
                var bootstrap = host.AddComponent<GameBootstrap>();
                bootstrap.CreatesFloor = false;
                _rules.Streaks = Streaks.Off;
                bootstrap.RulesDefinition = _rules;
                bootstrap.WeaponDefinitions = new[] { _rifle, _handgun };
                LogAssert.Expect(LogType.Log, GameBootstrap.ReadyLog);

                world = bootstrap.Build();

                Assert.That(world.Rules.Streaks, Is.EqualTo(Streaks.Off));
                Assert.That(world.Player.GetComponent<StreakController>(), Is.Null);
                Assert.That(world.Player.GetComponent<AimAssist>(), Is.Not.Null, "only the streaks follow the rule");
                Assert.That(GameSignals.Streak.Value, Is.Null, "nothing publishes a streak");
            }
            finally
            {
                DestroyWorld(world);
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void ThePlayerTuningStartsAtTheComponentDefaults()
        {
            var bootstrap = _host.GetComponent<GameBootstrap>();

            Assert.That(bootstrap.PlayerMaxHealth, Is.EqualTo(Damageable.DefaultMaxHealth));
            Assert.That(bootstrap.ThirdPersonOffset, Is.EqualTo(ViewRig.DefaultThirdPersonOffset));
            Assert.That(bootstrap.MinBoomLength, Is.EqualTo(CameraBoom.DefaultMinLength));
            Assert.That(bootstrap.AdsTransitionSeconds, Is.EqualTo(AdsZoom.DefaultTransitionSeconds));
            Assert.That(_world.Rig.ThirdPersonOffset, Is.EqualTo(ViewRig.DefaultThirdPersonOffset));
            Assert.That(_world.Rig.MinBoomLength, Is.EqualTo(CameraBoom.DefaultMinLength));
            Assert.That(_world.Ads.TransitionSeconds, Is.EqualTo(AdsZoom.DefaultTransitionSeconds));
        }

        [Test]
        public void ThePlayerTuningSetOnTheBootstrapReachesThePlayer()
        {
            // SetUp のプレイヤーが同じ場所に立っていると、三人称のカメラがそのカプセルに当たって縮む
            _world.Player.SetActive(false);
            Physics.SyncTransforms();
            var host = new GameObject("BootstrapTuned");
            BootstrapWorld world = null;
            try
            {
                var bootstrap = host.AddComponent<GameBootstrap>();
                bootstrap.CreatesFloor = false;
                _rules.ViewMode = ViewMode.ThirdPerson;
                bootstrap.WeaponDefinitions = new[] { _rifle, _handgun };
                bootstrap.RulesDefinition = _rules;
                _rules.PlayerMaxHealth = 150;
                bootstrap.ThirdPersonOffset = new Vector3(0.4f, 0.1f, -4f);
                bootstrap.MinBoomLength = 0.5f;
                bootstrap.AdsTransitionSeconds = 0.4f;
                LogAssert.Expect(LogType.Log, GameBootstrap.ReadyLog);

                world = bootstrap.Build();

                Assert.That(world.PlayerDamageable.MaxHealth, Is.EqualTo(150));
                Assert.That(world.PlayerDamageable.Health.Max, Is.EqualTo(150));
                Assert.That(world.PlayerDamageable.Health.Current, Is.EqualTo(150));
                Assert.That(world.Rig.ThirdPersonOffset, Is.EqualTo(new Vector3(0.4f, 0.1f, -4f)));
                Assert.That(world.Rig.MinBoomLength, Is.EqualTo(0.5f));
                Assert.That(world.Camera.transform.localPosition.z, Is.LessThan(-3.5f), "the camera sits at the configured distance");
                Assert.That(world.Ads.TransitionSeconds, Is.EqualTo(0.4f));
            }
            finally
            {
                DestroyWorld(world);
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void ThePlayerTuningSettersRejectInvalidValues()
        {
            var bootstrap = _host.GetComponent<GameBootstrap>();

            Assert.Throws<System.ArgumentOutOfRangeException>(() => bootstrap.PlayerMaxHealth = 0);
            Assert.Throws<System.ArgumentOutOfRangeException>(() => bootstrap.ThirdPersonOffset = new Vector3(0f, float.NaN, -2f));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => bootstrap.MinBoomLength = -0.1f);
            Assert.Throws<System.ArgumentOutOfRangeException>(() => bootstrap.MinBoomLength = float.PositiveInfinity);
            Assert.Throws<System.ArgumentOutOfRangeException>(() => bootstrap.AdsTransitionSeconds = float.NaN);

            Assert.That(bootstrap.PlayerMaxHealth, Is.EqualTo(Damageable.DefaultMaxHealth), "a rejected value leaves the old one");
            Assert.That(bootstrap.MinBoomLength, Is.EqualTo(CameraBoom.DefaultMinLength));
        }

        [Test]
        public void InvalidPlayerTuningFailsTheBuildBeforeCreatingAnything()
        {
            var host = new GameObject("Bootstrap-BrokenTuning");
            try
            {
                var bootstrap = host.AddComponent<GameBootstrap>();
                bootstrap.CreatesFloor = false;
                bootstrap.WeaponDefinitions = new[] { _rifle, _handgun };
                bootstrap.RulesDefinition = _rules;

                // Inspector の Min を通り抜ける値 (NaN) を、保存された値として入れる。
                var serialized = new UnityEditor.SerializedObject(bootstrap);
                serialized.FindProperty("_minBoomLength").floatValue = float.NaN;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                var playersBefore = Object.FindObjectsByType<Damageable>().Length;

                var e = Assert.Throws<System.ArgumentOutOfRangeException>(() => bootstrap.Build());
                Assert.That(e.ParamName, Is.EqualTo(nameof(GameBootstrap.MinBoomLength)));
                Assert.That(bootstrap.World, Is.Null);
                Assert.That(Object.FindObjectsByType<Damageable>().Length, Is.EqualTo(playersBefore), "no player is left behind");
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void PlayerCanBeShotAndFoundByEnemies()
        {
            var player = _world.Player;
            var damageable = _world.PlayerDamageable;

            Assert.That(player.CompareTag(EnemyAgent.TargetTag), Is.True);
            Assert.That(damageable, Is.SameAs(player.GetComponent<Damageable>()));
            Assert.That(damageable.Id, Is.EqualTo(GameBootstrap.PlayerName));
            Assert.That(damageable.Health.Max, Is.EqualTo(Damageable.DefaultMaxHealth));

            var hitbox = Hitbox.FindFor(_world.Controller);
            Assert.That(hitbox, Is.Not.Null, "the CharacterController is the body hitbox");
            Assert.That(hitbox.Zone, Is.EqualTo(HitZone.Body));
            Assert.That(hitbox.Owner, Is.SameAs(damageable));
        }

        [Test]
        public void FloorCanBeLeftToTheScene()
        {
            var host = new GameObject("BootstrapWithoutFloor");
            BootstrapWorld world = null;
            try
            {
                var bootstrap = host.AddComponent<GameBootstrap>();
                bootstrap.CreatesFloor = false;
                LogAssert.Expect(LogType.Log, GameBootstrap.ReadyLog);

                world = bootstrap.Build();

                Assert.That(world.Floor, Is.Null);
                Assert.That(world.Player, Is.Not.Null);
            }
            finally
            {
                DestroyWorld(world);
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void PlayerControllerIsOnThePlayerWithDefaultSpec()
        {
            var mover = _world.Mover;

            Assert.That(mover, Is.SameAs(_world.Player.GetComponent<PlayerController>()));
            Assert.That(mover.WalkSpeed, Is.EqualTo(PlayerController.DefaultWalkSpeed));
            Assert.That(mover.SprintSpeed, Is.EqualTo(PlayerController.DefaultSprintSpeed));
            Assert.That(mover.CrouchSpeed, Is.EqualTo(PlayerController.DefaultCrouchSpeed));
            Assert.That(mover.CrouchHeight, Is.EqualTo(PlayerController.DefaultCrouchHeight));
            Assert.That(mover.JumpHeight, Is.EqualTo(PlayerController.DefaultJumpHeight));
            Assert.That(mover.Gravity, Is.EqualTo(PlayerController.DefaultGravity));
            Assert.That(mover.AirControl, Is.EqualTo(PlayerController.DefaultAirControl));
        }

        [Test]
        public void WeaponIsOnTheCamera()
        {
            Assert.That(_world.Weapon, Is.Not.Null);
            Assert.That(_world.Weapon, Is.SameAs(_world.Camera.GetComponent<WeaponController>()));
            Assert.That(_world.Weapon.Definitions, Is.EqualTo(new[] { _rifle, _handgun }), "the bootstrap hands its loadout to the player");
            Assert.That(_world.Camera.gameObject.activeSelf, Is.True, "the camera is active again after the weapon is attached");
        }

        [Test]
        public void ThePlayerCarriesOneVisionControllerAndAThermalHighlighterBoundToIt()
        {
            var vision = _world.Player.GetComponents<VisionModeController>();
            var thermal = _world.Player.GetComponents<ThermalHighlighter>();

            Assert.That(vision, Has.Length.EqualTo(1), "goggles and thermal scopes need the controller to change the view");
            Assert.That(thermal, Has.Length.EqualTo(1));
            Assert.That(thermal[0].Controller, Is.SameAs(vision[0]));
            Assert.That(vision[0].Damageable, Is.SameAs(_world.Player.GetComponent<Damageable>()));
            Assert.That(vision[0].Goggles, Is.EqualTo(VisionMode.None), "the player starts without goggles");
            Assert.That(thermal[0].IsActive, Is.False);
        }

        [Test]
        public void ThePlayerMovesWithTheCarryLoadOfTheWeaponAndTheInventory()
        {
            Assert.That(_world.Mover.CarryLoad, Is.InstanceOf<PlayerCarryLoad>());
            Assert.That(_world.Player.GetComponent<PlayerInventory>(), Is.Not.Null);
            Assert.That(
                _world.Mover.CarryLoad.CarrySpeedMultiplier(CarryLoadSpec.Default),
                Is.EqualTo(_world.Weapon.CarrySpeedMultiplier(CarryLoadSpec.Default)).Within(1e-5f),
                "the default rule counts no item weight, so only the weapon counts");
        }

        [Test]
        public void WithAimWhileReloadingOffAimingWaitsForTheReloadToEnd()
        {
            var ads = _world.Ads;
            var aim = new InputAction("Aim", InputActionType.Button);
            var attack = new InputAction("Attack", InputActionType.Button);
            var reload = new InputAction("Reload", InputActionType.Button);
            try
            {
                ads.Initialize(_world.Camera, _world.Look, aim);
                _world.Weapon.Initialize(_world.Camera.transform, attack, reload);
                _world.Weapon.Rules = GameRules.Default.WithActions(ActionRules.Default.WithAimWhileReloading(AimWhileReloading.Off));
                Assert.That(_world.Weapon.Fire(), Is.True);
                Assert.That(_world.Weapon.StartReload(), Is.True);

                ads.Tick(true, true, 0.01f);

                Assert.That(ads.AdsBlocker(), Is.True);
                Assert.That(ads.IsAiming, Is.False, "no aiming while the reload runs");
                Assert.That(_world.Weapon.Weapon.IsReloading, Is.True, "pressing Aim does not cancel the reload");

                _world.Weapon.Tick(_world.Weapon.Weapon.Spec.ReloadSeconds + 0.1f);
                LogAssert.Expect(LogType.Log, AimDownSights.FormatAdsLog(true));
                ads.Tick(true, false, 0.01f);

                Assert.That(_world.Weapon.Weapon.IsReloading, Is.False);
                Assert.That(ads.IsAiming, Is.True, "holding Aim through the reload aims once it ends");
            }
            finally
            {
                aim.Dispose();
                attack.Dispose();
                reload.Dispose();
            }
        }

        [Test]
        public void ByDefaultAimingDuringAReloadKeepsTheReload()
        {
            var ads = _world.Ads;
            var aim = new InputAction("Aim", InputActionType.Button);
            var attack = new InputAction("Attack", InputActionType.Button);
            var reload = new InputAction("Reload", InputActionType.Button);
            try
            {
                ads.Initialize(_world.Camera, _world.Look, aim);
                _world.Weapon.Initialize(_world.Camera.transform, attack, reload);
                Assert.That(_world.Weapon.Fire(), Is.True);
                Assert.That(_world.Weapon.StartReload(), Is.True);

                LogAssert.Expect(LogType.Log, AimDownSights.FormatAdsLog(true));
                ads.Tick(true, true, 0.01f);

                Assert.That(ads.IsAiming, Is.True);
                Assert.That(_world.Weapon.Weapon.IsReloading, Is.True);
            }
            finally
            {
                aim.Dispose();
                attack.Dispose();
                reload.Dispose();
            }
        }

        [Test]
        public void WithAimInReloadInterruptedByStartingToAimCancelsTheReload()
        {
            var ads = _world.Ads;
            var aim = new InputAction("Aim", InputActionType.Button);
            var attack = new InputAction("Attack", InputActionType.Button);
            var reload = new InputAction("Reload", InputActionType.Button);
            try
            {
                ads.Initialize(_world.Camera, _world.Look, aim);
                _world.Weapon.Initialize(_world.Camera.transform, attack, reload);

                // Off も一緒に入れて、Aim で取り消す方が優先されることも確かめる。
                _world.Weapon.Rules = GameRules.Default.WithActions(ActionRules.Default
                    .WithAimWhileReloading(AimWhileReloading.Off)
                    .WithReloadInterruptedBy(new[] { ReloadInterrupt.Aim }));
                Assert.That(_world.Weapon.Fire(), Is.True);
                Assert.That(_world.Weapon.StartReload(), Is.True);

                LogAssert.Expect(LogType.Log, AimDownSights.FormatAdsLog(true));
                LogAssert.Expect(LogType.Log, WeaponController.FormatReloadCancelledLog(ReloadInterrupt.Aim));
                ads.Tick(true, true, 0.01f);

                Assert.That(ads.IsAiming, Is.True);
                Assert.That(_world.Weapon.Weapon.IsReloading, Is.False);

                LogAssert.Expect(LogType.Log, AimDownSights.FormatAdsLog(false));
                Assert.That(_world.Weapon.StartReload(), Is.True, "reloading from aim is still allowed");
                Assert.That(ads.IsAiming, Is.False, "the reload lowers the sights");
                ads.Tick(true, false, 0.01f);
                Assert.That(_world.Weapon.Weapon.IsReloading, Is.True, "holding Aim after the reload began does not cancel it");
            }
            finally
            {
                aim.Dispose();
                attack.Dispose();
                reload.Dispose();
            }
        }

        [Test]
        public void InterruptReloadCancelsOnlyForAnActionTheRulesList()
        {
            var attack = new InputAction("Attack", InputActionType.Button);
            var reload = new InputAction("Reload", InputActionType.Button);
            try
            {
                _world.Weapon.Initialize(_world.Camera.transform, attack, reload);
                Assert.That(_world.Weapon.InterruptReload(ReloadInterrupt.Aim), Is.False, "nothing to cancel without a reload");
                Assert.That(_world.Weapon.Fire(), Is.True);
                Assert.That(_world.Weapon.StartReload(), Is.True);

                Assert.That(_world.Weapon.InterruptReload(ReloadInterrupt.Aim), Is.False, "Aim is not in the default list");
                Assert.That(_world.Weapon.Weapon.IsReloading, Is.True);

                LogAssert.Expect(LogType.Log, "[Combat] reload-cancelled by=Melee");
                Assert.That(_world.Weapon.InterruptReload(ReloadInterrupt.Melee), Is.True);
                Assert.That(_world.Weapon.Weapon.IsReloading, Is.False);
            }
            finally
            {
                attack.Dispose();
                reload.Dispose();
            }
        }

        [Test]
        public void AdsZoomsToTheHeldWeaponAndStopsTheSprint()
        {
            var ads = _world.Ads;
            Assert.That(ads, Is.Not.Null);
            Assert.That(ads, Is.SameAs(_world.Player.GetComponent<AimDownSights>()));

            var aim = new InputAction("Aim", InputActionType.Button);
            var attack = new InputAction("Attack", InputActionType.Button);
            var reload = new InputAction("Reload", InputActionType.Button);
            try
            {
                // Edit Mode では Awake が走らないので、実行時に Awake が解決する参照をここで渡す。
                ads.Initialize(_world.Camera, _world.Look, aim);
                _world.Weapon.Initialize(_world.Camera.transform, attack, reload);
                var hip = _world.Camera.fieldOfView;

                Assert.That(_world.Mover.UpdateSprint(Vector2.up, true, true, InputDeviceKind.KeyboardMouse), Is.True);
                LogAssert.Expect(LogType.Log, AimDownSights.FormatAdsLog(true));
                ads.Tick(true, true, 1f);

                Assert.That(_world.Mover.IsSprinting, Is.False, "aiming down sights stops the sprint");
                Assert.That(_world.Mover.UpdateSprint(Vector2.up, true, false, InputDeviceKind.KeyboardMouse), Is.False, "holding Sprint does not resume it");
                Assert.That(_world.Camera.fieldOfView, Is.EqualTo(TestWeapons.RifleAdsFieldOfView).Within(1e-4f));
                Assert.That(_world.Look.SensitivityMultiplier, Is.LessThan(1f));

                LogAssert.Expect(LogType.Log, AimDownSights.FormatAdsLog(false));
                Assert.That(_world.Weapon.SelectNext(), Is.True);
                Assert.That(ads.IsAiming, Is.False, "switching weapons stops aiming");
                ads.Tick(true, false, 1f);
                Assert.That(ads.IsAiming, Is.False, "holding Aim through the switch does not aim again");

                LogAssert.Expect(LogType.Log, AimDownSights.FormatAdsLog(true));
                ads.Tick(true, true, 1f);
                Assert.That(_world.Camera.fieldOfView, Is.EqualTo(TestWeapons.HandgunAdsFieldOfView).Within(1e-4f));

                LogAssert.Expect(LogType.Log, AimDownSights.FormatAdsLog(false));
                ads.Tick(false, false, 1f);
                Assert.That(_world.Camera.fieldOfView, Is.EqualTo(hip).Within(1e-4f));
                Assert.That(_world.Look.SensitivityMultiplier, Is.EqualTo(1f));
            }
            finally
            {
                aim.Dispose();
                attack.Dispose();
                reload.Dispose();
            }
        }

        [Test]
        public void StartingAReloadStopsAToggledAds()
        {
            var ads = _world.Ads;
            var aim = new InputAction("Aim", InputActionType.Button);
            var attack = new InputAction("Attack", InputActionType.Button);
            var reload = new InputAction("Reload", InputActionType.Button);
            try
            {
                ads.Initialize(_world.Camera, _world.Look, aim);
                _world.Weapon.Initialize(_world.Camera.transform, attack, reload);
                ads.ControlSettings = ControlSettings.Default.With(InputDeviceKind.KeyboardMouse, HoldAction.Aim, HoldMode.Toggle);
                var hip = _world.Camera.fieldOfView;

                ads.Tick(true, true, InputDeviceKind.KeyboardMouse, 1f);
                ads.Tick(false, false, InputDeviceKind.KeyboardMouse, 1f);
                Assert.That(ads.IsAiming, Is.True, "a toggled ADS stays on after letting go");

                Assert.That(_world.Weapon.Fire(), Is.True);
                Assert.That(ads.IsAiming, Is.True, "firing does not stop aiming");

                LogAssert.Expect(LogType.Log, AimDownSights.FormatAdsLog(false));
                Assert.That(_world.Weapon.StartReload(), Is.True);
                Assert.That(ads.IsAiming, Is.False, "starting a reload stops aiming");

                ads.Tick(false, false, InputDeviceKind.KeyboardMouse, 1f);
                Assert.That(_world.Camera.fieldOfView, Is.EqualTo(hip).Within(1e-4f));

                Assert.That(_world.Weapon.StartReload(), Is.False, "already reloading");
            }
            finally
            {
                aim.Dispose();
                attack.Dispose();
                reload.Dispose();
            }
        }

        [Test]
        public void DeathAndRespawnReleaseTheCrouchAndSnapTheAdsBack()
        {
            var ads = _world.Ads;
            var aim = new InputAction("Aim", InputActionType.Button);
            try
            {
                ads.Initialize(_world.Camera, _world.Look, aim);
                var hip = _world.Camera.fieldOfView;

                // 初期値ではゲームパッドのしゃがみは Toggle。
                Assert.That(_world.Mover.UpdateCrouch(true, true, false, false, InputDeviceKind.Gamepad), Is.True);
                ads.Tick(true, true, 1f);

                LogAssert.Expect(LogType.Log, CrouchInput.FormatLog(false, HoldMode.Toggle, CrouchInput.ReasonDeath));
                LogAssert.Expect(LogType.Log, AimDownSights.FormatAdsLog(false));
                _world.PlayerDamageable.Kill();

                Assert.That(_world.Mover.IsCrouchActive, Is.False);
                Assert.That(ads.IsAiming, Is.False);
                Assert.That(_world.Camera.fieldOfView, Is.EqualTo(hip), "death snaps the view back to the hip");

                _world.Mover.UpdateCrouch(true, true, false, false, InputDeviceKind.Gamepad);
                LogAssert.Expect(LogType.Log, CrouchInput.FormatLog(false, HoldMode.Toggle, CrouchInput.ReasonRespawn));
                _world.PlayerDamageable.ResetHealth();
                Assert.That(_world.Mover.IsCrouchActive, Is.False);
            }
            finally
            {
                aim.Dispose();
            }
        }

        [Test]
        public void PlacesFiveTargetDummiesInARowInFrontOfThePlayer()
        {
            var targets = _world.Targets;

            Assert.That(targets.Count, Is.EqualTo(GameBootstrap.TargetDummyCount));
            Assert.That(GameBootstrap.TargetDummyCount, Is.EqualTo(5));
            Assert.That(_world.TargetRoot.name, Is.EqualTo(GameBootstrap.TargetsName));

            for (var i = 0; i < targets.Count; i++)
            {
                var dummy = targets[i];
                Assert.That(dummy.transform.parent, Is.SameAs(_world.TargetRoot.transform));
                Assert.That(dummy.name, Is.EqualTo(GameBootstrap.TargetDummyName(i)));
                Assert.That(dummy.Damageable.Id, Is.EqualTo(GameBootstrap.TargetDummyName(i)));
                Assert.That(dummy.Damageable.Health.Max, Is.EqualTo(GameBootstrap.TargetDummyMaxHealth));
                Assert.That(dummy.RespawnDelay, Is.EqualTo(3f));

                var position = dummy.transform.position;
                Assert.That(position.y, Is.EqualTo(0f).Within(1e-4f), "stands on the floor");
                Assert.That(position.z, Is.EqualTo(GameBootstrap.TargetDummyDistance).Within(1e-4f), "in front of the player (+Z)");
                Assert.That(position.z, Is.LessThan(_rifle.Range));
                Assert.That(dummy.HomePosition, Is.EqualTo(position));
            }

            // プレイヤーから見て左 (-X) から等間隔に並び、真ん中が正面に来る。
            for (var i = 1; i < targets.Count; i++)
            {
                var gap = targets[i].transform.position.x - targets[i - 1].transform.position.x;
                Assert.That(gap, Is.EqualTo(GameBootstrap.TargetDummySpacing).Within(1e-4f));
            }

            Assert.That(targets[targets.Count / 2].transform.position.x, Is.EqualTo(0f).Within(1e-4f));
        }

        [Test]
        public void TargetDummiesHaveHeadAndBodyHitboxesOwnedByTheirDamageable()
        {
            foreach (var dummy in _world.Targets)
            {
                var body = dummy.transform.Find(TargetDummy.BodyName).GetComponent<Hitbox>();
                var head = dummy.transform.Find(TargetDummy.HeadName).GetComponent<Hitbox>();
                Assert.That(body.Zone, Is.EqualTo(HitZone.Body));
                Assert.That(head.Zone, Is.EqualTo(HitZone.Head));
                Assert.That(body.Owner, Is.SameAs(dummy.Damageable));
                Assert.That(head.Owner, Is.SameAs(dummy.Damageable));
                Assert.That(body.GetComponent<CapsuleCollider>(), Is.Not.Null);
                Assert.That(head.GetComponent<SphereCollider>(), Is.Not.Null);
            }
        }

        [Test]
        public void ShootingStraightAheadFromTheEyesHitsTheMiddleDummysBody()
        {
            var middle = _world.Targets[_world.Targets.Count / 2];
            Physics.SyncTransforms();

            var eye = _world.Camera.transform.position;
            Assert.That(Hitscan.TryFirstHit(eye, Vector3.forward, _rifle.Range, _world.Player.transform, out var hit), Is.True);

            var hitbox = Hitbox.FindFor(hit.collider);
            Assert.That(hitbox, Is.Not.Null);
            Assert.That(hitbox.Zone, Is.EqualTo(HitZone.Body));
            Assert.That(hitbox.Owner, Is.SameAs(middle.Damageable));
        }

        [Test]
        public void TargetDummiesAreLaidOutAlongThePlayersFacing()
        {
            // 右 (+X) を向いたプレイヤーなら、前方 = +X、左 = +Z になる。
            var positions = GameBootstrap.TargetDummyPositions(Vector3.zero, Quaternion.Euler(0f, 90f, 0f));

            Assert.That(positions, Has.Length.EqualTo(GameBootstrap.TargetDummyCount));
            foreach (var position in positions)
            {
                Assert.That(position.x, Is.EqualTo(GameBootstrap.TargetDummyDistance).Within(1e-4f));
                Assert.That(position.y, Is.EqualTo(0f).Within(1e-4f));
            }

            Assert.That(positions[0].z, Is.GreaterThan(positions[positions.Length - 1].z), "the first one is on the player's left");
        }

        [Test]
        public void TargetDummyNamesCountFromOne()
        {
            Assert.That(GameBootstrap.TargetDummyName(0), Is.EqualTo("TargetDummy1"));
            Assert.That(GameBootstrap.TargetDummyName(4), Is.EqualTo("TargetDummy5"));
        }

        [Test]
        public void TargetDummyPositionsAreTenMetresAheadOfTheFeetTwoMetresApart()
        {
            var positions = GameBootstrap.TargetDummyPositions(new Vector3(1f, 0f, 2f), Quaternion.identity);

            var expected = new[]
            {
                new Vector3(-3f, 0f, 12f),
                new Vector3(-1f, 0f, 12f),
                new Vector3(1f, 0f, 12f),
                new Vector3(3f, 0f, 12f),
                new Vector3(5f, 0f, 12f),
            };
            Assert.That(positions, Has.Length.EqualTo(expected.Length));
            for (var i = 0; i < expected.Length; i++)
            {
                Assert.That(Vector3.Distance(positions[i], expected[i]), Is.LessThan(1e-4f), "dummy " + i + " at " + positions[i]);
            }
        }

        [Test]
        public void TargetDummyPositionsIgnoreThePlayersPitch()
        {
            var level = GameBootstrap.TargetDummyPositions(Vector3.zero, Quaternion.identity);

            // 見上げても見下ろしても水平な前方に並び、真下を向いて前方が無いときは +Z に並ぶ。
            foreach (var pitch in new[] { -30f, 45f, 90f })
            {
                var pitched = GameBootstrap.TargetDummyPositions(Vector3.zero, Quaternion.Euler(pitch, 0f, 0f));
                for (var i = 0; i < level.Length; i++)
                {
                    Assert.That(Vector3.Distance(pitched[i], level[i]), Is.LessThan(1e-4f), "pitch " + pitch + ", dummy " + i);
                }
            }
        }

        [Test]
        public void HoldControlWhileDeadRejectsMissingParts()
        {
            Assert.Throws<System.ArgumentNullException>(() => GameBootstrap.HoldControlWhileDead(null, _world.Mover));
            Assert.Throws<System.ArgumentNullException>(() => GameBootstrap.HoldControlWhileDead(_world.PlayerDamageable, null));
        }

        [Test]
        public void PlayerHasThePresentationAndGrenadeComponentsAndIsTheLocalPlayer()
        {
            var player = _world.Player;

            Assert.That(player.GetComponent<WeaponPresentation>(), Is.Not.Null);
            Assert.That(player.GetComponent<BloodPresenter>(), Is.Not.Null);
            Assert.That(player.GetComponent<CharacterAudio>(), Is.Not.Null);
            Assert.That(player.GetComponent<CharacterAnimator>(), Is.Not.Null);
            Assert.That(player.GetComponent<GrenadeThrower>(), Is.Not.Null);
            Assert.That(_world.PlayerDamageable.IsLocalPlayer, Is.True);
            Assert.That(_world.Targets[0].Damageable.IsLocalPlayer, Is.False);
        }

        [Test]
        public void HudShowsThePlayerHealthAndWaitsForTheMatch()
        {
            var model = _world.HudModel;

            Assert.That(model.CurrentHealth, Is.EqualTo(Damageable.DefaultMaxHealth));
            Assert.That(model.RemainingSeconds, Is.Null, "MatchRunner binds the time when it begins the match");

            _world.PlayerDamageable.TakeDamage(25);
            Assert.That(model.CurrentHealth, Is.EqualTo(75));
        }

        [Test]
        public void HudFollowsTheHealthAfterItIsReset()
        {
            var model = _world.HudModel;
            var damageable = _world.PlayerDamageable;
            damageable.TakeDamage(40);

            damageable.ResetHealth();
            Assert.That(model.CurrentHealth, Is.EqualTo(100));

            damageable.TakeDamage(10);
            Assert.That(model.CurrentHealth, Is.EqualTo(90));
        }

        [Test]
        public void HudFollowsTheWeaponOnceItIsInitialized()
        {
            var model = _world.HudModel;
            var controller = _world.Weapon;

            // Edit Mode では WeaponController の Awake が走らないので、武器はまだ無い。
            Assert.That(controller.Weapon, Is.Null);
            Assert.That(model.MagazineAmmo, Is.Null);

            var attack = new InputAction("Attack", InputActionType.Button);
            var reload = new InputAction("Reload", InputActionType.Button);
            try
            {
                controller.Initialize(_world.Camera.transform, attack, reload);
                Assert.That(model.MagazineAmmo, Is.EqualTo(_rifle.MagazineSize));
                Assert.That(model.IsReserveInfinite, Is.EqualTo(_world.Rules.InfiniteReserve));

                Assert.That(model.WeaponSpec.Id, Is.EqualTo(_rifle.Id));

                Assert.That(controller.Weapon.TryFire(), Is.True);
                Assert.That(model.MagazineAmmo, Is.EqualTo(_rifle.MagazineSize - 1));
                Assert.That(model.IsReserveInfinite, Is.EqualTo(_world.Rules.InfiniteReserve));

                var rifle = controller.Weapon;
                Assert.That(controller.SelectNext(), Is.True);
                Assert.That(model.WeaponSpec.Id, Is.EqualTo(_handgun.Id));
                Assert.That(model.MagazineAmmo, Is.EqualTo(_handgun.MagazineSize));
                Assert.That(model.IsReserveInfinite, Is.EqualTo(_world.Rules.InfiniteReserve));
                rifle.Tick(_rifle.FireInterval);
                rifle.TryFire();
                Assert.That(model.MagazineAmmo, Is.EqualTo(_handgun.MagazineSize));
                Assert.That(model.IsReserveInfinite, Is.EqualTo(_world.Rules.InfiniteReserve));
            }
            finally
            {
                attack.Dispose();
                reload.Dispose();
            }
        }

        [Test]
        public void CreatesDirectionalLight()
        {
            var light = _world.Light;

            Assert.That(light.name, Is.EqualTo(GameBootstrap.LightName));
            Assert.That(light.type, Is.EqualTo(LightType.Directional));
            Assert.That(Quaternion.Angle(light.transform.rotation, Quaternion.Euler(GameBootstrap.LightEuler)), Is.LessThan(1e-3f));
        }
    }
}
