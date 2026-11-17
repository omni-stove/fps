using Shooter.Core;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    public sealed class NoDefaultUiTests
    {
        private IPauseState _defaultPause;
        private ILoadoutSelect _defaultLoadoutSelect;
        private IMenuNavigator _defaultMenu;
        private GameObject _host;
        private BootstrapWorld _world;

        [SetUp]
        public void SetUp()
        {
            GameSignals.ResetAll();
            GameUi.Pause = null;
            GameUi.LoadoutSelect = null;
            GameUi.Menu = null;
            _defaultPause = GameUi.Pause;
            _defaultLoadoutSelect = GameUi.LoadoutSelect;
            _defaultMenu = GameUi.Menu;
            GameUi.ClearDefaults();

            _host = new GameObject("NoDefaultUiTests");
            var bootstrap = _host.AddComponent<GameBootstrap>();
            LogAssert.Expect(LogType.Log, GameBootstrap.ReadyLog);
            _world = bootstrap.Build();
        }

        [TearDown]
        public void TearDown()
        {
            if (_world != null)
            {
                Object.DestroyImmediate(_world.Floor);
                Object.DestroyImmediate(_world.Player);
                Object.DestroyImmediate(_world.Light.gameObject);
                Object.DestroyImmediate(_world.TargetRoot);
            }

            Object.DestroyImmediate(_host);
            GameSignals.ResetAll();

            if (_defaultPause != null)
            {
                GameUi.UseDefaultPause(_defaultPause);
            }

            if (_defaultLoadoutSelect != null)
            {
                GameUi.UseDefaultLoadoutSelect(_defaultLoadoutSelect);
            }

            if (_defaultMenu != null)
            {
                GameUi.UseDefaultMenu(_defaultMenu);
            }
        }

        [Test]
        public void BootstrapBuildsTheWorldWithoutAHud()
        {
            Assert.That(Object.FindObjectsByType<UIDocument>(FindObjectsInactive.Include), Is.Empty, "the kit builds no screen of its own");

            Assert.That(_world.Player, Is.Not.Null);
            Assert.That(_world.Weapon, Is.Not.Null);
            Assert.That(_world.ActionReader, Is.Not.Null);
        }

        [Test]
        public void TheBootstrapStillPublishesWhatAUiWouldShow()
        {
            Assert.That(GameSignals.LocalPlayer.Value, Is.Not.Null);
            Assert.That(GameSignals.LocalPlayer.Value.Player, Is.SameAs(_world.Player));
            Assert.That(GameSignals.LocalPlayer.Value.Actions, Is.SameAs(_world.ActionReader));
            Assert.That(GameSignals.HudModel.Value, Is.SameAs(_world.HudModel));
            Assert.That(_world.HudModel.CurrentHealth, Is.EqualTo(_world.PlayerDamageable.Health.Current), "the model follows the player without a HUD");
            Assert.That(GameSignals.HudDensity.Value, Is.EqualTo((HudDensity?)_world.Rules.HudDensity));
            Assert.That(GameSignals.PauseTimeMode.Value, Is.EqualTo((PauseTimeMode?)PauseTimeMode.Freeze));
        }

        [Test]
        public void ScreensAreClosedWhenNothingIsRegistered()
        {
            Assert.That(GameUi.Pause, Is.Null);
            Assert.That(GameUi.LoadoutSelect, Is.Null);
            Assert.That(GameUi.Menu, Is.Null);
            Assert.That(GameUi.IsPauseOpen, Is.False);
            Assert.That(GameUi.IsLoadoutSelectOpen, Is.False);
        }

        [Test]
        public void MatchPlaysToTheEndWithoutAHud()
        {
            var runner = _host.AddComponent<MatchRunner>();
            var match = new Match(30f, 2, MatchRunner.PlayerId);
            LogAssert.Expect(LogType.Log, "[Match] state=Playing kills=0 remaining=30.0");
            runner.Begin(_world, match);

            _world.Targets[0].Damageable.Kill();
            LogAssert.Expect(LogType.Log, "[Match] state=Won kills=2 remaining=30.0");
            _world.Targets[1].Damageable.Kill();

            Assert.That(match.Result, Is.EqualTo(MatchOutcome.Won));
            Assert.That(_world.HudModel.SoloResult, Is.EqualTo(MatchOutcome.Won), "the result reaches the model even with no HUD");
            Assert.That(_world.Mover.enabled, Is.False);
            Assert.That(_world.Weapon.enabled, Is.False);
        }
    }
}
