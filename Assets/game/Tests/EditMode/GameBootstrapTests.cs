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

        [SetUp]
        public void SetUp()
        {
            _rifle = ScriptableObject.CreateInstance<WeaponDefinition>();
            _rifle.name = "Rifle";
            WeaponAssetBuilder.Configure(_rifle, WeaponAssetBuilder.RifleSpec());
            _handgun = ScriptableObject.CreateInstance<WeaponDefinition>();
            _handgun.name = "Handgun";
            WeaponAssetBuilder.Configure(_handgun, WeaponAssetBuilder.HandgunSpec());

            _host = new GameObject("Bootstrap");
            var bootstrap = _host.AddComponent<GameBootstrap>();
            bootstrap.WeaponDefinitions = new[] { _rifle, _handgun };
            LogAssert.Expect(LogType.Log, GameBootstrap.ReadyLog);
            _world = bootstrap.Build();
        }

        [TearDown]
        public void TearDown()
        {
            DestroyWorld(_world);
            Object.DestroyImmediate(_host);
            Object.DestroyImmediate(_rifle);
            Object.DestroyImmediate(_handgun);
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

                Assert.That(_world.Weapon.SelectNext(), Is.True);
                ads.Tick(true, false, 0f);
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
            var hud = _world.Hud;
            Assert.That(hud, Is.Not.Null);
            Assert.That(hud.name, Is.EqualTo(GameBootstrap.HudName));
            Assert.That(hud.gameObject.activeSelf, Is.True);

            var document = hud.GetComponent<UIDocument>();
            Assert.That(document, Is.Not.Null);
            Assert.That(document.visualTreeAsset, Is.SameAs(Resources.Load<VisualTreeAsset>(Hud.LayoutResourcePath)));

            var settings = _world.HudPanelSettings;
            Assert.That(document.panelSettings, Is.SameAs(settings));
            Assert.That(settings.name, Is.EqualTo(GameBootstrap.HudPanelSettingsName));
            Assert.That(settings.themeStyleSheet, Is.SameAs(Resources.Load<ThemeStyleSheet>(Hud.ThemeResourcePath)));
            Assert.That(settings.scaleMode, Is.EqualTo(PanelScaleMode.ScaleWithScreenSize));
            Assert.That(settings.referenceResolution, Is.EqualTo(GameBootstrap.HudReferenceResolution));
        }

        [Test]
        public void HudShowsThePlayerHealthAndWaitsForTheMatch()
        {
            var model = _world.Hud.Model;

            Assert.That(model.HealthText, Is.EqualTo(HudFormat.Health(GameBootstrap.PlayerMaxHealth)));
            Assert.That(model.TimeText, Is.EqualTo(HudFormat.NoTime), "MatchRunner binds the time when it begins the match");

            _world.PlayerDamageable.TakeDamage(25);
            Assert.That(model.HealthText, Is.EqualTo("75"));
        }

        [Test]
        public void HudFollowsTheHealthAfterItIsReset()
        {
            var model = _world.Hud.Model;
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
            var model = _world.Hud.Model;
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

                Assert.That(model.WeaponNameText, Is.EqualTo(_rifle.DisplayName));

                Assert.That(controller.Weapon.TryFire(), Is.True);
                Assert.That(model.AmmoText, Is.EqualTo(HudFormat.Ammo(_rifle.MagazineSize - 1, _rifle.ReserveAmmo)));

                // 持ち替えると HUD は新しい武器を購読し直し、しまった武器の射撃では変わらない。
                var rifle = controller.Weapon;
                Assert.That(controller.SelectNext(), Is.True);
                Assert.That(model.WeaponNameText, Is.EqualTo(_handgun.DisplayName));
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
    }
}
