using System;
using System.Collections.Generic;
using Shooter.Core;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    public sealed class DemoPanelSettingsTests
    {
        private static readonly Vector2Int _expectedResolution = new Vector2Int(1920, 1080);
        private const float ExpectedMatch = 0.5f;

        private readonly List<Object> _created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var created in _created)
            {
                if (created != null)
                {
                    Object.DestroyImmediate(created);
                }
            }

            _created.Clear();
            MenuNavigator.TakeReturnReason();
            GameSignals.ResetAll();
        }

        [Test]
        public void CreateScalesWithTheScreenFromTheHudResolution()
        {
            var theme = LoadTheme();

            var settings = Track(DemoPanelSettings.Create("DemoPanelSettingsTests", theme));

            Assert.That(settings.name, Is.EqualTo("DemoPanelSettingsTests"));
            Assert.That(settings.themeStyleSheet, Is.SameAs(theme));
            Assert.That(settings.scaleMode, Is.EqualTo(PanelScaleMode.ScaleWithScreenSize));
            Assert.That(settings.screenMatchMode, Is.EqualTo(PanelScreenMatchMode.MatchWidthOrHeight));
            Assert.That(settings.referenceResolution, Is.EqualTo(_expectedResolution));
            Assert.That(settings.referenceResolution, Is.EqualTo(Hud.ReferenceResolution));
            Assert.That(settings.match, Is.EqualTo(ExpectedMatch));
        }

        [Test]
        public void CreateRejectsAMissingThemeOrName()
        {
            var theme = LoadTheme();

            Assert.Throws<ArgumentNullException>(() => DemoPanelSettings.Create("DemoPanelSettingsTests", null));
            Assert.Throws<ArgumentException>(() => DemoPanelSettings.Create(string.Empty, theme));
            Assert.Throws<ArgumentException>(() => DemoPanelSettings.Create(null, theme));
        }

        [Test]
        public void TheHudAndTheMainMenuUseTheSameSettings()
        {
            var hud = Hud.Create("DemoPanelSettingsTests.Hud");
            Track(hud.gameObject);
            var hudSettings = Track(hud.OwnedPanelSettings);

            var menuObject = Track(new GameObject("DemoPanelSettingsTests.Menu"));
            var menu = menuObject.AddComponent<MainMenuScreen>();
            menu.Show();
            var menuSettings = Track(menu.OwnedPanelSettings);

            Assert.That(hudSettings, Is.Not.Null);
            Assert.That(menuSettings, Is.Not.Null);
            Assert.That(menuSettings, Is.Not.SameAs(hudSettings), "each screen owns and destroys its own settings");
            Assert.That(hudSettings.name, Is.EqualTo(Hud.PanelSettingsName));
            Assert.That(menuSettings.name, Is.EqualTo(MainMenuScreen.PanelSettingsName));

            foreach (var settings in new[] { hudSettings, menuSettings })
            {
                Assert.That(settings.themeStyleSheet, Is.SameAs(LoadTheme()), settings.name);
                Assert.That(settings.scaleMode, Is.EqualTo(PanelScaleMode.ScaleWithScreenSize), settings.name);
                Assert.That(settings.screenMatchMode, Is.EqualTo(PanelScreenMatchMode.MatchWidthOrHeight), settings.name);
                Assert.That(settings.referenceResolution, Is.EqualTo(_expectedResolution), settings.name);
                Assert.That(settings.match, Is.EqualTo(ExpectedMatch), settings.name);
            }
        }

        private static ThemeStyleSheet LoadTheme()
        {
            var theme = Resources.Load<ThemeStyleSheet>(Hud.ThemeResourcePath);
            Assert.That(theme, Is.Not.Null, "Resources/" + Hud.ThemeResourcePath + ".tss is missing");
            return theme;
        }

        private T Track<T>(T created) where T : Object
        {
            _created.Add(created);
            return created;
        }
    }
}
