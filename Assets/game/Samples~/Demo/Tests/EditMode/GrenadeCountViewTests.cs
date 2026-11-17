using System.Collections.Generic;
using Shooter.Core;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    public sealed class GrenadeCountViewTests
    {
        private static readonly GameRules _twoGrenades =
            GameRules.Default.WithActions(ActionRules.Default.WithGrenade(Grenade.On).WithGrenadeCount(2));

        private static readonly GrenadeSpec _smoke = new GrenadeSpec(
            "ecaf6e57-cb77-4707-9321-6ea5b659408f",
            "grenade.smoke",
            new ImpactSpec(damage: 0, areaEffect: new AreaEffectSpec(5f, 3f, 0.5f, 0, visualId: "smoke")),
            fuseSeconds: 1f);

        private static readonly GrenadeCatalog _fragAndSmoke =
            new GrenadeCatalog(new[] { GrenadeSpec.Default, _smoke }, new[] { GrenadeSpec.DefaultId, _smoke.Id });

        private readonly List<GameObject> _created = new List<GameObject>();
        private PanelSettings _hudSettings;

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
            if (_hudSettings != null)
            {
                Object.DestroyImmediate(_hudSettings);
                _hudSettings = null;
            }

            GameSignals.ResetAll();
        }

        [Test]
        public void TheGrenadeCountNamesTheTypeOnlyWhenThereIsAChoice()
        {
            var thrower = CreateThrower(_twoGrenades);
            var hud = new GameObject("HUD");
            _created.Add(hud);
            var view = hud.AddComponent<GrenadeCountView>();

            view.Show(thrower);
            Assert.That(view.Text, Is.EqualTo(Localizer.Resolve(HudText.Grenades(2))));

            thrower.ApplyCatalog(_fragAndSmoke);
            thrower.SelectNext();
            Assert.That(view.Text, Is.EqualTo(Localizer.Resolve(HudText.Grenades(DisplayNames.Of(_smoke), 2))));
        }

        [Test]
        public void TheGrenadeCountShowsTheLocalPlayersThrowerFromTheSignal()
        {
            var view = CreateHud().GrenadeCount;
            var thrower = CreateThrower(_twoGrenades);
            Assert.That(view.Source, Is.Null, "nothing to show before a local player is published");
            Assert.That(view.Element.style.display.value, Is.EqualTo(DisplayStyle.None));

            GameSignals.LocalPlayer.Publish(new LocalPlayerSignal(thrower.gameObject, null, null, null, null, thrower, null, null));
            Assert.That(view.Source, Is.SameAs(thrower));
            Assert.That(view.Text, Is.EqualTo(Localizer.Resolve(HudText.Grenades(2))));
            Assert.That(((Label)view.Element).text, Is.EqualTo(view.Text));
            Assert.That(view.Element.style.display.value, Is.EqualTo(DisplayStyle.Flex));

            thrower.Configure(GameRules.Default);
            Assert.That(view.Text, Is.Empty, "the view rereads the thrower when its rule turns grenades off");
            Assert.That(view.Element.style.display.value, Is.EqualTo(DisplayStyle.None));

            GameSignals.LocalPlayer.Clear();
            Assert.That(view.Source, Is.Null);
        }

        [Test]
        public void ThePublishedCountShowsWithoutALocalPlayerAndWinsOverIt()
        {
            var hud = CreateHud();
            var view = hud.GrenadeCount;

            GameSignals.GrenadeCount.Publish(new GrenadeCountStatus(true, 3, null));
            Assert.That(view.Source, Is.Null);
            Assert.That(view.Text, Is.EqualTo(Localizer.Resolve(HudText.Grenades(3))));
            Assert.That(view.Element.style.display.value, Is.EqualTo(DisplayStyle.Flex));

            hud.RebuildView();
            Assert.That(view.Text, Is.EqualTo(Localizer.Resolve(HudText.Grenades(3))), "rebinding catches up with the published count");

            var thrower = CreateThrower(_twoGrenades);
            GameSignals.LocalPlayer.Publish(new LocalPlayerSignal(thrower.gameObject, null, null, null, null, thrower, null, null));
            Assert.That(view.Source, Is.Null, "the published count wins while it is out");
            Assert.That(view.Text, Is.EqualTo(Localizer.Resolve(HudText.Grenades(3))));

            GameSignals.GrenadeCount.Publish(new GrenadeCountStatus(false, 3, null));
            Assert.That(view.Text, Is.Empty, "grenades off by the rule");
            Assert.That(view.Element.style.display.value, Is.EqualTo(DisplayStyle.None));

            GameSignals.GrenadeCount.Clear();
            Assert.That(view.Source, Is.SameAs(thrower), "clearing the count goes back to the local player");
            Assert.That(view.Text, Is.EqualTo(Localizer.Resolve(HudText.Grenades(2))));
        }

        [Test]
        public void ThePublishedCountNamesTheTypeById()
        {
            var view = CreateHud().GrenadeCount;
            Assume.That(KitTuning.Grenades.TryFind(GrenadeSpec.DefaultId, out var frag), Is.True);

            GameSignals.GrenadeCount.Publish(new GrenadeCountStatus(true, 2, GrenadeSpec.DefaultId));
            Assert.That(view.Text, Is.EqualTo(Localizer.Resolve(HudText.Grenades(DisplayNames.Of(frag), 2))));

            GameSignals.GrenadeCount.Publish(new GrenadeCountStatus(true, -1, "not-in-the-json"));
            Assert.That(view.Text, Is.EqualTo(Localizer.Resolve(HudText.Grenades(LocalizedText.Literal("not-in-the-json"), 0))), "an unknown Id is shown as it is");
        }

        private Hud CreateHud()
        {
            var hud = Hud.Create("GrenadeTests.Hud");
            _created.Add(hud.gameObject);
            _hudSettings = hud.OwnedPanelSettings;
            return hud;
        }

        private GrenadeThrower CreateThrower(GameRules rules)
        {
            var body = CreateBox("Thrower", new Vector3(0f, 1f, 0f), new Vector3(1f, 2f, 1f));
            body.AddComponent<Damageable>().Initialize("Thrower", 100);
            var thrower = body.AddComponent<GrenadeThrower>();
            thrower.Configure(rules);
            thrower.Thrown += grenade => _created.Add(grenade.gameObject);
            return thrower;
        }

        private GameObject CreateBox(string name, Vector3 position, Vector3 size)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _created.Add(box);
            box.name = name;
            box.transform.position = position;
            box.transform.localScale = size;
            Physics.SyncTransforms();
            return box;
        }
    }
}
