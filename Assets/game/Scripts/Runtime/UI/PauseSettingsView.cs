using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Fps.Runtime
{
    /// <summary>
    /// ポーズ画面の「設定」で <see cref="SettingsView"/> を開く。<see cref="Hud.AddViews"/> が <see cref="PauseMenu"/> と同じ GameObject に付け、
    /// 同じ Hud.uxml の <see cref="PauseMenu.UxmlName"/> の中の、ボタンの枠 (<see cref="PanelName"/>) と設定画面の枠 (<see cref="SlotName"/>) を使う。
    /// 開いている間はボタンの枠を隠し、「戻る」・キャンセルで閉じてボタンの枠に戻る。ポーズ画面が閉じたとき (Pause の押し直しなど) と
    /// 自分が消えるときも閉じる。閉じると設定画面が保存する。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PauseMenu))]
    public sealed class PauseSettingsView : HudElementView
    {
        /// <summary>Hud.uxml のポーズ画面のボタンの枠。</summary>
        public const string PanelName = "pause-panel";

        /// <summary>Hud.uxml の設定画面の枠。</summary>
        public const string SlotName = "pause-settings";

        private SettingsView _view;
        private PauseMenu _pause;
        private VisualElement _panel;
        private VisualElement _slot;

        public override string ElementName => PauseMenu.UxmlName;

        /// <summary>開く設定画面。初めて触れたときに既定の保存先で作る。</summary>
        public SettingsView SettingsView
        {
            get
            {
                if (_view == null)
                {
                    UseSettingsView(new SettingsView());
                }

                return _view;
            }
        }

        /// <summary>設定画面を開いているか。</summary>
        public bool IsSettingsOpen => _view != null && _view.IsOpen;

        /// <summary>設定画面の枠。<see cref="HudElementView.Bind"/> の前は null。</summary>
        public VisualElement Slot => _slot;

        /// <summary>ポーズ画面のボタンの枠。<see cref="HudElementView.Bind"/> の前は null。</summary>
        public VisualElement Panel => _panel;

        /// <summary>開く設定画面を差し替える (テストで保存先を変えるときなど)。閉じている間だけ。</summary>
        /// <exception cref="InvalidOperationException">設定画面を開いている。</exception>
        public void UseSettingsView(SettingsView view)
        {
            if (view == null)
            {
                throw new ArgumentNullException(nameof(view));
            }

            if (IsSettingsOpen)
            {
                throw new InvalidOperationException("cannot replace the settings view while it is open");
            }

            if (_view != null)
            {
                _view.BackRequested -= CloseSettings;
            }

            _view = view;
            _view.BackRequested += CloseSettings;
        }

        /// <summary>ボタンの枠を隠して設定画面を開く。枠を受け取っていないか、既に開いていれば何もしない。</summary>
        public void OpenSettings()
        {
            if (_slot == null || IsSettingsOpen)
            {
                return;
            }

            SetDisplayed(_panel, false);
            SetDisplayed(_slot, true);
            SettingsView.Open(_slot);
        }

        /// <summary>設定画面を閉じて (保存して) ボタンの枠に戻し、「設定」にフォーカスを戻す。開いていなければ何もしない。</summary>
        public void CloseSettings()
        {
            if (!IsSettingsOpen)
            {
                return;
            }

            _view.Close();
            if (_slot != null)
            {
                SetDisplayed(_slot, false);
            }

            if (_panel != null)
            {
                SetDisplayed(_panel, true);
                _panel.Q<Button>(PauseMenu.SettingsButtonName)?.Focus();
            }
        }

        protected override void OnBound(VisualElement element)
        {
            _panel = FindRequired(element, PanelName);
            _slot = FindRequired(element, SlotName);
            SetDisplayed(_slot, false);
            SetDisplayed(_panel, true);

            _pause = GetComponent<PauseMenu>();
            if (_pause != null)
            {
                _pause.Closed -= CloseSettings;
                _pause.Closed += CloseSettings;
                _pause.OpenSettings = OpenSettings;
            }
        }

        protected override void OnUnbinding(VisualElement element)
        {
            CloseSettings();
            if (_pause != null)
            {
                _pause.Closed -= CloseSettings;
                if (_pause.OpenSettings == (Action)OpenSettings)
                {
                    _pause.OpenSettings = null;
                }
            }

            _pause = null;
            _panel = null;
            _slot = null;
        }

        private void OnDestroy()
        {
            // 開いたまま HUD ごと消える (シーンを離れる) ときも、変えた値を保存する。
            CloseSettings();
        }

        private static VisualElement FindRequired(VisualElement element, string elementName)
        {
            var found = element.Q<VisualElement>(elementName);
            if (found == null)
            {
                throw new InvalidOperationException(
                    $"the pause menu layout has no element named '{elementName}'; check Assets/game/UI/Resources/Hud/Hud.uxml");
            }

            return found;
        }

        private static void SetDisplayed(VisualElement element, bool displayed)
        {
            element.style.display = displayed ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }
}
