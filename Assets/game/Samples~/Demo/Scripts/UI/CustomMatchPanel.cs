using System;
using Shooter.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace Shooter.Runtime
{
    public sealed class CustomMatchPanel : IDisposable
    {
        public const string LogTag = "[CustomMatch]";

        public const string LayoutResourcePath = "Menu/CustomMatch";

        public const string PanelName = "custom-match";
        public const string CodeFieldName = "custom-match-code-field";
        public const string JoinButtonName = "custom-match-join";
        public const string NoticeName = "custom-match-notice";
        public const string BackButtonName = "custom-match-back";

        public const string ErrorNoticeClass = "custom-match__notice--error";

        public const string CodePlaceholderKey = "customMatch.codePlaceholder";
        public const string UnavailableKey = "customMatch.unavailable";
        public const string CodeRequiredKey = "customMatch.codeRequired";
        public const string InvalidCodeKey = "customMatch.invalidCode";
        public const string SearchingKey = "customMatch.searching";
        public const string JoinedKey = "customMatch.joined";
        public const string NotFoundKey = "customMatch.notFound";
        public const string FullKey = "customMatch.full";
        public const string VersionMismatchKey = "customMatch.versionMismatch";
        public const string FailedKey = "customMatch.failed";

        private readonly VisualElement _container;
        private readonly LocalizedTexts _texts;
        private readonly TextField _codeField;
        private readonly Button _join;
        private readonly Label _notice;
        private readonly Button _back;
        private readonly VisualElement _navigationRoot;
        private LocalizedText? _noticeSource;
        private bool _noticeIsError;
        private int _request;
        private bool _searching;
        private bool _joined;
        private bool _disposed;

        public CustomMatchPanel(VisualElement container)
        {
            _container = container ?? throw new ArgumentNullException(nameof(container));

            var layout = Resources.Load<VisualTreeAsset>(LayoutResourcePath);
            if (layout == null)
            {
                throw new InvalidOperationException(
                    $"custom match layout 'Resources/{LayoutResourcePath}.uxml' was not found (expected under UI/Resources of the Demo sample)");
            }

            container.Clear();
            layout.CloneTree(container);
            Require<VisualElement>(PanelName);
            _codeField = Require<TextField>(CodeFieldName);
            _join = Require<Button>(JoinButtonName);
            _notice = Require<Label>(NoticeName);
            _back = Require<Button>(BackButtonName);

            _codeField.maxLength = JoinCode.MaxLength;

            _join.clicked += HandleJoinClicked;
            _back.clicked += Cancel;
            _codeField.RegisterCallback<KeyDownEvent>(HandleCodeKeyDown, TrickleDown.TrickleDown);

            _texts = LocalizedTexts.Bind(container);
            RefreshPlaceholder();

            // 何も選んでいないときの入力はパネルの根に届くので、根で受ける (パネルに付く前なら入れ物で受ける)
            _navigationRoot = container.panel?.visualTree ?? container;
            _navigationRoot.RegisterCallback<NavigationCancelEvent>(HandleCancel, TrickleDown.TrickleDown);

            GameSignals.JoinByCode.Changed += HandleJoinByCodeChanged;
            Localizer.Changed += HandleLanguageChanged;

            Render();
            FocusFirstEnabled();
        }

        public event Action Cancelled;

        public event Action Joined;

        public VisualElement Container => _container;

        public IJoinByCode JoinByCode => GameSignals.JoinByCode.Value;

        public TextField CodeField => _codeField;

        public Button JoinButton => _join;

        public Button BackButton => _back;

        public string Notice => _notice.style.display != DisplayStyle.None ? _notice.text : null;

        public bool IsNoticeError => _notice.ClassListContains(ErrorNoticeClass);

        public bool IsSearching => _searching;

        public bool HasJoined => _joined;

        public bool CanJoin => !_disposed && !_searching && !_joined && JoinByCode != null;

        // 空や使えない文字のコードは、サーバーを探す前にここで止めて、どう直せばよいかを出す。
        public bool Join()
        {
            if (!CanJoin)
            {
                return false;
            }

            var entered = _codeField.value ?? string.Empty;
            if (entered.Trim().Length == 0)
            {
                ShowNotice(new LocalizedText(CodeRequiredKey), true);
                return false;
            }

            if (!JoinCode.TryNormalize(entered, out var code))
            {
                ShowNotice(new LocalizedText(InvalidCodeKey, JoinCode.MaxLength), true);
                return false;
            }

            var joinByCode = JoinByCode;
            var request = ++_request;
            _searching = true;
            ShowNotice(new LocalizedText(SearchingKey), false);
            Render();
            Debug.Log($"{LogTag} looking for a match with join code {code}");

            try
            {
                // 呼び戻しは呼んだその場で返ることもあるので、探している状態にしてから呼ぶ。
                joinByCode.Join(code, outcome => HandleOutcome(request, code, outcome));
            }
            catch (Exception e)
            {
                Debug.LogError($"{LogTag} could not look for join code {code}: {e.Message}");
                Debug.LogException(e);
                if (request == _request)
                {
                    _request++;
                    _searching = false;
                    ShowNotice(new LocalizedText(FailedKey), true);
                    Render();
                    KeepFocus();
                }

                return false;
            }

            return true;
        }

        public void Cancel()
        {
            if (!_disposed)
            {
                Cancelled?.Invoke();
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _request++;
            GameSignals.JoinByCode.Changed -= HandleJoinByCodeChanged;
            Localizer.Changed -= HandleLanguageChanged;
            _navigationRoot.UnregisterCallback<NavigationCancelEvent>(HandleCancel, TrickleDown.TrickleDown);
            _codeField.UnregisterCallback<KeyDownEvent>(HandleCodeKeyDown, TrickleDown.TrickleDown);
            _join.clicked -= HandleJoinClicked;
            _back.clicked -= Cancel;
            _container.Clear();
            Cancelled = null;
            Joined = null;
        }

        public static string OutcomeKey(JoinCodeOutcome outcome)
        {
            switch (outcome)
            {
                case JoinCodeOutcome.Joined:
                    return JoinedKey;
                case JoinCodeOutcome.NotFound:
                    return NotFoundKey;
                case JoinCodeOutcome.Full:
                    return FullKey;
                case JoinCodeOutcome.VersionMismatch:
                    return VersionMismatchKey;
                default:
                    return FailedKey;
            }
        }

        // 閉じた後や二度目に届いた答えは、今の画面の問い合わせのものではないので捨てる。
        private void HandleOutcome(int request, string code, JoinCodeOutcome outcome)
        {
            if (_disposed || request != _request || !_searching)
            {
                return;
            }

            _searching = false;
            Debug.Log($"{LogTag} join code {code}: {outcome}");
            if (outcome == JoinCodeOutcome.Joined)
            {
                _joined = true;
                ShowNotice(new LocalizedText(JoinedKey), false);
                Render();
                Joined?.Invoke();
                return;
            }

            ShowNotice(new LocalizedText(OutcomeKey(outcome)), true);
            Render();
            KeepFocus();
        }

        private void Render()
        {
            if (_disposed)
            {
                return;
            }

            _codeField.SetEnabled(CanJoin);
            _join.SetEnabled(CanJoin);

            if (JoinByCode == null && !_searching && !_joined)
            {
                ShowNotice(new LocalizedText(UnavailableKey), false);
            }
            else if (_noticeSource.HasValue && _noticeSource.Value.Key == UnavailableKey)
            {
                HideNotice();
            }
        }

        private void ShowNotice(LocalizedText text, bool isError)
        {
            _noticeSource = text;
            _noticeIsError = isError;
            RefreshNotice();
        }

        private void HideNotice()
        {
            _noticeSource = null;
            _noticeIsError = false;
            RefreshNotice();
        }

        private void RefreshNotice()
        {
            if (!_noticeSource.HasValue)
            {
                _notice.text = string.Empty;
                _notice.style.display = DisplayStyle.None;
                _notice.RemoveFromClassList(ErrorNoticeClass);
                return;
            }

            _notice.text = Localizer.Resolve(_noticeSource.Value);
            _notice.EnableInClassList(ErrorNoticeClass, _noticeIsError);
            _notice.style.display = DisplayStyle.Flex;
        }

        private void RefreshPlaceholder()
        {
            _codeField.textEdition.placeholder = Localizer.Get(CodePlaceholderKey);
        }

        // 押したボタンが探している間に使えなくなるとフォーカスが消え、ゲームパッドで動かせなくなるので、使える要素へ移す。
        private void KeepFocus()
        {
            var focused = _container.focusController?.focusedElement as VisualElement;
            if (focused != null && _container.Contains(focused) && focused.enabledInHierarchy)
            {
                return;
            }

            if (focused == null || _container.Contains(focused))
            {
                FocusFirstEnabled();
            }
        }

        private void FocusFirstEnabled()
        {
            var target = _codeField.enabledSelf ? (VisualElement)_codeField : _back;
            target.Focus();

            // パネルに付いた直後は配置が終わっておらず Focus が効かないことがあるので、次の更新でもう一度選ぶ
            if (target.panel != null && target.focusController?.focusedElement != target)
            {
                target.schedule.Execute(() =>
                {
                    if (!_disposed && target.enabledInHierarchy)
                    {
                        target.Focus();
                    }
                });
            }
        }

        private void HandleJoinClicked()
        {
            Join();
        }

        // コードを打った手のまま入れるよう、欄の Enter でも入るボタンと同じことをする。
        private void HandleCodeKeyDown(KeyDownEvent evt)
        {
            if (evt.keyCode != KeyCode.Return && evt.keyCode != KeyCode.KeypadEnter)
            {
                return;
            }

            evt.StopPropagation();
            Join();
        }

        private void HandleCancel(NavigationCancelEvent evt)
        {
            evt.StopPropagation();
            _navigationRoot.focusController?.IgnoreEvent(evt);
            Cancel();
        }

        private void HandleJoinByCodeChanged(IJoinByCode joinByCode)
        {
            Render();
            KeepFocus();
        }

        private void HandleLanguageChanged(GameLanguage language)
        {
            if (_disposed)
            {
                return;
            }

            _texts.Refresh();
            RefreshPlaceholder();
            RefreshNotice();
        }

        private T Require<T>(string name)
            where T : VisualElement
        {
            var element = _container.Q<T>(name);
            if (element == null)
            {
                throw new InvalidOperationException(
                    $"custom match layout 'Resources/{LayoutResourcePath}.uxml' has no {typeof(T).Name} named '{name}'");
            }

            return element;
        }
    }
}
