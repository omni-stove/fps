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
    /// <see cref="Hud.AddViews"/> が最初から付け、出すものが来るまで隠しておく。要素を持っている間は <see cref="GameSignals.Domination"/> を購読し、
    /// 出された状態を <see cref="Show"/> し、消されたら隠す。要素を受けたときは今の状態に合わせる。言語が変わったら今の言語で出し直す。
    /// 出している間は、残り時間をこちらで出すので Hud の残り時間の欄 (<see cref="HudView.TimeLabelName"/> の親) を隠す。
    /// HUD が上の欄にチームの点数を出している (<see cref="Hud.ShowsTeamScores"/>) 間は出さず、拠点は <see cref="ControlPointsView"/> が出す。
    /// </summary>
    /// <remarks>
    /// 状態は毎フレーム出し直されるので、表示する整数や持ち主が変わったときだけ文字列を作り直す。
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
        private DominationStatus _source;
        private bool _subscribed;
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
        private bool? _shownEnded;

        public override string ElementName => UxmlName;

        /// <summary>出している状態。出していなければ null。</summary>
        public DominationStatus Source => _source;

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

        private void Show(DominationStatus status)
        {
            var hud = GetComponent<Hud>();
            if (status == null || (hud != null && hud.ShowsTeamScores))
            {
                Clear();
                return;
            }

            var samePoints = _source != null && _source.Points.Count == status.Points.Count;
            _source = status;
            if (samePoints)
            {
                Refresh();
                return;
            }

            ForgetShownValues();
            Rebuild();
            Refresh();
        }

        private void Clear()
        {
            _source = null;
            ForgetShownValues();
            AlphaScoreText = string.Empty;
            BravoScoreText = string.Empty;
            TimeText = string.Empty;
            ResultText = string.Empty;
            Rebuild();
        }

        private void Refresh()
        {
            if (_source == null)
            {
                return;
            }

            var alpha = DominationHudFormat.DisplayedScore(_source.AlphaScore);
            if (alpha != _shownAlpha)
            {
                _shownAlpha = alpha;
                AlphaScoreText = Localizer.Resolve(HudText.TeamScore(Team.Alpha, alpha));
                SetText(_alphaLabel, AlphaScoreText);
            }

            var bravo = DominationHudFormat.DisplayedScore(_source.BravoScore);
            if (bravo != _shownBravo)
            {
                _shownBravo = bravo;
                BravoScoreText = Localizer.Resolve(HudText.TeamScore(Team.Bravo, bravo));
                SetText(_bravoLabel, BravoScoreText);
            }

            var seconds = HudFormat.DisplayedSeconds(_source.RemainingSeconds);
            if (seconds != _shownSeconds)
            {
                _shownSeconds = seconds;
                TimeText = HudNumberFormat.RemainingTime(_source.RemainingSeconds);
                SetText(_timeLabel, TimeText);
            }

            for (var i = 0; i < _source.Points.Count; i++)
            {
                var owner = _source.Points[i].Owner;
                var contested = _source.Points[i].Contested;
                if (owner == _shownOwners[i] && contested == _shownContested[i])
                {
                    continue;
                }

                _shownOwners[i] = owner;
                _shownContested[i] = contested;
                _pointTexts[i] = Localizer.Resolve(DominationHudText.PointOwner(i, owner));
                ApplyPoint(i);
            }

            if (_source.IsEnded != _shownEnded)
            {
                _shownEnded = _source.IsEnded;
                ResultText = _source.IsEnded ? Localizer.Resolve(HudText.Result(_source.Winner)) : string.Empty;
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

        protected override void OnBound(VisualElement element)
        {
            Subscribe();

            // 要素を持っていない間の通知は受けていないので、覚えている状態を捨てて今の状態から作り直す
            _source = null;
            Show(GameSignals.Domination.Value);
        }

        private void OnDestroy()
        {
            Unsubscribe();
        }

        private void Subscribe()
        {
            if (!_subscribed)
            {
                GameSignals.Domination.Changed += HandleStatusChanged;
                _subscribed = true;
            }
        }

        private void Unsubscribe()
        {
            if (_subscribed)
            {
                GameSignals.Domination.Changed -= HandleStatusChanged;
                _subscribed = false;
            }
        }

        private void HandleStatusChanged(DominationStatus status)
        {
            // Edit Mode では OnDestroy が呼ばれずに消えることがあるので、消えた後に届いた通知で購読を外す
            if (this == null)
            {
                GameSignals.Domination.Changed -= HandleStatusChanged;
                return;
            }

            Show(status);
        }

        protected override void OnLanguageChanged()
        {
            if (_source == null)
            {
                return;
            }

            // 値が変わったときだけ文字列を作り直すので、覚えている値を忘れて今の言語で作り直させる
            ForgetShownValues();
            Rebuild();
            Refresh();
        }

        protected override void OnUnbinding(VisualElement element)
        {
            Unsubscribe();
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
            _shownEnded = null;

            var count = _source != null ? _source.Points.Count : 0;
            _shownOwners = new Team?[count];
            _shownContested = new bool[count];
            _pointTexts.Clear();
            for (var i = 0; i < count; i++)
            {
                _pointTexts.Add(Localizer.Resolve(DominationHudText.PointOwner(i, null)));
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
