using System.Collections.Generic;
using Shooter.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace Shooter.Runtime
{
    [DisallowMultipleComponent]
    public sealed class SpectateView : HudElementView
    {
        public const string UxmlName = "spectate";

        public const string RowClass = "spectate__row";

        public const string TargetRowClass = "spectate__row--target";

        private readonly List<string> _lines = new List<string>();
        private readonly List<Label> _rows = new List<Label>();
        private SpectateStatus? _status;
        private HudModel _model;
        private string _shownName;
        private bool _subscribed;

        public override string ElementName => UxmlName;

        public SpectateStatus? Status => _status;

        public bool IsShown => _status.HasValue;

        public IReadOnlyList<string> Lines => _lines;

        public void Show(SpectateStatus? status)
        {
            _status = status;
            Refresh();
        }

        public void Refresh()
        {
            _lines.Clear();
            _shownName = null;
            if (_status.HasValue)
            {
                var status = _status.Value;
                var participants = GameSignals.Participants.Value;
                _shownName = CurrentName(status, participants);
                var notice = _model != null ? _model.Notice : HudNotice.None;
                foreach (var line in SpectateText.Lines(status, participants, notice))
                {
                    _lines.Add(Localizer.Resolve(line));
                }
            }

            WriteRows();
        }

        protected override void OnBound(VisualElement element)
        {
            element.Clear();
            _rows.Clear();
            Subscribe();
            WatchModel(GameSignals.HudModel.Value);
            Show(GameSignals.Spectate.Value);
        }

        protected override void OnLanguageChanged()
        {
            Refresh();
        }

        protected override void OnUnbinding(VisualElement element)
        {
            Unsubscribe();
            WatchModel(null);
            element.Clear();
            _rows.Clear();
            element.style.display = DisplayStyle.None;
        }

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

        // 参加者の表は出し直さずに中身だけ変わる (名前が後から届く) ので、見ている間は名前だけ毎フレーム読み直す
        private void Update()
        {
            if (Element == null || !_status.HasValue)
            {
                return;
            }

            if (CurrentName(_status.Value, GameSignals.Participants.Value) != _shownName)
            {
                Refresh();
            }
        }

        private void OnDestroy()
        {
            Unsubscribe();
            WatchModel(null);
        }

        private static string CurrentName(SpectateStatus status, IParticipants participants)
        {
            return status.TargetId.HasValue && participants != null && participants.TryGet(status.TargetId.Value, out var participant)
                ? participant.Name
                : null;
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

                _rows[i].EnableInClassList(TargetRowClass, i == 0);
            }

            element.style.display = IsShown ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void WatchModel(HudModel model)
        {
            if (_model == model)
            {
                return;
            }

            if (_model != null)
            {
                _model.Changed -= HandleModelChanged;
            }

            _model = model;
            if (_model != null)
            {
                _model.Changed += HandleModelChanged;
            }
        }

        private void Subscribe()
        {
            if (_subscribed)
            {
                return;
            }

            GameSignals.Spectate.Changed += HandleSpectateChanged;
            GameSignals.Participants.Changed += HandleParticipantsChanged;
            GameSignals.HudModel.Changed += HandleHudModelChanged;
            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed)
            {
                return;
            }

            GameSignals.Spectate.Changed -= HandleSpectateChanged;
            GameSignals.Participants.Changed -= HandleParticipantsChanged;
            GameSignals.HudModel.Changed -= HandleHudModelChanged;
            _subscribed = false;
        }

        // Edit Mode では OnDestroy が呼ばれずに消えることがあるので、消えた後に届いた通知で購読を外す
        private bool ForgetIfDestroyed()
        {
            if (this != null)
            {
                return false;
            }

            Unsubscribe();
            WatchModel(null);
            return true;
        }

        private void HandleSpectateChanged(SpectateStatus? status)
        {
            if (!ForgetIfDestroyed())
            {
                Show(status);
            }
        }

        private void HandleParticipantsChanged(IParticipants participants)
        {
            if (!ForgetIfDestroyed() && _status.HasValue)
            {
                Refresh();
            }
        }

        private void HandleHudModelChanged(HudModel model)
        {
            if (ForgetIfDestroyed())
            {
                return;
            }

            WatchModel(model);
            if (_status.HasValue)
            {
                Refresh();
            }
        }

        private void HandleModelChanged(HudField field)
        {
            if (!ForgetIfDestroyed() && field == HudField.Notice && _status.HasValue)
            {
                Refresh();
            }
        }
    }
}
