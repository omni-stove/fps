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

        public const string RowClass = "streak__row";

        public const string ReadyRowClass = "streak__row--ready";

        public const string TargetingRowClass = "streak__row--targeting";

        private readonly InteractKeyLabel _keyLabel = new InteractKeyLabel();
        private readonly List<string> _lines = new List<string>();
        private readonly List<bool> _readyLines = new List<bool>();
        private readonly List<Label> _rows = new List<Label>();
        private readonly InteractKeyLabel _confirmLabel = new InteractKeyLabel();
        private readonly InteractKeyLabel _cancelLabel = new InteractKeyLabel();
        private StreakStatus? _status;
        private StreakTargetingStatus? _targeting;
        private string _shownKeyLabel = string.Empty;
        private string _shownConfirmLabel = string.Empty;
        private string _shownCancelLabel = string.Empty;
        private InputAction _action;
        private InputAction _confirmAction;
        private InputAction _cancelAction;
        private bool _actionResolved;
        private bool _targetingActionsResolved;
        private bool _subscribed;

        public override string ElementName => UxmlName;

        public StreakStatus? Status => _status;

        public StreakTargetingStatus? Targeting => _targeting;

        public bool IsShown => _status.HasValue || _targeting.HasValue;

        public bool IsTargeting => _targeting.HasValue;

        public IReadOnlyList<string> Lines => _lines;

        public string KeyLabel => _status.HasValue && _status.Value.ReadyCount > 0 ? _shownKeyLabel : string.Empty;

        public string ConfirmKeyLabel => _targeting.HasValue ? _shownConfirmLabel : string.Empty;

        public string CancelKeyLabel => _targeting.HasValue ? _shownCancelLabel : string.Empty;

        public bool IsReadyLine(int index)
        {
            if (index < 0 || index >= _readyLines.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(index), index, $"index must be between 0 and {_readyLines.Count - 1}");
            }

            return _readyLines[index];
        }

        public void Show(StreakStatus? status)
        {
            _status = status;
            _shownKeyLabel = status.HasValue && status.Value.ReadyCount > 0 ? CurrentKeyLabel() : string.Empty;
            Refresh();
        }

        // 選んでいる間は見下ろした画面に切り替わり、数や次の報酬より操作の案内が要るので、案内だけを出す。
        public void ShowTargeting(StreakTargetingStatus? targeting)
        {
            _targeting = targeting;
            RefreshTargetingLabels();
            Refresh();
        }

        public void Refresh()
        {
            _lines.Clear();
            _readyLines.Clear();
            if (_targeting.HasValue)
            {
                foreach (var text in StreakText.TargetingLines(_targeting.Value, _shownConfirmLabel, _shownCancelLabel))
                {
                    _lines.Add(Localizer.Resolve(text));
                    _readyLines.Add(false);
                }
            }
            else if (_status.HasValue)
            {
                var status = _status.Value;
                var texts = StreakText.Lines(status, _shownKeyLabel);
                var readyFrom = texts.Count - status.ReadyCount;
                for (var i = 0; i < texts.Count; i++)
                {
                    _lines.Add(Localizer.Resolve(texts[i]));
                    _readyLines.Add(i >= readyFrom);
                }
            }

            WriteRows();
        }

        protected override void OnBound(VisualElement element)
        {
            element.Clear();
            _rows.Clear();
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
            _rows.Clear();
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
            if (keyLabel != _shownKeyLabel)
            {
                _shownKeyLabel = keyLabel;
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

        private void WriteRows()
        {
            var element = Element;
            if (element == null)
            {
                return;
            }

            while (_rows.Count < _lines.Count)
            {
                var row = new Label { pickingMode = PickingMode.Ignore };
                row.AddToClassList(RowClass);
                element.Add(row);
                _rows.Add(row);
            }

            while (_rows.Count > _lines.Count)
            {
                var last = _rows.Count - 1;
                _rows[last].RemoveFromHierarchy();
                _rows.RemoveAt(last);
            }

            for (var i = 0; i < _rows.Count; i++)
            {
                if (_rows[i].text != _lines[i])
                {
                    _rows[i].text = _lines[i];
                }

                _rows[i].EnableInClassList(ReadyRowClass, _readyLines[i]);
                _rows[i].EnableInClassList(TargetingRowClass, _targeting.HasValue);
            }

            element.style.display = IsShown ? DisplayStyle.Flex : DisplayStyle.None;
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
    }
}
