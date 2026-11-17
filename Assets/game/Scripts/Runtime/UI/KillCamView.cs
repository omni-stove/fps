using System;
using Fps.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace Fps.Runtime
{
    /// <summary>
    /// キルカム (<see cref="DeathCamera.KillCam"/>) の再生中に、画面の枠と見出し (<see cref="KillCamText.Caption(KillCamStatus)"/>) と再生の進みを出すビュー。
    /// Hud.uxml の <see cref="UxmlName"/> を使い、見た目は Hud.uss の kill-cam のクラスで決める。
    /// 要素を持っている間 <see cref="GameSignals.KillCam"/> を購読し、再生が出ている間は枠を出して進みを合わせ、消えたら閉じる。
    /// 要素を受けたときは今の再生に合わせる (要素を持たない間は購読しないので、その間の変化を後から拾う)。言語が変わったら文にし直す。
    /// 再生中であることを伝える表示なので、HUD の量 (<see cref="HudVisibility"/>) では隠さない。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class KillCamView : HudElementView
    {
        public const string LogTag = KillCamLog.Tag;

        /// <summary>Hud.uxml のキルカムの枠。</summary>
        public const string UxmlName = "kill-cam";

        /// <summary>枠の中の見出しの Label。</summary>
        public const string CaptionName = "kill-cam-caption";

        /// <summary>枠の中の再生の進みの塗り。</summary>
        public const string ProgressName = "kill-cam-progress";

        private LocalizedText _caption;
        private string _killerName;
        private bool _subscribed;
        private string _text = string.Empty;
        private Label _captionLabel;
        private VisualElement _progressFill;

        public override string ElementName => UxmlName;

        /// <summary>出しているか。</summary>
        public bool IsShown { get; private set; }

        /// <summary>出している見出し。出していなければ空文字。</summary>
        public string Caption => IsShown ? _text : string.Empty;

        /// <summary>再生の進み (0 から 1)。出していなければ 0。</summary>
        public float Progress { get; private set; }

        /// <summary>枠と caption の見出しを出し、進みを 0 にする。</summary>
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

        /// <summary>進みを progress (0 から 1 に丸める) にする。出していなければ何もしない。</summary>
        public void SetProgress(float progress)
        {
            if (!IsShown)
            {
                return;
            }

            Progress = float.IsNaN(progress) ? 0f : Mathf.Clamp01(progress);
            RefreshProgress();
        }

        /// <summary>枠と見出しを消す。出していなければ何もしない。</summary>
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
                    $"the kill cam layout needs a Label '{CaptionName}' and an element '{ProgressName}'; check Assets/game/UI/Resources/Hud/Hud.uxml");
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
