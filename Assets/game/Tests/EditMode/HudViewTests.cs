using System;
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
            return new WeaponSpec("00000000-0000-4000-8000-000000000001", "Rifle", WeaponCategory.AssaultRifle, FireMode.Auto, 30, 90, 0f, 1f, 25, 100f, DamageFalloff.None);
        }

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
        public void CrosshairGapGrowsFromTheMinimumToTheMaximum()
        {
            Assert.That(HudView.CrosshairGap(0f), Is.EqualTo(HudView.MinCrosshairGap));
            Assert.That(HudView.CrosshairGap(1f), Is.EqualTo(HudView.MaxCrosshairGap));
            Assert.That(HudView.CrosshairGap(0.5f), Is.GreaterThan(HudView.MinCrosshairGap).And.LessThan(HudView.MaxCrosshairGap));
            Assert.That(HudView.CrosshairGap(2f), Is.EqualTo(HudView.MaxCrosshairGap));
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
            Assert.That(view.ResultLabel.text, Is.EqualTo(HudFormat.Draw));
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
            Assert.That(view.AmmoLabel.text, Is.EqualTo(HudFormat.Ammo(0, 90, true)));
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
            Assert.That(view.AmmoLabel.text, Is.EqualTo("30 / " + HudFormat.InfiniteReserve));

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
                "00000000-0000-4000-8000-000000000002", "Handgun", WeaponCategory.Handgun, FireMode.Semi, 12, 48, 0.2f, 1.5f, 34, 60f, DamageFalloff.None)));
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
            Assert.That(view.NoticeLabel.text, Is.EqualTo(HudFormat.CheckpointSaved));
            Assert.That(view.NoticeLabel.style.display.value, Is.EqualTo(DisplayStyle.Flex));

            notice.ShowRestartCountdown(2f);
            Assert.That(view.NoticeLabel.text, Is.EqualTo(HudFormat.CheckpointRestart(2)));

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
        public void StopsUpdatingAfterDispose()
        {
            var model = new HudModel();
            var health = new Health(100);
            model.BindHealth(health);
            var view = new HudView(CloneLayout(), model);

            view.Dispose();
            health.TakeDamage(10);

            Assert.That(model.HealthText, Is.EqualTo("90"));
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
    }
}
