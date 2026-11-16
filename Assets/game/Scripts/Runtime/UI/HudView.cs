using System;
using System.Globalization;
using Fps.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace Fps.Runtime
{
    /// <summary>
    /// <see cref="HudModel"/> の文字列を Hud.uxml の Label に写す。<see cref="HudModel.Changed"/> が来たときだけ書き換え、
    /// 毎フレームは何もしない。表示の判断 (書式・いつ変わるか) はすべて HudModel が持つ。
    /// HUD の量で消す項目 (<see cref="HudVisibility"/>) は、モデルの値によらず欄ごと隠す。
    /// </summary>
    public sealed class HudView : IDisposable
    {
        /// <summary>Hud.uxml の体力の欄 (見出しと <see cref="HealthLabelName"/> を含む)。HUD の量で消すときは欄ごと隠す。</summary>
        public const string HealthPanelName = "hud-health-panel";

        /// <summary>Hud.uxml の体力の Label。</summary>
        public const string HealthLabelName = "hud-health";

        /// <summary>Hud.uxml の弾数の欄 (見出しと <see cref="AmmoLabelName"/> を含む)。HUD の量で消すときは欄ごと隠す。</summary>
        public const string AmmoPanelName = "hud-ammo-panel";

        /// <summary>Hud.uxml の弾数の Label。</summary>
        public const string AmmoLabelName = "hud-ammo";

        /// <summary>Hud.uxml の残り時間の欄 (<see cref="TimeLabelName"/> を含む)。HUD の量で消すときは欄ごと隠す。</summary>
        public const string TimePanelName = "hud-time-panel";

        /// <summary>Hud.uxml の、弾数の上に出す武器名の Label。武器名が空のときは隠す。</summary>
        public const string WeaponNameLabelName = "hud-weapon-name";

        /// <summary>Hud.uxml の残り時間の Label。</summary>
        public const string TimeLabelName = "hud-time";

        /// <summary>Hud.uxml の照準。一人称の ADS 中とダッシュ中は隠す。</summary>
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

        /// <summary>目からの射線が狙う点より手前で遮られている間、照準 (<see cref="CrosshairName"/>) に付けるクラス。色は Hud.uss で決める。</summary>
        public const string CrosshairBlockedClass = "hud__crosshair--blocked";

        /// <summary>Hud.uxml の、目からの射線が遮られた点に出す印。遮られている間だけ出す。</summary>
        public const string AimBlockMarkerName = "hud-aim-block";

        /// <summary>印の画像を渡されたとき、印 (<see cref="AimBlockMarkerName"/>) に付けるクラス。</summary>
        public const string AimBlockImageClass = "hud__aim-block--image";

        /// <summary>印の画像が無いときにコードで作る × の 1 本ずつに付けるクラス。</summary>
        public const string AimBlockBarClass = "hud__aim-block-bar";

        /// <summary>コードで作る × の 2 本目に付けるクラス。</summary>
        public const string AimBlockBarCrossClass = "hud__aim-block-bar--cross";

        /// <summary>広がり 0 で円錐の縁がこれより内のときの、中央から線の内側の端までの隙間 (基準解像度の px)。中央の点と線が重ならない幅。</summary>
        public const float MinCrosshairGap = 6f;

        /// <summary>円錐の縁が <see cref="MinCrosshairGap"/> より内で広がり 1 のときの、中央から線の内側の端までの隙間 (基準解像度の px)。</summary>
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

        /// <summary>Hud.uxml の溜めの Label。溜めの無い武器では隠す。</summary>
        public const string ChargeLabelName = "hud-charge";

        /// <summary>Hud.uxml の熱の Label。熱の無い武器では隠す。</summary>
        public const string HeatLabelName = "hud-heat";

        /// <summary>Hud.uxml の弾薬の種類の Label。種類が無ければ隠す。</summary>
        public const string AmmoTypeLabelName = "hud-ammo-type";

        /// <summary>Hud.uxml の倍率の Label。倍率が無ければ隠す。</summary>
        public const string MagnificationLabelName = "hud-magnification";

        /// <summary>Hud.uxml のロックオンの Label。ロックオンしていなければ隠す。</summary>
        public const string LockOnLabelName = "hud-lock-on";

        /// <summary>溜めの書式のキー。{0} は 0〜100 の %。</summary>
        public const string ChargeKey = "hud.charge";

        /// <summary>熱の書式のキー。{0} は 0〜100 の %。</summary>
        public const string HeatKey = "hud.heat";

        /// <summary>倍率の書式のキー。{0} は倍率。</summary>
        public const string MagnificationKey = "hud.magnification";

        /// <summary>ロックオンの途中の書式のキー。{0} は 0〜100 の %。</summary>
        public const string LockOnKey = "hud.lockOn";

        /// <summary>ロックオンし終えたときの文のキー。</summary>
        public const string LockedOnKey = "hud.lockedOn";

        /// <summary>ロックオンし終えたときにロックオンの Label に付ける USS クラス。</summary>
        public const string LockedOnClass = "hud__lock-on--locked";

        /// <summary>リロード中に弾数の Label に付ける USS クラス。</summary>
        public const string ReloadingClass = "hud__value--reloading";

        /// <summary>体力が少ないときに体力の Label に付ける USS クラス。</summary>
        public const string LowHealthClass = "hud__value--low";

        private readonly HudModel _model;
        private Color _alphaColor = Color.white;
        private Color _bravoColor = Color.white;
        private bool _disposed;
        private HudVisibility _visibility;

        public VisualElement HealthPanel { get; }
        public Label HealthLabel { get; }
        public VisualElement AmmoPanel { get; }
        public Label AmmoLabel { get; }
        public Label WeaponNameLabel { get; }
        public VisualElement TimePanel { get; }
        public Label TimeLabel { get; }
        public VisualElement Crosshair { get; }
        public VisualElement CrosshairDot { get; }
        public VisualElement CrosshairTop { get; }
        public VisualElement CrosshairBottom { get; }
        public VisualElement CrosshairLeft { get; }
        public VisualElement CrosshairRight { get; }
        public VisualElement AimBlockMarker { get; }
        public Label NoticeLabel { get; }
        public VisualElement ScorePanel { get; }
        public Label FirstScoreLabel { get; }
        public Label SecondScoreLabel { get; }
        public Label ResultLabel { get; }
        public VisualElement ReloadBar { get; }
        public VisualElement ReloadFill { get; }
        public VisualElement ReloadWindow { get; }
        public Label ChargeLabel { get; }
        public Label HeatLabel { get; }
        public Label AmmoTypeLabel { get; }
        public Label MagnificationLabel { get; }
        public Label LockOnLabel { get; }

        /// <summary>すべての項目を出す (<see cref="HudVisibility.Full"/>) View を作る。</summary>
        public HudView(VisualElement root, HudModel model)
            : this(root, model, HudVisibility.Full)
        {
        }

        /// <summary>
        /// root の下から Label と照準を名前で探し、今のモデルの文字列を入れてから変更を購読する。visibility で消す項目は隠す。
        /// 見つからなければ <see cref="InvalidOperationException"/> を投げる (UXML と名前がずれている)。
        /// </summary>
        public HudView(VisualElement root, HudModel model, HudVisibility visibility)
        {
            if (root == null)
            {
                throw new ArgumentNullException(nameof(root));
            }

            _model = model ?? throw new ArgumentNullException(nameof(model));
            _visibility = visibility ?? throw new ArgumentNullException(nameof(visibility));
            HealthPanel = Require<VisualElement>(root, HealthPanelName);
            HealthLabel = Require<Label>(HealthPanel, HealthLabelName);
            AmmoPanel = Require<VisualElement>(root, AmmoPanelName);
            AmmoLabel = Require<Label>(AmmoPanel, AmmoLabelName);
            WeaponNameLabel = Require<Label>(root, WeaponNameLabelName);
            TimePanel = Require<VisualElement>(root, TimePanelName);
            TimeLabel = Require<Label>(TimePanel, TimeLabelName);
            Crosshair = Require<VisualElement>(root, CrosshairName);
            CrosshairDot = Require<VisualElement>(Crosshair, CrosshairDotName);
            CrosshairTop = Require<VisualElement>(Crosshair, CrosshairTopName);
            CrosshairBottom = Require<VisualElement>(Crosshair, CrosshairBottomName);
            CrosshairLeft = Require<VisualElement>(Crosshair, CrosshairLeftName);
            CrosshairRight = Require<VisualElement>(Crosshair, CrosshairRightName);
            AimBlockMarker = Require<VisualElement>(root, AimBlockMarkerName);
            SetAimBlockMarker(null);
            NoticeLabel = Require<Label>(root, NoticeLabelName);
            ScorePanel = Require<VisualElement>(root, ScorePanelName);
            FirstScoreLabel = Require<Label>(root, FirstScoreLabelName);
            SecondScoreLabel = Require<Label>(root, SecondScoreLabelName);
            ResultLabel = Require<Label>(root, ResultLabelName);
            ReloadBar = Require<VisualElement>(root, ReloadBarName);
            ReloadFill = Require<VisualElement>(ReloadBar, ReloadFillName);
            ReloadWindow = Require<VisualElement>(ReloadBar, ReloadWindowName);
            ChargeLabel = Require<Label>(root, ChargeLabelName);
            HeatLabel = Require<Label>(root, HeatLabelName);
            AmmoTypeLabel = Require<Label>(root, AmmoTypeLabelName);
            MagnificationLabel = Require<Label>(root, MagnificationLabelName);
            LockOnLabel = Require<Label>(root, LockOnLabelName);

            ShowHealth();
            ShowAmmo();
            ShowWeaponName();
            ShowTime();
            ShowNotice();
            ShowScores();
            ShowResult();
            ShowReloadProgress();
            ShowCrosshair();
            ShowAimBlock();
            ShowWeaponGauges();
            _model.Changed += OnChanged;
        }

        /// <summary>今出す項目。</summary>
        public HudVisibility Visibility => _visibility;

        /// <summary>出す項目を visibility に替え、項目ごとに出し直す。</summary>
        public void ApplyVisibility(HudVisibility visibility)
        {
            _visibility = visibility ?? throw new ArgumentNullException(nameof(visibility));
            ShowHealth();
            ShowAmmo();
            ShowWeaponName();
            ShowTime();
            ShowScores();
            ShowCrosshair();
            ShowAimBlock();
            ShowWeaponGauges();
        }

        // モデルは文でなく型の付いた値を持つので、言語が変わったら言葉の入る欄をここで今の言語の文に作り直す。
        public void RefreshText()
        {
            ShowWeaponName();
            ShowNotice();
            ShowScores();
            ShowResult();
            ShowWeaponGauges();
        }

        /// <summary>チームの点数を塗る色を決め、今の点数を塗り直す。決めるまでは白で塗る。</summary>
        public void SetTeamColors(Color alpha, Color bravo)
        {
            _alphaColor = alpha;
            _bravoColor = bravo;
            ShowScores();
        }

        /// <summary>
        /// 目からの射線が遮られた点に出す印の見た目を決める。marker を渡せばその画像を出し、null なら × の形を要素で作る。
        /// </summary>
        public void SetAimBlockMarker(Texture2D marker)
        {
            AimBlockMarker.Clear();
            AimBlockMarker.EnableInClassList(AimBlockImageClass, marker != null);
            AimBlockMarker.style.backgroundImage = marker != null ? new StyleBackground(marker) : new StyleBackground(StyleKeyword.Null);
            if (marker != null)
            {
                return;
            }

            for (var i = 0; i < 2; i++)
            {
                var bar = new VisualElement { pickingMode = PickingMode.Ignore };
                bar.AddToClassList(AimBlockBarClass);
                bar.EnableInClassList(AimBlockBarCrossClass, i == 1);
                AimBlockMarker.Add(bar);
            }
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
                    ShowTime();
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
                    ShowAimBlock();
                    break;
                case HudField.AimBlock:
                    ShowAimBlock();
                    break;
                case HudField.Charge:
                    ShowCharge();
                    break;
                case HudField.Heat:
                    ShowHeat();
                    break;
                case HudField.AmmoType:
                    ShowAmmoType();
                    break;
                case HudField.Magnification:
                    ShowMagnification();
                    break;
                case HudField.LockOn:
                    ShowLockOn();
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(field), field, "field is not a valid HudField");
            }
        }

        private void ShowHealth()
        {
            HealthLabel.text = _model.HealthText;
            HealthLabel.EnableInClassList(LowHealthClass, _model.IsHealthLow);
            HealthPanel.style.display = Display(_visibility.Health);
        }

        private void ShowAmmo()
        {
            AmmoLabel.text = _model.AmmoText;
            AmmoLabel.EnableInClassList(ReloadingClass, _model.IsReloading);
            AmmoPanel.style.display = Display(_visibility.Ammo);
        }

        private void ShowWeaponName()
        {
            ShowOrHide(WeaponNameLabel, GameText.Resolve(HudText.WeaponName(_model.WeaponSpec)), _visibility.WeaponName);
        }

        private void ShowTime()
        {
            TimeLabel.text = _model.TimeText;
            TimePanel.style.display = Display(_visibility.Time);
        }

        /// <summary>案内の文を入れ、空なら Label ごと隠す (背景の帯だけが残らないように)。</summary>
        private void ShowNotice()
        {
            ShowOrHide(NoticeLabel, GameText.Resolve(HudText.Notice(_model.Notice)));
        }

        private void ShowResult()
        {
            ShowOrHide(ResultLabel, GameText.Resolve(HudText.Result(_model.HasResult, _model.Winner)));
        }

        /// <summary>点数を入れてチームの色で塗る。点数が無いか HUD の量で消すなら欄ごと隠す。</summary>
        private void ShowScores()
        {
            var first = _model.FirstScoreTeam;
            var second = _model.SecondScoreTeam;
            FirstScoreLabel.text = GameText.Resolve(HudText.Score(first, _model.FirstScore));
            SecondScoreLabel.text = GameText.Resolve(HudText.Score(second, _model.SecondScore));
            if (first.HasValue && second.HasValue)
            {
                FirstScoreLabel.style.color = TeamColor(first.Value);
                SecondScoreLabel.style.color = TeamColor(second.Value);
                ScorePanel.style.display = Display(_visibility.Score);
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

        private void ShowWeaponGauges()
        {
            ShowCharge();
            ShowHeat();
            ShowAmmoType();
            ShowMagnification();
            ShowLockOn();
        }

        private void ShowCharge()
        {
            ShowOrHide(ChargeLabel, Percent(ChargeKey, _model.ChargeFraction), _visibility.Ammo);
        }

        private void ShowHeat()
        {
            ShowOrHide(HeatLabel, Percent(HeatKey, _model.HeatFraction), _visibility.Ammo);
        }

        private void ShowAmmoType()
        {
            ShowOrHide(AmmoTypeLabel, GameText.Resolve(HudText.AmmoType(_model.AmmoType)), _visibility.Ammo);
        }

        private void ShowMagnification()
        {
            var magnification = _model.Magnification;
            var text = magnification.HasValue
                ? GameText.Format(MagnificationKey, magnification.Value.ToString("0.#", CultureInfo.InvariantCulture))
                : string.Empty;
            ShowOrHide(MagnificationLabel, text, _visibility.Ammo);
        }

        // ロックオンは照準に重ねる合図なので、弾数ではなく照準を出すかに合わせる。
        private void ShowLockOn()
        {
            var text = _model.IsLockedOn ? GameText.Get(LockedOnKey) : Percent(LockOnKey, _model.LockOnProgress);
            LockOnLabel.EnableInClassList(LockedOnClass, _model.IsLockedOn);
            ShowOrHide(LockOnLabel, text, _visibility.Crosshair);
        }

        private static string Percent(string key, float? fraction)
        {
            return fraction.HasValue ? GameText.Format(key, Mathf.RoundToInt(fraction.Value * 100f)) : string.Empty;
        }

        /// <summary>広がり spread (0〜1) のときの、中央から線の内側の端までの隙間 (基準解像度の px)。範囲外は端に揃える。</summary>
        public static float CrosshairGap(float spread)
        {
            return CrosshairGap(spread, 0f);
        }

        /// <summary>
        /// 散らばりの円錐の縁が coneGap (基準解像度の px) にあり、広がり spread (0〜1) のときの、中央から線の内側の端までの隙間。
        /// 線の内側を円錐の縁 (<see cref="MinCrosshairGap"/> より内なら MinCrosshairGap) に置き、そこから広がりの分 (最大で Max - Min) だけ外へ離す。
        /// </summary>
        public static float CrosshairGap(float spread, float coneGap)
        {
            var inner = Mathf.Max(MinCrosshairGap, float.IsNaN(coneGap) ? 0f : coneGap);
            return inner + ((MaxCrosshairGap - MinCrosshairGap) * Mathf.Clamp01(spread));
        }

        /// <summary>
        /// 照準を出すか隠し、4 本の線を中央から隙間の分だけ外へずらす。Hud.uss は線を中央に接する位置に置いているので、
        /// 隙間は translate だけで決まる。大きさ 0 の箱を中央に置いているので、箱の scale で線と隙間がまとめて中央へ縮む。
        /// </summary>
        private void ShowCrosshair()
        {
            Crosshair.style.display = Display(_model.IsCrosshairVisible && _visibility.Crosshair);
            var scale = _visibility.CrosshairScale;
            Crosshair.style.scale = new Scale(new Vector2(scale, scale));

            // 円錐の縁は画面上の位置なので、箱の scale で縮む分を先に割り戻しておく。
            var coneGap = scale > 0f ? _model.CrosshairConeGap / scale : _model.CrosshairConeGap;
            var gap = CrosshairGap(_model.CrosshairSpread, coneGap);
            CrosshairTop.style.translate = new Translate(new Length(0f), new Length(-gap));
            CrosshairBottom.style.translate = new Translate(new Length(0f), new Length(gap));
            CrosshairLeft.style.translate = new Translate(new Length(-gap), new Length(0f));
            CrosshairRight.style.translate = new Translate(new Length(gap), new Length(0f));
        }

        /// <summary>
        /// 照準を出している間に射線が遮られていれば、照準に <see cref="CrosshairBlockedClass"/> を付け、遮られた点に印を出す。
        /// 遮られた点が画面に写らなければ照準だけ変える。
        /// </summary>
        private void ShowAimBlock()
        {
            var blocked = _model.IsAimBlocked && _model.IsCrosshairVisible && _visibility.Crosshair;
            Crosshair.EnableInClassList(CrosshairBlockedClass, blocked);
            var point = _model.AimBlockPoint;
            AimBlockMarker.style.display = Display(blocked && point.HasValue);
            if (blocked && point.HasValue)
            {
                AimBlockMarker.style.left = Length.Percent(point.Value.X * 100f);
                AimBlockMarker.style.top = Length.Percent((1f - point.Value.Y) * 100f);
            }
        }

        private static void ShowOrHide(Label label, string text, bool allowed = true)
        {
            label.text = text;
            label.style.display = Display(allowed && !string.IsNullOrEmpty(text));
        }

        private static DisplayStyle Display(bool shown)
        {
            return shown ? DisplayStyle.Flex : DisplayStyle.None;
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
