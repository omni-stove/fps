using Shooter.Core;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    /// <summary>
    /// 既定の HUD (<see cref="Hud.Create"/>) にミニマップ・コンパス・チャット・ボイス・観戦・投票の view が揃って Hud.uxml の枠を受け、
    /// HUD の量でミニマップとコンパスを出し入れすることを確かめる。
    /// </summary>
    public sealed class HudTests
    {
        private Hud _hud;

        [SetUp]
        public void SetUp()
        {
            _hud = Hud.Create("HudTests.Hud");
        }

        [TearDown]
        public void TearDown()
        {
            GameSignals.ResetAll();
            if (_hud != null)
            {
                var settings = _hud.OwnedPanelSettings;
                Object.DestroyImmediate(_hud.gameObject);
                if (settings != null)
                {
                    Object.DestroyImmediate(settings);
                }

                _hud = null;
            }
        }

        [Test]
        public void TheDefaultHudCarriesTheMapChatVoiceSpectateAndVoteViews()
        {
            Assert.That(_hud.Minimap, Is.Not.Null, "minimap");
            Assert.That(_hud.Compass, Is.Not.Null, "compass");
            Assert.That(_hud.Chat, Is.Not.Null, "chat");
            Assert.That(_hud.VoiceIndicator, Is.Not.Null, "voice");
            Assert.That(_hud.Spectate, Is.Not.Null, "spectate");
            Assert.That(_hud.MapVote, Is.Not.Null, "map vote");
        }

        [Test]
        public void EachViewIsBoundToItsSlotInTheLayout()
        {
            Assert.That(_hud.Minimap.Element, Is.Not.Null);
            Assert.That(_hud.Minimap.Element.name, Is.EqualTo(MinimapView.UxmlName));
            Assert.That(_hud.Compass.Element, Is.Not.Null);
            Assert.That(_hud.Compass.Element.name, Is.EqualTo(CompassView.UxmlName));
            Assert.That(_hud.Chat.Element, Is.Not.Null);
            Assert.That(_hud.Chat.Element.name, Is.EqualTo(ChatView.UxmlName));
            Assert.That(_hud.VoiceIndicator.Element, Is.Not.Null);
            Assert.That(_hud.VoiceIndicator.Element.name, Is.EqualTo(VoiceIndicatorView.UxmlName));
            Assert.That(_hud.Spectate.Element, Is.Not.Null);
            Assert.That(_hud.Spectate.Element.name, Is.EqualTo(SpectateView.UxmlName));
            Assert.That(_hud.MapVote.Element, Is.Not.Null);
            Assert.That(_hud.MapVote.Panel, Is.Not.Null);
            Assert.That(_hud.MapVote.Element.Q<VisualElement>(MapVoteView.PanelName), Is.SameAs(_hud.MapVote.Panel));
        }

        [Test]
        public void TheNewViewsStayHiddenUntilTheirValuesArePublished()
        {
            Assert.That(_hud.Minimap.Element.style.display.value, Is.EqualTo(DisplayStyle.None));
            Assert.That(_hud.Compass.Element.style.display.value, Is.EqualTo(DisplayStyle.None));
            Assert.That(_hud.Chat.Element.style.display.value, Is.EqualTo(DisplayStyle.None));
            Assert.That(_hud.VoiceIndicator.Element.style.display.value, Is.EqualTo(DisplayStyle.None));
            Assert.That(_hud.Spectate.Element.style.display.value, Is.EqualTo(DisplayStyle.None));
            Assert.That(_hud.MapVote.Panel.style.display.value, Is.EqualTo(DisplayStyle.None));
        }

        [Test]
        public void AddingTheViewsAgainDoesNotDuplicateThem()
        {
            Hud.AddViews(_hud.gameObject);

            Assert.That(_hud.GetComponents<MinimapView>(), Has.Length.EqualTo(1));
            Assert.That(_hud.GetComponents<CompassView>(), Has.Length.EqualTo(1));
            Assert.That(_hud.GetComponents<ChatView>(), Has.Length.EqualTo(1));
            Assert.That(_hud.GetComponents<VoiceIndicatorView>(), Has.Length.EqualTo(1));
            Assert.That(_hud.GetComponents<SpectateView>(), Has.Length.EqualTo(1));
            Assert.That(_hud.GetComponents<MapVoteView>(), Has.Length.EqualTo(1));
        }

        [Test]
        public void TheMinimalDensityHidesTheMinimapAndTheCompass()
        {
            Assert.That(_hud.Minimap.IsAllowed, Is.True);
            Assert.That(_hud.Compass.IsAllowed, Is.True);

            _hud.ApplyDensity(HudDensity.Minimal);

            Assert.That(_hud.Minimap.IsAllowed, Is.False);
            Assert.That(_hud.Compass.IsAllowed, Is.False);
            Assert.That(_hud.Minimap.Element.style.visibility.value, Is.EqualTo(Visibility.Hidden));
            Assert.That(_hud.Compass.Element.style.visibility.value, Is.EqualTo(Visibility.Hidden));

            _hud.ApplyDensity(HudDensity.Full);

            Assert.That(_hud.Minimap.IsAllowed, Is.True);
            Assert.That(_hud.Compass.IsAllowed, Is.True);
            Assert.That(_hud.Minimap.Element.style.visibility.value, Is.EqualTo(Visibility.Visible));
        }

        [TestCase(HudDensity.Full, true)]
        [TestCase(HudDensity.Minimal, false)]
        [TestCase(HudDensity.Hardcore, false)]
        public void ThePublishedDensityDecidesTheMinimapAndTheCompass(HudDensity density, bool shown)
        {
            GameSignals.HudDensity.Publish(density);

            Assert.That(_hud.Visibility.Density, Is.EqualTo(density));
            Assert.That(_hud.Minimap.IsAllowed, Is.EqualTo(shown));
            Assert.That(_hud.Compass.IsAllowed, Is.EqualTo(shown));
        }

        [Test]
        public void TheDensityCarriesOverWhenTheLayoutIsRebuilt()
        {
            GameSignals.HudDensity.Publish(HudDensity.Minimal);

            Assert.That(_hud.RebuildView(), Is.True);

            Assert.That(_hud.Minimap.IsAllowed, Is.False);
            Assert.That(_hud.Minimap.Element.style.visibility.value, Is.EqualTo(Visibility.Hidden));
            Assert.That(_hud.Compass.Element.style.visibility.value, Is.EqualTo(Visibility.Hidden));
        }

        [Test]
        public void ThePublishedMinimapIsShownAtTheFullDensity()
        {
            GameSignals.Minimap.Publish(new MinimapStatus(System.Numerics.Vector3.Zero, 0f, new MinimapContact[0]));

            Assert.That(_hud.Minimap.Status.HasValue, Is.True);
            Assert.That(_hud.Minimap.Element.style.display.value, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(_hud.Compass.Element.style.display.value, Is.EqualTo(DisplayStyle.Flex));
        }
    }
}
