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

        private readonly InteractKeyLabel _keyLabel = new InteractKeyLabel();
        private readonly List<string> _lines = new List<string>();
        private readonly List<bool> _readyLines = new List<bool>();
        private readonly List<Label> _rows = new List<Label>();
        private StreakStatus? _status;
        private string _shownKeyLabel = string.Empty;
        private InputAction _action;
        private bool _actionResolved;
        private bool _subscribed;

        public override string ElementName => UxmlName;

        public StreakStatus? Status => _status;

        public bool IsShown => _status.HasValue;

        public IReadOnlyList<string> Lines => _lines;

        public string KeyLabel => _status.HasValue && _status.Value.ReadyCount > 0 ? _shownKeyLabel : string.Empty;

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

        public void Refresh()
        {
            _lines.Clear();
            _readyLines.Clear();
            if (_status.HasValue)
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
            }

            element.style.display = IsShown ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void Subscribe()
        {
            if (!_subscribed)
            {
                GameSignals.Streak.Changed += HandleStreakChanged;
                _subscribed = true;
            }
        }

        private void Unsubscribe()
        {
            if (_subscribed)
            {
                GameSignals.Streak.Changed -= HandleStreakChanged;
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
    }
}
