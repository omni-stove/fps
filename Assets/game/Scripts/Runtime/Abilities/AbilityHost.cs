using System;
using System.Collections.Generic;
using System.Globalization;
using Fps.Core;
using UnityEngine;

namespace Fps.Runtime
{
    /// <summary>
    /// プレイヤーの能力の枠 (<see cref="SlotCount"/> 個。0 番を Ability1、1 番を Ability2 の入力で使う)。
    /// 枠には <see cref="Equip(int, AbilityDefinition)"/> で能力の定義を入れ、回数と時間は枠ごとの <see cref="AbilitySlot"/>、
    /// 振る舞いは定義が作る <see cref="IAbility"/> が受け持つ。ルールの <see cref="ActionRules.Abilities"/> が Off の間は使えず、
    /// 使っている途中で Off になったら止める。倒れた・ダウンした・操作を手放した (<see cref="PlayerController.Handoff"/>) ときも止める。
    /// 入力は同じ GameObject の <see cref="PlayerActionReader.PressedCommands"/> と <see cref="PlayerActionReader.HeldCommands"/> を毎フレーム読む。
    /// 体力が戻った (<see cref="Damageable.HealthReset"/>、出撃し直し) ら回数と待ち時間を戻す。
    /// 使った・効果が出た・終えた・断った・回数が戻ったことを <see cref="LogTag"/> 付きのログに出す。
    /// オフラインでは <see cref="AbilityActionInstaller"/> が付けてルールを渡し、<see cref="GameBootstrap"/> が枠に入れる。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AbilityHost : MonoBehaviour
    {
        public const string LogTag = "[Ability]";

        /// <summary>枠の数。入力の Ability1 / Ability2 に合わせる。</summary>
        public const int SlotCount = 2;

        /// <summary>空の枠をログに出すときの名前。</summary>
        public const string EmptySlotName = "-";

        private readonly AbilityDefinition[] _definitions = new AbilityDefinition[SlotCount];
        private readonly AbilitySlot[] _slots = new AbilitySlot[SlotCount];
        private readonly IAbility[] _abilities = new IAbility[SlotCount];
        private readonly AbilityContext[] _contexts = new AbilityContext[SlotCount];
        private readonly bool[] _pendingEffect = new bool[SlotCount];

        private Abilities _rule = Abilities.Off;
        private PlayerActionReader _input;
        private PlayerController _mover;
        private Damageable _damageable;
        private bool _resolved;

        /// <summary>能力を使うか。<see cref="Configure"/> の前は Off。</summary>
        public Abilities Rule => _rule;

        /// <summary>ルールで能力を使うことになっているか。</summary>
        public bool UsesAbilities => _rule == Abilities.On;

        // 置いたタレットのように、持ち主の手を離れて撃つ物が味方撃ちのルールに従えるよう、Configure で渡されたルールを覚えておく。
        public FriendlyFire FriendlyFire { get; private set; } = FriendlyFire.Off;

        public PlayerCommands HeldCommands { get; private set; }

        /// <summary>同じ GameObject の移動。無ければ null。</summary>
        public PlayerController Mover
        {
            get
            {
                Resolve();
                return _mover;
            }
        }

        /// <summary>
        /// 入力を読む先。未設定なら同じ GameObject の <see cref="PlayerActionReader"/> を探して使う。
        /// null を入れると、次のフレームで探し直す。
        /// </summary>
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

        /// <summary>index の枠を使う入力。</summary>
        /// <exception cref="ArgumentOutOfRangeException">index が 0 以上 <see cref="SlotCount"/> 未満でない。</exception>
        public static PlayerCommands CommandFor(int index)
        {
            RequireIndex(index);
            return index == 0 ? PlayerCommands.Ability1 : PlayerCommands.Ability2;
        }

        /// <summary>例: <c>[Ability] rule=On slot1=NullAbility slot2=-</c>。</summary>
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

        /// <summary>例: <c>[Ability] slot=1 equip=NullAbility charges=1 recharge=10.0 cooldown=1.0 windup=0.0</c>。空にしたら <c>equip=-</c> だけ。</summary>
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

        /// <summary>例: <c>[Ability] slot=1 id=NullAbility used charges=0/1 cooldown=1.0</c>。</summary>
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

        /// <summary>例: <c>[Ability] slot=1 id=NullAbility effect</c>。</summary>
        public static string FormatEffectLog(int index, AbilityDefinition definition)
        {
            RequireIndex(index);
            return $"{LogTag} slot={index + 1} id={NameOf(definition)} effect";
        }

        /// <summary>例: <c>[Ability] slot=1 id=NullAbility ended interrupted=false</c>。</summary>
        public static string FormatEndedLog(int index, AbilityDefinition definition, bool interrupted)
        {
            RequireIndex(index);
            return $"{LogTag} slot={index + 1} id={NameOf(definition)} ended interrupted={(interrupted ? "true" : "false")}";
        }

        /// <summary>例: <c>[Ability] slot=1 id=NullAbility rejected reason=Cooldown wait=0.6</c>。待たずに使えるようにならない理由なら wait は付けない。</summary>
        public static string FormatRejectedLog(int index, AbilityDefinition definition, AbilityUseBlock reason, AbilitySlot slot)
        {
            RequireIndex(index);
            var line = $"{LogTag} slot={index + 1} id={NameOf(definition)} rejected reason={reason}";
            if (slot != null && (reason == AbilityUseBlock.Cooldown || reason == AbilityUseBlock.NoCharges))
            {
                var wait = slot.SecondsUntilReady;
                line += float.IsInfinity(wait) ? " wait=never" : " wait=" + HudFormat.AbilityWaitSeconds(wait);
            }

            return line;
        }

        /// <summary>例: <c>[Ability] slot=1 id=NullAbility recharged charges=1/1</c>。</summary>
        public static string FormatRechargedLog(int index, AbilityDefinition definition, AbilitySlot slot)
        {
            RequireIndex(index);
            if (slot == null)
            {
                throw new ArgumentNullException(nameof(slot));
            }

            return $"{LogTag} slot={index + 1} id={NameOf(definition)} recharged charges={slot.Charges}/{slot.Spec.MaxCharges}";
        }

        /// <summary>
        /// rules の <see cref="ActionRules.Abilities"/> で使うかを決める。Off なら使っている能力を止める。
        /// 何度呼んでもよく、最後に渡した rules に従う。決めた内容を <see cref="FormatRuleLog"/> でログに出す。
        /// </summary>
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
        }

