using System;
using Shooter.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace Shooter.Runtime
{
    // 再生中であることを伝える表示なので、HUD の量 (HudVisibility) では隠さない。
    // 要素を持たない間は購読しないので、要素を受けたときに今の再生に合わせてその間の変化を拾う。
    [DisallowMultipleComponent]
    public sealed class KillCamView : HudElementView
    {
        public const string LogTag = KillCamLog.Tag;

        public const string UxmlName = "kill-cam";

        public const string CaptionName = "kill-cam-caption";

        public const string ProgressName = "kill-cam-progress";

        private LocalizedText _caption;
        private string _killerName;
        private bool _subscribed;
        private string _text = string.Empty;
        private Label _captionLabel;
        private VisualElement _progressFill;

        public override string ElementName => UxmlName;

        public bool IsShown { get; private set; }

        public string Caption => IsShown ? _text : string.Empty;

        public float Progress { get; private set; }

        public void Show(LocalizedText caption)
        {
            _killerName = null;
            _caption = caption;
            _text = Localizer.Resolve(caption);
            IsShown = true;
            Progress = 0f;
            Debug.Log(LogTag + " showing the frame \"" + _text + "\"", this);
            Refresh();
        }

        public void SetProgress(float progress)
        {
            if (!IsShown)
            {
                return;
            }

            Progress = float.IsNaN(progress) ? 0f : Mathf.Clamp01(progress);
            RefreshProgress();
        }

        public void Hide()
        {
            if (!IsShown)
            {
                return;
            }

            IsShown = false;
            _killerName = null;
            _caption = default;
            _text = string.Empty;
            Progress = 0f;
            Debug.Log(LogTag + " hiding the frame", this);
            Refresh();
        }

        protected override void OnBound(VisualElement element)
        {
            _captionLabel = element.Q<Label>(CaptionName);
            _progressFill = element.Q<VisualElement>(ProgressName);
            if (_captionLabel == null || _progressFill == null)
            {
                throw new InvalidOperationException(
                    $"the kill cam layout needs a Label '{CaptionName}' and an element '{ProgressName}'; check Assets/game/Samples~/Demo/UI/Resources/Hud/Hud.uxml");
            }

            if (IsShown)
            {
                _text = Localizer.Resolve(_caption);
            }

            Refresh();
            GameSignals.KillCam.Changed += HandleKillCamChanged;
            _subscribed = true;
            Apply(GameSignals.KillCam.Value);
        }

        public void Apply(KillCamStatus? status)
        {
            if (!status.HasValue)
            {
                Hide();
                return;
            }

            var value = status.Value;
            if (!IsShown || _killerName != value.KillerName)
            {
                Show(KillCamText.Caption(value));
                _killerName = value.KillerName;
            }

            SetProgress(value.Progress);
        }

        protected override void OnLanguageChanged()
        {
            if (IsShown)
            {
                _text = Localizer.Resolve(_caption);
            }

            Refresh();
        }

        protected override void OnUnbinding(VisualElement element)
        {
            Unsubscribe();
            element.style.display = DisplayStyle.None;
            _captionLabel = null;
            _progressFill = null;
        }

        private void OnDisable()
        {
            Hide();
        }

        private void OnDestroy()
        {
            Unsubscribe();
        }

        private void Unsubscribe()
        {
            if (_subscribed)
            {
                GameSignals.KillCam.Changed -= HandleKillCamChanged;
                _subscribed = false;
            }
        }

        private void HandleKillCamChanged(KillCamStatus? status)
        {
            // 破棄の後に届いた通知では消えた要素に書かない。
            if (this == null)
            {
                return;
            }

            Apply(status);
        }

        private void Refresh()
        {
            var element = Element;
            if (element == null)
            {
                return;
            }

            _captionLabel.text = _text;
            RefreshProgress();
            element.style.display = IsShown ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void RefreshProgress()
        {
            if (_progressFill != null)
            {
                _progressFill.style.width = Length.Percent(Progress * 100f);
            }
        }
    }
}
