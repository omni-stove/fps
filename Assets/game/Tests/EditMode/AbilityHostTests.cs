using System;
using System.Text.RegularExpressions;
using Fps.Core;
using Fps.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Fps.Tests.EditMode
{
    /// <summary>
    /// 能力の枠 (<see cref="AbilityHost"/>): ルールの Off で使えないこと、入力で使って回数が減り待ち時間の間は断ること、
    /// 効果が出るまでの秒数と呼ばれる順、止められたとき、移動の横取りで <see cref="PlayerActionConditions.AbilityMoving"/> が立つこと、
    /// HUD (<see cref="AbilitySlotsView"/>) に回数と待ち時間が出ること。
    /// </summary>
    public sealed class AbilityHostTests
    {
        private static readonly GameRules _on = GameRules.Default.WithActions(ActionRules.Default.WithAbilities(Abilities.On));

        private GameObject _player;
        private AbilityHost _host;
        private NullAbilityDefinition _null;
        private RecordingAbilityDefinition _recording;
        private Hud _hud;

        [SetUp]
        public void SetUp()
        {
            _player = new GameObject("AbilityHostTests.Player");
            _host = _player.AddComponent<AbilityHost>();

            _null = ScriptableObject.CreateInstance<NullAbilityDefinition>();
            _null.name = "NullAbility";
            _null.Id = "null";
            _null.MaxCharges = 2;
            _null.RechargeSeconds = 10f;
            _null.CooldownSeconds = 1f;
            _null.WindupSeconds = 0f;

            _recording = ScriptableObject.CreateInstance<RecordingAbilityDefinition>();
            _recording.name = "Recording";
            _recording.Id = "recording";
            _recording.MaxCharges = 1;
            _recording.RechargeSeconds = 5f;
            _recording.CooldownSeconds = 0f;
            _recording.WindupSeconds = 0.5f;
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
            Object.DestroyImmediate(_recording);
        }

        [Test]
        public void TheRuleIsOffUntilConfigured()
        {
            _host.Equip(0, _null);

            Assert.That(_host.Rule, Is.EqualTo(Abilities.Off));
            Assert.That(_host.UseBlock(0), Is.EqualTo(AbilityUseBlock.RuleOff));
            Assert.That(_host.TryUse(0), Is.False);
            Assert.That(_host.Slot(0).Charges, Is.EqualTo(2), "a refused use keeps the charge");
        }

        [Test]
        public void OffRulesIgnoreTheInput()
        {
            _host.Configure(GameRules.Default);
            _host.Equip(0, _null);

            _host.Step(PlayerCommands.Ability1, 0.1f);

            Assert.That(_host.IsRunning(0), Is.False);
            Assert.That(_host.Slot(0).Charges, Is.EqualTo(2));
        }

        [Test]
        public void Ability1UsesTheFirstSlotAndTheCooldownBlocksIt()
        {
            _host.Configure(_on);
            _host.Equip(0, _null);

            LogAssert.Expect(LogType.Log, "[Ability] slot=1 id=null used charges=1/2 cooldown=1.0");
            _host.Step(PlayerCommands.Ability1, 0f);
            Assert.That(_host.Slot(0).Charges, Is.EqualTo(1));
            Assert.That(_host.IsRunning(0), Is.True);

            // 何もしない能力は効果が出たら終わる。
            LogAssert.Expect(LogType.Log, AbilityHost.FormatEndedLog(0, _null, false));
            _host.Step(PlayerCommands.None, 0.4f);
            Assert.That(_host.IsRunning(0), Is.False);

            LogAssert.Expect(LogType.Log, "[Ability] slot=1 id=null rejected reason=Cooldown wait=0.6");
            _host.Step(PlayerCommands.Ability1, 0f);
            Assert.That(_host.Slot(0).Charges, Is.EqualTo(1), "the cooldown keeps the charge");
            Assert.That(_host.UseBlock(0), Is.EqualTo(AbilityUseBlock.Cooldown));

            _host.Step(PlayerCommands.None, 0.6f);
            Assert.That(_host.TryUse(0), Is.True);
            Assert.That(_host.Slot(0).Charges, Is.Zero);
        }

        [Test]
        public void Ability2UsesTheSecondSlotOnly()
        {
            _host.Configure(_on);
            _host.Equip(new AbilityDefinition[] { _null, _recording });

            _host.Step(PlayerCommands.Ability2, 0f);

            Assert.That(_host.Slot(0).Charges, Is.EqualTo(2));
            Assert.That(_host.IsRunning(1), Is.True);
            Assert.That(_recording.Calls, Is.EqualTo(new[] { "begin" }));
        }

        [Test]
        public void TheEffectStartsAfterTheWindupAndTheAbilityDecidesWhenToEnd()
        {
            _host.Configure(_on);
            _host.Equip(0, _recording);

            Assert.That(_host.TryUse(0), Is.True);
            _host.Step(PlayerCommands.None, 0.3f);
            LogAssert.Expect(LogType.Log, AbilityHost.FormatEffectLog(0, _recording));
            _host.Step(PlayerCommands.None, 0.3f);
            _host.Step(PlayerCommands.None, 0.1f);
            _recording.KeepRunning = false;
            _host.Step(PlayerCommands.None, 0.1f);

            Assert.That(
                _recording.Calls,
                Is.EqualTo(new[] { "begin", "tick:WindingUp", "tick:effect", "tick:Active", "tick:Active", "end" }));
            Assert.That(_host.IsRunning(0), Is.False);
        }

        [Test]
        public void ANoWindupAbilitySeesTheEffectOnItsFirstTick()
        {
            _recording.WindupSeconds = 0f;
            _host.Configure(_on);
            _host.Equip(0, _recording);

            _host.TryUse(0);
            _host.Step(PlayerCommands.None, 0.1f);
            _host.Step(PlayerCommands.None, 0.1f);

            Assert.That(_recording.Calls, Is.EqualTo(new[] { "begin", "tick:effect", "tick:Active" }));
        }

        [Test]
        public void TheAbilityCanRefuse()
        {
            _recording.Allows = false;
            _host.Configure(_on);
            _host.Equip(0, _recording);

            LogAssert.Expect(LogType.Log, AbilityHost.FormatRejectedLog(0, _recording, AbilityUseBlock.Refused, _host.Slot(0)));
            Assert.That(_host.TryUse(0), Is.False);
            Assert.That(_host.Slot(0).Charges, Is.EqualTo(1));
            Assert.That(_recording.Calls, Is.Empty);
        }

        [Test]
        public void TurningTheRuleOffInterruptsTheAbility()
        {
            _host.Configure(_on);
            _host.Equip(0, _recording);
            _host.TryUse(0);

            _host.Configure(GameRules.Default);

            Assert.That(_recording.Calls, Is.EqualTo(new[] { "begin", "end:interrupted" }));
            Assert.That(_host.IsRunning(0), Is.False);
        }

        [Test]
        public void ReplacingTheSlotInterruptsTheAbility()
        {
            _host.Configure(_on);
            _host.Equip(0, _recording);
            _host.TryUse(0);

            _host.Equip(0, _null);

            Assert.That(_recording.Calls, Is.EqualTo(new[] { "begin", "end:interrupted" }));
            Assert.That(_host.Definition(0), Is.SameAs(_null));
            Assert.That(_host.Slot(0).Charges, Is.EqualTo(2), "the new ability starts full");
        }

        [Test]
        public void AnExceptionInTickEndsTheAbilityAsInterrupted()
        {
            _recording.ThrowOnTick = true;
            _host.Configure(_on);
            _host.Equip(0, _recording);
            _host.TryUse(0);

            LogAssert.Expect(LogType.Exception, new Regex("boom"));
            _host.Step(PlayerCommands.None, 0.1f);

            Assert.That(_recording.Calls, Is.EqualTo(new[] { "begin", "tick:WindingUp", "end:interrupted" }));
        }

        [Test]
        public void AnInvalidDefinitionThrowsAndKeepsTheSlot()
        {
            _host.Equip(0, _null);
            _recording.MaxCharges = 0;

            Assert.Throws<AbilityDefinitionException>(() => _host.Equip(0, _recording));
            Assert.That(_host.Definition(0), Is.SameAs(_null));

            _recording.MaxCharges = 1;
            _recording.ReturnNull = true;
            Assert.Throws<AbilityDefinitionException>(() => _host.Equip(0, _recording));
            Assert.That(_host.Definition(0), Is.SameAs(_null));
        }

        [Test]
        public void EquippingAListEmptiesBadSlotsAndWarnsAboutExtras()
        {
            _recording.CooldownSeconds = -1f;

            LogAssert.Expect(LogType.Warning, new Regex("1 more abilities were given"));
            LogAssert.Expect(LogType.Error, new Regex("could not equip slot 2"));
            _host.Equip(new AbilityDefinition[] { _null, _recording, _null });

            Assert.That(_host.Definition(0), Is.SameAs(_null));
            Assert.That(_host.Definition(1), Is.Null);
            Assert.That(_host.Slot(1), Is.Null);
            Assert.That(_host.UseBlock(1), Is.EqualTo(AbilityUseBlock.RuleOff));

            _host.Configure(_on);
            Assert.That(_host.UseBlock(1), Is.EqualTo(AbilityUseBlock.Empty));
        }

        [Test]
        public void SlotIndicesAreChecked()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => _host.Equip(AbilityHost.SlotCount, _null));
            Assert.Throws<ArgumentOutOfRangeException>(() => _host.TryUse(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => AbilityHost.CommandFor(2));
            Assert.Throws<ArgumentOutOfRangeException>(() => _host.Step(PlayerCommands.None, -1f));
            Assert.Throws<ArgumentNullException>(() => _host.Configure(null));
        }

        [Test]
        public void ARespawnRefillsTheSlots()
        {
            Object.DestroyImmediate(_host);
            var damageable = _player.AddComponent<Damageable>();
            damageable.Initialize("Player", 100);
            _host = _player.AddComponent<AbilityHost>();
            _host.Configure(_on);
            _host.Equip(0, _null);
            _host.TryUse(0);
            _host.Step(PlayerCommands.None, 0.1f);
            Assert.That(_host.Slot(0).Charges, Is.EqualTo(1));

            damageable.ResetHealth();

            Assert.That(_host.Slot(0).Charges, Is.EqualTo(2));
            Assert.That(_host.Slot(0).CooldownRemaining, Is.Zero);
        }

        [Test]
        public void MovementFromAnAbilityRaisesAbilityMovingUntilItEnds()
        {
            var controller = _player.AddComponent<CharacterController>();
            controller.height = 2f;
            controller.radius = 0.5f;
            controller.center = Vector3.zero;
            var mover = _player.AddComponent<PlayerController>();
            var look = _player.AddComponent<FirstPersonLook>();
            var move = new InputAction("Move", InputActionType.Value, expectedControlType: "Vector2");
            var sprint = new InputAction("Sprint", InputActionType.Button);
            var crouch = new InputAction("Crouch", InputActionType.Button);
            var jump = new InputAction("Jump", InputActionType.Button);
            try
            {
                mover.Initialize(controller, look, move, sprint, crouch, jump, new MotorSpec(5f, 8f, 1.2f, 20f, 0.1f, 2.5f));
                var pull = new StillOverride();
                _recording.Movement = pull;
                _recording.WindupSeconds = 0f;
                _host.Configure(_on);
                _host.Equip(0, _recording);

                _host.TryUse(0);
                mover.Tick(Vector2.zero, false, false, false, 0.02f);

                Assert.That(mover.MovementOverrideConditions & PlayerActionConditions.AbilityMoving, Is.EqualTo(PlayerActionConditions.AbilityMoving));
                Assert.That(mover.MovementOverrideConditions & PlayerActionConditions.Sprinting, Is.EqualTo(PlayerActionConditions.Sprinting), "the ability's own bits are kept");
                Assert.That(pull.Begins, Is.EqualTo(1));

                _recording.KeepRunning = false;
                _host.Step(PlayerCommands.None, 0.02f);

                Assert.That(pull.Ends, Is.EqualTo(1), "ending the ability ends its movement");
                Assert.That(mover.MovementOverrides, Is.Empty);
                Assert.That(mover.MovementOverrideConditions, Is.EqualTo(PlayerActionConditions.None));
            }
            finally
            {
                move.Dispose();
                sprint.Dispose();
                crouch.Dispose();
                jump.Dispose();
            }
        }

        [Test]
        public void HandingOffControlBlocksAndInterrupts()
        {
            _player.AddComponent<CharacterController>();
            var mover = _player.AddComponent<PlayerController>();
            Object.DestroyImmediate(_host);
            _host = _player.AddComponent<AbilityHost>();
            _host.Configure(_on);
            _host.Equip(0, _recording);
            _host.TryUse(0);
            var seat = new object();

            mover.Handoff.Claim(seat);
            _host.Step(PlayerCommands.None, 0.1f);

            Assert.That(_recording.Calls, Is.EqualTo(new[] { "begin", "end:interrupted" }));
            Assert.That(_host.UseBlock(0), Is.EqualTo(AbilityUseBlock.Incapacitated));
        }

        [Test]
        public void TheInstallerAddsOneHostAndAppliesTheRule()
        {
            var installer = new AbilityActionInstaller();
            installer.Install(_player, _on);
            installer.Install(_player, _on);

            Assert.That(_player.GetComponents<AbilityHost>().Length, Is.EqualTo(1));
            Assert.That(_host.Rule, Is.EqualTo(Abilities.On));
        }

        [Test]
        public void ALoadoutCarriesAbilities()
        {
            var loadout = ScriptableObject.CreateInstance<LoadoutDefinition>();
            try
            {
                Assert.That(loadout.Abilities, Is.Empty);
                var abilities = new AbilityDefinition[] { _null, null };
                loadout.SetAbilities(abilities);
                abilities[0] = _recording;

                Assert.That(loadout.Abilities, Is.EqualTo(new AbilityDefinition[] { _null, null }), "the list is copied");

                _host.Equip(loadout.Abilities);
                Assert.That(_host.Definition(0), Is.SameAs(_null));
                Assert.That(_host.Definition(1), Is.Null);
            }
            finally
            {
                Object.DestroyImmediate(loadout);
            }
        }

        [Test]
        public void TheHudShowsChargesAndTheWaitWhileAbilitiesAreOn()
        {
            _hud = Hud.Create("AbilityHostTests.Hud", null, null, null);
            var view = AbilitySlotsView.AttachTo(_hud);
            Assert.That(AbilitySlotsView.AttachTo(_hud), Is.SameAs(view), "attached once");
            _host.Equip(0, _null);
            view.Show(_host);

            Assert.That(view.IsShown, Is.False, "abilities are off");
            Assert.That(view.Text(0), Is.Empty);
            Assert.That(view.Element.style.display.value, Is.EqualTo(UnityEngine.UIElements.DisplayStyle.None));

            _host.Configure(_on);
            view.Refresh();
            Assert.That(view.IsShown, Is.True);
            Assert.That(view.Text(0), Is.EqualTo(Localizer.Resolve(new LocalizedText("hud.ability", 1, 2, 2))));
            Assert.That(view.Text(1), Is.Empty, "the empty slot shows nothing");
            Assert.That(view.IsWaiting(0), Is.False);

            _host.TryUse(0);
            _host.Step(PlayerCommands.None, 0.25f);
            view.Refresh();
            Assert.That(view.Text(0), Is.EqualTo(Localizer.Resolve(new LocalizedText("hud.ability.wait", 1, 1, 2, "0.8"))));
            Assert.That(view.IsWaiting(0), Is.True);
            Assert.That(view.Element.childCount, Is.EqualTo(AbilityHost.SlotCount));
            Assert.That(view.Element[0].ClassListContains(AbilitySlotsView.WaitingRowClass), Is.True);

            view.Show(null);
            Assert.That(view.IsShown, Is.False);
        }

        [Test]
        public void TheHudInterfaceAttachesTheView()
        {
            _hud = Hud.Create("AbilityHostTests.Hud", null, null, null);
            IHud asInterface = _hud;

            var slots = asInterface.AttachAbilitySlots();

            Assert.That(slots, Is.SameAs(_hud.GetComponent<AbilitySlotsView>()));
        }

        [Test]
        public void LogsAreTagged()
        {
            Assert.That(AbilityHost.FormatRuleLog(Abilities.On, new AbilityDefinition[] { _null, null }), Is.EqualTo("[Ability] rule=On slot1=null slot2=-"));
            Assert.That(AbilityHost.FormatEquipLog(1, null, null), Is.EqualTo("[Ability] slot=2 equip=-"));
            Assert.That(
                AbilityHost.FormatEquipLog(0, _null, _null.ToSpec()),
                Is.EqualTo("[Ability] slot=1 equip=null charges=2 recharge=10.0 cooldown=1.0 windup=0.0"));
            Assert.That(AbilityHost.FormatEffectLog(0, _null), Is.EqualTo("[Ability] slot=1 id=null effect"));
            Assert.That(AbilityHost.FormatEndedLog(1, _null, true), Is.EqualTo("[Ability] slot=2 id=null ended interrupted=true"));

            var spent = new AbilitySlot(new AbilitySpec(1, 0f, 0f, 0f));
            spent.TryStart();
            spent.End();
            Assert.That(
                AbilityHost.FormatRejectedLog(0, _null, AbilityUseBlock.NoCharges, spent),
                Is.EqualTo("[Ability] slot=1 id=null rejected reason=NoCharges wait=never"));
            Assert.That(AbilityHost.FormatRechargedLog(0, _null, new AbilitySlot(new AbilitySpec(2, 1f, 0f, 0f))), Is.EqualTo("[Ability] slot=1 id=null recharged charges=2/2"));
        }

        private sealed class StillOverride : IMovementOverride
        {
            public int Begins;
            public int Ends;

            public int Priority => 0;

            public bool IsActive => true;

            public PlayerActionConditions Conditions => PlayerActionConditions.Sprinting;

            public void Begin(PlayerController mover)
            {
                Begins++;
            }

            public MovementOverrideStep Step(PlayerController mover, MovementOverrideInput input, float deltaSeconds)
            {
                return MovementOverrideStep.Move(Vector3.zero, Vector3.zero);
            }

            public void End(PlayerController mover)
            {
                Ends++;
            }
        }
    }
}
