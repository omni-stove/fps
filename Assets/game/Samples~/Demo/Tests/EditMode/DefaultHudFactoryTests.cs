using Shooter.Core;
using Shooter.Editor;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    /// <summary>
    /// 既定の HUD の作り役 (<see cref="DefaultHudFactory"/>) が、Bootstrap の出した自分のプレイヤーと HUD の値に HUD を 1 つ作り、
    /// ビューとデバッグ表示 (<see cref="ActionDebugOverlay"/>) をそのプレイヤーにつなぎ、外すか切ると片付けることを確かめる。
    /// </summary>
    public sealed class DefaultHudFactoryTests
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
            WeaponAssets.Configure(_rifle, TestWeapons.RifleSpec());
            _handgun = ScriptableObject.CreateInstance<WeaponDefinition>();
            _handgun.name = "Handgun";
            WeaponAssets.Configure(_handgun, TestWeapons.HandgunSpec());
            _rules = ScriptableObject.CreateInstance<GameRulesDefinition>();
            _rules.name = "TestRules";
            GameRulesAssets.Configure(_rules, _testRules);

            GameSignals.ResetAll();
            DefaultHudFactory.Register();
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
            DefaultHudFactory.Unregister();
            DestroyWorld(_world);
            Object.DestroyImmediate(_host);
            Object.DestroyImmediate(_rifle);
            Object.DestroyImmediate(_handgun);
            Object.DestroyImmediate(_rules);
            GameSignals.ResetAll();
        }

        [Test]
        public void TheHudShowsThePublishedModel()
        {
            Assert.That(DefaultHud, Is.Not.Null);
            Assert.That(DefaultHud.ShownModel, Is.SameAs(_world.HudModel));
        }

        [Test]
        public void ClearingTheBootstrapSignalsTakesTheHudAway()
        {
            _host.GetComponent<GameBootstrap>().ClearSignals();

            Assert.That(DefaultHudFactory.Current, Is.Null, "the default HUD goes with the player and its model");
        }

        [Test]
        public void TheActionDebugOverlayReadsThePlayersActionReader()
        {
            Assert.That(_world.Player.GetComponent<ActionDebugOverlay>().Reader, Is.SameAs(_world.ActionReader));
        }

        [Test]
        public void TheDefaultHudCarriesTheGrenadeAbilityAndDominationViewsFromTheStart()
        {
            var hud = DefaultHud;

            Assert.That(hud.GrenadeCount, Is.Not.Null);
            Assert.That(hud.AbilitySlots, Is.Not.Null);
            Assert.That(hud.Domination, Is.Not.Null);

            Assert.That(hud.GetComponents<GrenadeCountView>(), Has.Length.EqualTo(1));
            Assert.That(hud.GetComponents<AbilitySlotsView>(), Has.Length.EqualTo(1));
            Assert.That(hud.GetComponents<DominationView>(), Has.Length.EqualTo(1));
            Assert.That(hud.Domination.Source, Is.Null, "nothing to show until a domination match begins");
        }

        [Test]
        public void HudUsesAUIDocumentWithCodeBuiltPanelSettings()
        {
            var hud = DefaultHud;
            Assert.That(hud, Is.Not.Null, "the default HUD factory builds the HUD for the published player");
            Assert.That(hud.name, Is.EqualTo(DefaultHudFactory.HudObjectName));
            Assert.That(hud.gameObject.activeSelf, Is.True);

            var document = hud.GetComponent<UIDocument>();
            Assert.That(document, Is.Not.Null);
            Assert.That(document.visualTreeAsset, Is.SameAs(Resources.Load<VisualTreeAsset>(Hud.LayoutResourcePath)));

            var settings = hud.OwnedPanelSettings;
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

            Assert.That(grenades, Is.Not.Null, "the default HUD carries the grenade count from the start");
            Assert.That(grenades.Source, Is.SameAs(_world.Grenades), "the view reads the published local player");
        }

        [Test]
        public void HudShowsTheAbilitySlots()
        {
            var slots = DefaultHud.GetComponent<AbilitySlotsView>();

            Assert.That(_world.Abilities, Is.Not.Null, "PlayerActions.Attach gives the player an AbilityHost");
            Assert.That(slots, Is.Not.Null, "the default HUD carries the ability slots from the start");
            Assert.That(slots.Source, Is.SameAs(_world.Abilities), "the view reads the published local player");
            Assert.That(slots.IsShown, Is.False, "the default rules keep abilities off and no ability is equipped");
        }

        [Test]
        public void ClearingTheLocalPlayerEmptiesTheGrenadeCountAndTheAbilitySlots()
        {
            GameSignals.LocalPlayer.Clear();

            Assert.That(DefaultHud, Is.Not.Null, "the HUD stays while its model is published");

            Assert.That(DefaultHud.GrenadeCount.Source, Is.Null);
            Assert.That(DefaultHud.GrenadeCount.Text, Is.Empty);
            Assert.That(DefaultHud.AbilitySlots.Source, Is.Null);
            Assert.That(DefaultHud.AbilitySlots.IsShown, Is.False);
        }

        [Test]
        public void HudHasThePartViewsBoundToTheLayout()
        {
            var hud = DefaultHud;
            HudElementView[] views =
            {
                hud.HitMarker, hud.DamageDirection, hud.KillFeed, hud.Scoreboard, hud.LoadoutSelect, hud.TutorialPrompt, hud.AbilitySlots, hud.Domination,
            };

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
        public void ANewPlayerKeepsTheSameHudAndMovesItsViewsToThatPlayer()
        {
            var hud = DefaultHud;
            var other = new GameObject("GameBootstrapTests.Other").AddComponent<GameBootstrap>();
            other.CreatesFloor = false;
            other.WeaponDefinitions = new[] { _rifle, _handgun };
            other.RulesDefinition = _rules;
            BootstrapWorld otherWorld = null;
            try
            {
                LogAssert.Expect(LogType.Log, GameBootstrap.ReadyLog);
                otherWorld = other.Build();

                Assert.That(DefaultHudFactory.Current, Is.SameAs(hud), "one HUD for the screen");
                Assert.That(Object.FindObjectsByType<Hud>(FindObjectsInactive.Include), Has.Length.EqualTo(1));
                Assert.That(hud.ShownModel, Is.SameAs(otherWorld.HudModel));
                Assert.That(hud.GrenadeCount.Source, Is.SameAs(otherWorld.Grenades));
                Assert.That(otherWorld.Player.GetComponent<ActionDebugOverlay>().Reader, Is.SameAs(otherWorld.ActionReader));
            }
            finally
            {
                DestroyWorld(otherWorld);
                Object.DestroyImmediate(other.gameObject);
            }
        }

        [Test]
        public void UnregisteringTheDefaultHudRemovesItAndStopsBuildingIt()
        {
            var hud = DefaultHud;
            var settings = hud.OwnedPanelSettings;

            DefaultHudFactory.Unregister();

            Assert.That(hud == null, Is.True, "the HUD is destroyed");
            Assert.That(settings == null, Is.True, "with the panel settings made for it");
            Assert.That(DefaultHudFactory.Current, Is.Null);

            GameSignals.LocalPlayer.Publish(GameSignals.LocalPlayer.Value);
            Assert.That(DefaultHudFactory.Current, Is.Null, "nothing builds a HUD once unregistered");

            DefaultHudFactory.Register();
            Assert.That(DefaultHudFactory.Current, Is.Not.Null, "registering again builds it for the published player");
        }

        [Test]
        public void TurningTheDefaultHudOffRemovesItUntilItIsTurnedOnAgain()
        {
            var hud = DefaultHud;
            try
            {
                DefaultHudFactory.Enabled = false;
                Assert.That(hud == null, Is.True, "turning it off takes the HUD away");
                Assert.That(DefaultHudFactory.Current, Is.Null);

                GameSignals.LocalPlayer.Publish(GameSignals.LocalPlayer.Value);
                Assert.That(DefaultHudFactory.Current, Is.Null, "a game with its own HUD gets no default HUD");

                DefaultHudFactory.Unregister();
                DefaultHudFactory.Register();
                Assert.That(DefaultHudFactory.Current, Is.Null, "registering again keeps it off");
            }
            finally
            {
                DefaultHudFactory.Enabled = true;
            }

            Assert.That(DefaultHudFactory.Current, Is.Not.Null, "turning it on builds it for the published player");
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

        // 中身を確かめるため、既定の作り役が作った HUD を具体クラスで読む
        private static Hud DefaultHud => DefaultHudFactory.Current;
    }
}
