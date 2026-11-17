using Shooter.Core;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    public sealed class AbilitySlotsViewTests
    {
        private const string ChargeKind = "remote-charge";

        private static readonly GameRules _on = GameRules.Default.WithActions(ActionRules.Default.WithAbilities(Abilities.On));

        private GameObject _player;
        private AbilityHost _host;
        private NullAbilityDefinition _null;
        private Hud _hud;

        [SetUp]
        public void SetUp()
        {
            _player = new GameObject("AbilitySlotsViewTests.Player");
            _host = _player.AddComponent<AbilityHost>();

            _null = ScriptableObject.CreateInstance<NullAbilityDefinition>();
            _null.name = "NullAbility";
            _null.Id = "null";
            _null.MaxCharges = 2;
            _null.RechargeSeconds = 10f;
            _null.CooldownSeconds = 1f;
            _null.WindupSeconds = 0f;
        }

        [TearDown]
        public void TearDown()
        {
            if (_hud != null)
            {
                var settings = _hud.OwnedPanelSettings;
                Object.DestroyImmediate(_hud.gameObject);
                if (settings != null)
                {
                    Object.DestroyImmediate(settings);
                }
            }

            Object.DestroyImmediate(_player);
            Object.DestroyImmediate(_null);
            GameSignals.ResetAll();
        }

        [Test]
        public void TheHudShowsChargesAndTheWaitWhileAbilitiesAreOn()
        {
            _hud = Hud.Create("AbilitySlotsViewTests.Hud");
            var view = AbilitySlotsView.AttachTo(_hud);
            Assert.That(AbilitySlotsView.AttachTo(_hud), Is.SameAs(view), "attached once");
            _host.Equip(0, _null);
            view.Show(_host);

            Assert.That(view.IsShown, Is.False, "abilities are off");
            Assert.That(view.Text(0), Is.Empty);
            Assert.That(view.Element.style.display.value, Is.EqualTo(UnityEngine.UIElements.DisplayStyle.None));

            _host.Configure(_on);
            Assert.That(view.IsShown, Is.True, "the view rereads the slots when the host changes");
            Assert.That(view.Text(0), Is.EqualTo(Localizer.Resolve(new LocalizedText("hud.ability", 1, 2, 2))));
            Assert.That(view.Text(1), Is.Empty, "the empty slot shows nothing");
            Assert.That(view.IsWaiting(0), Is.False);

            _host.TryUse(0);
            _host.Step(PlayerCommands.None, 0.25f);
            Assert.That(view.Text(0), Is.EqualTo(Localizer.Resolve(new LocalizedText("hud.ability.wait", 1, 1, 2, "0.8"))));
            Assert.That(view.IsWaiting(0), Is.True);
            Assert.That(view.Element.childCount, Is.EqualTo(AbilityHost.SlotCount));
            Assert.That(view.Element[0].ClassListContains(AbilitySlotsView.WaitingRowClass), Is.True);

            view.Show(null);
            Assert.That(view.IsShown, Is.False);
        }

        [Test]
        public void TheHudShowsTheLocalPlayersSlotsFromTheSignal()
        {
            _hud = Hud.Create("AbilitySlotsViewTests.Hud");
            var view = _hud.AbilitySlots;
            _host.Configure(_on);
            _host.Equip(0, _null);
            Assert.That(view.Source, Is.Null, "nothing to show before a local player is published");

            GameSignals.LocalPlayer.Publish(new LocalPlayerSignal(_player, null, null, null, null, null, _host, null));
            Assert.That(view.Source, Is.SameAs(_host));
            Assert.That(view.Text(0), Is.EqualTo(Localizer.Resolve(new LocalizedText("hud.ability", 1, 2, 2))));

            _host.TryUse(0);
            Assert.That(view.Text(0), Is.EqualTo(Localizer.Resolve(new LocalizedText("hud.ability.active", 1, 1, 2))));
            _host.Step(PlayerCommands.None, 0.25f);
            Assert.That(view.Text(0), Is.EqualTo(Localizer.Resolve(new LocalizedText("hud.ability.wait", 1, 1, 2, "0.8"))));

            _hud.RebuildView();
            Assert.That(view.Source, Is.SameAs(_host), "a rebuilt HUD picks the published player up again");

            GameSignals.LocalPlayer.Clear();
            Assert.That(view.Source, Is.Null);
            Assert.That(view.IsShown, Is.False);
        }

        [Test]
        public void TheHudCarriesTheViewFromTheStart()
        {
            _hud = Hud.Create("AbilitySlotsViewTests.Hud");

            Assert.That(_hud.AbilitySlots, Is.Not.Null);
            Assert.That(_hud.GetComponents<AbilitySlotsView>(), Has.Length.EqualTo(1));
        }

        [Test]
        public void TheDetonateHintNamesThePlacedChargeAndItsCount()
        {
            Assert.That(
                AbilitySlotsView.FollowUpText(AbilityFollowUp.Detonate(ChargeKind, RemoteChargeSpec.DefaultNameKey, 2)),
                Is.EqualTo(new LocalizedText(AbilitySlotsView.DetonateKey, new LocalizedText(RemoteChargeSpec.DefaultNameKey), 2)));
        }

        [Test]
        public void TheDetonateHintNamesTheChargeByIdWithoutANameKey()
        {
            Assert.That(
                AbilitySlotsView.FollowUpText(AbilityFollowUp.Detonate(ChargeKind, string.Empty, 1)),
                Is.EqualTo(new LocalizedText(AbilitySlotsView.DetonateKey, LocalizedText.Literal(ChargeKind), 1)));
        }
    }
}
