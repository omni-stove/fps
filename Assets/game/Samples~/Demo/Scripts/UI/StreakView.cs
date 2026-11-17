using System;
using System.Collections.Generic;
using Shooter.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Shooter.Runtime
{
    /// <summary>
    /// 自分のストリークを、右下の弾数の欄より上に 1 行ずつ出す (<see cref="StreakText.Lines"/>)。Hud.uxml の <see cref="UxmlName"/> に行を入れる。
    /// <see cref="Hud.AddViews"/> が最初から付け、要素を持っている間は <see cref="GameSignals.Streak"/> を購読する。値が無い間は隠す。
    /// 使える報酬の行には UseStreak を今の入力機器で押すキー (<see cref="InteractKeyLabel"/>) を入れ、入力機器を替えたら書き直す。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class StreakView : HudElementView
    {
        /// <summary>Hud.uxml のストリークの VisualElement。</summary>
        public const string UxmlName = "streak";

        /// <summary>1 行分の Label に付ける USS クラス。</summary>
        public const string RowClass = "streak__row";

        /// <summary>使える報酬の行に足す USS クラス。</summary>
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

        /// <summary>出しているストリーク。出していなければ null。</summary>
        public StreakStatus? Status => _status;

        /// <summary>何か出しているか。</summary>
        public bool IsShown => _status.HasValue;

        /// <summary>出している行の文。出していなければ空。</summary>
        public IReadOnlyList<string> Lines => _lines;

        /// <summary>使える報酬の行に入れている UseStreak のキーの表記。使える報酬が無ければ空文字。</summary>
        public string KeyLabel => _status.HasValue && _status.Value.ReadyCount > 0 ? _shownKeyLabel : string.Empty;

        /// <summary>index の行が使える報酬の行 (<see cref="ReadyRowClass"/> を付けている) か。</summary>
        /// <exception cref="ArgumentOutOfRangeException">index が 0 以上 <see cref="Lines"/> の数未満でない。</exception>
        public bool IsReadyLine(int index)
        {
            if (index < 0 || index >= _readyLines.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(index), index, $"index must be between 0 and {_readyLines.Count - 1}");
            }

            return _readyLines[index];
        }

        /// <summary>status を出す。null なら隠す。要素を持っている間は <see cref="GameSignals.Streak"/> が出されるたびに呼ばれる。</summary>
        public void Show(StreakStatus? status)
        {
            _status = status;
            _shownKeyLabel = status.HasValue && status.Value.ReadyCount > 0 ? CurrentKeyLabel() : string.Empty;
            Refresh();
        }

        /// <summary>出しているストリークを今の言語とキーの表記で文にし直して行に写す。</summary>
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
