using Fps.Core;
using Fps.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Fps.Tests.EditMode
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
            GameUi.HudFactory = null;
            GameUi.Pause = null;
            GameUi.LoadoutSelect = null;
            GameUi.Menu = null;
            _defaultPause = GameUi.Pause;
            _defaultLoadoutSelect = GameUi.LoadoutSelect;
            _defaultMenu = GameUi.Menu;
            GameUi.ClearDefaults();

            _host = new GameObject("NoDefaultUiTests");
            var bootstrap = _host.AddComponent<GameBootstrap>();
            LogAssert.Expect(LogType.Log, GameBootstrap.NoHudLog);
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

            GameUi.UseDefaultHudFactory(new DefaultHudFactory());
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
            Assert.That(GameUi.HudFactory, Is.Null);
            Assert.That(_world.Hud, Is.Null);
            Assert.That(_world.HudPanelSettings, Is.Null);
            Assert.That(GameUi.FindHud(), Is.Null);
            Assert.That(GameObject.Find(GameBootstrap.HudName), Is.Null);

            Assert.That(_world.Player, Is.Not.Null);
            Assert.That(_world.Weapon, Is.Not.Null);
            Assert.That(_world.ActionReader, Is.Not.Null);
            Assert.That(_world.Player.GetComponent<ActionDebugOverlay>(), Is.Null, "no factory attaches the debug overlay");
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

            Assert.That(match.Result, Is.EqualTo(MatchResult.Won));
            Assert.That(_world.Mover.enabled, Is.False);
            Assert.That(_world.Weapon.enabled, Is.False);
        }

        [Test]
        public void ARegisteredFactoryThatReturnsNothingStillFails()
        {
            GameUi.HudFactory = new NullHudFactory();
            try
            {
                Assert.Throws<System.InvalidOperationException>(() => GameUi.CreateHud("NoDefaultUiTests.Hud", null, null, null));
            }
            finally
            {
                GameUi.HudFactory = null;
            }

            Assert.That(GameUi.CreateHud("NoDefaultUiTests.Hud", null, null, null), Is.Null);
        }

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
