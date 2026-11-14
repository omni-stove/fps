using Fps.Core;
using Fps.Editor;
using Fps.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Fps.Tests.EditMode
{
    public sealed class GameBootstrapTests
    {
        private GameObject _host;
        private BootstrapWorld _world;
        private WeaponDefinition _rifle;
        private WeaponDefinition _handgun;
        private GameRulesDefinition _rules;

        /// <summary>SetUp で配線するルール。2 秒後から毎秒 10 回復、予備弾は 2 倍で無限。</summary>
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
            WeaponAssetBuilder.Configure(_rifle, WeaponAssetBuilder.RifleSpec());
            _handgun = ScriptableObject.CreateInstance<WeaponDefinition>();
            _handgun.name = "Handgun";
            WeaponAssetBuilder.Configure(_handgun, WeaponAssetBuilder.HandgunSpec());
            _rules = ScriptableObject.CreateInstance<GameRulesDefinition>();
            _rules.name = "TestRules";
            GameRulesAssetBuilder.Configure(_rules, _testRules);

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
            DestroyWorld(_world);
            Object.DestroyImmediate(_host);
            Object.DestroyImmediate(_rifle);
            Object.DestroyImmediate(_handgun);
            Object.DestroyImmediate(_rules);
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
        public void PlayerHasTheActionReaderWiredToTheOverlayAndTheMockBody()
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

            Assert.That(player.GetComponent<ActionDebugOverlay>().Reader, Is.SameAs(reader));
            Assert.That(player.GetComponent<PlayerMockBody>().Reader, Is.SameAs(reader));
        }

        [Test]
        public void ActionDebugOverlayIsSupportedInTheEditorAndStartsVisible()
        {
            var overlay = _world.Player.GetComponent<ActionDebugOverlay>();

            Assert.That(ActionDebugOverlay.IsSupported, Is.True);
            Assert.That(overlay.IsVisible, Is.True);

            overlay.SetVisible(false);
            Assert.That(overlay.IsVisible, Is.False);
            overlay.SetVisible(true);
            Assert.That(overlay.IsVisible, Is.True);
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

                LogAssert.NoUnexpectedReceived();
            }
            finally
            {
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

            // Edit Mode では Hud の OnDestroy が走らないので、PanelSettings もここで消す。
            if (world.Hud != null)
            {
                Object.DestroyImmediate(world.Hud.gameObject);
            }

            if (world.HudPanelSettings != null)
            {
                Object.DestroyImmediate(world.HudPanelSettings);
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
                Assert.That(received, Has.Count.EqualTo(1), "raised only when the world is first built");
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
                bootstrap.ViewMode = ViewMode.ThirdPerson;
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
        public void PlayerCanBeShotAndFoundByEnemies()
        {
            var player = _world.Player;
            var damageable = _world.PlayerDamageable;

            Assert.That(player.CompareTag(EnemyAgent.TargetTag), Is.True);
            Assert.That(damageable, Is.SameAs(player.GetComponent<Damageable>()));
            Assert.That(damageable.Id, Is.EqualTo(GameBootstrap.PlayerName));
            Assert.That(damageable.Health.Max, Is.EqualTo(GameBootstrap.PlayerMaxHealth));

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
                Assert.That(_world.Camera.fieldOfView, Is.EqualTo(WeaponAssetBuilder.RifleAdsFieldOfView).Within(1e-4f));
                Assert.That(_world.Look.SensitivityMultiplier, Is.LessThan(1f));

                LogAssert.Expect(LogType.Log, AimDownSights.FormatAdsLog(false));
                Assert.That(_world.Weapon.SelectNext(), Is.True);
                Assert.That(ads.IsAiming, Is.False, "switching weapons stops aiming");
                ads.Tick(true, false, 1f);
                Assert.That(ads.IsAiming, Is.False, "holding Aim through the switch does not aim again");

                LogAssert.Expect(LogType.Log, AimDownSights.FormatAdsLog(true));
                ads.Tick(true, true, 1f);
                Assert.That(_world.Camera.fieldOfView, Is.EqualTo(WeaponAssetBuilder.HandgunAdsFieldOfView).Within(1e-4f));

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
        public void HudUsesAUIDocumentWithCodeBuiltPanelSettings()
        {
            Assert.That(_world.Hud, Is.InstanceOf<Hud>(), "the default HUD factory builds the HUD");
            var hud = DefaultHud;
            Assert.That(hud.name, Is.EqualTo(GameBootstrap.HudName));
            Assert.That(hud.gameObject.activeSelf, Is.True);

            var document = hud.GetComponent<UIDocument>();
            Assert.That(document, Is.Not.Null);
            Assert.That(document.visualTreeAsset, Is.SameAs(Resources.Load<VisualTreeAsset>(Hud.LayoutResourcePath)));

            var settings = _world.HudPanelSettings;
            Assert.That(document.panelSettings, Is.SameAs(settings));
            Assert.That(settings.name, Is.EqualTo(Hud.PanelSettingsName));
            Assert.That(settings.themeStyleSheet, Is.SameAs(Resources.Load<ThemeStyleSheet>(Hud.ThemeResourcePath)));
            Assert.That(settings.scaleMode, Is.EqualTo(PanelScaleMode.ScaleWithScreenSize));
            Assert.That(settings.referenceResolution, Is.EqualTo(Hud.ReferenceResolution));
        }

        [Test]
        public void HudShowsTheGrenadeCount()
        {
            var grenades = DefaultHud.GetComponent<GrenadeCountView>();

            Assert.That(grenades, Is.Not.Null, "GameBootstrap attaches the grenade count through IHud");
            Assert.That(grenades.Source, Is.SameAs(_world.Grenades));
        }

        [Test]
        public void HudShowsTheAbilitySlots()
        {
            var slots = DefaultHud.GetComponent<AbilitySlotsView>();

            Assert.That(_world.Abilities, Is.Not.Null, "PlayerActions.Attach gives the player an AbilityHost");
            Assert.That(slots, Is.Not.Null, "GameBootstrap attaches the ability slots through IHud");
            Assert.That(slots.Source, Is.SameAs(_world.Abilities));
            Assert.That(slots.IsShown, Is.False, "the default rules keep abilities off and no ability is equipped");
        }

        [Test]
        public void HudHasThePartViewsBoundToTheLayout()
        {
            var hud = DefaultHud;
            HudElementView[] views = { hud.HitMarker, hud.DamageDirection, hud.KillFeed, hud.Scoreboard, hud.LoadoutSelect, hud.TutorialPrompt };

            Assert.That(views, Has.None.Null);
            Assert.That(hud.PauseMenu, Is.Not.Null);
            Assert.That(hud.PauseMenu.TimeMode, Is.EqualTo(PauseTimeMode.Freeze), "offline pauses stop time");

            // Edit Mode では UIDocument が木を作らないことがあり、そのときは View もビューの要素も無い。
            Assume.That(hud.View, Is.Not.Null, "the UIDocument built no visual tree in Edit Mode");
            foreach (var view in views)
            {
                Assert.That(view.Element, Is.Not.Null, view.ElementName);
                Assert.That(view.Element.name, Is.EqualTo(view.ElementName));
                Assert.That(view.Element.style.display.value, Is.EqualTo(DisplayStyle.None), "nothing is shown until something happens");
            }
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
            var model = DefaultHud.Model;

            Assert.That(model.HealthText, Is.EqualTo(HudFormat.Health(GameBootstrap.PlayerMaxHealth)));
            Assert.That(model.TimeText, Is.EqualTo(HudFormat.NoTime), "MatchRunner binds the time when it begins the match");

            _world.PlayerDamageable.TakeDamage(25);
            Assert.That(model.HealthText, Is.EqualTo("75"));
        }

        [Test]
        public void HudFollowsTheHealthAfterItIsReset()
        {
            var model = DefaultHud.Model;
            var damageable = _world.PlayerDamageable;
            damageable.TakeDamage(40);

            damageable.ResetHealth();
            Assert.That(model.HealthText, Is.EqualTo("100"));

            damageable.TakeDamage(10);
            Assert.That(model.HealthText, Is.EqualTo("90"));
        }

        [Test]
        public void HudFollowsTheWeaponOnceItIsInitialized()
        {
            var model = DefaultHud.Model;
            var controller = _world.Weapon;

            // Edit Mode では WeaponController の Awake が走らないので、武器はまだ無い。
            Assert.That(controller.Weapon, Is.Null);
            Assert.That(model.AmmoText, Is.EqualTo(HudFormat.NoAmmo));

            var attack = new InputAction("Attack", InputActionType.Button);
            var reload = new InputAction("Reload", InputActionType.Button);
            try
            {
                controller.Initialize(_world.Camera.transform, attack, reload);
                Assert.That(model.AmmoText, Is.EqualTo(HudFormat.Ammo(_rifle.MagazineSize, _rifle.ReserveAmmo)));

                Assert.That(model.WeaponNameText, Is.EqualTo(Localizer.Resolve(new LocalizedText(_rifle.NameKey))));
                Assert.That(model.WeaponNameText, Is.Not.EqualTo(_rifle.NameKey), "the HUD shows the text from the table, not the key");

                Assert.That(controller.Weapon.TryFire(), Is.True);
                Assert.That(model.AmmoText, Is.EqualTo(HudFormat.Ammo(_rifle.MagazineSize - 1, _rifle.ReserveAmmo)));

                // 持ち替えると HUD は新しい武器を購読し直し、しまった武器の射撃では変わらない。
                var rifle = controller.Weapon;
                Assert.That(controller.SelectNext(), Is.True);
                Assert.That(model.WeaponNameText, Is.EqualTo(Localizer.Resolve(new LocalizedText(_handgun.NameKey))));
                Assert.That(model.AmmoText, Is.EqualTo(HudFormat.Ammo(_handgun.MagazineSize, _handgun.ReserveAmmo)));
                rifle.Tick(_rifle.FireInterval);
                rifle.TryFire();
                Assert.That(model.AmmoText, Is.EqualTo(HudFormat.Ammo(_handgun.MagazineSize, _handgun.ReserveAmmo)));
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

        [Test]
        public void BuildFailsWithoutAHudFactoryAndLeavesNothingBehind()
        {
            GameUi.HudFactory = new NullHudFactory();
            var bootstrap = new GameObject("GameBootstrapTests.NoHud").AddComponent<GameBootstrap>();
            try
            {
                var ex = Assert.Throws<System.InvalidOperationException>(() => bootstrap.Build());
                Assert.That(ex.Message, Does.Contain("returned no HUD"));
                Assert.That(bootstrap.World, Is.Null);
            }
            finally
            {
                GameUi.HudFactory = null;
                Object.DestroyImmediate(bootstrap.gameObject);
            }

            Assert.That(GameUi.HudFactory, Is.InstanceOf<DefaultHudFactory>(), "clearing the replacement goes back to the default");
        }

        // 中身を確かめるため、既定の生成役が作った HUD を具体クラスで読む
        private Hud DefaultHud => (Hud)_world.Hud;

        private sealed class NullHudFactory : IHudFactory
        {
            public IHud Create(string objectName, Damageable player, WeaponController weapon, Match match)
            {
                return null;
            }

            public void AttachActionDebug(GameObject player, PlayerActionReader reader)
            {
            }
        }
    }
}
