using System;
using System.Collections.Generic;
using Shooter.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace Shooter.Runtime
{
    [DisallowMultipleComponent]
    public sealed class LoadoutSelectView : HudElementView
    {
        public const string UxmlName = "loadout-select";

        [SerializeField, Tooltip("Where the names of the offered abilities are looked up. Empty uses the GameContent one; abilities missing from it show their id")]
        private AbilityCatalogAsset _abilityCatalog;

        private LoadoutSelection _selection;
        private AbilitySelection _abilities;
        private LoadoutSelectPanel _panel;
        private string _status;
        private bool _statusIsError;
        private bool _busy;
        private bool _restoreCursor;
        private static int _openCursorCount;

        // ゲーム進行は GameUi.LoadoutSelect から開くので、どの HUD の画面に出すかを、木を受けている画面の新しい順で決める。
        private static readonly List<LoadoutSelectView> _bound = new List<LoadoutSelectView>();

        public static bool IsAnyOpen => _openCursorCount > 0;

        public override string ElementName => UxmlName;

        public bool IsOpen => _selection != null;

        public static LoadoutSelectView Active
        {
            get
            {
                // Edit Mode では OnDestroy が呼ばれずに消えることがあるので、消えたものはここで外す
                _bound.RemoveAll(view => view == null);
                return _bound.Count > 0 ? _bound[_bound.Count - 1] : null;
            }
        }

        public LoadoutSelection Selection => _selection;

        public AbilitySelection Abilities => _abilities;

        // 候補を作る NetV2LoadoutChooser も GameContent のカタログを引くので、既定では同じものから名前を引く。
        public AbilityCatalogAsset AbilityCatalog
        {
            get => _abilityCatalog;
            set => _abilityCatalog = value;
        }

        public AbilityCatalogAsset EffectiveAbilityCatalog => GameContent.Pick(_abilityCatalog, c => c.AbilityCatalog);

        public LoadoutSelectPanel Panel => _panel;

        public event Action<LoadoutChoice> Confirmed;

        public event Action Closed;

        public void Open(LoadoutSelection selection, AbilitySelection abilities = null)
        {
            _selection = selection ?? throw new ArgumentNullException(nameof(selection));
            _abilities = abilities;
            _status = null;
            _statusIsError = false;
            _busy = false;
            ReleaseCursor();
            Rebuild();
        }

        public void Close()
        {
            if (_selection == null)
            {
                return;
            }

            _selection = null;
            _abilities = null;
            _status = null;
            _busy = false;
            DisposePanel();
            if (Element != null)
            {
                Element.style.display = DisplayStyle.None;
            }

            RestoreCursor();
        }

        public void ShowSending()
        {
            SetStatus(LoadoutFormat.Sending, false, true);
        }

        public void ShowAnswer(bool accepted, string reason)
        {
            SetStatus(accepted ? LoadoutFormat.Accepted : LoadoutFormat.Rejected(reason), !accepted, false);
        }

        protected override void OnBound(VisualElement element)
        {
            _bound.Remove(this);
            _bound.Add(this);
            Rebuild();
        }

        // 選んでいる並びは LoadoutSelection が持つので、作り直しても選びかけのものは失わない。
        protected override void OnLanguageChanged()
        {
            Rebuild();
        }

        protected override void OnUnbinding(VisualElement element)
        {
            _bound.Remove(this);
            DisposePanel();
            element.Clear();
            element.style.display = DisplayStyle.None;
        }

        private void OnDisable()
        {
            Close();
        }

        private void SetStatus(string text, bool error, bool busy)
        {
            _status = text;
            _statusIsError = error;
            _busy = busy;
            if (_panel != null)
            {
                _panel.ShowStatus(text, error);
                _panel.SetBusy(busy);
            }
        }

        private void Rebuild()
        {
            DisposePanel();
            var element = Element;
            if (element == null)
            {
                return;
            }

            if (_selection == null)
            {
                element.Clear();
                element.style.display = DisplayStyle.None;
                return;
            }

            element.style.display = DisplayStyle.Flex;
            _panel = new LoadoutSelectPanel(element, _selection, null, _abilities, _abilities != null ? EffectiveAbilityCatalog : null);
            _panel.Confirmed += HandleConfirmed;
            _panel.Cancelled += HandleCancelled;
            _panel.ShowStatus(_status, _statusIsError);
            _panel.SetBusy(_busy);
        }

        private void DisposePanel()
        {
            if (_panel == null)
            {
                return;
            }

            _panel.Dispose();
            _panel = null;
        }

        private void HandleConfirmed(LoadoutChoice choice)
        {
            var handlers = Confirmed;
            if (handlers == null)
            {
                Debug.LogWarning(LoadoutPreferenceStore.LogTag + " a loadout was chosen but nothing sends it", this);
                return;
            }

            handlers(choice);
        }

        private void HandleCancelled()
        {
            Close();
            Closed?.Invoke();
        }

        private void ReleaseCursor()
        {
            // Edit Mode のテストでエディタのカーソルを書き換えない
            if (!Application.isPlaying || _restoreCursor)
            {
                return;
            }

            _restoreCursor = true;
            _openCursorCount++;
            UnityEngine.Cursor.lockState = CursorLockMode.None;
            UnityEngine.Cursor.visible = true;
        }

        private void RestoreCursor()
        {
            if (!_restoreCursor)
            {
                return;
            }

            _restoreCursor = false;
            _openCursorCount = Math.Max(0, _openCursorCount - 1);
            // 開いている間に PauseMenu が開閉したり視点操作が切り替わったりするので、控えた値は戻さず決め直す。
            // ポーズ中はメニューがカーソルを使うので触らず、閉じるときに PauseMenu が決め直す。
            if (!PauseMenu.IsAnyOpen)
            {
                FirstPersonLook.RefreshCursorLock();
            }
        }
    }
}
