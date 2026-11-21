using System;
using System.Collections.Generic;
using Shooter.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Shooter.Runtime
{
    [DisallowMultipleComponent]
    public sealed class StreakView : HudElementView
    {
        public const string UxmlName = "streak";

        public const string KillsClass = "streak__kills";

        public const string CardsClass = "streak__cards";

        public const string CardClass = "streak__card";

        public const string IconClass = "streak__icon";

        public const string IconNameClass = "streak__icon-name";

        public const string GaugeClass = "streak__gauge";

        public const string GaugeFillClass = "streak__gauge-fill";

        public const string CaptionClass = "streak__caption";

        public const string SwitchClass = "streak__switch";

        public const string RowClass = "streak__row";

        public const string TargetingRowClass = "streak__row--targeting";

        public static readonly IReadOnlyDictionary<StreakCardState, string> StateClasses = new Dictionary<StreakCardState, string>
        {
            { StreakCardState.Charging, "streak__card--charging" },
            { StreakCardState.Selected, "streak__card--selected" },
            { StreakCardState.Held, "streak__card--held" },
            { StreakCardState.Spent, "streak__card--spent" },
        };

        private readonly InteractKeyLabel _keyLabel = new InteractKeyLabel();
        private readonly InteractKeyLabel _switchKeyLabel = new InteractKeyLabel();
        private readonly InteractKeyLabel _confirmLabel = new InteractKeyLabel();
        private readonly InteractKeyLabel _cancelLabel = new InteractKeyLabel();
        private readonly List<string> _lines = new List<string>();
        private readonly List<Card> _cards = new List<Card>();
        private StreakStatus? _status;
        private StreakTargetingStatus? _targeting;
        private string _killsText = string.Empty;
        private string _switchText = string.Empty;
        private string _shownKeyLabel = string.Empty;
        private string _shownSwitchKeyLabel = string.Empty;
        private string _shownConfirmLabel = string.Empty;
        private string _shownCancelLabel = string.Empty;
        [NonSerialized]
        private InputAction _action;
        [NonSerialized]
        private InputAction _switchAction;
        [NonSerialized]
        private InputAction _confirmAction;
        [NonSerialized]
        private InputAction _cancelAction;
        private bool _actionResolved;
        private bool _switchActionResolved;
        private bool _targetingActionsResolved;
        private StreakIconCatalog _icons;
        private bool _iconsLoaded;
        private bool _subscribed;

        public override string ElementName => UxmlName;

        public StreakStatus? Status => _status;

        public StreakTargetingStatus? Targeting => _targeting;

        public bool IsShown => _status.HasValue || _targeting.HasValue;

        public bool IsTargeting => _targeting.HasValue;

        public IReadOnlyList<string> Lines => _lines;

        public string KillsText => _killsText;

        public IReadOnlyList<Card> Cards => _cards;

        public string SwitchText => _switchText;

        public string KeyLabel => _status.HasValue && _status.Value.ReadyCount > 0 ? _shownKeyLabel : string.Empty;

        public string SwitchKeyLabel => _status.HasValue && StreakText.ShowsSwitch(_status.Value) ? _shownSwitchKeyLabel : string.Empty;

        public string ConfirmKeyLabel => _targeting.HasValue ? _shownConfirmLabel : string.Empty;

        public string CancelKeyLabel => _targeting.HasValue ? _shownCancelLabel : string.Empty;

        // テストやゲーム側が、Resources と GameContent を通さずに表を渡せるようにする。
        public StreakIconCatalog Icons
        {
            get => _icons;
            set
            {
                _icons = value;
                _iconsLoaded = true;
                Refresh();
            }
        }

        public void Show(StreakStatus? status)
        {
            _status = status;
            _shownKeyLabel = status.HasValue && status.Value.ReadyCount > 0 ? CurrentKeyLabel() : string.Empty;
            _shownSwitchKeyLabel = status.HasValue && StreakText.ShowsSwitch(status.Value) ? CurrentSwitchKeyLabel() : string.Empty;
            Refresh();
        }

        // 選んでいる間は見下ろした画面に切り替わり、数や報酬の枠より操作の案内が要るので、案内だけを出す。
        public void ShowTargeting(StreakTargetingStatus? targeting)
        {
            _targeting = targeting;
            RefreshTargetingLabels();
            Refresh();
        }

        public void Refresh()
        {
            _lines.Clear();
            _cards.Clear();
            _killsText = string.Empty;
            _switchText = string.Empty;
            if (_targeting.HasValue)
            {
                foreach (var text in StreakText.TargetingLines(_targeting.Value, _shownConfirmLabel, _shownCancelLabel))
                {
                    _lines.Add(Localizer.Resolve(text));
                }
            }
            else if (_status.HasValue)
            {
                var status = _status.Value;
                _killsText = Localizer.Resolve(StreakText.Kills(status));
                foreach (var slot in status.Slots)
                {
                    _cards.Add(new Card(
                        slot.RewardId,
                        Localizer.Resolve(StreakText.Name(slot)),
                        Localizer.Resolve(StreakText.Caption(status, slot, _shownKeyLabel)),
                        StreakText.Fill(status, slot),
                        StreakText.StateOf(status, slot),
                        IconOf(slot.RewardId)));
                }

                if (StreakText.ShowsSwitch(status))
                {
                    _switchText = Localizer.Resolve(StreakText.Switch(_shownSwitchKeyLabel));
                }
            }

            Draw();
        }

        protected override void OnBound(VisualElement element)
        {
            element.Clear();
            Subscribe();
            _targeting = GameSignals.StreakTargeting.Value;
            RefreshTargetingLabels();
            Show(GameSignals.Streak.Value);
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
        }

        private void OnDestroy()
        {
            Unsubscribe();
        }

        private void Update()
        {
            if (_targeting.HasValue)
            {
                if (RefreshTargetingLabels())
                {
                    Refresh();
                }

                return;
            }

            if (!_status.HasValue || _status.Value.ReadyCount == 0)
            {
                return;
            }

            var keyLabel = CurrentKeyLabel();
            var switchKeyLabel = StreakText.ShowsSwitch(_status.Value) ? CurrentSwitchKeyLabel() : string.Empty;
            if (keyLabel != _shownKeyLabel || switchKeyLabel != _shownSwitchKeyLabel)
            {
                _shownKeyLabel = keyLabel;
                _shownSwitchKeyLabel = switchKeyLabel;
                Refresh();
            }
        }

        // アクションは Input System の設定が読まれた後に探したいので、初めて使える報酬が出たときに探す
        private string CurrentKeyLabel()
        {
            if (!_actionResolved)
            {
                _action = InputSystem.actions?.FindAction(InputActionPaths.UseStreak);
                _actionResolved = true;
            }

            return _keyLabel.For(_action);
        }

        private string CurrentSwitchKeyLabel()
        {
            if (!_switchActionResolved)
            {
                _switchAction = InputSystem.actions?.FindAction(InputActionPaths.SwitchStreak);
                _switchActionResolved = true;
            }

            return _switchKeyLabel.For(_switchAction);
        }

        // 変わったら true を返す
        private bool RefreshTargetingLabels()
        {
            if (!_targeting.HasValue)
            {
                return false;
            }

            if (!_targetingActionsResolved)
            {
                _confirmAction = InputSystem.actions?.FindAction(InputActionPaths.Attack);
                _cancelAction = InputSystem.actions?.FindAction(InputActionPaths.Aim);
                _targetingActionsResolved = true;
            }

            var confirm = _confirmLabel.For(_confirmAction);
            var cancel = _cancelLabel.For(_cancelAction);
            if (confirm == _shownConfirmLabel && cancel == _shownCancelLabel)
            {
                return false;
            }

            _shownConfirmLabel = confirm;
            _shownCancelLabel = cancel;
            return true;
        }

        private Texture2D IconOf(string rewardId)
        {
            if (!_iconsLoaded)
            {
                _icons = StreakIconCatalog.Load();
                _iconsLoaded = true;
            }

            return _icons != null ? _icons.Find(rewardId) : null;
        }

        // 値が変わったときにだけ呼ばれ、枠の数も報酬の数だけと少ないので、合わせ直さずに作り直す。
        private void Draw()
        {
            var element = Element;
            if (element == null)
            {
                return;
            }

            element.Clear();
            element.style.display = IsShown ? DisplayStyle.Flex : DisplayStyle.None;
            if (_targeting.HasValue)
            {
                foreach (var line in _lines)
                {
                    var row = NewLabel(line, RowClass);
                    row.AddToClassList(TargetingRowClass);
                    element.Add(row);
                }

                return;
            }

            if (!_status.HasValue)
            {
                return;
            }

            element.Add(NewLabel(_killsText, KillsClass));
            if (_cards.Count > 0)
            {
                var cards = new VisualElement { pickingMode = PickingMode.Ignore };
                cards.AddToClassList(CardsClass);
                foreach (var card in _cards)
                {
                    cards.Add(DrawCard(card));
                }

                element.Add(cards);
            }

            if (_switchText.Length > 0)
            {
                element.Add(NewLabel(_switchText, SwitchClass));
            }
        }

        private static VisualElement DrawCard(Card card)
        {
            var root = new VisualElement { pickingMode = PickingMode.Ignore, name = card.RewardId };
            root.AddToClassList(CardClass);
            root.AddToClassList(StateClasses[card.State]);

            var icon = new VisualElement { pickingMode = PickingMode.Ignore };
            icon.AddToClassList(IconClass);
            if (card.Icon != null)
            {
                icon.style.backgroundImage = new StyleBackground(card.Icon);
            }
            else
            {
                icon.Add(NewLabel(card.Name, IconNameClass));
            }

            root.Add(icon);

            var gauge = new VisualElement { pickingMode = PickingMode.Ignore };
            gauge.AddToClassList(GaugeClass);
            var fill = new VisualElement { pickingMode = PickingMode.Ignore };
            fill.AddToClassList(GaugeFillClass);
            fill.style.width = Length.Percent(card.Fill * 100f);
            gauge.Add(fill);
            root.Add(gauge);

            root.Add(NewLabel(card.Caption, CaptionClass));
            return root;
        }

        private static Label NewLabel(string text, string className)
        {
            var label = new Label(text) { pickingMode = PickingMode.Ignore };
            label.AddToClassList(className);
            return label;
        }

        private void Subscribe()
        {
            if (!_subscribed)
            {
                GameSignals.Streak.Changed += HandleStreakChanged;
                GameSignals.StreakTargeting.Changed += HandleTargetingChanged;
                _subscribed = true;
            }
        }

        private void Unsubscribe()
        {
            if (_subscribed)
            {
                GameSignals.Streak.Changed -= HandleStreakChanged;
                GameSignals.StreakTargeting.Changed -= HandleTargetingChanged;
                _subscribed = false;
            }
        }

        private void HandleStreakChanged(StreakStatus? status)
        {
            // Edit Mode では OnDestroy が呼ばれずに消えることがあるので、消えた後に届いた通知で購読を外す
            if (this == null)
            {
                GameSignals.Streak.Changed -= HandleStreakChanged;
                return;
            }

            Show(status);
        }

        private void HandleTargetingChanged(StreakTargetingStatus? targeting)
        {
            // Edit Mode では OnDestroy が呼ばれずに消えることがあるので、消えた後に届いた通知で購読を外す
            if (this == null)
            {
                GameSignals.StreakTargeting.Changed -= HandleTargetingChanged;
                return;
            }

            ShowTargeting(targeting);
        }

        public readonly struct Card
        {
            public Card(string rewardId, string name, string caption, float fill, StreakCardState state, Texture2D icon)
            {
                RewardId = rewardId;
                Name = name;
                Caption = caption;
                Fill = fill;
                State = state;
                Icon = icon;
            }

            public string RewardId { get; }

            public string Name { get; }

            public string Caption { get; }

            public float Fill { get; }

            public StreakCardState State { get; }

            public Texture2D Icon { get; }
        }
    }
}
