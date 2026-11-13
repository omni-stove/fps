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
            return new WeaponSpec("rifle", null, FireMode.Auto, 30, 90, 0f, 1f, 25, 100f, DamageFalloff.None);
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
            Assert.That(root.Q<Label>(HudView.TimeLabelName), Is.Not.Null);
            Assert.That(root.Q<VisualElement>(HudView.CrosshairName), Is.Not.Null);
            Assert.That(root.Q<Label>(HudView.NoticeLabelName), Is.Not.Null);
            Assert.That(root.Q<VisualElement>(HudView.ScorePanelName), Is.Not.Null);
            Assert.That(root.Q<Label>(HudView.FirstScoreLabelName), Is.Not.Null);
            Assert.That(root.Q<Label>(HudView.SecondScoreLabelName), Is.Not.Null);
            Assert.That(root.Q<Label>(HudView.ResultLabelName), Is.Not.Null);
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
