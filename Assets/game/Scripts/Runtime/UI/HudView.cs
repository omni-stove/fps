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

        /// <summary>Hud.uxml の、弾数の上に出す武器名の Label。武器名が空のときは隠す。</summary>
        public const string WeaponNameLabelName = "hud-weapon-name";

        /// <summary>Hud.uxml の残り時間の Label。</summary>
        public const string TimeLabelName = "hud-time";

        /// <summary>Hud.uxml の照準。ADS 中とダッシュ中は隠す。</summary>
        public const string CrosshairName = "hud-crosshair";

        /// <summary>Hud.uxml の照準の中央の点 (<see cref="CrosshairName"/> の子)。</summary>
        public const string CrosshairDotName = "hud-crosshair-dot";

        /// <summary>Hud.uxml の照準の上の線 (<see cref="CrosshairName"/> の子)。</summary>
        public const string CrosshairTopName = "hud-crosshair-top";

        /// <summary>Hud.uxml の照準の下の線 (<see cref="CrosshairName"/> の子)。</summary>
        public const string CrosshairBottomName = "hud-crosshair-bottom";

        /// <summary>Hud.uxml の照準の左の線 (<see cref="CrosshairName"/> の子)。</summary>
        public const string CrosshairLeftName = "hud-crosshair-left";

        /// <summary>Hud.uxml の照準の右の線 (<see cref="CrosshairName"/> の子)。</summary>
        public const string CrosshairRightName = "hud-crosshair-right";

        /// <summary>広がり 0 のときの、中央から線の内側の端までの隙間 (基準解像度の px)。</summary>
        public const float MinCrosshairGap = 6f;

        /// <summary>広がり 1 のときの、中央から線の内側の端までの隙間 (基準解像度の px)。</summary>
        public const float MaxCrosshairGap = 30f;

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

        /// <summary>Hud.uxml の、弾数の下に出すリロードの進みのバー。進みが無いときは隠す。</summary>
        public const string ReloadBarName = "hud-reload-bar";

        /// <summary>Hud.uxml の、リロードの進みの塗り (<see cref="ReloadBarName"/> の子)。幅を進みの % にする。</summary>
        public const string ReloadFillName = "hud-reload-fill";

        /// <summary>Hud.uxml の、アクティブリロードで成功になる幅 (<see cref="ReloadBarName"/> の子)。幅が 0 のときは隠す。</summary>
        public const string ReloadWindowName = "hud-reload-window";

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
        public Label WeaponNameLabel { get; }
        public Label TimeLabel { get; }
        public VisualElement Crosshair { get; }
        public VisualElement CrosshairDot { get; }
        public VisualElement CrosshairTop { get; }
        public VisualElement CrosshairBottom { get; }
        public VisualElement CrosshairLeft { get; }
        public VisualElement CrosshairRight { get; }
        public Label NoticeLabel { get; }
        public VisualElement ScorePanel { get; }
        public Label FirstScoreLabel { get; }
        public Label SecondScoreLabel { get; }
        public Label ResultLabel { get; }
        public VisualElement ReloadBar { get; }
        public VisualElement ReloadFill { get; }
        public VisualElement ReloadWindow { get; }

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
            WeaponNameLabel = Require<Label>(root, WeaponNameLabelName);
            TimeLabel = Require<Label>(root, TimeLabelName);
            Crosshair = Require<VisualElement>(root, CrosshairName);
            CrosshairDot = Require<VisualElement>(Crosshair, CrosshairDotName);
            CrosshairTop = Require<VisualElement>(Crosshair, CrosshairTopName);
            CrosshairBottom = Require<VisualElement>(Crosshair, CrosshairBottomName);
            CrosshairLeft = Require<VisualElement>(Crosshair, CrosshairLeftName);
            CrosshairRight = Require<VisualElement>(Crosshair, CrosshairRightName);
            NoticeLabel = Require<Label>(root, NoticeLabelName);
            ScorePanel = Require<VisualElement>(root, ScorePanelName);
            FirstScoreLabel = Require<Label>(root, FirstScoreLabelName);
            SecondScoreLabel = Require<Label>(root, SecondScoreLabelName);
            ResultLabel = Require<Label>(root, ResultLabelName);
            ReloadBar = Require<VisualElement>(root, ReloadBarName);
            ReloadFill = Require<VisualElement>(ReloadBar, ReloadFillName);
            ReloadWindow = Require<VisualElement>(ReloadBar, ReloadWindowName);

            ShowHealth();
            ShowAmmo();
            ShowWeaponName();
            TimeLabel.text = _model.TimeText;
            ShowNotice();
            ShowScores();
            ShowResult();
            ShowReloadProgress();
            ShowCrosshair();
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
                case HudField.WeaponName:
                    ShowWeaponName();
                    break;
                case HudField.ReloadProgress:
                    ShowReloadProgress();
                    break;
                case HudField.Crosshair:
                    ShowCrosshair();
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

        private void ShowWeaponName()
        {
            ShowOrHide(WeaponNameLabel, _model.WeaponNameText);
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

        /// <summary>進みがあればバーを出して塗りの幅を進みにし、成功になる幅を重ねる。進みが無ければバーごと隠す。</summary>
        private void ShowReloadProgress()
        {
            var progress = _model.ReloadProgress;
            if (!progress.HasValue)
            {
                ReloadBar.style.display = DisplayStyle.None;
                return;
            }

            ReloadBar.style.display = DisplayStyle.Flex;
            ReloadFill.style.width = Length.Percent(progress.Value * 100f);

            var start = _model.ReloadWindowStart;
            var end = _model.ReloadWindowEnd;
            if (end > start)
            {
                ReloadWindow.style.left = Length.Percent(start * 100f);
                ReloadWindow.style.width = Length.Percent((end - start) * 100f);
                ReloadWindow.style.display = DisplayStyle.Flex;
            }
            else
            {
                ReloadWindow.style.display = DisplayStyle.None;
            }
        }

        /// <summary>広がり spread (0〜1) のときの、中央から線の内側の端までの隙間 (基準解像度の px)。範囲外は端に揃える。</summary>
        public static float CrosshairGap(float spread)
        {
            return Mathf.Lerp(MinCrosshairGap, MaxCrosshairGap, Mathf.Clamp01(spread));
        }

        /// <summary>
        /// 照準を出すか隠し、4 本の線を中央から隙間の分だけ外へずらす。Hud.uss は線を中央に接する位置に置いているので、
        /// 隙間は translate だけで決まる。
        /// </summary>
        private void ShowCrosshair()
        {
            Crosshair.style.display = _model.IsCrosshairVisible ? DisplayStyle.Flex : DisplayStyle.None;

            var gap = CrosshairGap(_model.CrosshairSpread);
            CrosshairTop.style.translate = new Translate(new Length(0f), new Length(-gap));
            CrosshairBottom.style.translate = new Translate(new Length(0f), new Length(gap));
            CrosshairLeft.style.translate = new Translate(new Length(-gap), new Length(0f));
            CrosshairRight.style.translate = new Translate(new Length(gap), new Length(0f));
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
