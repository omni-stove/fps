using System;
using System.Collections.Generic;
using Fps.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace Fps.Runtime
{
    /// <summary>
    /// 出現前に持っていく武器を選ぶビュー。Hud.uxml の <see cref="UxmlName"/> を使い、中身は <see cref="LoadoutSelectPanel"/> が作る。
    /// </summary>
    /// <remarks>
    /// 開く・閉じるのと、決めた並びをどこへ送るかは持ち主が決める。オンラインでは Fps.Net の NetLoadoutChooser が、
    /// 倒されてリスポーンを待っている間に <see cref="Open"/> し、<see cref="Confirmed"/> で受けた並びをサーバーへ送って
    /// 答えを <see cref="ShowAnswer"/> で出す。
    /// 開いている間 (Play 中) はカーソルのロックを外してマウスで押せるようにし、閉じたら開く前の状態に戻す。
    /// HUD の木が作り直されたときは、開いていれば同じ選択の中身で作り直す。
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class LoadoutSelectView : HudElementView
    {
        /// <summary>Hud.uxml のロードアウト選択の要素。</summary>
        public const string UxmlName = "loadout-select";

        private LoadoutSelection _selection;
        private LoadoutSelectPanel _panel;
        private string _status;
        private bool _statusIsError;
        private bool _busy;
        private bool _restoreCursor;
        private CursorLockMode _previousLockState;
        private bool _previousCursorVisible;

        public override string ElementName => UxmlName;

        /// <summary>開いているか。</summary>
        public bool IsOpen => _selection != null;

        /// <summary>開いている中身。閉じていれば null。</summary>
        public LoadoutSelection Selection => _selection;

        /// <summary>今の木に作った中身。閉じているか、木を受けていなければ null。</summary>
        public LoadoutSelectPanel Panel => _panel;

        /// <summary>決定でルールに合う並びを選んだとき、スロット順の武器の Id を渡す。</summary>
        public event Action<IReadOnlyList<string>> Confirmed;

        /// <summary>Cancel で閉じたとき。</summary>
        public event Action Closed;

        /// <summary>selection を出す。既に開いていれば中身を差し替える (案内と待ちは消す)。</summary>
        /// <exception cref="ArgumentNullException">selection が null。</exception>
        public void Open(LoadoutSelection selection)
        {
            _selection = selection ?? throw new ArgumentNullException(nameof(selection));
            _status = null;
            _statusIsError = false;
            _busy = false;
            ReleaseCursor();
            Rebuild();
        }

        /// <summary>閉じる。開いていなければ何もしない。</summary>
        public void Close()
        {
            if (_selection == null)
            {
                return;
            }

            _selection = null;
            _status = null;
            _busy = false;
            DisposePanel();
            if (Element != null)
            {
                Element.style.display = DisplayStyle.None;
            }

            RestoreCursor();
        }

        /// <summary>決めた並びを送って答えを待っている間の案内を出し、操作を止める。</summary>
        public void ShowSending()
        {
            SetStatus(LoadoutFormat.Sending, false, true);
        }

        /// <summary>送った並びの答えを出し、操作を戻す。断られたなら理由をエラーで出す。</summary>
        public void ShowAnswer(bool accepted, string reason)
        {
            SetStatus(accepted ? LoadoutFormat.Accepted : LoadoutFormat.Rejected(reason), !accepted, false);
        }

        protected override void OnBound(VisualElement element)
        {
            Rebuild();
        }

        protected override void OnUnbinding(VisualElement element)
        {
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
            _panel = new LoadoutSelectPanel(element, _selection);
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

        private void HandleConfirmed(IReadOnlyList<string> weaponIds)
        {
            var handlers = Confirmed;
            if (handlers == null)
            {
                Debug.LogWarning(LoadoutPreferenceStore.LogTag + " a loadout was chosen but nothing sends it", this);
                return;
            }

            handlers(weaponIds);
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

            _previousLockState = UnityEngine.Cursor.lockState;
            _previousCursorVisible = UnityEngine.Cursor.visible;
            _restoreCursor = true;
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
            UnityEngine.Cursor.lockState = _previousLockState;
            UnityEngine.Cursor.visible = _previousCursorVisible;
        }
    }
}
