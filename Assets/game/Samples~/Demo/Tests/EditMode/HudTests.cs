using Shooter.Core;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
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
        public void TheCrosshairHidesWhileAStreakSpotIsBeingChosen()
        {
            var view = _hud.View;
            Assert.That(view, Is.Not.Null);
            Assert.That(view.Crosshair.style.display.value, Is.EqualTo(DisplayStyle.Flex));

            GameSignals.StreakTargeting.Publish(new StreakTargetingStatus(
                StreakSpec.AirstrikeRewardId, StreakSpec.DefaultAirstrikeNameKey,
                System.Numerics.Vector3.Zero, new System.Numerics.Vector3(4f, 0f, 9f), 30f, 100f, 24f));

            Assert.That(view.IsAimHidden, Is.True);
            Assert.That(view.Crosshair.style.display.value, Is.EqualTo(DisplayStyle.None));

            GameSignals.StreakTargeting.Clear();

            Assert.That(view.IsAimHidden, Is.False);
            Assert.That(view.Crosshair.style.display.value, Is.EqualTo(DisplayStyle.Flex));
        }

        [Test]
        public void ARebuiltViewStartsWithTheAimHiddenWhileChoosing()
        {
            GameSignals.StreakTargeting.Publish(new StreakTargetingStatus(
                StreakSpec.AirstrikeRewardId, StreakSpec.DefaultAirstrikeNameKey,
                System.Numerics.Vector3.Zero, System.Numerics.Vector3.Zero, 0f, 100f, 24f));

            _hud.RebuildView();

            Assert.That(_hud.View.IsAimHidden, Is.True);
            Assert.That(_hud.View.Crosshair.style.display.value, Is.EqualTo(DisplayStyle.None));
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
        public void TheDefaultHudCarriesTheVoteKickWalletBuyMenuVisionAndDroneViews()
        {
            Assert.That(_hud.VoteKick, Is.Not.Null, "vote kick");
            Assert.That(_hud.Wallet, Is.Not.Null, "wallet");
            Assert.That(_hud.BuyMenu, Is.Not.Null, "buy menu");
            Assert.That(_hud.VisionOverlay, Is.Not.Null, "vision overlay");
            Assert.That(_hud.DroneHud, Is.Not.Null, "drone");
        }

        [Test]
        public void TheVoteKickWalletBuyMenuVisionAndDroneViewsAreBoundToTheirSlots()
        {
            Assert.That(_hud.VoteKick.Element, Is.Not.Null);
            Assert.That(_hud.VoteKick.Element.name, Is.EqualTo(VoteKickView.UxmlName));
            Assert.That(_hud.Wallet.Element, Is.Not.Null);
            Assert.That(_hud.Wallet.Element.name, Is.EqualTo(WalletView.UxmlName));
            Assert.That(_hud.VisionOverlay.Element, Is.Not.Null);
            Assert.That(_hud.VisionOverlay.Element.name, Is.EqualTo(VisionOverlayView.UxmlName));
            Assert.That(_hud.DroneHud.Element, Is.Not.Null);
            Assert.That(_hud.DroneHud.Element.name, Is.EqualTo(DroneHudView.UxmlName));
            Assert.That(_hud.BuyMenu.Element, Is.Not.Null);
            Assert.That(_hud.BuyMenu.Element.name, Is.EqualTo(BuyMenuView.UxmlName));
            Assert.That(_hud.BuyMenu.Host, Is.Not.Null);
            Assert.That(_hud.BuyMenu.Host.parent, Is.SameAs(_hud.BuyMenu.Element));

            var pause = _hud.BuyMenu.Element.Q<VisualElement>("pause-menu");
            Assert.That(pause, Is.Not.Null);
            Assert.That(
                _hud.BuyMenu.Element.IndexOf(_hud.BuyMenu.Host),
                Is.LessThan(_hud.BuyMenu.Element.IndexOf(pause)),
                "the buy menu sits under the pause menu");
        }

        [Test]
        public void TheVoteKickWalletBuyMenuVisionAndDroneViewsStayHiddenUntilTheirValuesArePublished()
        {
            Assert.That(_hud.VoteKick.IsShown, Is.False);
            Assert.That(_hud.VoteKick.Element.style.display.value, Is.EqualTo(DisplayStyle.None));
            Assert.That(_hud.Wallet.IsShown, Is.False);
            Assert.That(_hud.Wallet.Element.style.display.value, Is.EqualTo(DisplayStyle.None));
            Assert.That(_hud.BuyMenu.IsOpen, Is.False);
            Assert.That(_hud.BuyMenu.Host.style.display.value, Is.EqualTo(DisplayStyle.None));
            Assert.That(_hud.VisionOverlay.IsShown, Is.False);
            Assert.That(_hud.VisionOverlay.Element.style.display.value, Is.EqualTo(DisplayStyle.None));
            Assert.That(_hud.DroneHud.IsShown, Is.False);
            Assert.That(_hud.DroneHud.Element.style.display.value, Is.EqualTo(DisplayStyle.None));
        }

        [Test]
        public void TheAttachedViewsShowThePublishedValues()
        {
            GameSignals.VoteKick.Publish(new VoteKickStatus(4, 2, 1, 3, 25f));
            GameSignals.Wallet.Publish(new WalletStatus(800, 16000, false));
            GameSignals.Vision.Publish(new VisionStatus(VisionMode.Night, VisionSource.Goggles));
            GameSignals.Drone.Publish(new DroneStatus(0.5f, 0.9f, 0, true));

            Assert.That(_hud.VoteKick.IsShown, Is.True);
            Assert.That(_hud.VoteKick.Element.style.display.value, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(_hud.Wallet.IsShown, Is.True);
            Assert.That(_hud.Wallet.Element.style.display.value, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(_hud.VisionOverlay.IsShown, Is.True);
            Assert.That(_hud.VisionOverlay.Element.style.display.value, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(_hud.DroneHud.IsShown, Is.True);
            Assert.That(_hud.DroneHud.Element.style.display.value, Is.EqualTo(DisplayStyle.Flex));
        }

        [Test]
        public void TheAttachedBuyMenuOpensOnlyDuringTheBuyTime()
        {
            GameSignals.Wallet.Publish(new WalletStatus(800, 16000, false));

            Assert.That(_hud.BuyMenu.Open(), Is.False, "the round is under way");

            GameSignals.Wallet.Publish(new WalletStatus(800, 16000, true));

            Assert.That(_hud.BuyMenu.Open(), Is.True);
            Assert.That(_hud.BuyMenu.Host.style.display.value, Is.EqualTo(DisplayStyle.Flex));

            GameSignals.Wallet.Publish(new WalletStatus(800, 16000, false));

            Assert.That(_hud.BuyMenu.IsOpen, Is.False, "the buy time is over");
        }

        [Test]
        public void TheBuyMenuIsOpenedWithTheInventoryKey()
        {
            Assert.That(BuyMenuView.ToggleActionPath, Is.EqualTo(InputActionPaths.Inventory));
            Assert.That(BuyMenuView.TakesToggleKey, Is.False, "no wallet means no buy time");
        }

        [Test]
        public void AddingTheViewsAgainDoesNotDuplicateTheVoteKickWalletBuyMenuVisionAndDroneViews()
        {
            Hud.AddViews(_hud.gameObject);

            Assert.That(_hud.GetComponents<VoteKickView>(), Has.Length.EqualTo(1));
            Assert.That(_hud.GetComponents<WalletView>(), Has.Length.EqualTo(1));
            Assert.That(_hud.GetComponents<BuyMenuView>(), Has.Length.EqualTo(1));
            Assert.That(_hud.GetComponents<VisionOverlayView>(), Has.Length.EqualTo(1));
            Assert.That(_hud.GetComponents<DroneHudView>(), Has.Length.EqualTo(1));
        }

        [Test]
        public void TheDefaultHudCarriesTheNetStatsAndNameplateViewsInTheirSlots()
        {
            Assert.That(_hud.NetStats, Is.Not.Null, "net stats");
            Assert.That(_hud.Nameplates, Is.Not.Null, "nameplates");
            Assert.That(_hud.NetStats.Element, Is.Not.Null);
            Assert.That(_hud.NetStats.Element.name, Is.EqualTo(NetStatsView.UxmlName));
            Assert.That(_hud.Nameplates.Element, Is.Not.Null);
            Assert.That(_hud.Nameplates.Element.name, Is.EqualTo(NameplateView.UxmlName));
        }

        [Test]
        public void AddingTheViewsAgainDoesNotDuplicateTheNetStatsAndNameplateViews()
        {
            Hud.AddViews(_hud.gameObject);

            Assert.That(_hud.GetComponents<NetStatsView>(), Has.Length.EqualTo(1));
            Assert.That(_hud.GetComponents<NameplateView>(), Has.Length.EqualTo(1));
        }

        [Test]
        public void TheNameplatesStayHiddenWithNobodyToName()
        {
            Assert.That(_hud.Nameplates.ShownCount, Is.EqualTo(0));
            Assert.That(_hud.Nameplates.Element.style.display.value, Is.EqualTo(DisplayStyle.None));
        }

        [Test]
        public void TheNetStatsLineIsHiddenOfflineEvenWhenEnabled()
        {
            var netStats = _hud.NetStats;
            netStats.Store = null;
            netStats.Settings = new RecordingSettingsSink(ControlSettings.Default.WithShowNetStats(true));

            Assert.That(GameSignals.NetStats.HasValue, Is.False, "nothing publishes the net stats offline");
            Assert.That(netStats.IsShown, Is.False);
            Assert.That(netStats.Element.style.display.value, Is.EqualTo(DisplayStyle.None));
        }

        [Test]
        public void TheHudNetStatsLineIsToggledOnlineAndStartsHidden()
        {
            var netStats = _hud.NetStats;
            netStats.Store = null;
            netStats.Settings = new RecordingSettingsSink(ControlSettings.Default);
            GameSignals.NetStats.Publish(new NetStatsSignal(48, 3, 1234));

            Assert.That(netStats.IsShown, Is.False, "hidden by default");
            Assert.That(netStats.Element.style.display.value, Is.EqualTo(DisplayStyle.None));

            netStats.Toggle();

            Assert.That(netStats.IsShown, Is.True);
            Assert.That(netStats.Element.style.display.value, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(((Label)netStats.Element).text, Is.EqualTo("Ping 48 ms  ロス 3%  tick 1234"));

            netStats.Toggle();

            Assert.That(netStats.IsShown, Is.False);
            Assert.That(netStats.Element.style.display.value, Is.EqualTo(DisplayStyle.None));
            Assert.That(((Label)netStats.Element).text, Is.Empty);
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
