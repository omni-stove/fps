using System.Collections.Generic;
using Shooter.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace Shooter.Runtime
{
    [DisallowMultipleComponent]
    public sealed class VoiceIndicatorView : HudElementView
    {
        public const string UxmlName = "voice";

        public const string RowClass = "voice__row";

        public const string LocalRowClass = "voice__row--local";

        private readonly List<string> _lines = new List<string>();
        private readonly List<Label> _rows = new List<Label>();
        private readonly List<string> _shownNames = new List<string>();
        private VoiceStatus? _status;
        private bool _subscribed;

        public override string ElementName => UxmlName;

        public VoiceStatus? Status => _status;

        public bool IsShown => _lines.Count > 0;

        public bool ShowsLocal => _status.HasValue && _status.Value.IsLocalSpeaking;

        public IReadOnlyList<string> Lines => _lines;

        public void Show(VoiceStatus? status)
        {
            _status = status;
            Refresh();
        }

        public void Refresh()
        {
            _lines.Clear();
            _shownNames.Clear();
            if (_status.HasValue)
            {
                var status = _status.Value;
                var participants = GameSignals.Participants.Value;
                ReadNames(status, participants, _shownNames);
                foreach (var line in VoiceText.Lines(status, participants))
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
            Show(GameSignals.Voice.Value);
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

        // 参加者の表は出し直さずに中身だけ変わる (名前が後から届く) ので、誰かが話している間は名前だけ毎フレーム読み直す
        private void Update()
        {
            if (Element == null || !_status.HasValue || _status.Value.SpeakingIds.Count == 0)
            {
                return;
            }

            var status = _status.Value;
            var participants = GameSignals.Participants.Value;
            for (var i = 0; i < status.SpeakingIds.Count; i++)
            {
                if (NameOf(status.SpeakingIds[i], participants) != _shownNames[i])
                {
                    Refresh();
                    return;
                }
            }
        }

        private void OnDestroy()
        {
            Unsubscribe();
        }

        private static void ReadNames(VoiceStatus status, IParticipants participants, List<string> names)
        {
            foreach (var id in status.SpeakingIds)
            {
                names.Add(NameOf(id, participants));
            }
        }

        private static string NameOf(int id, IParticipants participants)
        {
            return participants != null && participants.TryGet(id, out var participant) ? participant.Name : null;
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

            var local = ShowsLocal;
            for (var i = 0; i < _rows.Count; i++)
            {
                if (_rows[i].text != _lines[i])
                {
                    _rows[i].text = _lines[i];
                }

                _rows[i].EnableInClassList(LocalRowClass, local && i == 0);
            }

            element.style.display = IsShown ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void Subscribe()
        {
            if (_subscribed)
            {
                return;
            }

            GameSignals.Voice.Changed += HandleVoiceChanged;
            GameSignals.Participants.Changed += HandleParticipantsChanged;
            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed)
            {
                return;
            }

            GameSignals.Voice.Changed -= HandleVoiceChanged;
            GameSignals.Participants.Changed -= HandleParticipantsChanged;
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
            return true;
        }

        private void HandleVoiceChanged(VoiceStatus? status)
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
    }
}
