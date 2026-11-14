using System;
using System.Collections.Generic;
using Fps.Core;
using UnityEngine.UIElements;

namespace Fps.Runtime
{
    /// <summary>
    /// オフラインのモード (<see cref="ModeRunnerBase"/>) の HUD。画面左上に残り時間・陣営ごとの得点・派生モードが足す行・試合結果を出す。
    /// Hud.uxml の <see cref="UxmlName"/> の枠に、行の Label をコードで足す。何を出すかは <see cref="ModeRunnerBase"/> が
    /// <see cref="Show"/> / <see cref="Hide"/> で決める。要素をまだ受けていない間 (HUD の木を作り直している間) も中身を覚えておき、受けたときに合わせる。
    /// 中身が変わらない行は書き直さない (毎フレーム呼ばれるため)。
    /// 受け取った文は今の言語 (<see cref="Localizer.Resolve"/>) で文にし、言語が変わったら文にし直す。
    /// </summary>
    public sealed class ModeHudView : HudElementView, IModeHud
    {
        /// <summary>Hud.uxml のモードの枠。</summary>
        public const string UxmlName = "mode-hud";

        public const string TimeClass = "mode-hud__time";
        public const string ScoresClass = "mode-hud__scores";
        public const string ScoreClass = "mode-hud__score";

        /// <summary>自分の陣営の行に足す USS クラス。</summary>
        public const string OwnScoreClass = "mode-hud__score--own";

        /// <summary>単独で首位の陣営の行に足す USS クラス。</summary>
        public const string LeadingScoreClass = "mode-hud__score--leading";

        public const string LinesClass = "mode-hud__lines";
        public const string LineClass = "mode-hud__line";
        public const string ResultClass = "mode-hud__result";

        private readonly List<ModeHudScoreLine> _scores = new List<ModeHudScoreLine>();
        private readonly List<LocalizedText> _lineSources = new List<LocalizedText>();
        private readonly List<string> _lines = new List<string>();
        private LocalizedText _timeSource;
        private LocalizedText _resultSource;
        private string _time = string.Empty;
        private string _result = string.Empty;
        private bool _shown;

        private Label _timeLabel;
        private VisualElement _scoresBox;
        private VisualElement _linesBox;
        private Label _resultLabel;

        public override string ElementName => UxmlName;

        /// <summary>出しているか。</summary>
        public bool IsShown => _shown;

        /// <summary>出している残り時間 (今の言語の文)。</summary>
        public string TimeText => _time;

        /// <summary>出している得点の行 (上から)。</summary>
        public IReadOnlyList<ModeHudScoreLine> Scores => _scores;

        /// <summary>出している、派生モードが足した行 (上から、今の言語の文)。</summary>
        public IReadOnlyList<string> Lines => _lines;

        /// <summary>出している試合結果 (今の言語の文)。試合中は空文字。</summary>
        public string ResultText => _result;

        /// <summary>
        /// 中身を差し替えて出す。scores は上から並べる順、lines は得点の下に足す行。文にして空になる行は出さない。
        /// result が文にして空なら結果の行を隠す。
        /// </summary>
        /// <exception cref="ArgumentNullException">scores が null。</exception>
        public void Show(LocalizedText time, IReadOnlyList<ModeHudScoreLine> scores, IReadOnlyList<LocalizedText> lines, LocalizedText result)
        {
            if (scores == null)
            {
                throw new ArgumentNullException(nameof(scores));
            }

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

        /// <summary>隠す。</summary>
        public void Hide()
        {
            _shown = false;
            Refresh();
        }

        protected override void OnBound(VisualElement element)
        {
            element.Clear();
            _timeLabel = AddLabel(element, TimeClass);
            _scoresBox = AddBox(element, ScoresClass);
            _linesBox = AddBox(element, LinesClass);
            _resultLabel = AddLabel(element, ResultClass);

            // 要素を持っていない間の言語の変更は届かないので、受けたときに今の言語で文にし直す
            ResolveTexts();
            Refresh();
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
                SetText(label, _scores[i].Text);
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
