using System;
using System.Collections.Generic;
using Fps.Core;
using Fps.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace Fps.Tests.EditMode
{
    public sealed class HudViewTests
    {
        private static WeaponSpec NewSpec()
        {
            return new WeaponSpec("00000000-0000-4000-8000-000000000001", "weapon.rifle", WeaponCategory.AssaultRifle, FireMode.Auto, 30, 90, 0f, 1f, 25, 100f, DamageFalloff.None);
        }

        private static readonly AmmoTypeSpec Slug = new AmmoTypeSpec("00000000-0000-4000-8000-0000000000a1", "ammo.slug");

        private static VisualElement CloneLayout()
        {
            var layout = Resources.Load<VisualTreeAsset>(Hud.LayoutResourcePath);
            Assert.That(layout, Is.Not.Null, "Assets/game/UI/Resources/Hud/Hud.uxml should be loadable from Resources");
            return layout.CloneTree();
        }

        [Test]
        public void TheThemeIsLoadableFromResources()
        {
            Assert.That(Resources.Load<ThemeStyleSheet>(Hud.ThemeResourcePath), Is.Not.Null);
        }

        [Test]
        public void TheLayoutHasEveryElementTheViewNeeds()
        {
            var root = CloneLayout();

            Assert.That(root.Q<Label>(HudView.HealthLabelName), Is.Not.Null);
            Assert.That(root.Q<Label>(HudView.AmmoLabelName), Is.Not.Null);
            Assert.That(root.Q<Label>(HudView.WeaponNameLabelName), Is.Not.Null);
            Assert.That(root.Q<Label>(HudView.TimeLabelName), Is.Not.Null);
            Assert.That(root.Q<VisualElement>(HudView.CrosshairName), Is.Not.Null);
            Assert.That(root.Q<Label>(HudView.NoticeLabelName), Is.Not.Null);
            Assert.That(root.Q<VisualElement>(HudView.ScorePanelName), Is.Not.Null);
            Assert.That(root.Q<Label>(HudView.FirstScoreLabelName), Is.Not.Null);
            Assert.That(root.Q<Label>(HudView.SecondScoreLabelName), Is.Not.Null);
            Assert.That(root.Q<Label>(HudView.ResultLabelName), Is.Not.Null);
            var reloadBar = root.Q<VisualElement>(HudView.ReloadBarName);
            Assert.That(reloadBar, Is.Not.Null);
            Assert.That(reloadBar.Q<VisualElement>(HudView.ReloadFillName), Is.Not.Null);
            Assert.That(reloadBar.Q<VisualElement>(HudView.ReloadWindowName), Is.Not.Null);
            Assert.That(root.Q<Label>(HudView.ChargeLabelName), Is.Not.Null);
            Assert.That(root.Q<Label>(HudView.HeatLabelName), Is.Not.Null);
            Assert.That(root.Q<Label>(HudView.AmmoTypeLabelName), Is.Not.Null);
            Assert.That(root.Q<Label>(HudView.MagnificationLabelName), Is.Not.Null);
            Assert.That(root.Q<Label>(HudView.LockOnLabelName), Is.Not.Null);
            Assert.That(root.Q<VisualElement>(HudView.AimBlockMarkerName), Is.Not.Null);
        }

        private static Weapon NewActiveReloadWeapon()
        {
            var actions = new ActionRules(
                Downed.Off,
                ActionRules.DefaultDownedBleedoutSeconds,
                ActionRules.DefaultDownedHealthFraction,
                ActionRules.DefaultReviveSeconds,
                Cover.Off,
                BlindFire.Off,
                ActiveReload.On);
            var rules = new GameRules(
                FriendlyFire.Off,
                RespawnMode.Individual,
                GameRules.DefaultWaveIntervalSeconds,
                0f,
                0f,
                DeathBody.Vanish,
                DeathCamera.None,
                false,
                1f,
                Destruction.Off,
                actions);
            var spec = new WeaponSpec(
                "00000000-0000-4000-8000-000000000001", "Rifle", WeaponCategory.AssaultRifle, FireMode.Auto, 30, 90, 0f, 2f, 25, 100f, DamageFalloff.None,
                activeReloadWindowStart: 0.5f,
                activeReloadWindowLength: 0.25f,
                activeReloadFailSeconds: 0.5f);
            return new Weapon(spec, rules);
        }

        [Test]
        public void TheCrosshairHasADotAndFourLines()
        {
            var crosshair = CloneLayout().Q<VisualElement>(HudView.CrosshairName);

            Assert.That(crosshair.Q<VisualElement>(HudView.CrosshairDotName), Is.Not.Null);
            Assert.That(crosshair.Q<VisualElement>(HudView.CrosshairTopName), Is.Not.Null);
            Assert.That(crosshair.Q<VisualElement>(HudView.CrosshairBottomName), Is.Not.Null);
            Assert.That(crosshair.Q<VisualElement>(HudView.CrosshairLeftName), Is.Not.Null);
            Assert.That(crosshair.Q<VisualElement>(HudView.CrosshairRightName), Is.Not.Null);
        }

        [TestCase(ViewMode.FirstPerson, false, false, true)]
        [TestCase(ViewMode.FirstPerson, true, false, false)]
        [TestCase(ViewMode.FirstPerson, false, true, false)]
        [TestCase(ViewMode.ThirdPerson, false, false, true)]
        [TestCase(ViewMode.ThirdPerson, true, false, true)]
        [TestCase(ViewMode.ThirdPerson, false, true, false)]
        [TestCase(ViewMode.ThirdPerson, true, true, false)]
        public void TheCrosshairStaysWhileAimingInThirdPersonOnly(ViewMode view, bool aiming, bool sprinting, bool expected)
        {
            Assert.That(Hud.ShowsCrosshair(view, aiming, sprinting), Is.EqualTo(expected));
        }

        [Test]
        public void TheCrosshairLinesMoveOutWithTheSpreadAndTheCrosshairHidesWhenAsked()
        {
            var model = new HudModel();
            var view = new HudView(CloneLayout(), model);

            Assert.That(view.Crosshair.style.display.value, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(view.CrosshairTop.style.translate.value.y.value, Is.EqualTo(-HudView.MinCrosshairGap));
            Assert.That(view.CrosshairRight.style.translate.value.x.value, Is.EqualTo(HudView.MinCrosshairGap));

            model.SetCrosshair(1f, true);
            Assert.That(view.CrosshairTop.style.translate.value.y.value, Is.EqualTo(-HudView.MaxCrosshairGap));
            Assert.That(view.CrosshairBottom.style.translate.value.y.value, Is.EqualTo(HudView.MaxCrosshairGap));
            Assert.That(view.CrosshairLeft.style.translate.value.x.value, Is.EqualTo(-HudView.MaxCrosshairGap));
            Assert.That(view.CrosshairRight.style.translate.value.x.value, Is.EqualTo(HudView.MaxCrosshairGap));

            model.SetCrosshair(1f, false);
            Assert.That(view.Crosshair.style.display.value, Is.EqualTo(DisplayStyle.None));
        }

        [Test]
        public void ABlockedAimTintsTheCrosshairAndMarksTheBlockedPoint()
        {
            var model = new HudModel();
            var view = new HudView(CloneLayout(), model);

            Assert.That(view.Crosshair.ClassListContains(HudView.CrosshairBlockedClass), Is.False);
            Assert.That(view.AimBlockMarker.style.display.value, Is.EqualTo(DisplayStyle.None));

            model.SetAimBlock(new System.Numerics.Vector2(0.25f, 0.75f));

            Assert.That(view.Crosshair.ClassListContains(HudView.CrosshairBlockedClass), Is.True);
            Assert.That(view.AimBlockMarker.style.display.value, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(view.AimBlockMarker.style.left.value.value, Is.EqualTo(25f).Within(1e-4f));
            Assert.That(view.AimBlockMarker.style.left.value.unit, Is.EqualTo(LengthUnit.Percent));
            Assert.That(view.AimBlockMarker.style.top.value.value, Is.EqualTo(25f).Within(1e-4f));

            model.ClearAimBlock();

            Assert.That(view.Crosshair.ClassListContains(HudView.CrosshairBlockedClass), Is.False);
            Assert.That(view.AimBlockMarker.style.display.value, Is.EqualTo(DisplayStyle.None));
        }

        [Test]
        public void ABlockedPointOffScreenOnlyTintsTheCrosshair()
        {
            var model = new HudModel();
            var view = new HudView(CloneLayout(), model);

            model.SetAimBlock(null);

            Assert.That(view.Crosshair.ClassListContains(HudView.CrosshairBlockedClass), Is.True);
            Assert.That(view.AimBlockMarker.style.display.value, Is.EqualTo(DisplayStyle.None));
        }

        [Test]
        public void TheBlockMarkerHidesWithTheCrosshair()
        {
            var model = new HudModel();
            var view = new HudView(CloneLayout(), model);
            model.SetAimBlock(new System.Numerics.Vector2(0.5f, 0.5f));

            model.SetCrosshair(0f, false);
            Assert.That(view.AimBlockMarker.style.display.value, Is.EqualTo(DisplayStyle.None));
            Assert.That(view.Crosshair.ClassListContains(HudView.CrosshairBlockedClass), Is.False);

            model.SetCrosshair(0f, true);
            Assert.That(view.AimBlockMarker.style.display.value, Is.EqualTo(DisplayStyle.Flex));

            view.ApplyVisibility(HudVisibility.Hardcore);
            Assert.That(view.AimBlockMarker.style.display.value, Is.EqualTo(DisplayStyle.None));
        }

        [Test]
        public void TheBlockMarkerIsBuiltAsACrossUnlessAnImageIsGiven()
        {
            var view = new HudView(CloneLayout(), new HudModel());

            Assert.That(view.AimBlockMarker.childCount, Is.EqualTo(2));
            Assert.That(view.AimBlockMarker.ElementAt(0).ClassListContains(HudView.AimBlockBarClass), Is.True);
            Assert.That(view.AimBlockMarker.ElementAt(1).ClassListContains(HudView.AimBlockBarCrossClass), Is.True);
            Assert.That(view.AimBlockMarker.ClassListContains(HudView.AimBlockImageClass), Is.False);

            var image = new Texture2D(4, 4);
            try
            {
                view.SetAimBlockMarker(image);

                Assert.That(view.AimBlockMarker.childCount, Is.Zero);
                Assert.That(view.AimBlockMarker.ClassListContains(HudView.AimBlockImageClass), Is.True);
                Assert.That(view.AimBlockMarker.style.backgroundImage.value.texture, Is.SameAs(image));

                view.SetAimBlockMarker(null);

                Assert.That(view.AimBlockMarker.childCount, Is.EqualTo(2));
                Assert.That(view.AimBlockMarker.ClassListContains(HudView.AimBlockImageClass), Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(image);
            }
        }

        [Test]
        public void TheBlockPointIsMappedToTheViewportAndDroppedBehindTheCamera()
        {
            var camera = new GameObject("Camera").AddComponent<Camera>();
            try
            {
                var ahead = Hud.AimBlockViewport(camera, camera.transform.position + (camera.transform.forward * 5f));
                Assert.That(ahead.HasValue, Is.True);
                Assert.That(ahead.Value.X, Is.EqualTo(0.5f).Within(1e-4f));
                Assert.That(ahead.Value.Y, Is.EqualTo(0.5f).Within(1e-4f));

                Assert.That(Hud.AimBlockViewport(camera, camera.transform.position - (camera.transform.forward * 5f)).HasValue, Is.False);
                Assert.That(Hud.AimBlockViewport(null, Vector3.zero).HasValue, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(camera.gameObject);
            }
        }

        [Test]
        public void TheCrosshairLinesSitOnTheConeEdgeAndBloomOutFromIt()
        {
            var model = new HudModel();
            var view = new HudView(CloneLayout(), model);

            model.SetCrosshair(0f, 40f, true);
            Assert.That(view.CrosshairTop.style.translate.value.y.value, Is.EqualTo(-40f).Within(1e-4f));
            Assert.That(view.CrosshairRight.style.translate.value.x.value, Is.EqualTo(40f).Within(1e-4f));

            model.SetCrosshair(1f, 40f, true);
            Assert.That(view.CrosshairBottom.style.translate.value.y.value, Is.EqualTo(64f).Within(1e-4f));
            Assert.That(view.CrosshairLeft.style.translate.value.x.value, Is.EqualTo(-64f).Within(1e-4f));
        }

        [Test]
        public void AShrunkCrosshairKeepsTheConeEdgeWhereItIsOnScreen()
        {
            var model = new HudModel();
            var view = new HudView(CloneLayout(), model, HudVisibility.Minimal);

            model.SetCrosshair(0f, 30f, true);

            // 箱を SmallCrosshairScale (0.6) で縮めるので、線は 30 / 0.6 = 50 ずらして画面で 30 に見せる。
            Assert.That(view.CrosshairTop.style.translate.value.y.value, Is.EqualTo(-50f).Within(1e-3f));
        }

        [Test]
        public void TheLayoutHasThePanelsTheDensityHides()
        {
            var root = CloneLayout();

            Assert.That(root.Q<VisualElement>(HudView.HealthPanelName).Q<Label>(HudView.HealthLabelName), Is.Not.Null);
            Assert.That(root.Q<VisualElement>(HudView.AmmoPanelName).Q<Label>(HudView.AmmoLabelName), Is.Not.Null);
            Assert.That(root.Q<VisualElement>(HudView.TimePanelName).Q<Label>(HudView.TimeLabelName), Is.Not.Null);
        }

        [Test]
        public void HardcoreHidesHealthAmmoWeaponNameAndCrosshairButKeepsTimeAndScore()
        {
            var model = new HudModel();
            model.SetHealth(100, 100);
            model.SetAmmo(30, 90, false);
            model.SetWeaponSpec(NewSpec());
            model.SetTime(60f);
            model.SetScores(Team.Alpha, 1, 2);

            var view = new HudView(CloneLayout(), model, HudVisibility.Hardcore);

            Assert.That(view.HealthPanel.style.display.value, Is.EqualTo(DisplayStyle.None));
            Assert.That(view.AmmoPanel.style.display.value, Is.EqualTo(DisplayStyle.None));
            Assert.That(view.WeaponNameLabel.style.display.value, Is.EqualTo(DisplayStyle.None));
            Assert.That(view.Crosshair.style.display.value, Is.EqualTo(DisplayStyle.None));
            Assert.That(view.TimePanel.style.display.value, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(view.ScorePanel.style.display.value, Is.EqualTo(DisplayStyle.Flex));

            // 値が変わっても隠したまま
            model.SetHealth(50, 100);
            model.SetCrosshair(0.5f, true);
            Assert.That(view.HealthPanel.style.display.value, Is.EqualTo(DisplayStyle.None));
            Assert.That(view.Crosshair.style.display.value, Is.EqualTo(DisplayStyle.None));
            Assert.That(view.HealthLabel.text, Is.EqualTo("50"), "the label keeps the value so switching back shows it at once");
        }

        [Test]
        public void ApplyingFullAgainShowsEverythingThatHasAValue()
        {
            var model = new HudModel();
            model.SetWeaponSpec(NewSpec());
            var view = new HudView(CloneLayout(), model, HudVisibility.Hardcore);

            view.ApplyVisibility(HudVisibility.Full);

            Assert.That(view.Visibility, Is.SameAs(HudVisibility.Full));
            Assert.That(view.HealthPanel.style.display.value, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(view.AmmoPanel.style.display.value, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(view.WeaponNameLabel.style.display.value, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(view.Crosshair.style.display.value, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(view.Crosshair.style.scale.value.value.x, Is.EqualTo(HudVisibility.FullCrosshairScale));
            Assert.That(view.ScorePanel.style.display.value, Is.EqualTo(DisplayStyle.None), "no scores were set");
        }

        [Test]
        public void MinimalShrinksTheCrosshair()
        {
            var view = new HudView(CloneLayout(), new HudModel(), HudVisibility.Minimal);

            Assert.That(view.Crosshair.style.display.value, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(view.Crosshair.style.scale.value.value.x, Is.EqualTo(HudVisibility.SmallCrosshairScale));
            Assert.That(view.Crosshair.style.scale.value.value.y, Is.EqualTo(HudVisibility.SmallCrosshairScale));
        }

        [Test]
        public void TheViewRejectsANullVisibility()
        {
            Assert.Throws<ArgumentNullException>(() => new HudView(CloneLayout(), new HudModel(), null));
            var view = new HudView(CloneLayout(), new HudModel());
            Assert.Throws<ArgumentNullException>(() => view.ApplyVisibility(null));
        }

        [Test]
        public void CrosshairGapGrowsFromTheMinimumToTheMaximum()
        {
            Assert.That(HudView.CrosshairGap(0f), Is.EqualTo(HudView.MinCrosshairGap));
            Assert.That(HudView.CrosshairGap(1f), Is.EqualTo(HudView.MaxCrosshairGap));
            Assert.That(HudView.CrosshairGap(0.5f), Is.GreaterThan(HudView.MinCrosshairGap).And.LessThan(HudView.MaxCrosshairGap));
            Assert.That(HudView.CrosshairGap(2f), Is.EqualTo(HudView.MaxCrosshairGap));
        }

        [Test]
        public void CrosshairGapStartsAtTheConeEdgeButNeverInsideTheMinimum()
        {
            Assert.That(HudView.CrosshairGap(0f, 40f), Is.EqualTo(40f));
            Assert.That(HudView.CrosshairGap(1f, 40f), Is.EqualTo(64f));
            Assert.That(HudView.CrosshairGap(0f, 2f), Is.EqualTo(HudView.MinCrosshairGap));
            Assert.That(HudView.CrosshairGap(1f, 0f), Is.EqualTo(HudView.MaxCrosshairGap));
            Assert.That(HudView.CrosshairGap(0f, float.NaN), Is.EqualTo(HudView.MinCrosshairGap));
        }

        [Test]
        public void TheReloadBarIsHiddenUntilThereIsProgress()
        {
            var view = new HudView(CloneLayout(), new HudModel());

            Assert.That(view.ReloadBar.style.display.value, Is.EqualTo(DisplayStyle.None));
        }

        [Test]
        public void TheReloadBarShowsTheProgressAndTheWindowAndHidesWhenTheReloadEnds()
        {
            var model = new HudModel();
            var weapon = NewActiveReloadWeapon();
            model.BindWeapon(weapon);
            var view = new HudView(CloneLayout(), model);
            Assert.That(weapon.TryFire(), Is.True);
            Assert.That(weapon.TryStartReload(), Is.True);

            weapon.Tick(0.5f);
            model.RefreshReloadProgress();

            Assert.That(view.ReloadBar.style.display.value, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(view.ReloadFill.style.width.value, Is.EqualTo(Length.Percent(25f)));
            Assert.That(view.ReloadWindow.style.display.value, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(view.ReloadWindow.style.left.value, Is.EqualTo(Length.Percent(50f)));
            Assert.That(view.ReloadWindow.style.width.value, Is.EqualTo(Length.Percent(25f)));

            weapon.Tick(1.5f);

            Assert.That(weapon.IsReloading, Is.False);
            Assert.That(view.ReloadBar.style.display.value, Is.EqualTo(DisplayStyle.None));
        }

        [Test]
        public void AnEmptyWindowIsHiddenWhileTheBarStays()
        {
            var model = new HudModel();
            var view = new HudView(CloneLayout(), model);

            model.SetReloadProgress(0.4f, 0f, 0f);

            Assert.That(view.ReloadBar.style.display.value, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(view.ReloadFill.style.width.value, Is.EqualTo(Length.Percent(40f)));
            Assert.That(view.ReloadWindow.style.display.value, Is.EqualTo(DisplayStyle.None));

            model.ClearReloadProgress();
            Assert.That(view.ReloadBar.style.display.value, Is.EqualTo(DisplayStyle.None));
        }

        [Test]
        public void TheOfflineHudHidesTheScoresAndTheResult()
        {
            var view = new HudView(CloneLayout(), new HudModel());

            Assert.That(view.ScorePanel.style.display.value, Is.EqualTo(DisplayStyle.None));
            Assert.That(view.ResultLabel.style.display.value, Is.EqualTo(DisplayStyle.None));
        }

        [Test]
        public void ScoresShowTheOwnTeamFirstInTheTeamColors()
        {
            var model = new HudModel();
            var view = new HudView(CloneLayout(), model);
            var alpha = new Color(0f, 0f, 1f);
            var bravo = new Color(1f, 0f, 0f);
            view.SetTeamColors(alpha, bravo);

            model.SetScores(Team.Bravo, 1, 4);

            Assert.That(view.ScorePanel.style.display.value, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(view.FirstScoreLabel.text, Is.EqualTo("BRAVO 4"));
            Assert.That(view.SecondScoreLabel.text, Is.EqualTo("ALPHA 1"));
            Assert.That(view.FirstScoreLabel.style.color.value, Is.EqualTo(bravo));
            Assert.That(view.SecondScoreLabel.style.color.value, Is.EqualTo(alpha));

            model.ClearScores();
            Assert.That(view.ScorePanel.style.display.value, Is.EqualTo(DisplayStyle.None));
        }

        [Test]
        public void TheResultIsShownOnlyWhileItHasText()
        {
            var model = new HudModel();
            var view = new HudView(CloneLayout(), model);

            model.SetResult(null);
            Assert.That(view.ResultLabel.text, Is.EqualTo(Localizer.Resolve(HudFormat.Draw)));
            Assert.That(view.ResultLabel.style.display.value, Is.EqualTo(DisplayStyle.Flex));

            model.ClearResult();
            Assert.That(view.ResultLabel.style.display.value, Is.EqualTo(DisplayStyle.None));
        }

        [Test]
        public void ReloadingAndLowHealthChangeTheStyle()
        {
            var model = new HudModel();
            var view = new HudView(CloneLayout(), model);

            model.SetAmmo(0, 90, true);
            model.SetHealth(10, 100);
            Assert.That(view.AmmoLabel.text, Is.EqualTo(Localizer.Resolve(HudNumberFormat.Reloading) + " / 90"));
            Assert.That(view.AmmoLabel.ClassListContains(HudView.ReloadingClass), Is.True);
            Assert.That(view.HealthLabel.ClassListContains(HudView.LowHealthClass), Is.True);

            model.SetAmmo(30, 60, false);
            model.SetHealth(100, 100);
            Assert.That(view.AmmoLabel.ClassListContains(HudView.ReloadingClass), Is.False);
            Assert.That(view.HealthLabel.ClassListContains(HudView.LowHealthClass), Is.False);
        }

        [Test]
        public void AnInfiniteReserveIsShownAsInfinity()
        {
            var model = new HudModel();
            var view = new HudView(CloneLayout(), model);
            var infinite = new GameRules(
                FriendlyFire.Off,
                RespawnMode.Individual,
                GameRules.DefaultWaveIntervalSeconds,
                0f,
                0f,
                DeathBody.Vanish,
                DeathCamera.None,
                true,
                1f,
                Destruction.Off);

            model.BindWeapon(new Weapon(NewSpec(), infinite));
            Assert.That(view.AmmoLabel.text, Is.EqualTo("30 / ∞"));

            model.BindWeapon(new Weapon(NewSpec()));
            Assert.That(view.AmmoLabel.text, Is.EqualTo("30 / 90"), "a finite reserve shows the number");
        }

        [Test]
        public void TheWeaponNameFollowsTheBoundWeaponAndIsHiddenWithoutOne()
        {
            var model = new HudModel();
            var view = new HudView(CloneLayout(), model);

            Assert.That(view.WeaponNameLabel.style.display.value, Is.EqualTo(DisplayStyle.None));

            model.BindWeapon(new Weapon(NewSpec()));
            Assert.That(view.WeaponNameLabel.text, Is.EqualTo("Rifle"));
            Assert.That(view.WeaponNameLabel.style.display.value, Is.EqualTo(DisplayStyle.Flex));

            model.BindWeapon(new Weapon(new WeaponSpec(
                "00000000-0000-4000-8000-000000000002", "weapon.handgun", WeaponCategory.Handgun, FireMode.Semi, 12, 48, 0.2f, 1.5f, 34, 60f, DamageFalloff.None)));
            Assert.That(view.WeaponNameLabel.text, Is.EqualTo("Handgun"));
            Assert.That(view.AmmoLabel.text, Is.EqualTo("12 / 48"));

            model.BindWeapon(null);
            Assert.That(view.WeaponNameLabel.style.display.value, Is.EqualTo(DisplayStyle.None));
        }

        [Test]
        public void TheNoticeIsHiddenWhileEmptyAndShownWhileItHasText()
        {
            var model = new HudModel();
            var notice = new CampaignNotice();
            model.BindNotice(notice);
            var view = new HudView(CloneLayout(), model);

            Assert.That(view.NoticeLabel.style.display.value, Is.EqualTo(DisplayStyle.None));

            notice.ShowSaved(1f);
            Assert.That(view.NoticeLabel.text, Is.EqualTo(Localizer.Resolve(HudFormat.CheckpointSaved)));
            Assert.That(view.NoticeLabel.style.display.value, Is.EqualTo(DisplayStyle.Flex));

            notice.ShowRestartCountdown(2f);
            Assert.That(view.NoticeLabel.text, Is.EqualTo(Localizer.Resolve(HudFormat.CheckpointRestart(2))));

            notice.Clear();
            Assert.That(view.NoticeLabel.text, Is.Empty);
            Assert.That(view.NoticeLabel.style.display.value, Is.EqualTo(DisplayStyle.None));
        }

        [Test]
        public void ShowsTheModelTextAtOnce()
        {
            var model = new HudModel();
            model.BindHealth(new Health(100));
            model.BindWeapon(new Weapon(NewSpec()));
            model.BindMatch(new Match(125f, 1, 0));

            var view = new HudView(CloneLayout(), model);

            Assert.That(view.HealthLabel.text, Is.EqualTo("100"));
            Assert.That(view.AmmoLabel.text, Is.EqualTo("30 / 90"));
            Assert.That(view.TimeLabel.text, Is.EqualTo("02:05"));
        }

        [Test]
        public void UpdatesTheLabelsFromEvents()
        {
            var model = new HudModel();
            var health = new Health(100);
            var weapon = new Weapon(NewSpec());
            var match = new Match(60f, 1, 0);
            model.BindHealth(health);
            model.BindWeapon(weapon);
            model.BindMatch(match);
            var view = new HudView(CloneLayout(), model);

            health.TakeDamage(30);
            weapon.TryFire();
            match.Start();
            match.Tick(1f);

            Assert.That(view.HealthLabel.text, Is.EqualTo("70"));
            Assert.That(view.AmmoLabel.text, Is.EqualTo("29 / 90"));
            Assert.That(view.TimeLabel.text, Is.EqualTo("00:59"));
        }

        [Test]
        public void ShowsThePlaceholdersWithoutValues()
        {
            var view = new HudView(CloneLayout(), new HudModel());

            Assert.That(view.HealthLabel.text, Is.EqualTo("--"));
            Assert.That(view.AmmoLabel.text, Is.EqualTo("-- / --"));
            Assert.That(view.TimeLabel.text, Is.EqualTo("--:--"));
        }

        [Test]
        public void ClearingTheValuesShowsThePlaceholdersAgain()
        {
            var model = new HudModel();
            model.SetHealth(50, 100);
            model.SetAmmo(12, 48, false);
            model.SetTime(30f);
            var view = new HudView(CloneLayout(), model);

            model.ClearHealth();
            model.ClearAmmo();
            model.ClearTime();

            Assert.That(view.HealthLabel.text, Is.EqualTo("--"));
            Assert.That(view.AmmoLabel.text, Is.EqualTo("-- / --"));
            Assert.That(view.TimeLabel.text, Is.EqualTo("--:--"));
        }

        [Test]
        public void HidingTheReserveShowsOnlyTheMagazineAndShowingItBringsTheReserveBack()
        {
            var model = new HudModel();
            var view = new HudView(CloneLayout(), model);

            model.SetReserveAmmoShown(false);
            Assert.That(view.AmmoLabel.text, Is.EqualTo("--"), "no weapon yet");

            model.SetAmmo(12, 48, false);
            Assert.That(view.AmmoLabel.text, Is.EqualTo("12"));

            model.SetAmmo(0, 48, true);
            Assert.That(view.AmmoLabel.text, Is.EqualTo(Localizer.Resolve(HudNumberFormat.Reloading)));

            model.SetAmmo(12, 36, false);
            model.SetReserveAmmoShown(true);
            Assert.That(view.AmmoLabel.text, Is.EqualTo("12 / 36"));
        }

        [Test]
        public void ReloadingWithAnInfiniteReserveShowsTheReloadingTextAndInfinity()
        {
            var model = new HudModel();
            var view = new HudView(CloneLayout(), model);

            model.SetAmmo(0, 0, true, true);

            Assert.That(view.AmmoLabel.text, Is.EqualTo(Localizer.Resolve(HudNumberFormat.Reloading) + " / ∞"));
        }

        [Test]
        public void TheTimeRoundsUpAndStopsAtTheLongestShownTime()
        {
            var model = new HudModel();
            var view = new HudView(CloneLayout(), model);

            model.SetTime(0.2f);
            Assert.That(view.TimeLabel.text, Is.EqualTo("00:01"));

            model.SetTime(0f);
            Assert.That(view.TimeLabel.text, Is.EqualTo("00:00"));

            model.SetTime(float.PositiveInfinity);
            Assert.That(view.TimeLabel.text, Is.EqualTo("99:59"));
        }

        [Test]
        public void TheNumberFormatMatchesTheHudFormat()
        {
            Assert.That(HudNumberFormat.Health(75), Is.EqualTo(HudFormat.Health(75)));
            Assert.That(HudNumberFormat.Health(null), Is.EqualTo(HudFormat.NoHealth));
            Assert.That(HudNumberFormat.Ammo(30, 90, false, false, true), Is.EqualTo(HudFormat.Ammo(30, 90, false, false)));
            Assert.That(HudNumberFormat.Ammo(0, 90, true, false, true), Is.EqualTo(HudFormat.Ammo(0, 90, true, false)));
            Assert.That(HudNumberFormat.Ammo(30, 90, false, true, true), Is.EqualTo(HudFormat.Ammo(30, 90, false, true)));
            Assert.That(HudNumberFormat.Ammo(0, 90, true, true, true), Is.EqualTo(HudFormat.Ammo(0, 90, true, true)));
            Assert.That(HudNumberFormat.Ammo(30, 90, false, false, false), Is.EqualTo(HudFormat.Magazine(30, false)));
            Assert.That(HudNumberFormat.Ammo(0, 90, true, false, false), Is.EqualTo(HudFormat.Magazine(0, true)));
            Assert.That(HudNumberFormat.Ammo(null, null, false, false, true), Is.EqualTo(HudFormat.NoAmmo));
            Assert.That(HudNumberFormat.Ammo(null, null, false, false, false), Is.EqualTo(HudFormat.NoMagazine));
            Assert.That(HudNumberFormat.Time(65), Is.EqualTo(HudFormat.Time(65f)));
            Assert.That(HudNumberFormat.Time(HudFormat.MaxDisplayedSeconds), Is.EqualTo(HudFormat.Time(HudFormat.MaxDisplayedSeconds)));
            Assert.That(HudNumberFormat.Time(null), Is.EqualTo(HudFormat.NoTime));
        }

        [Test]
        public void TheNumberFormatRejectsNegativeNumbers()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => HudNumberFormat.Health(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => HudNumberFormat.Ammo(-1, 0, false, false, true));
            Assert.Throws<ArgumentOutOfRangeException>(() => HudNumberFormat.Ammo(0, -1, false, false, true));
            Assert.Throws<ArgumentOutOfRangeException>(() => HudNumberFormat.Time(-1));
        }

        [Test]
        public void StopsUpdatingAfterDispose()
        {
            var model = new HudModel();
            var health = new Health(100);
            model.BindHealth(health);
            var view = new HudView(CloneLayout(), model);

            view.Dispose();
            health.TakeDamage(10);

            Assert.That(model.CurrentHealth, Is.EqualTo(90));
            Assert.That(view.HealthLabel.text, Is.EqualTo("100"));
        }

        [Test]
        public void RejectsALayoutWithoutTheLabels()
        {
            var root = new VisualElement();
            root.Add(new Label { name = HudView.HealthLabelName });

            Assert.Throws<InvalidOperationException>(() => new HudView(root, new HudModel()));
        }

        [Test]
        public void RejectsMissingArguments()
        {
            Assert.Throws<ArgumentNullException>(() => new HudView(null, new HudModel()));
            Assert.Throws<ArgumentNullException>(() => new HudView(CloneLayout(), null));
        }

        private static Func<StringTable> UseGaugeTexts()
        {
            var previous = GameText.Source;
            var table = new StringTable(new Dictionary<string, string>
            {
                [HudView.ChargeKey] = "Charge {0}%",
                [HudView.HeatKey] = "Heat {0}%",
                [HudView.MagnificationKey] = "x{0}",
                [HudView.LockOnKey] = "Lock {0}%",
                [HudView.LockedOnKey] = "Locked",
                ["ammo.slug"] = "Slug",
            });
            GameText.Use(() => table);
            return previous;
        }

        private static void AssertGaugesHidden(HudView view)
        {
            Assert.That(view.ChargeLabel.style.display.value, Is.EqualTo(DisplayStyle.None), "charge");
            Assert.That(view.HeatLabel.style.display.value, Is.EqualTo(DisplayStyle.None), "heat");
            Assert.That(view.AmmoTypeLabel.style.display.value, Is.EqualTo(DisplayStyle.None), "ammo type");
            Assert.That(view.MagnificationLabel.style.display.value, Is.EqualTo(DisplayStyle.None), "magnification");
            Assert.That(view.LockOnLabel.style.display.value, Is.EqualTo(DisplayStyle.None), "lock-on");
        }

        [Test]
        public void TheWeaponGaugesAreHiddenWithoutValues()
        {
            var view = new HudView(CloneLayout(), new HudModel());

            AssertGaugesHidden(view);
        }

        [Test]
        public void TheWeaponGaugesShowTheirValuesAndHideWhenCleared()
        {
            var previous = UseGaugeTexts();
            try
            {
                var model = new HudModel();
                var view = new HudView(CloneLayout(), model);

                model.SetCharge(0.5f);
                model.SetHeat(0.25f);
                model.SetAmmoType(Slug);
                model.SetMagnification(4f);
                model.SetLockOn(0.4f);

                Assert.That(view.ChargeLabel.text, Is.EqualTo("Charge 50%"));
                Assert.That(view.HeatLabel.text, Is.EqualTo("Heat 25%"));
                Assert.That(view.AmmoTypeLabel.text, Is.EqualTo("Slug"));
                Assert.That(view.MagnificationLabel.text, Is.EqualTo("x4"));
                Assert.That(view.LockOnLabel.text, Is.EqualTo("Lock 40%"));
                Assert.That(view.ChargeLabel.style.display.value, Is.EqualTo(DisplayStyle.Flex));
                Assert.That(view.HeatLabel.style.display.value, Is.EqualTo(DisplayStyle.Flex));
                Assert.That(view.AmmoTypeLabel.style.display.value, Is.EqualTo(DisplayStyle.Flex));
                Assert.That(view.MagnificationLabel.style.display.value, Is.EqualTo(DisplayStyle.Flex));
                Assert.That(view.LockOnLabel.style.display.value, Is.EqualTo(DisplayStyle.Flex));
                Assert.That(view.LockOnLabel.ClassListContains(HudView.LockedOnClass), Is.False);

                model.SetLockOn(1f);
                Assert.That(view.LockOnLabel.text, Is.EqualTo("Locked"));
                Assert.That(view.LockOnLabel.ClassListContains(HudView.LockedOnClass), Is.True);

                model.ClearCharge();
                model.ClearHeat();
                model.ClearAmmoType();
                model.ClearMagnification();
                model.ClearLockOn();
                AssertGaugesHidden(view);
            }
            finally
            {
                GameText.Use(previous);
            }
        }

        [Test]
        public void HardcoreHidesTheWeaponGaugesEvenWithValues()
        {
            var model = new HudModel();
            model.SetCharge(0.5f);
            model.SetHeat(0.5f);
            model.SetAmmoType(Slug);
            model.SetMagnification(2f);
            model.SetLockOn(0.5f);

            var view = new HudView(CloneLayout(), model, HudVisibility.Hardcore);
            AssertGaugesHidden(view);

            view.ApplyVisibility(HudVisibility.Full);
            Assert.That(view.ChargeLabel.style.display.value, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(view.LockOnLabel.style.display.value, Is.EqualTo(DisplayStyle.Flex));
        }
    }
}
