using System;
using System.Collections.Generic;
using System.Globalization;
using Shooter.Core;
using Shooter.Protocol;
using Shooter.Runtime;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Shooter.NetV2
{
    // 見え方は手元の画面の表示だけで決まり、サーバーは扱わない。だからゴーグルの回数・待ち時間・掛ける動きもオフラインの AbilityHost と同じ枠 (AbilitySlot) で
    // 手元で数え、能力の並びの n 番目のゴーグルを Ability(n+1) の入力で掛け・切り替え・外す。照準器の見え方は持っている武器の付けた物で決める。
    // サーマルで光らせるのは、最新のスナップショットで敵のチームにいて生きている相手の体 (NetV2RemotePlayer.Body)
    public sealed class NetV2VisionPart : INetV2ClientPart
    {
        private static readonly string[] _slotPaths = { InputActionPaths.Ability1, InputActionPaths.Ability2 };

        private readonly Func<int> _localPlayerId;
        private readonly Func<NetV2LocalPlayer> _localPlayer;
        private readonly Func<SightSpec> _sight;
        private readonly Func<IEnumerable<NetV2RemotePlayer>> _remotes;
        private readonly Func<int, bool> _slotPressed;
        private readonly Action<string> _log;
        private readonly HashSet<int> _hostile = new HashSet<int>();
        private readonly List<GameObject> _hostileBodies = new List<GameObject>();
        private GogglesSlot[] _slots;
        private InputAction[] _actions;
        private NetV2LocalPlayer _attachedTo;
        private VisionModeController _controller;
        private ThermalHighlighter _thermal;
        private NetV2LoadoutChooser _chooser;
        private AbilityCatalogAsset _catalog;
        private bool _abilitiesOn;
        private bool _hasOwn;
        private bool _alive;
        private bool _downed;
        private bool _disposed;

        // slots は能力の枠の順で、ゴーグルでない枠は null。押したかを引く口と相手の体の一覧は、テストや別の入力・別の体の持ち方から差し替えられるよう受け取る
        public NetV2VisionPart(
            Func<int> localPlayerId,
            IReadOnlyList<VisionGogglesDefinition> slots,
            Func<NetV2LocalPlayer> localPlayer = null,
            Func<SightSpec> sight = null,
            Func<IEnumerable<NetV2RemotePlayer>> remotes = null,
            Func<int, bool> slotPressed = null,
            Action<string> log = null)
        {
            _localPlayerId = localPlayerId ?? throw new ArgumentNullException(nameof(localPlayerId));
            _localPlayer = localPlayer ?? (() => NetV2Client.Current != null ? NetV2Client.Current.LocalPlayer : null);
            _sight = sight ?? (() => NetV2Client.Current != null ? NetV2Client.Current.OwnSight : null);
            _remotes = remotes ?? FindRemotesInScene;
            _slotPressed = slotPressed ?? ReadSlotPressed;
            _log = log ?? (line => Debug.Log(line));
            _slots = BuildSlots(slots ?? throw new ArgumentNullException(nameof(slots)));
        }

        public static int SlotCount => _slotPaths.Length;

        // 枠の順で、ゴーグルでない枠は null
        public IReadOnlyList<VisionGogglesDefinition> Slots
        {
            get
            {
                var definitions = new VisionGogglesDefinition[_slots.Length];
                for (var i = 0; i < definitions.Length; i++)
                {
                    definitions[i] = _slots[i] != null ? _slots[i].Definition : null;
                }

                return definitions;
            }
        }

        public VisionModeController Controller => _controller;

        public ThermalHighlighter Thermal => _thermal;

        // NetV2ClientParts.Registered に並べる口。再生には自分の体と押す入力が無いので出さない
        public static INetV2ClientPart Create(NetV2ClientPartsContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            if (context.Replay)
            {
                return null;
            }

            var client = context.Client;
            var part = new NetV2VisionPart(() => client.PlayerId, SlotsOf(context.Abilities), context.LocalPlayer, log: context.Log);
            if (context.Loadout != null)
            {
                part.Follow(context.Loadout, context.AbilityCatalog);
            }

            return part;
        }

        // ロードアウトの能力の並びを、枠ごとのゴーグルの定義にする。枠の数を超えた能力は使う入力が無いので外す
        public static IReadOnlyList<VisionGogglesDefinition> SlotsOf(IReadOnlyList<AbilityDefinition> abilities)
        {
            var slots = new VisionGogglesDefinition[SlotCount];
            if (abilities == null)
            {
                return slots;
            }

            for (var i = 0; i < slots.Length && i < abilities.Count; i++)
            {
                slots[i] = abilities[i] as VisionGogglesDefinition;
            }

            return slots;
        }

        // 受け入れられた能力の選択 (枠の順の Id、「なし」は空の Id) を、カタログで引いたゴーグルの定義にする。
        // カタログが壊れていれば AbilityCatalogAsset.TryFind の例外をそのまま投げる
        public static IReadOnlyList<VisionGogglesDefinition> SlotsOf(IReadOnlyList<string> abilityIds, AbilityCatalogAsset catalog)
        {
            var slots = new VisionGogglesDefinition[SlotCount];
            if (abilityIds == null || catalog == null)
            {
                return slots;
            }

            for (var i = 0; i < slots.Length && i < abilityIds.Count; i++)
            {
                slots[i] = catalog.TryFind(abilityIds[i], out var definition) ? definition as VisionGogglesDefinition : null;
            }

            return slots;
        }

        // オンラインには覗き込みの進み具合が無いので、覗いていれば覗き切ったものとして照準器の見え方を引く。倒れている間は何も覗いていない
        public static SightView SightOf(SightSpec sight, bool aiming, bool alive)
        {
            if (!alive)
            {
                return SightView.None;
            }

            return SightView.Of(sight ?? SightSpec.Default, ViewMode.FirstPerson, aiming ? 1f : 0f);
        }

        public bool IsThermalTarget(int playerId)
        {
            return _hostile.Contains(playerId);
        }

        // 掛けていたゴーグルの見え方を、新しい枠のどれも持っていなければ外す。外す押し方が無くなり、掛けたままになるため
        public void SetSlots(IReadOnlyList<VisionGogglesDefinition> slots)
        {
            if (slots == null)
            {
                throw new ArgumentNullException(nameof(slots));
            }

            EndRunning();
            _slots = BuildSlots(slots);
            if (_controller != null && _controller.GogglesOn && !AnySlotHas(_controller.Goggles))
            {
                _controller.SetGoggles(VisionMode.None);
            }
        }

        // 選んだ選択を持つ側 (NetV2LoadoutChooser) は部品より長く生きるので、Dispose で外す
        internal void Follow(NetV2LoadoutChooser chooser, AbilityCatalogAsset catalog)
        {
            if (chooser == null)
            {
                throw new ArgumentNullException(nameof(chooser));
            }

            Unfollow();
            if (_disposed)
            {
                return;
            }

            _chooser = chooser;
            _catalog = catalog;
            _chooser.AbilitiesAccepted += HandleAbilitiesAccepted;
        }

        public bool Handle(IMessage message)
        {
            return false;
        }

        public void Apply(SnapshotMessage snapshot)
        {
            if (_disposed || snapshot == null)
            {
                return;
            }

            var abilitiesOn = snapshot.Match.Rules.Abilities == Shooter.Core.Abilities.On;
            if (_abilitiesOn && !abilitiesOn)
            {
                EndRunning();
                TakeOff(VisionLog.GogglesOffAbilitiesOff(OwnerId()));
            }

            _abilitiesOn = abilitiesOn;
            ReadPlayers(snapshot);
        }

        public void Tick(float deltaSeconds)
        {
            if (_disposed)
            {
                return;
            }

            if (!(deltaSeconds >= 0f) || float.IsInfinity(deltaSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds), deltaSeconds, "deltaSeconds must not be negative and must be finite");
            }

            var local = Attach();
            var stopped = !_abilitiesOn || IsIncapacitated(local);
            for (var i = 0; i < _slots.Length; i++)
            {
                StepSlot(i, deltaSeconds, stopped);
            }

            if (!_abilitiesOn || local == null || GameUi.IsPauseOpen)
            {
                return;
            }

            for (var i = 0; i < _slots.Length; i++)
            {
                if (_slots[i] != null && _slotPressed(i))
                {
                    Use(i, local);
                }
            }
        }

        // 足した部品は自分の体に残すと、部品を作り直したとき (つなぎ直し) に前の部品の口を読んだまま残るので、外して画面の見え方も戻す
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            Unfollow();
            EndRunning();
            Detach();
        }

        private NetV2LocalPlayer Attach()
        {
            var local = _localPlayer();

            // マップを替えると自分の体は作り直されるので、Unity の破棄済みの参照は待たずに付け直す
            if (local == _attachedTo && (local == null || _controller != null))
            {
                return local;
            }

            Detach();
            if (local == null)
            {
                return null;
            }

            _attachedTo = local;
            var body = local.gameObject;
            _controller = PlayerActions.GetOrAdd<VisionModeController>(body);
            _controller.SightSource = () => SightOf(_sight(), local != null && local.IsAiming, _alive || !_hasOwn);
            _thermal = PlayerActions.GetOrAdd<ThermalHighlighter>(body);

            // 自分の体に陣営は無く、場の Damageable はみな敵に見えてしまうので、光らせるのはスナップショットで敵と分かった相手の体だけにする
            _thermal.Candidates = () => Array.Empty<Damageable>();
            _thermal.Bodies = HostileBodies;
            _thermal.Controller = _controller;
            _log(VisionLog.NetAttached(OwnerId()));
            return local;
        }

        private void Detach()
        {
            EndRunning();
            var controller = _controller;
            var thermal = _thermal;
            _controller = null;
            _thermal = null;
            _attachedTo = null;
            if (controller != null)
            {
                controller.SetGoggles(VisionMode.None);
                controller.SightSource = () => SightView.None;
                controller.Refresh();
            }

            DestroyComponent(thermal);
            DestroyComponent(controller);
        }

        private void ReadPlayers(SnapshotMessage snapshot)
        {
            _hostile.Clear();
            var players = snapshot.Players;
            var own = players.IndexOf(_localPlayerId());
            var wasAlive = _hasOwn && _alive;
            var wasDead = _hasOwn && !_alive;
            if (own < 0)
            {
                // 自分の行が無い間は誰が味方か決められないので、味方を光らせないよう誰も光らせない
                _hasOwn = false;
                _downed = false;
                return;
            }

            var row = players[own];
            _hasOwn = true;
            _alive = row.Health > 0;
            _downed = row.Downed.IsDowned;
            if (wasAlive && !_alive)
            {
                EndRunning();
                TakeOff(VisionLog.GogglesOffOnDeath(OwnerId()));
            }
            else if (wasDead && _alive)
            {
                // オフラインの AbilityHost が体力を戻したとき (HealthReset) と同じく、生き返ったら回数と待ち時間を戻す
                ResetSlots();
            }

            var team = row.Team;
            for (var i = 0; i < players.Count; i++)
            {
                var other = players[i];
                if (other.PlayerId == row.PlayerId || other.Team == team || other.Concealed || other.Health <= 0)
                {
                    continue;
                }

                _hostile.Add(other.PlayerId);
            }
        }

        private IEnumerable<GameObject> HostileBodies()
        {
            _hostileBodies.Clear();
            if (_hostile.Count == 0)
            {
                return _hostileBodies;
            }

            var remotes = _remotes();
            if (remotes == null)
            {
                return _hostileBodies;
            }

            foreach (var remote in remotes)
            {
                if (remote != null && _hostile.Contains(remote.PlayerId) && remote.Body != null)
                {
                    _hostileBodies.Add(remote.Body.gameObject);
                }
            }

            return _hostileBodies;
        }

        private void StepSlot(int index, float deltaSeconds, bool stopped)
        {
            var state = _slots[index];
            if (state == null)
            {
                return;
            }

            var slot = state.Slot;
            var step = slot.Tick(deltaSeconds);
            if ((step & AbilitySlotStep.Recharged) != 0)
            {
                _log(AbilityHost.FormatRechargedLog(index, state.Definition, slot));
            }

            if (!slot.IsRunning)
            {
                return;
            }

            if (stopped || _controller == null)
            {
                End(index, true);
                return;
            }

            if (slot.Phase == AbilityPhase.Active)
            {
                PutOn(index);
            }
        }

        private void Use(int index, NetV2LocalPlayer local)
        {
            var state = _slots[index];
            var slot = state.Slot;
            if (IsIncapacitated(local))
            {
                _log(AbilityHost.FormatRejectedLog(index, state.Definition, AbilityUseBlock.Incapacitated, slot));
                return;
            }

            if (slot.IsRunning)
            {
                _log(AbilityHost.FormatRejectedLog(index, state.Definition, AbilityUseBlock.Running, slot));
                return;
            }

            // 切り替えと外すのはオフラインの VisionGogglesAbility と同じく回数も待ち時間も使わない
            var next = state.Ability.NextAfter(_controller.Goggles);
            if (next.HasValue)
            {
                _controller.SetGoggles(next.Value);
                _log(AbilityHost.FormatFollowUpLog(index, state.Definition));
                return;
            }

            if (!slot.TryStart())
            {
                _log(AbilityHost.FormatRejectedLog(index, state.Definition, slot.Block, slot));
                return;
            }

            _log(AbilityHost.FormatUsedLog(index, state.Definition, slot));
            if (slot.Phase == AbilityPhase.Active)
            {
                PutOn(index);
            }
        }

        private void PutOn(int index)
        {
            var state = _slots[index];
            _log(AbilityHost.FormatEffectLog(index, state.Definition));
            _controller.SetGoggles(state.Ability.Mode);
            End(index, false);
        }

        private void End(int index, bool interrupted)
        {
            var state = _slots[index];
            if (state != null && state.Slot.End())
            {
                _log(AbilityHost.FormatEndedLog(index, state.Definition, interrupted));
            }
        }

        private void EndRunning()
        {
            for (var i = 0; i < _slots.Length; i++)
            {
                End(i, true);
            }
        }

        private void ResetSlots()
        {
            foreach (var state in _slots)
            {
                state?.Slot.Reset();
            }
        }

        private void TakeOff(string line)
        {
            if (_controller == null || !_controller.GogglesOn)
            {
                return;
            }

            _controller.SetGoggles(VisionMode.None);
            _log(line);
        }

        private bool AnySlotHas(VisionMode mode)
        {
            foreach (var state in _slots)
            {
                if (state != null && state.Ability.NextAfter(mode).HasValue)
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsIncapacitated(NetV2LocalPlayer local)
        {
            return (_hasOwn && (!_alive || _downed)) || (local != null && local.Handoff.IsHandedOff);
        }

        // サーバーは受け入れた能力を持たせているので、引けなかった枠に前のゴーグルを残さず空にする
        private void HandleAbilitiesAccepted(IReadOnlyList<string> abilityIds)
        {
            if (_disposed)
            {
                return;
            }

            IReadOnlyList<VisionGogglesDefinition> slots;
            try
            {
                slots = SlotsOf(abilityIds, _catalog);
            }
            catch (AbilityDefinitionException e)
            {
                _log(VisionLog.NetSlotsEmptied(e.Message));
                slots = new VisionGogglesDefinition[SlotCount];
            }

            SetSlots(slots);
        }

        private void Unfollow()
        {
            if (_chooser != null)
            {
                _chooser.AbilitiesAccepted -= HandleAbilitiesAccepted;
            }

            _chooser = null;
            _catalog = null;
        }

        // 見え方が壊れたゴーグル (None・重なり) は掛けられないので、枠を空にしてログに残す
        private GogglesSlot[] BuildSlots(IReadOnlyList<VisionGogglesDefinition> definitions)
        {
            var slots = new GogglesSlot[Math.Min(definitions.Count, SlotCount)];
            var ids = new string[slots.Length];
            for (var i = 0; i < slots.Length; i++)
            {
                var definition = definitions[i];
                if (definition == null)
                {
                    continue;
                }

                try
                {
                    slots[i] = new GogglesSlot(definition, new VisionGogglesAbility(definition.ToModes()), new AbilitySlot(definition.ToSpec()));
                    ids[i] = definition.Id;
                    _log(AbilityHost.FormatEquipLog(i, definition, slots[i].Slot.Spec));
                }
                catch (AbilityDefinitionException e)
                {
                    _log(VisionLog.NetSlotInvalid(i, e.Message));
                }
            }

            _log(VisionLog.NetSlots(ids));
            return slots;
        }

        private string OwnerId()
        {
            return _localPlayerId().ToString(CultureInfo.InvariantCulture);
        }

        private bool ReadSlotPressed(int slot)
        {
            if (_actions == null)
            {
                _actions = new InputAction[_slotPaths.Length];
                var asset = InputSystem.actions;
                for (var i = 0; i < _slotPaths.Length && asset != null; i++)
                {
                    _actions[i] = asset.FindAction(_slotPaths[i]);
                }
            }

            return slot >= 0 && slot < _actions.Length && _actions[slot] != null && _actions[slot].WasPressedThisFrame();
        }

        private static IEnumerable<NetV2RemotePlayer> FindRemotesInScene()
        {
            return UnityEngine.Object.FindObjectsByType<NetV2RemotePlayer>(FindObjectsInactive.Exclude);
        }

        private static void DestroyComponent(Component component)
        {
            if (component == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(component);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(component);
            }
        }

        private sealed class GogglesSlot
        {
            public GogglesSlot(VisionGogglesDefinition definition, VisionGogglesAbility ability, AbilitySlot slot)
            {
                Definition = definition;
                Ability = ability;
                Slot = slot;
            }

            public VisionGogglesDefinition Definition { get; }

            public VisionGogglesAbility Ability { get; }

            public AbilitySlot Slot { get; }
        }
    }
}