        /// <summary>
        /// index の枠に definition を入れる。null なら空にする。前に入っていた能力を使っている途中なら止める。
        /// 回数は上限から、待ち時間は無しで始める。入れた内容を <see cref="FormatEquipLog"/> でログに出す。
        /// definition が不正なら、枠を変えずに例外を投げる。
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">index が 0 以上 <see cref="SlotCount"/> 未満でない。</exception>
        /// <exception cref="AbilityDefinitionException">definition の値が不正か、振る舞いを作れない。</exception>
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
            _definitions[index] = definition;
            _slots[index] = slot;
            _abilities[index] = ability;
            _contexts[index] = slot != null ? new AbilityContext(this, index, definition, slot) : null;
            _pendingEffect[index] = false;

            Debug.Log(FormatEquipLog(index, definition, spec), this);
        }

        /// <summary>
        /// definitions を先頭から順に枠へ入れ、足りない枠は空にする。null なら全部空にする。
        /// 不正な定義はエラーをログに出してその枠を空にし、残りの枠は続ける。<see cref="SlotCount"/> を超えた分は警告を出して入れない。
        /// </summary>
        public void Equip(IReadOnlyList<AbilityDefinition> definitions)
        {
            var count = definitions != null ? definitions.Count : 0;
            if (count > SlotCount)
            {
                Debug.LogWarning(
                    $"{LogTag} '{name}' has {SlotCount} ability slots; {count - SlotCount} more abilities were given and are not equipped", this);
            }

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

        /// <summary>index の枠の定義。空なら null。</summary>
        public AbilityDefinition Definition(int index)
        {
            RequireIndex(index);
            return _definitions[index];
        }

        /// <summary>index の枠の回数と時間。空なら null。</summary>
        public AbilitySlot Slot(int index)
        {
            RequireIndex(index);
            return _slots[index];
        }

        /// <summary>index の枠の能力を使っている途中か。</summary>
        public bool IsRunning(int index)
        {
            RequireIndex(index);
            return _slots[index] != null && _slots[index].IsRunning;
        }

        /// <summary>持ち主が能力を使えない状態 (倒れている・ダウンしている・操作を手放している) か。</summary>
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

        /// <summary>index の枠を今使えない理由。使えるなら <see cref="AbilityUseBlock.None"/>。</summary>
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

        /// <summary>
        /// index の枠の能力を使う。使えなければ何もせず false (ルールが Off か枠が空のときは黙って、それ以外は理由をログに出す)。
        /// 使えたら回数を減らして <see cref="IAbility.Begin"/> を呼び、<see cref="FormatUsedLog"/> をログに出す。
        /// </summary>
        public bool TryUse(int index)
        {
            var block = UseBlock(index);
            if (block != AbilityUseBlock.None)
            {
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

        /// <summary>
        /// 1 フレーム分進める。枠の時間を deltaSeconds 進め、使っている能力の <see cref="IAbility.Tick"/> を呼び、
        /// その後 pressed に入っている枠の入力 (<see cref="CommandFor"/>) で <see cref="TryUse"/> する。
        /// 使えない状態 (ルールが Off・<see cref="IsIncapacitated"/>) になっていれば、使っている能力を止める。
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">deltaSeconds が負か有限でない。</exception>
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

        /// <summary>使っている能力を止め、すべての枠の回数を上限に、待ち時間を無しに戻す。</summary>
        public void ResetSlots()
        {
            for (var i = 0; i < SlotCount; i++)
            {
                EndAbility(i, true);
                _slots[i]?.Reset();
            }
        }

        /// <summary>index の枠の能力を使っている途中なら止める (<see cref="IAbility.End"/> の interrupted は true)。</summary>
        public void Interrupt(int index)
        {
            RequireIndex(index);
            EndAbility(index, true);
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
