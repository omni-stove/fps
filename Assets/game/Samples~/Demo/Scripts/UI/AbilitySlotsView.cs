using System;
using Shooter.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace Shooter.Runtime
{
    [DisallowMultipleComponent]
    public sealed class AbilitySlotsView : HudElementView
    {
        public const string UxmlName = "ability-slots";

        public const string RowClass = "ability-slots__row";

        public const string WaitingRowClass = "ability-slots__row--waiting";

        public const string WithHintKey = "hud.ability.withHint";

        public const string DetonateKey = "hud.remoteCharge.detonate";

        private readonly string[] _texts = new string[AbilityHost.SlotCount];
        private readonly bool[] _waiting = new bool[AbilityHost.SlotCount];
        private readonly Label[] _rows = new Label[AbilityHost.SlotCount];
        private AbilityHost _source;
        private bool _subscribed;

        // 出している枠が GameSignals から来たか。来ていれば、要素を持っていない間にプレイヤーが消されたことを、受けたときに隠して合わせる
        private bool _showsSignal;

        public override string ElementName => UxmlName;

        public AbilityHost Source => _source;

        public bool IsShown { get; private set; }

        public static AbilitySlotsView AttachTo(Hud hud)
        {
            if (hud == null)
            {
                throw new ArgumentNullException(nameof(hud));
            }

            var view = hud.GetComponent<AbilitySlotsView>();
            if (view != null)
            {
                return view;
            }

            view = hud.gameObject.AddComponent<AbilitySlotsView>();
            try
            {
                hud.RebuildView();
            }
            catch (InvalidOperationException e)
            {
                Debug.LogError($"{Hud.LogTag} could not show the ability slots on the HUD: {e.Message}", hud);
            }

            return view;
        }

        public string Text(int index)
        {
            RequireIndex(index);
            return _texts[index] ?? string.Empty;
        }

        public bool IsWaiting(int index)
        {
            RequireIndex(index);
            return _waiting[index];
        }

        public void Show(AbilityHost host)
        {
            _showsSignal = false;
            Watch(host);
        }

        public static LocalizedText FollowUpText(AbilityFollowUp followUp)
        {
            switch (followUp.Kind)
            {
                case AbilityFollowUpKind.Detonate:
                    return new LocalizedText(DetonateKey, TargetName(followUp), followUp.Count);
                default:
                    throw new ArgumentOutOfRangeException(nameof(followUp), followUp.Kind, "no hint for this follow-up kind");
            }
        }

        private static LocalizedText TargetName(AbilityFollowUp followUp)
        {
            var key = followUp.TargetNameKey ?? string.Empty;
            return key.Length > 0 ? new LocalizedText(key) : LocalizedText.Literal(followUp.TargetId ?? string.Empty);
        }

        public void Refresh()
        {
            // 破棄された持ち主は Unity の == null で拾い、出していないものとして扱う。
            var host = _source != null ? _source : null;
            var shown = false;
            for (var i = 0; i < AbilityHost.SlotCount; i++)
            {
                var slot = host != null && host.UsesAbilities ? host.Slot(i) : null;
                _texts[i] = slot != null ? Localizer.Resolve(Describe(host, i, slot)) : string.Empty;
                // 能力ごとの CanUse は毎フレーム聞かない (重い判定やログを出すものがあり得る)。枠の回数と時間だけで決める。
                _waiting[i] = slot != null && !slot.CanStart;
                shown |= slot != null;
                WriteRow(i, slot != null);
            }

            IsShown = shown;
            if (Element != null)
            {
                Element.style.display = shown ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        protected override void OnBound(VisualElement element)
        {
            element.Clear();
            for (var i = 0; i < AbilityHost.SlotCount; i++)
            {
                var row = new Label { pickingMode = PickingMode.Ignore };
                row.AddToClassList(RowClass);
                element.Add(row);
                _rows[i] = row;
            }

            Subscribe();
            var player = GameSignals.LocalPlayer.Value;
            if (player != null || _showsSignal)
            {
                ShowPlayer(player);
                return;
            }

            Refresh();
        }

        protected override void OnLanguageChanged()
        {
            Refresh();
        }

        protected override void OnUnbinding(VisualElement element)
        {
            Unsubscribe();
            element.Clear();
            element.style.display = DisplayStyle.None;
            Array.Clear(_rows, 0, _rows.Length);
        }

        private void OnDestroy()
        {
            Unsubscribe();
            if (!ReferenceEquals(_source, null))
            {
                _source.Changed -= HandleHostChanged;
                _source = null;
            }
        }

        private void Watch(AbilityHost host)
        {
            if (!ReferenceEquals(_source, null))
            {
                _source.Changed -= HandleHostChanged;
            }

            _source = host;
            if (!ReferenceEquals(_source, null))
            {
                _source.Changed += HandleHostChanged;
            }

            Refresh();
        }

        private void ShowPlayer(LocalPlayerSignal player)
        {
            Watch(player?.Abilities);
            _showsSignal = true;
        }

        private void Subscribe()
        {
            if (!_subscribed)
            {
                GameSignals.LocalPlayer.Changed += HandleLocalPlayerChanged;
                _subscribed = true;
            }
        }

        private void Unsubscribe()
        {
            if (_subscribed)
            {
                GameSignals.LocalPlayer.Changed -= HandleLocalPlayerChanged;
                _subscribed = false;
            }
        }

        private void HandleLocalPlayerChanged(LocalPlayerSignal player)
        {
            // Edit Mode では OnDestroy が呼ばれずに消えることがあるので、消えた後に届いた通知で購読を外す
            if (this == null)
            {
                GameSignals.LocalPlayer.Changed -= HandleLocalPlayerChanged;
                return;
            }

            ShowPlayer(player);
        }

        private void HandleHostChanged(AbilityHost host)
        {
            if (this == null)
            {
                host.Changed -= HandleHostChanged;
                return;
            }

            Refresh();
        }

        private static LocalizedText Describe(AbilityHost host, int index, AbilitySlot slot)
        {
            var line = HudText.Ability(index + 1, slot);
            return host.TryGetFollowUp(index, out var followUp) ? new LocalizedText(WithHintKey, line, FollowUpText(followUp)) : line;
        }

        private void WriteRow(int index, bool visible)
        {
            var row = _rows[index];
            if (row == null)
            {
                return;
            }

            if (row.text != _texts[index])
            {
                row.text = _texts[index];
            }

            row.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            row.EnableInClassList(WaitingRowClass, _waiting[index]);
        }

        private static void RequireIndex(int index)
        {
            if (index < 0 || index >= AbilityHost.SlotCount)
            {
                throw new ArgumentOutOfRangeException(nameof(index), index, $"index must be between 0 and {AbilityHost.SlotCount - 1}");
            }
        }
    }
}
