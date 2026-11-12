using Fps.Core;
using Fps.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Fps.Tests.EditMode
{
    public sealed class GameBootstrapTests
    {
        private GameObject _host;
        private BootstrapWorld _world;

        [SetUp]
        public void SetUp()
        {
            _host = new GameObject("Bootstrap");
            var bootstrap = _host.AddComponent<GameBootstrap>();
            LogAssert.Expect(LogType.Log, GameBootstrap.ReadyLog);
            _world = bootstrap.Build();
        }

        [TearDown]
        public void TearDown()
        {
            DestroyWorld(_world);
            Object.DestroyImmediate(_host);
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

            Object.DestroyImmediate(world.Match.gameObject);
            if (world.Floor != null)
            {
                Object.DestroyImmediate(world.Floor);
            }

            Object.DestroyImmediate(world.Player);
            Object.DestroyImmediate(world.Light.gameObject);
            Object.DestroyImmediate(world.Target.gameObject);
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
        public void CameraIsAChildAtEyeHeight()
        {
            var camera = _world.Camera;

            Assert.That(camera, Is.Not.Null);
            Assert.That(camera.name, Is.EqualTo(GameBootstrap.CameraName));
            Assert.That(camera.transform.parent, Is.SameAs(_world.Player.transform));
            Assert.That(camera.transform.localPosition, Is.EqualTo(GameBootstrap.EyeOffset));
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
            Assert.That(mover.JumpHeight, Is.EqualTo(PlayerController.DefaultJumpHeight));
            Assert.That(mover.Gravity, Is.EqualTo(PlayerController.DefaultGravity));
            Assert.That(mover.AirControl, Is.EqualTo(PlayerController.DefaultAirControl));
        }

        [Test]
        public void WeaponIsOnTheCamera()
        {
            Assert.That(_world.Weapon, Is.Not.Null);
            Assert.That(_world.Weapon, Is.SameAs(_world.Camera.GetComponent<WeaponController>()));
        }

        [Test]
        public void TargetHasHeadAndBodyHitboxesOwnedByItsDamageable()
        {
            var target = _world.Target;

            Assert.That(target.name, Is.EqualTo(GameBootstrap.TargetName));
            Assert.That(target.transform.position, Is.EqualTo(GameBootstrap.TargetPosition));

            var body = target.transform.Find(GameBootstrap.TargetBodyName).GetComponent<Hitbox>();
            var head = target.transform.Find(GameBootstrap.TargetHeadName).GetComponent<Hitbox>();
            Assert.That(body.Zone, Is.EqualTo(HitZone.Body));
            Assert.That(head.Zone, Is.EqualTo(HitZone.Head));
            Assert.That(body.Owner, Is.SameAs(target));
            Assert.That(head.Owner, Is.SameAs(target));
            Assert.That(body.GetComponent<Collider>(), Is.Not.Null);
            Assert.That(head.GetComponent<Collider>(), Is.Not.Null);
        }

        [Test]
        public void ShootingStraightAheadFromTheEyesHitsTheTargetBody()
        {
            // カメラは identity 回転 (+Z 向き) で、目の高さは胴体のカプセルの範囲にある。
            var eyeY = GameBootstrap.PlayerSpawn.y + GameBootstrap.EyeOffset.y;
            var bodyTop = GameBootstrap.TargetPosition.y + GameBootstrap.TargetBodyLocalPosition.y + GameBootstrap.TargetBodyScale.y;
            var headBottom = GameBootstrap.TargetPosition.y + GameBootstrap.TargetHeadLocalPosition.y - GameBootstrap.TargetHeadScale.y / 2f;

            Assert.That(eyeY, Is.LessThan(bodyTop));
            Assert.That(eyeY, Is.LessThan(headBottom));
            Assert.That(GameBootstrap.TargetPosition.z, Is.LessThan(WeaponController.DefaultRange));
        }

        [Test]
        public void CreatesASoloMatchThatWaitsForPlay()
        {
            var runner = _world.Match;

            Assert.That(runner, Is.Not.Null);
            Assert.That(runner.name, Is.EqualTo(GameBootstrap.MatchName));
            Assert.That(runner.Match.State, Is.EqualTo(GameModeState.Waiting), "the match starts in Start, which only runs in Play Mode");
            Assert.That(runner.Match.RemainingSeconds, Is.EqualTo(SoloMatchRunner.DefaultTimeLimitSeconds));
            Assert.That(runner.Match.KillsToWin, Is.EqualTo(SoloMatchRunner.DefaultKillsToWin));
        }

        [Test]
        public void KillingTheTargetWinsTheMatch()
        {
            var match = _world.Match.Match;
            match.Start();
            LogAssert.Expect(LogType.Log, $"{Damageable.LogTag} target died id={GameBootstrap.TargetName}");
            LogAssert.Expect(LogType.Log, $"{SoloMatchRunner.LogTag} solo ended result={MatchResult.Won} kills=1");

            _world.Target.Kill();

            Assert.That(match.Result, Is.EqualTo(MatchResult.Won));
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
        public void HudShowsThePlayerHealthAndTheMatchTime()
        {
            var model = _world.Hud.Model;

            Assert.That(model.HealthText, Is.EqualTo(HudFormat.Health(GameBootstrap.PlayerMaxHealth)));
            Assert.That(model.TimeText, Is.EqualTo(HudFormat.Time(SoloMatchRunner.DefaultTimeLimitSeconds)));

            _world.PlayerDamageable.TakeDamage(25);
            Assert.That(model.HealthText, Is.EqualTo("75"));

            var match = _world.Match.Match;
            match.Start();
            match.Tick(61f);
            Assert.That(model.TimeText, Is.EqualTo("01:59"));
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
                Assert.That(model.AmmoText, Is.EqualTo(HudFormat.Ammo(WeaponController.DefaultMagazineSize, WeaponController.DefaultReserveAmmo)));

                Assert.That(controller.Weapon.TryFire(), Is.True);
                Assert.That(model.AmmoText, Is.EqualTo(HudFormat.Ammo(WeaponController.DefaultMagazineSize - 1, WeaponController.DefaultReserveAmmo)));
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
