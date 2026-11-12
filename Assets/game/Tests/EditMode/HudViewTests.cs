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
        }

        [Test]
        public void ShowsTheModelTextAtOnce()
        {
            var model = new HudModel();
            model.BindHealth(new Health(100));
            model.BindWeapon(new Weapon(new WeaponSpec(30, 90, 0f, 1f, 25)));
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
            var weapon = new Weapon(new WeaponSpec(30, 90, 0f, 1f, 25));
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
