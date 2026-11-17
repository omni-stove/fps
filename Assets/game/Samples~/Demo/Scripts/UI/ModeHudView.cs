using System;
using System.Collections.Generic;
using Shooter.Core;
using UnityEngine.UIElements;

namespace Shooter.Runtime
{
    public sealed class ModeHudView : HudElementView
    {
        public const string UxmlName = "mode-hud";

        public const string TimeClass = "mode-hud__time";
        public const string ScoresClass = "mode-hud__scores";
        public const string ScoreClass = "mode-hud__score";

        public const string OwnScoreClass = "mode-hud__score--own";

        public const string LeadingScoreClass = "mode-hud__score--leading";

        public const string LinesClass = "mode-hud__lines";
        public const string LineClass = "mode-hud__line";
        public const string ResultClass = "mode-hud__result";

        private readonly List<ModeHudScoreLine> _scores = new List<ModeHudScoreLine>();
        private readonly List<string> _scoreTexts = new List<string>();
        private readonly List<LocalizedText> _lineSources = new List<LocalizedText>();
        private readonly List<string> _lines = new List<string>();
        private readonly List<LocalizedText> _formattedLines = new List<LocalizedText>();
        private LocalizedText _timeSource;
        private LocalizedText _resultSource;
        private string _time = string.Empty;
        private string _result = string.Empty;
        private bool _shown;
        private bool _subscribed;

        // 出している中身が GameSignals から来たか。来ていれば、要素を持っていない間に状態が消されたことを、受けたときに隠して合わせる
        private bool _showsSignal;

        private Label _timeLabel;
        private VisualElement _scoresBox;
        private VisualElement _linesBox;
        private Label _resultLabel;

        public override string ElementName => UxmlName;

        public bool IsShown => _shown;

        public string TimeText => _time;

        public IReadOnlyList<ModeHudScoreLine> Scores => _scores;

        public IReadOnlyList<string> ScoreTexts => _scoreTexts;

        public IReadOnlyList<string> Lines => _lines;

        public string ResultText => _result;

        public void Show(LocalizedText time, IReadOnlyList<ModeHudScoreLine> scores, IReadOnlyList<LocalizedText> lines, LocalizedText result)
        {
            if (scores == null)
            {
                throw new ArgumentNullException(nameof(scores));
            }

            _showsSignal = false;
            Write(time, scores, lines, result);
        }

        public void Hide()
        {
            _showsSignal = false;
            _shown = false;
            Refresh();
        }

        public void Show(ModeStatus status)
        {
            if (status == null)
            {
                Hide();
                return;
            }

            var formatter = ModeHudFormatters.For(status);
            var scores = formatter.Scores(status)
                ?? throw new InvalidOperationException($"{formatter.GetType().Name}.Scores returned null for {status.Detail?.GetType().Name ?? "a mode without detail"}");
            _formattedLines.Clear();
            formatter.AppendLines(status, _formattedLines);
            Write(formatter.Time(status), scores, _formattedLines, formatter.Result(status));
            _showsSignal = true;
        }

        private void Write(LocalizedText time, IReadOnlyList<ModeHudScoreLine> scores, IReadOnlyList<LocalizedText> lines, LocalizedText result)
        {
            _timeSource = time;
            _resultSource = result;
            _scores.Clear();
            _scores.AddRange(scores);
            _lineSources.Clear();
            if (lines != null)
            {
                _lineSources.AddRange(lines);
            }

            ResolveTexts();
            _shown = true;
            Refresh();
        }

        protected override void OnBound(VisualElement element)
        {
            element.Clear();
            _timeLabel = AddLabel(element, TimeClass);
            _scoresBox = AddBox(element, ScoresClass);
            _linesBox = AddBox(element, LinesClass);
            _resultLabel = AddLabel(element, ResultClass);

            Subscribe();
            var status = GameSignals.ModeStatus.Value;
            if (status != null || _showsSignal)
            {
                Show(status);
                return;
            }

            // 要素を持っていない間の言語の変更は届かないので、受けたときに今の言語で文にし直す
            ResolveTexts();
            Refresh();
        }

        private void OnDestroy()
        {
            Unsubscribe();
        }

        private void Subscribe()
        {
            if (!_subscribed)
            {
                GameSignals.ModeStatus.Changed += HandleStatusChanged;
                _subscribed = true;
            }
        }

        private void Unsubscribe()
        {
            if (_subscribed)
            {
                GameSignals.ModeStatus.Changed -= HandleStatusChanged;
                _subscribed = false;
            }
        }

        private void HandleStatusChanged(ModeStatus status)
        {
            // Edit Mode では OnDestroy が呼ばれずに消えることがあるので、消えた後に届いた通知で購読を外す
            if (this == null)
            {
                GameSignals.ModeStatus.Changed -= HandleStatusChanged;
                return;
            }

            Show(status);
        }

        protected override void OnLanguageChanged()
        {
            ResolveTexts();
            Refresh();
        }

        private void ResolveTexts()
        {
            _time = Localizer.Resolve(_timeSource);
            _result = Localizer.Resolve(_resultSource);
            _scoreTexts.Clear();
            foreach (var score in _scores)
            {
                _scoreTexts.Add(Localizer.Resolve(score.Text));
            }

            _lines.Clear();
            foreach (var source in _lineSources)
            {
                var line = Localizer.Resolve(source);
                if (!string.IsNullOrEmpty(line))
                {
                    _lines.Add(line);
                }
            }
        }

        protected override void OnUnbinding(VisualElement element)
        {
            Unsubscribe();
            element.Clear();
            element.style.display = DisplayStyle.None;
            _timeLabel = null;
            _scoresBox = null;
            _linesBox = null;
            _resultLabel = null;
        }

        private void Refresh()
        {
            var element = Element;
            if (element == null)
            {
                return;
            }

            element.style.display = _shown ? DisplayStyle.Flex : DisplayStyle.None;
            if (!_shown)
            {
                return;
            }

            SetText(_timeLabel, _time);
            _timeLabel.style.display = _time.Length > 0 ? DisplayStyle.Flex : DisplayStyle.None;

            Resize(_scoresBox, _scores.Count, ScoreClass);
            for (var i = 0; i < _scores.Count; i++)
            {
                var label = (Label)_scoresBox[i];
                SetText(label, _scoreTexts[i]);
                label.EnableInClassList(OwnScoreClass, _scores[i].IsOwn);
                label.EnableInClassList(LeadingScoreClass, _scores[i].IsLeading);
            }

            Resize(_linesBox, _lines.Count, LineClass);
            for (var i = 0; i < _lines.Count; i++)
            {
                SetText((Label)_linesBox[i], _lines[i]);
            }

            SetText(_resultLabel, _result);
            _resultLabel.style.display = _result.Length > 0 ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private static void Resize(VisualElement box, int count, string labelClass)
        {
            while (box.childCount > count)
            {
                box.RemoveAt(box.childCount - 1);
            }

            while (box.childCount < count)
            {
                AddLabel(box, labelClass);
            }
        }

        // 毎フレーム呼ばれるので、中身が変わらない行は書き直さない。
        private static void SetText(Label label, string text)
        {
            if (label.text != text)
            {
                label.text = text;
            }
        }

        private static Label AddLabel(VisualElement parent, string className)
        {
            var label = new Label { pickingMode = PickingMode.Ignore };
            label.AddToClassList(className);
            parent.Add(label);
            return label;
        }

        private static VisualElement AddBox(VisualElement parent, string className)
        {
            var box = new VisualElement { pickingMode = PickingMode.Ignore };
            box.AddToClassList(className);
            parent.Add(box);
            return box;
        }
    }
}
