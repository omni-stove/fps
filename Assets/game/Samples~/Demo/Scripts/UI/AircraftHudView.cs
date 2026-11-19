using System.Collections.Generic;
using Shooter.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace Shooter.Runtime
{
    [DisallowMultipleComponent]
    public sealed class AircraftHudView : HudElementView
    {
        public const string UxmlName = "aircraft";

        public const string RowClass = "aircraft__row";

        public const string WarningClass = "aircraft__warning";

        private readonly List<string> _lines = new List<string>();
        private readonly List<Label> _rows = new List<Label>();
        private AircraftStatus? _status;
        private Label _warningRow;
        private string _warning = string.Empty;
        private bool _subscribed;

        public override string ElementName => UxmlName;

        public AircraftStatus? Status => _status;

        public bool IsShown => _status.HasValue;

        public IReadOnlyList<string> Lines => _lines;

        public string Warning => _warning;

        public void Show(AircraftStatus? status)
        {
            _status = status;
            Refresh();
        }

        public void Refresh()
        {
            _lines.Clear();
            _warning = string.Empty;
            if (_status.HasValue)
            {
                var status = _status.Value;
                foreach (var line in AircraftHudText.Readings(status))
                {
                    _lines.Add(Localizer.Resolve(line));
                }

                _warning = Localizer.Resolve(AircraftHudText.Warning(status));
            }

            WriteRows();
        }

        protected override void OnBound(VisualElement element)
        {
            element.Clear();
            _rows.Clear();
            _warningRow = null;
            Subscribe();
            Show(AircraftSignals.Status.Value);
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
            _warningRow = null;
            element.style.display = DisplayStyle.None;
        }

        // HUD を組んだ後に足されたときは、HUD が結び直さないので自分で結ぶ。
        private void Start()
        {
            if (Element != null)
            {
                return;
            }

            var document = GetComponent<UIDocument>();
            var root = document != null ? document.rootVisualElement : null;
            if (root != null && root.childCount > 0)
            {
                Bind(root);
            }
        }

        private void OnDestroy()
        {
            Unsubscribe();
        }

        // 飛んでいる間は値が毎フレーム変わるので、行を作り直さずに文字だけ書き換える。
        private void WriteRows()
        {
            var element = Element;
            if (element == null)
            {
                return;
            }

            if (_warningRow == null)
            {
                _warningRow = new Label { pickingMode = PickingMode.Ignore };
                _warningRow.AddToClassList(WarningClass);
                element.Add(_warningRow);
            }

            // 警告の前へ差し込むのは、読みの行を消してまた出したときにも警告をいちばん下に保つため。
            while (_rows.Count < _lines.Count)
            {
                var row = new Label { pickingMode = PickingMode.Ignore };
                row.AddToClassList(RowClass);
                element.Insert(element.IndexOf(_warningRow), row);
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
            }

            if (_warningRow.text != _warning)
            {
                _warningRow.text = _warning;
            }

            _warningRow.style.display = _warning.Length > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            element.style.display = IsShown ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void Subscribe()
        {
            if (_subscribed)
            {
                return;
            }

            AircraftSignals.Status.Changed += HandleAircraftChanged;
            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed)
            {
                return;
            }

            AircraftSignals.Status.Changed -= HandleAircraftChanged;
            _subscribed = false;
        }

        private void HandleAircraftChanged(AircraftStatus? status)
        {
            // Edit Mode では OnDestroy が呼ばれずに消えることがあるので、消えた後に届いた通知で購読を外す
            if (this == null)
            {
                AircraftSignals.Status.Changed -= HandleAircraftChanged;
                return;
            }

            Show(status);
        }
    }
}
