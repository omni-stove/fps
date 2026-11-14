using System.Collections.Generic;
using System.Text.RegularExpressions;
using Fps.Core;
using Fps.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Fps.Tests.EditMode
{
    /// <summary>
    /// <see cref="DamageVignetteView"/> が、被弾で画面端の画像を出して時間で消し、低体力の始まりと終わりをログに出すことと、
    /// <see cref="DamageFeedbackCatalog"/> の検査を確かめる。
    /// Edit Mode では MonoBehaviour の OnEnable が走らずイベントを購読しないので、ShowHit・SetHealthFraction・Tick を直接呼ぶ。
    /// </summary>
    public sealed class DamageVignetteViewTests
    {
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
        }

        [Test]
        public void TheLayoutHasTheVignetteBehindEverythingElse()
        {
            var root = CloneLayout();

            var vignette = root.Q<VisualElement>(DamageVignetteView.UxmlName);
            Assert.That(vignette, Is.Not.Null);
            Assert.That(vignette.parent.IndexOf(vignette), Is.Zero, "drawn first so the HUD values stay readable over it");
            Assert.That(vignette.pickingMode, Is.EqualTo(PickingMode.Ignore));
        }

        [Test]
        public void TheHudFactoryAttachesTheVignette()
        {
            var hud = Hud.Create("TestVignetteHud", null, null, null);
            _created.Add(hud.gameObject);
            _created.Add(hud.OwnedPanelSettings);

            Assert.That(hud.DamageVignette, Is.Not.Null);
        }

        [Test]
        public void AHitShowsTheVignetteAndFadesItOut()
        {
            var view = CreateView(out var root);
            view.Catalog = CreateCatalog();
            view.Bind(root);
            var element = view.Element;

            Assert.That(element.style.display.value, Is.EqualTo(DisplayStyle.None), "nothing before a hit");

            LogAssert.Expect(LogType.Log, new Regex(Regex.Escape(DamageVignetteView.LogTag) + " hit amount=50 intensity=0\\.80"));
            view.ShowHit(50);

            Assert.That(element.style.display.value, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(element.style.opacity.value, Is.EqualTo(0.8f).Within(1e-4f));
            Assert.That(element.style.backgroundImage.value.texture, Is.SameAs(view.Catalog.Vignette));

            var fade = view.Feedback.Settings.HitFadeSeconds;
            view.Tick(fade * 0.5f);
            Assert.That(view.Intensity, Is.EqualTo(0.4f).Within(1e-4f));
            Assert.That(element.style.opacity.value, Is.EqualTo(0.4f).Within(1e-4f));

            view.Tick(fade * 0.5f);
            Assert.That(view.Intensity, Is.Zero, "the effect returns to nothing over time");
            Assert.That(element.style.display.value, Is.EqualTo(DisplayStyle.None));
        }

        [Test]
        public void LowHealthKeepsTheVignetteUntilRecovery()
        {
            var view = CreateView(out var root);
            view.Catalog = CreateCatalog();
            view.Bind(root);
            var element = view.Element;

            LogAssert.Expect(LogType.Log, new Regex(Regex.Escape(DamageVignetteView.LogTag) + " low health start fraction=0\\.20 threshold=0\\.25"));
            view.SetHealthFraction(0.2f);

            Assert.That(view.IsLowHealth, Is.True);
            Assert.That(element.style.display.value, Is.EqualTo(DisplayStyle.Flex));
            view.Tick(10f);
            Assert.That(element.style.display.value, Is.EqualTo(DisplayStyle.Flex), "the red stays while the health is low");
            Assert.That(view.IsHeartbeatPlaying, Is.False, "the catalog has no heartbeat, so nothing plays and nothing fails");

            LogAssert.Expect(LogType.Log, new Regex(Regex.Escape(DamageVignetteView.LogTag) + " low health end reason=recovered fraction=0\\.60"));
            view.SetHealthFraction(0.6f);

            Assert.That(view.IsLowHealth, Is.False);
            Assert.That(element.style.display.value, Is.EqualTo(DisplayStyle.None));
        }

        [Test]
        public void FallingWhileLowEndsWithoutRecovering()
        {
            var view = CreateView(out var root);
            view.Catalog = CreateCatalog();
            view.Bind(root);

            LogAssert.Expect(LogType.Log, new Regex(Regex.Escape(DamageVignetteView.LogTag) + " low health start"));
            view.SetHealthFraction(0.1f);
            LogAssert.Expect(LogType.Log, new Regex(Regex.Escape(DamageVignetteView.LogTag) + " low health end reason=down"));
            view.SetHealthFraction(0f);

            Assert.That(view.IsLowHealth, Is.False);
        }

        [Test]
        public void WithoutACatalogNothingIsDrawnAndNothingFails()
        {
            var view = CreateView(out var root);
            view.Catalog = null;
            view.Bind(root);

            LogAssert.Expect(LogType.Log, new Regex(Regex.Escape(DamageVignetteView.LogTag) + " hit amount=30"));
            view.ShowHit(30);
            LogAssert.Expect(LogType.Log, new Regex(Regex.Escape(DamageVignetteView.LogTag) + " low health start"));
            view.SetHealthFraction(0.1f);

            Assert.That(view.Feedback.Settings, Is.SameAs(DamageFeedbackSettings.Default));
            Assert.That(view.Intensity, Is.GreaterThan(0f), "the state still runs for the logs");
            Assert.That(view.Element.style.display.value, Is.EqualTo(DisplayStyle.None), "but there is no image to draw");
            Assert.That(view.IsHeartbeatPlaying, Is.False);
        }

        [Test]
        public void TheCatalogValuesDriveTheState()
        {
            var view = CreateView(out _);
            var catalog = CreateCatalog();
            catalog.SetSettings(new DamageFeedbackSettings(0.5f, 20f, 2f, 0.4f, 0.2f, 0.6f));
            view.Catalog = catalog;

            Assert.That(view.Feedback.Settings.LowHealthThreshold, Is.EqualTo(0.4f));
            Assert.That(view.Feedback.Settings.HitFadeSeconds, Is.EqualTo(2f));
        }

        [Test]
        public void ANonPositiveHitShowsNothing()
        {
            var view = CreateView(out var root);
            view.Catalog = CreateCatalog();
            view.Bind(root);

            view.ShowHit(0);

            Assert.That(view.Intensity, Is.Zero);
            Assert.That(view.Element.style.display.value, Is.EqualTo(DisplayStyle.None));
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void CatalogValidateNamesEveryMissingPiece()
        {
            var catalog = ScriptableObject.CreateInstance<DamageFeedbackCatalog>();
            _created.Add(catalog);
            catalog.name = "Empty";

            var thrown = Assert.Throws<PresentationCatalogException>(() => catalog.Validate());

            Assert.That(thrown.Message, Does.Contain("vignette is missing"));
            Assert.That(thrown.Message, Does.Contain("heartbeat is missing"));
            Assert.That(thrown.Message, Does.Contain("breath is missing"));
        }

        [Test]
        public void CatalogFollowsThePresentationNamingRule()
        {
            Assert.That(DamageFeedbackCatalog.ResourcePath, Is.EqualTo("Presentation/DamageFeedbackCatalog"));
        }

        [Test]
        public void CatalogSettingsRoundTripAndVolumesAreClamped()
        {
            var catalog = ScriptableObject.CreateInstance<DamageFeedbackCatalog>();
            _created.Add(catalog);
            var settings = new DamageFeedbackSettings(0.5f, 20f, 2f, 0.4f, 0.2f, 0.6f);

            catalog.SetSettings(settings);
            catalog.HeartbeatVolume = 2f;
            catalog.BreathVolume = -1f;

            var read = catalog.ToSettings();
            Assert.That(read.HitPeakIntensity, Is.EqualTo(0.5f));
            Assert.That(read.FullHitDamage, Is.EqualTo(20f));
            Assert.That(read.HitFadeSeconds, Is.EqualTo(2f));
            Assert.That(read.LowHealthThreshold, Is.EqualTo(0.4f));
            Assert.That(read.LowHealthMinIntensity, Is.EqualTo(0.2f));
            Assert.That(read.LowHealthMaxIntensity, Is.EqualTo(0.6f));
            Assert.That(catalog.HeartbeatVolume, Is.EqualTo(1f));
            Assert.That(catalog.BreathVolume, Is.EqualTo(0f));
            Assert.Throws<System.ArgumentNullException>(() => catalog.SetSettings(null));
        }

        private DamageVignetteView CreateView(out VisualElement root)
        {
            var gameObject = new GameObject(nameof(DamageVignetteView));
            _created.Add(gameObject);
            root = CloneLayout();
            return gameObject.AddComponent<DamageVignetteView>();
        }

        private DamageFeedbackCatalog CreateCatalog()
        {
            var texture = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            _created.Add(texture);
            var catalog = ScriptableObject.CreateInstance<DamageFeedbackCatalog>();
            _created.Add(catalog);
            catalog.Vignette = texture;
            catalog.SetSettings(new DamageFeedbackSettings(0.8f, 50f, 0.8f, 0.25f, 0.35f, 0.7f));
            return catalog;
        }

        private static VisualElement CloneLayout()
        {
            var layout = Resources.Load<VisualTreeAsset>(Hud.LayoutResourcePath);
            Assert.That(layout, Is.Not.Null, "Assets/game/UI/Resources/Hud/Hud.uxml should be loadable from Resources");
            return layout.CloneTree();
        }
    }
}
