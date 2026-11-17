using System;
using System.Collections.Generic;
using System.Globalization;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    [DisallowMultipleComponent]
    public sealed class AbilityHost : MonoBehaviour
    {
        public const string LogTag = "[Ability]";

        // 入力が Ability1 / Ability2 の 2 つなので、それに合わせる。
        public const int SlotCount = 2;

        public const string EmptySlotName = "-";

        private readonly AbilityDefinition[] _definitions = new AbilityDefinition[SlotCount];
        private readonly AbilitySlot[] _slots = new AbilitySlot[SlotCount];
        private readonly IAbility[] _abilities = new IAbility[SlotCount];
        private readonly AbilityContext[] _contexts = new AbilityContext[SlotCount];
        private readonly bool[] _pendingEffect = new bool[SlotCount];
        private readonly AbilityFollowUp?[] _notifiedFollowUps = new AbilityFollowUp?[SlotCount];

        private Abilities _rule = Abilities.Off;
        private PlayerActionReader _input;
        private PlayerController _mover;
        private Damageable _damageable;
        private bool _resolved;
        private bool _changed;
        private int _deferDepth;

        public event Action<AbilityHost> Changed;

        public Abilities Rule => _rule;

        public bool UsesAbilities => _rule == Abilities.On;

        // 置いたタレットのように、持ち主の手を離れて撃つ物が味方撃ちのルールに従えるよう、Configure で渡されたルールを覚えておく。
        public FriendlyFire FriendlyFire { get; private set; } = FriendlyFire.Off;

        public PlayerCommands HeldCommands { get; private set; }

        public PlayerController Mover
        {
            get
            {
                Resolve();
                return _mover;
            }
        }

        public PlayerActionReader Input
        {
            get
            {
                if (_input == null)
                {
                    _input = GetComponent<PlayerActionReader>();
                }

                return _input;
            }
            set => _input = value;
        }

        public static PlayerCommands CommandFor(int index)
        {
            RequireIndex(index);
            return index == 0 ? PlayerCommands.Ability1 : PlayerCommands.Ability2;
        }

        public static string FormatRuleLog(Abilities rule, IReadOnlyList<AbilityDefinition> definitions)
        {
            if (definitions == null)
            {
                throw new ArgumentNullException(nameof(definitions));
            }

            var line = $"{LogTag} rule={rule}";
            for (var i = 0; i < definitions.Count; i++)
            {
                line += $" slot{i + 1}={NameOf(definitions[i])}";
            }

            return line;
        }

        public static string FormatEquipLog(int index, AbilityDefinition definition, AbilitySpec spec)
        {
            RequireIndex(index);
            if (definition == null || spec == null)
            {
                return $"{LogTag} slot={index + 1} equip={EmptySlotName}";
            }

            return string.Format(
                CultureInfo.InvariantCulture,
                "{0} slot={1} equip={2} charges={3} recharge={4:0.0} cooldown={5:0.0} windup={6:0.0}",
                LogTag,
                index + 1,
                NameOf(definition),
                spec.MaxCharges,
                spec.RechargeSeconds,
                spec.CooldownSeconds,
                spec.WindupSeconds);
        }

        public static string FormatUsedLog(int index, AbilityDefinition definition, AbilitySlot slot)
        {
            RequireIndex(index);
            if (slot == null)
            {
                throw new ArgumentNullException(nameof(slot));
            }

            return string.Format(
                CultureInfo.InvariantCulture,
                "{0} slot={1} id={2} used charges={3}/{4} cooldown={5:0.0}",
                LogTag,
                index + 1,
                NameOf(definition),
                slot.Charges,
                slot.Spec.MaxCharges,
                slot.CooldownRemaining);
        }

        public static string FormatEffectLog(int index, AbilityDefinition definition)
        {
            RequireIndex(index);
            return $"{LogTag} slot={index + 1} id={NameOf(definition)} effect";
        }

        public static string FormatEndedLog(int index, AbilityDefinition definition, bool interrupted)
        {
            RequireIndex(index);
            return $"{LogTag} slot={index + 1} id={NameOf(definition)} ended interrupted={(interrupted ? "true" : "false")}";
        }

        public static string FormatRejectedLog(int index, AbilityDefinition definition, AbilityUseBlock reason, AbilitySlot slot)
        {
            RequireIndex(index);
            var line = $"{LogTag} slot={index + 1} id={NameOf(definition)} rejected reason={reason}";
            if (slot != null && (reason == AbilityUseBlock.Cooldown || reason == AbilityUseBlock.NoCharges))
            {
                var wait = slot.SecondsUntilReady;
                line += float.IsInfinity(wait) ? " wait=never" : " wait=" + (HudFormat.AbilityWaitTenths(wait) / 10.0).ToString("0.0", CultureInfo.InvariantCulture);
            }

            return line;
        }

        public static string FormatFollowUpLog(int index, AbilityDefinition definition)
        {
            RequireIndex(index);
            return $"{LogTag} slot={index + 1} id={NameOf(definition)} follow-up";
        }

        public static string FormatRechargedLog(int index, AbilityDefinition definition, AbilitySlot slot)
        {
            RequireIndex(index);
            if (slot == null)
            {
                throw new ArgumentNullException(nameof(slot));
            }

            return $"{LogTag} slot={index + 1} id={NameOf(definition)} recharged charges={slot.Charges}/{slot.Spec.MaxCharges}";
        }

        public void Configure(GameRules rules)
        {
            if (rules == null)
            {
                throw new ArgumentNullException(nameof(rules));
            }

            _rule = rules.Actions.Abilities;
            FriendlyFire = rules.FriendlyFire;
            if (_rule != Abilities.On)
            {
                EndAll(true);
            }

            Debug.Log(FormatRuleLog(_rule, _definitions), this);
            MarkChanged();
            NotifyChanged();
        }

        public void Equip(int index, AbilityDefinition definition)
        {
            RequireIndex(index);

            // 破棄されたアセットや参照切れも Unity の == null で空として扱う。
            if (definition == null)
            {
                definition = null;
            }

            AbilitySpec spec = null;
            IAbility ability = null;
            if (definition != null)
            {
                spec = definition.ToSpec();
                ability = definition.CreateAbility();
                if (ability == null)
                {
                    throw new AbilityDefinitionException($"ability '{definition.Id}' did not create its behaviour (CreateAbility returned null)");
                }
            }

            EndAbility(index, true);
            var slot = spec != null ? new AbilitySlot(spec) : null;
            if (_slots[index] != null)
            {
                _slots[index].Changed -= MarkChanged;
            }

            if (slot != null)
            {
                slot.Changed += MarkChanged;
            }

            _definitions[index] = definition;
            _slots[index] = slot;
            _abilities[index] = ability;
            _contexts[index] = slot != null ? new AbilityContext(this, index, definition, slot) : null;
            _pendingEffect[index] = false;

            Debug.Log(FormatEquipLog(index, definition, spec), this);
            MarkChanged();
            NotifyChanged();
        }

        public void Equip(IReadOnlyList<AbilityDefinition> definitions)
        {
            var count = definitions != null ? definitions.Count : 0;
            if (count > SlotCount)
            {
                Debug.LogWarning(
                    $"{LogTag} '{name}' has {SlotCount} ability slots; {count - SlotCount} more abilities were given and are not equipped", this);
            }

            _deferDepth++;
            try
            {
                for (var i = 0; i < SlotCount; i++)
                {
                    var definition = i < count ? definitions[i] : null;
                    try
                    {
                        Equip(i, definition);
                    }
                    catch (AbilityDefinitionException e)
                    {
                        Debug.LogError($"{LogTag} could not equip slot {i + 1} on '{name}': {e.Message}", this);
                        Equip(i, null);
                    }
                }
            }
            finally
            {
                _deferDepth--;
            }

            NotifyChanged();
        }

        public AbilityDefinition Definition(int index)
        {
            RequireIndex(index);
            return _definitions[index];
        }

        public AbilitySlot Slot(int index)
        {
            RequireIndex(index);
            return _slots[index];
        }

        public bool IsRunning(int index)
        {
            RequireIndex(index);
            return _slots[index] != null && _slots[index].IsRunning;
        }

        public bool IsIncapacitated
        {
            get
            {
                Resolve();
                if (_damageable != null && (_damageable.IsDead || _damageable.IsDowned))
                {
                    return true;
                }

                return _mover != null && _mover.Handoff.IsHandedOff;
            }
        }

        public AbilityUseBlock UseBlock(int index)
        {
            RequireIndex(index);
            if (_rule != Abilities.On)
            {
                return AbilityUseBlock.RuleOff;
            }

            var slot = _slots[index];
            if (slot == null)
            {
                return AbilityUseBlock.Empty;
            }

            if (IsIncapacitated)
            {
                return AbilityUseBlock.Incapacitated;
            }

            var block = slot.Block;
            if (block != AbilityUseBlock.None)
            {
                return block;
            }

            return AskCanUse(index) ? AbilityUseBlock.None : AbilityUseBlock.Refused;
        }

        public bool TryUse(int index)
        {
            _deferDepth++;
            bool used;
            try
            {
                used = Use(index);
            }
            finally
            {
                _deferDepth--;
            }

            NotifyChanged();
            return used;
        }

        private bool Use(int index)
        {
            var block = UseBlock(index);
            if (block != AbilityUseBlock.None)
            {
                // 回数を使い切っても置いた C4 を起爆できるよう、枠だけで断る理由なら能力に先に聞く。
                if ((block == AbilityUseBlock.Cooldown || block == AbilityUseBlock.NoCharges) && TryFollowUp(index))
                {
                    return true;
                }

                if (block != AbilityUseBlock.RuleOff && block != AbilityUseBlock.Empty)
                {
                    Debug.Log(FormatRejectedLog(index, _definitions[index], block, _slots[index]), this);
                }

                return false;
            }

            var slot = _slots[index];
            slot.TryStart();
            _pendingEffect[index] = slot.Phase == AbilityPhase.Active;
            Debug.Log(FormatUsedLog(index, _definitions[index], slot), this);
            if (_pendingEffect[index])
            {
                Debug.Log(FormatEffectLog(index, _definitions[index]), this);
            }

            try
            {
                _abilities[index].Begin(_contexts[index]);
            }
            catch (Exception e)
            {
                Debug.LogException(e, this);
                EndAbility(index, true);
            }

            return true;
        }

        public bool TryGetFollowUp(int index, out AbilityFollowUp followUp)
        {
            RequireIndex(index);
            followUp = default;
            if (_rule != Abilities.On || !(_abilities[index] is IAbilityFollowUp source) || IsIncapacitated)
            {
                return false;
            }

            try
            {
                return source.TryGetFollowUp(_contexts[index], out followUp);
            }
            catch (Exception e)
            {
                Debug.LogException(e, this);
                followUp = default;
                return false;
            }
        }

        public void Step(PlayerCommands pressed, float deltaSeconds)
        {
            Step(pressed, PlayerCommands.None, deltaSeconds);
        }

        // 押し続けているボタンは入力の部品を持たないテストや smoke からも渡せるよう、読んだ結果を引数で受ける。
        public void Step(PlayerCommands pressed, PlayerCommands held, float deltaSeconds)
        {
            if (!(deltaSeconds >= 0f) || float.IsInfinity(deltaSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds), deltaSeconds, "deltaSeconds must not be negative and must be finite");
            }

            HeldCommands = held;
            _deferDepth++;
            try
            {
                var stopped = _rule != Abilities.On || IsIncapacitated;
                for (var i = 0; i < SlotCount; i++)
                {
                    StepSlot(i, deltaSeconds, stopped);
                }

                for (var i = 0; i < SlotCount; i++)
                {
                    if ((pressed & CommandFor(i)) != 0)
                    {
                        TryUse(i);
                    }
                }
            }
            finally
            {
                _deferDepth--;
            }

            NotifyChanged();
        }

        public void ResetSlots()
        {
            for (var i = 0; i < SlotCount; i++)
            {
                EndAbility(i, true);
                _slots[i]?.Reset();
            }

            NotifyChanged();
        }

        public void Interrupt(int index)
        {
            RequireIndex(index);
            EndAbility(index, true);
            NotifyChanged();
        }

        private void Awake()
        {
            Resolve();
        }

        // PlayerActionReader は LateUpdate で読むので、ここで見えるのは前のフレームに押されたもの。1 度の押下を 1 度だけ受け取るのは変わらない。
        private void Update()
        {
            var input = Input;
            var live = input != null && input.isActiveAndEnabled;
            var pressed = live ? input.PressedCommands : PlayerCommands.None;
            var held = live ? input.HeldCommands : PlayerCommands.None;
            Step(pressed, held, Time.deltaTime);
        }

        private void OnDisable()
        {
            EndAll(true);
            NotifyChanged();
        }

        private void OnDestroy()
        {
            EndAll(true);
            if (_damageable != null)
            {
                _damageable.HealthReset -= HandleHealthReset;
            }
        }

        private void StepSlot(int index, float deltaSeconds, bool stopped)
        {
            var slot = _slots[index];
            if (slot == null)
            {
                return;
            }

            var step = slot.Tick(deltaSeconds);
            if ((step & AbilitySlotStep.Recharged) != 0)
            {
                Debug.Log(FormatRechargedLog(index, _definitions[index], slot), this);
            }

            if (!slot.IsRunning)
            {
                return;
            }

            if (stopped)
            {
                EndAbility(index, true);
                return;
            }

            var effectStarted = (step & AbilitySlotStep.EffectStarted) != 0;
            if (effectStarted)
            {
                Debug.Log(FormatEffectLog(index, _definitions[index]), this);
            }

            var context = _contexts[index];
            context.EffectStartedThisFrame = effectStarted || _pendingEffect[index];
            _pendingEffect[index] = false;

            bool keepRunning;
            var threw = false;
            try
            {
                keepRunning = _abilities[index].Tick(context, deltaSeconds);
            }
            catch (Exception e)
            {
                Debug.LogException(e, this);
                keepRunning = false;
                threw = true;
            }
            finally
            {
                context.EffectStartedThisFrame = false;
            }

            // Tick の中で枠を入れ替えられていたら、新しい枠を止めない。
            if (!keepRunning && ReferenceEquals(_contexts[index], context))
            {
                EndAbility(index, threw);
            }
        }

        private void EndAbility(int index, bool interrupted)
        {
            var slot = _slots[index];
            if (slot == null || !slot.End())
            {
                return;
            }

            _pendingEffect[index] = false;
            var context = _contexts[index];
            try
            {
                _abilities[index].End(context, interrupted);
            }
            catch (Exception e)
            {
                Debug.LogException(e, this);
            }
            finally
            {
                // 能力が外し忘れても、終えた後に移動を横取りし続けないよう、ここで必ず外す。
                context.StopMovementOverride();
            }

            Debug.Log(FormatEndedLog(index, _definitions[index], interrupted), this);
        }

        private void EndAll(bool interrupted)
        {
            for (var i = 0; i < SlotCount; i++)
            {
                EndAbility(i, interrupted);
            }
        }

        private bool TryFollowUp(int index)
        {
            if (!(_abilities[index] is IAbilityFollowUp followUp))
            {
                return false;
            }

            try
            {
                if (!followUp.TryFollowUp(_contexts[index]))
                {
                    return false;
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e, this);
                return false;
            }

            Debug.Log(FormatFollowUpLog(index, _definitions[index]), this);
            return true;
        }

        private bool AskCanUse(int index)
        {
            try
            {
                return _abilities[index].CanUse(_contexts[index]);
            }
            catch (Exception e)
            {
                Debug.LogException(e, this);
                return false;
            }
        }

        // EditMode では Awake が走らないので、使う直前にも呼ぶ。
        private void Resolve()
        {
            if (_resolved)
            {
                return;
            }

            _resolved = true;
            _mover = GetComponent<PlayerController>();
            _damageable = GetComponent<Damageable>();
            if (_damageable != null)
            {
                _damageable.HealthReset += HandleHealthReset;
            }
        }

        private void HandleHealthReset(Health health)
        {
            if (this == null)
            {
                return;
            }

            ResetSlots();
        }

        private void MarkChanged()
        {
            _changed = true;
        }

        // 置いた C4 が撃たれて消えた・持ち主が倒れたなど、枠の外で変わる続けての操作は、知らせる前に聞き直して比べる。
        // 聞く相手は重い判定やログを出すことがあるので、聞いている UI がいないときは聞かない。
        private void NotifyChanged()
        {
            if (_deferDepth > 0)
            {
                return;
            }

            var handlers = Changed;
            if (handlers == null)
            {
                _changed = false;
                return;
            }

            for (var i = 0; i < SlotCount; i++)
            {
                AbilityFollowUp? followUp = TryGetFollowUp(i, out var current) ? current : (AbilityFollowUp?)null;
                if (!Nullable.Equals(followUp, _notifiedFollowUps[i]))
                {
                    _notifiedFollowUps[i] = followUp;
                    _changed = true;
                }
            }

            if (!_changed)
            {
                return;
            }

            _changed = false;
            try
            {
                handlers(this);
            }
            catch (Exception e)
            {
                Debug.LogException(e, this);
            }
        }

        private static string NameOf(AbilityDefinition definition)
        {
            return definition != null ? definition.Id : EmptySlotName;
        }

        private static void RequireIndex(int index)
        {
            if (index < 0 || index >= SlotCount)
            {
                throw new ArgumentOutOfRangeException(nameof(index), index, $"index must be between 0 and {SlotCount - 1}");
            }
        }
    }
}
