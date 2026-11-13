using System;
using System.Collections.Generic;
using Fps.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace Fps.Runtime
{
    /// <summary>
    /// 拠点制圧 (<see cref="Domination"/>) の HUD。画面の上中央に両チームのポイント・残り時間・各拠点の持ち主を出し、
    /// 試合が終わったら勝者を出す。Hud.uxml の <see cref="UxmlName"/> を使う。
    /// <see cref="Hud.AddViews"/> では付けず、<see cref="DominationRunner"/> が試合を始めるときに Hud の GameObject に付けて <see cref="Show"/> する。
    /// 出している間は、残り時間をこちらで出すので Hud の残り時間の欄 (<see cref="HudView.TimeLabelName"/> の親) を隠す。
    /// </summary>
    /// <remarks>
    /// 値は毎フレーム (<see cref="LateUpdate"/>) <see cref="Domination"/> から読み直し、表示する整数や持ち主が変わったときだけ文字列を作り直す。
    /// 文字列は Label が無い (UIDocument の木がまだ無い) ときも <see cref="AlphaScoreText"/> などで読める。
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class DominationView : HudElementView
    {
        /// <summary>Hud.uxml の拠点制圧の要素。</summary>
        public const string UxmlName = "domination";

        public const string ScoresClass = "domination__scores";
        public const string ScoreClass = "domination__score";
        public const string TimeClass = "domination__time";
        public const string PointsClass = "domination__points";
        public const string PointClass = "domination__point";

        /// <summary>両チームがいて占領が止まっている拠点に足す USS クラス。</summary>
        public const string ContestedClass = "domination__point--contested";

        public const string ResultClass = "domination__result";

        private readonly List<string> _pointTexts = new List<string>();
        private readonly List<Label> _pointLabels = new List<Label>();
        private Domination _source;
        private Label _alphaLabel;
        private Label _bravoLabel;
        private Label _timeLabel;
        private Label _resultLabel;
        private VisualElement _hiddenTimePanel;

        // 表示中の値。変わったときだけ文字列を作り直す。
        private int _shownAlpha = -1;
        private int _shownBravo = -1;
        private int _shownSeconds = -1;
        private Team?[] _shownOwners = Array.Empty<Team?>();
        private bool[] _shownContested = Array.Empty<bool>();
        private GameModeState? _shownState;

        public override string ElementName => UxmlName;

        /// <summary>出している試合。出していなければ null。</summary>
        public Domination Source => _source;

        /// <summary>Alpha のチーム名と切り捨てたポイント。出していなければ空文字。</summary>
        public string AlphaScoreText { get; private set; } = string.Empty;

        /// <summary>Bravo のチーム名と切り捨てたポイント。出していなければ空文字。</summary>
        public string BravoScoreText { get; private set; } = string.Empty;

        /// <summary>残り時間 (mm:ss)。出していなければ空文字。</summary>
        public string TimeText { get; private set; } = string.Empty;

        /// <summary>試合の結果。終わるまでは空文字。</summary>
        public string ResultText { get; private set; } = string.Empty;

        /// <summary>拠点ごとの呼び名と持ち主 (番号の順)。</summary>
        public IReadOnlyList<string> PointTexts => _pointTexts;

        /// <summary>domination を出し始める。前に出していた試合は手放す。null なら <see cref="Clear"/> と同じ。</summary>
        public void Show(Domination domination)
        {
            if (domination == null)
            {
                Clear();
                return;
            }

            _source = domination;
            ForgetShownValues();
            Rebuild();
            Refresh();
        }

        /// <summary>出すのをやめて要素を隠し、Hud の残り時間の欄を戻す。</summary>
        public void Clear()
        {
            _source = null;
            ForgetShownValues();
            AlphaScoreText = string.Empty;
            BravoScoreText = string.Empty;
            TimeText = string.Empty;
            ResultText = string.Empty;
            Rebuild();
        }

        /// <summary>試合の今の値を読み、変わったものだけ文字列と Label を書き直す。Play 中は LateUpdate が呼ぶ。</summary>
        public void Refresh()
        {
            if (_source == null)
            {
                return;
            }

            var alpha = DominationHudFormat.DisplayedScore(_source.GetScore(Team.Alpha));
            if (alpha != _shownAlpha)
            {
                _shownAlpha = alpha;
                AlphaScoreText = HudFormat.TeamScore(Team.Alpha, alpha);
                SetText(_alphaLabel, AlphaScoreText);
            }

            var bravo = DominationHudFormat.DisplayedScore(_source.GetScore(Team.Bravo));
            if (bravo != _shownBravo)
            {
                _shownBravo = bravo;
                BravoScoreText = HudFormat.TeamScore(Team.Bravo, bravo);
                SetText(_bravoLabel, BravoScoreText);
            }

            var seconds = HudFormat.DisplayedSeconds(_source.RemainingSeconds);
            if (seconds != _shownSeconds)
            {
                _shownSeconds = seconds;
                TimeText = HudFormat.Time(_source.RemainingSeconds);
                SetText(_timeLabel, TimeText);
            }

            for (var i = 0; i < _source.PointCount; i++)
            {
                var owner = _source.GetOwner(i);
                var contested = _source.IsContested(i);
                if (owner == _shownOwners[i] && contested == _shownContested[i])
                {
                    continue;
                }

                _shownOwners[i] = owner;
                _shownContested[i] = contested;
                _pointTexts[i] = DominationHudFormat.PointOwner(i, owner);
                ApplyPoint(i);
            }

            if (_source.State != _shownState)
            {
                _shownState = _source.State;
                ResultText = _source.State == GameModeState.Ended ? HudFormat.Result(_source.Winner) : string.Empty;
                SetText(_resultLabel, ResultText);
                if (_resultLabel != null)
                {
                    _resultLabel.style.display = ResultText.Length > 0 ? DisplayStyle.Flex : DisplayStyle.None;
                }
            }
        }

        /// <summary>チームの色 (<see cref="Hud.TeamColor"/>) が変わったときに呼ぶ。点数と拠点の欄を塗り直す。</summary>
        public void RefreshTeamColors()
        {
            ApplyScoreColors();
            for (var i = 0; i < _pointLabels.Count; i++)
            {
                ApplyPoint(i);
            }
        }

        private void LateUpdate()
        {
            Refresh();
        }

        protected override void OnBound(VisualElement element)
        {
            Rebuild();
            Refresh();
        }

        protected override void OnUnbinding(VisualElement element)
        {
            RestoreTimePanel();
            element.Clear();
            element.style.display = DisplayStyle.None;
            ForgetLabels();
        }

        private void ForgetShownValues()
        {
            _shownAlpha = -1;
            _shownBravo = -1;
            _shownSeconds = -1;
            _shownState = null;

            var count = _source != null ? _source.PointCount : 0;
            _shownOwners = new Team?[count];
            _shownContested = new bool[count];
            _pointTexts.Clear();
            for (var i = 0; i < count; i++)
            {
                _pointTexts.Add(DominationHudFormat.PointOwner(i, null));
            }
        }

        // 拠点の数は試合ごとに決まり、作り直すのは Show と木の作り直しのときだけなので、差分を取らずに作り直す。
        private void Rebuild()
        {
            var element = Element;
            if (element == null)
            {
                return;
            }

            element.Clear();
            ForgetLabels();
            if (_source == null)
            {
                element.style.display = DisplayStyle.None;
                RestoreTimePanel();
                return;
            }

            var scores = new VisualElement { pickingMode = PickingMode.Ignore };
            scores.AddToClassList(ScoresClass);
            _alphaLabel = CreateLabel(AlphaScoreText, ScoreClass);
            _timeLabel = CreateLabel(TimeText, TimeClass);
            _bravoLabel = CreateLabel(BravoScoreText, ScoreClass);
            ApplyScoreColors();
            scores.Add(_alphaLabel);
            scores.Add(_timeLabel);
            scores.Add(_bravoLabel);
            element.Add(scores);

            var points = new VisualElement { pickingMode = PickingMode.Ignore };
            points.AddToClassList(PointsClass);
            for (var i = 0; i < _pointTexts.Count; i++)
            {
                var label = CreateLabel(_pointTexts[i], PointClass);
                _pointLabels.Add(label);
                points.Add(label);
                ApplyPoint(i);
            }

            element.Add(points);

            _resultLabel = CreateLabel(ResultText, ResultClass);
            _resultLabel.style.display = ResultText.Length > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            element.Add(_resultLabel);

            element.style.display = DisplayStyle.Flex;
            HideTimePanel(element);
        }

        private void ApplyPoint(int index)
        {
            if (index >= _pointLabels.Count)
            {
                return;
            }

            var label = _pointLabels[index];
            label.text = _pointTexts[index];
            var owner = _shownOwners[index];
            label.style.color = owner.HasValue ? TeamColorOf(owner.Value) : TeamColors.Neutral;
            label.EnableInClassList(ContestedClass, _shownContested[index]);
        }

        private void ApplyScoreColors()
        {
            if (_alphaLabel != null)
            {
                _alphaLabel.style.color = TeamColorOf(Team.Alpha);
            }

            if (_bravoLabel != null)
            {
                _bravoLabel.style.color = TeamColorOf(Team.Bravo);
            }
        }

        // 拠点の円盤 (ControlPointZone) と色を揃えるため、Hud が色の組に従っていなければ、オフラインのプレイヤー (Alpha) から見た今の組の色にする
        private Color TeamColorOf(Team team)
        {
            var hud = GetComponent<Hud>();
            return hud != null && hud.FollowsTeamPalette ? hud.TeamColor(team) : TeamColors.Of(team, TeamColors.OfflineViewer);
        }

        /// <summary>残り時間はこちらで出すので、同じ上中央にある Hud の残り時間の欄を隠す。</summary>
        private void HideTimePanel(VisualElement element)
        {
            if (_hiddenTimePanel != null)
            {
                return;
            }

            var root = element.parent ?? element;
            var timePanel = root.Q<Label>(HudView.TimeLabelName)?.parent;
            if (timePanel == null || timePanel == root)
            {
                return;
            }

            timePanel.style.display = DisplayStyle.None;
            _hiddenTimePanel = timePanel;
        }

        private void RestoreTimePanel()
        {
            if (_hiddenTimePanel == null)
            {
                return;
            }

            _hiddenTimePanel.style.display = StyleKeyword.Null;
            _hiddenTimePanel = null;
        }

        private void ForgetLabels()
        {
            _alphaLabel = null;
            _bravoLabel = null;
            _timeLabel = null;
            _resultLabel = null;
            _pointLabels.Clear();
        }

        private static void SetText(Label label, string text)
        {
            if (label != null)
            {
                label.text = text;
            }
        }

        private static Label CreateLabel(string text, string className)
        {
            var label = new Label(text ?? string.Empty) { pickingMode = PickingMode.Ignore };
            label.AddToClassList(className);
            return label;
        }
    }
}
