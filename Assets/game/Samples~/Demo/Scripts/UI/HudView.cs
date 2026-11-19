using System;
using System.Globalization;
using Shooter.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace Shooter.Runtime
{
    public sealed class HudView : IDisposable
    {
        public const string HealthPanelName = "hud-health-panel";

        public const string HealthLabelName = "hud-health";

        public const string AmmoPanelName = "hud-ammo-panel";

        public const string AmmoLabelName = "hud-ammo";

        public const string TimePanelName = "hud-time-panel";

        public const string WeaponNameLabelName = "hud-weapon-name";

        public const string TimeLabelName = "hud-time";

        public const string CrosshairName = "hud-crosshair";

        public const string CrosshairDotName = "hud-crosshair-dot";

        public const string CrosshairTopName = "hud-crosshair-top";

        public const string CrosshairBottomName = "hud-crosshair-bottom";

        public const string CrosshairLeftName = "hud-crosshair-left";

        public const string CrosshairRightName = "hud-crosshair-right";

        public const string CrosshairBlockedClass = "hud__crosshair--blocked";

        public const string AimBlockMarkerName = "hud-aim-block";

        public const string AimBlockImageClass = "hud__aim-block--image";

        public const string AimBlockBarClass = "hud__aim-block-bar";

        public const string AimBlockBarCrossClass = "hud__aim-block-bar--cross";

        // 中央の点と線が重ならない幅。
        public const float MinCrosshairGap = 6f;

        public const float MaxCrosshairGap = 30f;

        public const string NoticeLabelName = "hud-notice";

        public const string ScorePanelName = "hud-score-panel";

        public const string FirstScoreLabelName = "hud-score-first";

        public const string SecondScoreLabelName = "hud-score-second";

        public const string ResultLabelName = "hud-result";

        public const string RestartLabelName = "hud-result-restart";

        public const string ReloadBarName = "hud-reload-bar";

        public const string ReloadFillName = "hud-reload-fill";

        public const string ReloadWindowName = "hud-reload-window";

        public const string ChargeLabelName = "hud-charge";

        public const string HeatLabelName = "hud-heat";

        public const string AmmoTypeLabelName = "hud-ammo-type";

        public const string MagnificationLabelName = "hud-magnification";

        public const string LockOnLabelName = "hud-lock-on";

        public const string ChargeKey = "hud.charge";

        public const string HeatKey = "hud.heat";

        public const string MagnificationKey = "hud.magnification";

        public const string LockOnKey = "hud.lockOn";

        public const string LockedOnKey = "hud.lockedOn";

        public const string LockedOnClass = "hud__lock-on--locked";

        public const string ReloadingClass = "hud__value--reloading";

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

        public Label RestartLabel { get; }
        public VisualElement ReloadBar { get; }
        public VisualElement ReloadFill { get; }
        public VisualElement ReloadWindow { get; }
        public Label ChargeLabel { get; }
        public Label HeatLabel { get; }
        public Label AmmoTypeLabel { get; }
        public Label MagnificationLabel { get; }
        public Label LockOnLabel { get; }

        public HudView(VisualElement root, HudModel model)
            : this(root, model, HudVisibility.Full)
        {
        }

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
            // 後から足した欄なので、ゲームが差し替えた古いレイアウトに無くても HUD を出せるようにする。
            RestartLabel = root.Q<Label>(RestartLabelName);
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

        public HudVisibility Visibility => _visibility;

        public bool IsAimHidden { get; private set; }

        // ストリークの地点を選ぶ見下ろした画面では、照準は画面の中央を指さず狙いにも使わないので、照準に重ねるものごと隠す。
        public void SetAimHidden(bool hidden)
        {
            if (IsAimHidden == hidden)
            {
                return;
            }

            IsAimHidden = hidden;
            ShowCrosshair();
            ShowAimBlock();
            ShowLockOn();
        }

        private bool ShowsAim => _visibility.Crosshair && !IsAimHidden;

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

        public void SetTeamColors(Color alpha, Color bravo)
        {
            _alphaColor = alpha;
            _bravoColor = bravo;
            ShowScores();
        }

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

        public Color TeamColor(Team team)
        {
            return team == Team.Bravo ? _bravoColor : _alphaColor;
        }

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
            HealthLabel.text = HudNumberFormat.Health(_model);
            HealthLabel.EnableInClassList(LowHealthClass, _model.IsHealthLow);
            HealthPanel.style.display = Display(_visibility.Health);
        }

        private void ShowAmmo()
        {
            AmmoLabel.text = HudNumberFormat.Ammo(_model);
            AmmoLabel.EnableInClassList(ReloadingClass, _model.IsReloading);
            AmmoPanel.style.display = Display(_visibility.Ammo);
        }

        private void ShowWeaponName()
        {
            ShowOrHide(WeaponNameLabel, GameText.Resolve(HudText.WeaponName(_model.WeaponSpec)), _visibility.WeaponName);
        }

        private void ShowTime()
        {
            TimeLabel.text = HudNumberFormat.Time(_model);
            TimePanel.style.display = Display(_visibility.Time && _model.HasTime);
        }

        // 案内が空なら Label ごと隠すのは、背景の帯だけが残らないようにするため。
        private void ShowNotice()
        {
            ShowOrHide(NoticeLabel, GameText.Resolve(HudText.Notice(_model.Notice)));
        }

        private void ShowResult()
        {
            ShowOrHide(ResultLabel, GameText.Resolve(HudText.Result(_model.HasResult, _model.Winner, _model.SoloResult)));
            if (RestartLabel != null)
            {
                ShowOrHide(RestartLabel, GameText.Resolve(HudText.Restart(_model.SoloResult, _model.RestartKey)));
            }
        }

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
            ShowOrHide(LockOnLabel, text, ShowsAim);
        }

        private static string Percent(string key, float? fraction)
        {
            return fraction.HasValue ? GameText.Format(key, Mathf.RoundToInt(fraction.Value * 100f)) : string.Empty;
        }

        public static float CrosshairGap(float spread)
        {
            return CrosshairGap(spread, 0f);
        }

        public static float CrosshairGap(float spread, float coneGap)
        {
            var inner = Mathf.Max(MinCrosshairGap, float.IsNaN(coneGap) ? 0f : coneGap);
            return inner + ((MaxCrosshairGap - MinCrosshairGap) * Mathf.Clamp01(spread));
        }

        // Hud.uss は線を中央に接する位置に置いているので、隙間は translate だけで決まる。
        // 大きさ 0 の箱を中央に置いているので、箱の scale で線と隙間がまとめて中央へ縮む。
        private void ShowCrosshair()
        {
            Crosshair.style.display = Display(_model.IsCrosshairVisible && ShowsAim);
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

        private void ShowAimBlock()
        {
            var blocked = _model.IsAimBlocked && _model.IsCrosshairVisible && ShowsAim;
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
                    $"the HUD layout has no {typeof(T).Name} named '{elementName}'; check Assets/game/Samples~/Demo/UI/Resources/Hud/Hud.uxml");
            }

            return element;
        }
    }
}
