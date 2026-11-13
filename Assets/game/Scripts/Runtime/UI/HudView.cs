using System;
using Fps.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace Fps.Runtime
{
    /// <summary>
    /// <see cref="HudModel"/> の文字列を Hud.uxml の Label に写す。<see cref="HudModel.Changed"/> が来たときだけ書き換え、
    /// 毎フレームは何もしない。表示の判断 (書式・いつ変わるか) はすべて HudModel が持つ。
    /// </summary>
    public sealed class HudView : IDisposable
    {
        /// <summary>Hud.uxml の体力の Label。</summary>
        public const string HealthLabelName = "hud-health";

        /// <summary>Hud.uxml の弾数の Label。</summary>
        public const string AmmoLabelName = "hud-ammo";

        /// <summary>Hud.uxml の残り時間の Label。</summary>
        public const string TimeLabelName = "hud-time";

        /// <summary>Hud.uxml の照準。</summary>
        public const string CrosshairName = "hud-crosshair";

        /// <summary>Hud.uxml の一時的な案内の Label。案内が空のときは隠す。</summary>
        public const string NoticeLabelName = "hud-notice";

        /// <summary>Hud.uxml のチームの点数の欄。点数が無いときは隠す。</summary>
        public const string ScorePanelName = "hud-score-panel";

        /// <summary>Hud.uxml の、先に出すチーム (自分のチーム) の点数の Label。</summary>
        public const string FirstScoreLabelName = "hud-score-first";

        /// <summary>Hud.uxml の、後に出すチーム (相手のチーム) の点数の Label。</summary>
        public const string SecondScoreLabelName = "hud-score-second";

        /// <summary>Hud.uxml の試合結果の Label。結果が空のときは隠す。</summary>
        public const string ResultLabelName = "hud-result";

        /// <summary>リロード中に弾数の Label に付ける USS クラス。</summary>
        public const string ReloadingClass = "hud__value--reloading";

        /// <summary>体力が少ないときに体力の Label に付ける USS クラス。</summary>
        public const string LowHealthClass = "hud__value--low";

        private readonly HudModel _model;
        private Color _alphaColor = Color.white;
        private Color _bravoColor = Color.white;
        private bool _disposed;

        public Label HealthLabel { get; }
        public Label AmmoLabel { get; }
        public Label TimeLabel { get; }
        public VisualElement Crosshair { get; }
        public Label NoticeLabel { get; }
        public VisualElement ScorePanel { get; }
        public Label FirstScoreLabel { get; }
        public Label SecondScoreLabel { get; }
        public Label ResultLabel { get; }

        /// <summary>
        /// root の下から Label と照準を名前で探し、今のモデルの文字列を入れてから変更を購読する。
        /// 見つからなければ <see cref="InvalidOperationException"/> を投げる (UXML と名前がずれている)。
        /// </summary>
        public HudView(VisualElement root, HudModel model)
        {
            if (root == null)
            {
                throw new ArgumentNullException(nameof(root));
            }

            _model = model ?? throw new ArgumentNullException(nameof(model));
            HealthLabel = Require<Label>(root, HealthLabelName);
            AmmoLabel = Require<Label>(root, AmmoLabelName);
            TimeLabel = Require<Label>(root, TimeLabelName);
            Crosshair = Require<VisualElement>(root, CrosshairName);
            NoticeLabel = Require<Label>(root, NoticeLabelName);
            ScorePanel = Require<VisualElement>(root, ScorePanelName);
            FirstScoreLabel = Require<Label>(root, FirstScoreLabelName);
            SecondScoreLabel = Require<Label>(root, SecondScoreLabelName);
            ResultLabel = Require<Label>(root, ResultLabelName);

            ShowHealth();
            ShowAmmo();
            TimeLabel.text = _model.TimeText;
            ShowNotice();
            ShowScores();
            ShowResult();
            _model.Changed += OnChanged;
        }

        /// <summary>チームの点数を塗る色を決め、今の点数を塗り直す。決めるまでは白で塗る。</summary>
        public void SetTeamColors(Color alpha, Color bravo)
        {
            _alphaColor = alpha;
            _bravoColor = bravo;
            ShowScores();
        }

        /// <summary>team を塗る色。<see cref="SetTeamColors"/> の前は白。</summary>
        public Color TeamColor(Team team)
        {
            return team == Team.Bravo ? _bravoColor : _alphaColor;
        }

        /// <summary>モデルの購読を外す。Label の文字列はそのまま残る。</summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _model.Changed -= OnChanged;
            _disposed = true;
        }

        private void OnChanged(HudField field)
        {
            switch (field)
            {
                case HudField.Health:
                    ShowHealth();
                    break;
                case HudField.Ammo:
                    ShowAmmo();
                    break;
                case HudField.Time:
                    TimeLabel.text = _model.TimeText;
                    break;
                case HudField.Notice:
                    ShowNotice();
                    break;
                case HudField.Score:
                    ShowScores();
                    break;
                case HudField.Result:
                    ShowResult();
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(field), field, "field is not a valid HudField");
            }
        }

        private void ShowHealth()
        {
            HealthLabel.text = _model.HealthText;
            HealthLabel.EnableInClassList(LowHealthClass, _model.IsHealthLow);
        }

        private void ShowAmmo()
        {
            AmmoLabel.text = _model.AmmoText;
            AmmoLabel.EnableInClassList(ReloadingClass, _model.IsReloading);
        }

        /// <summary>案内の文字列を入れ、空なら Label ごと隠す (背景の帯だけが残らないように)。</summary>
        private void ShowNotice()
        {
            ShowOrHide(NoticeLabel, _model.NoticeText);
        }

        private void ShowResult()
        {
            ShowOrHide(ResultLabel, _model.ResultText);
        }

        /// <summary>点数を入れてチームの色で塗る。点数が無ければ欄ごと隠す。</summary>
        private void ShowScores()
        {
            FirstScoreLabel.text = _model.FirstScoreText;
            SecondScoreLabel.text = _model.SecondScoreText;

            var first = _model.FirstScoreTeam;
            var second = _model.SecondScoreTeam;
            if (first.HasValue && second.HasValue)
            {
                FirstScoreLabel.style.color = TeamColor(first.Value);
                SecondScoreLabel.style.color = TeamColor(second.Value);
                ScorePanel.style.display = DisplayStyle.Flex;
            }
            else
            {
                ScorePanel.style.display = DisplayStyle.None;
            }
        }

        private static void ShowOrHide(Label label, string text)
        {
            label.text = text;
            label.style.display = string.IsNullOrEmpty(text) ? DisplayStyle.None : DisplayStyle.Flex;
        }

        private static T Require<T>(VisualElement root, string elementName)
            where T : VisualElement
        {
            var element = root.Q<T>(elementName);
            if (element == null)
            {
                throw new InvalidOperationException(
                    $"the HUD layout has no {typeof(T).Name} named '{elementName}'; check Assets/game/UI/Resources/Hud/Hud.uxml");
            }

            return element;
        }
    }
}
