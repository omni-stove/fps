using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Shooter.Runtime
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PauseMenu))]
    public sealed class PauseSettingsView : HudElementView
    {
        public const string PanelName = "pause-panel";

        public const string SlotName = "pause-settings-slot";

        private SettingsView _view;
        private PauseMenu _pause;
        private VisualElement _panel;
        private VisualElement _slot;

        public override string ElementName => PauseMenu.UxmlName;

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

        public bool IsSettingsOpen => _view != null && _view.IsOpen;

        public VisualElement Slot => _slot;

        public VisualElement Panel => _panel;

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
                    $"the pause menu layout has no element named '{elementName}'; check Assets/game/Samples~/Demo/UI/Resources/Hud/Hud.uxml");
            }

            return found;
        }

        private static void SetDisplayed(VisualElement element, bool displayed)
        {
            element.style.display = displayed ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }
}
